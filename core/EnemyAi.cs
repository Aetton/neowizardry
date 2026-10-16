using System.Collections.Immutable;

namespace NeoWizardry.Core;

// Authored behavior policies for the tactical laboratory; not recovered Wiz8 monster AI.
public enum EnemyTactic { Brute, Hunter, Flanker, Skirmisher }
public enum EnemyAiMode { Idle, Pursue, Attack, Search, Retreat, Blocked }
public sealed record EnemyAiProfile(EnemyTactic Tactic,int Vision=7,int MemoryRounds=3,int RetreatPercent=25);
public sealed record EnemyBrain(EnemyAiProfile Profile,int MaxHp,string? TargetId=null,BattleCell? LastSeen=null,int? SeenRound=null,EnemyAiMode Mode=EnemyAiMode.Idle);
public sealed record EnemyDecision(TacticalOrder Order,EnemyBrain Brain,string Reason);

public static class EnemyAi
{
    public static GameState Enable(GameState state,int authoredMaxHp)
    {
        Game.Validate(state);
        if(state.Melee?.Field is not {} field)throw new InvalidOperationException("Enemy AI requires a tactical field");
        if(authoredMaxHp<1 || TacticalCombat.Enemies(state).Any(e=>e.Hp>authoredMaxHp))throw new ArgumentOutOfRangeException(nameof(authoredMaxHp));
        var brains=TacticalCombat.Enemies(state).Select((e,i)=>(e.Id,Brain:new EnemyBrain(new((EnemyTactic)(i%4)),authoredMaxHp)))
            .ToImmutableDictionary(p=>p.Id,p=>p.Brain);
        var next=state with {Melee=state.Melee with {Field=field with {Brains=brains}}};Game.Validate(next);return next;
    }
    public static GameState SetTactic(GameState state,string id,EnemyTactic tactic)
    {
        Game.Validate(state);if(!Enum.IsDefined(tactic))throw new ArgumentOutOfRangeException(nameof(tactic));
        var f=state.Melee?.Field??throw new InvalidOperationException("No tactical field");
        if(f.Brains is not {} brains || !brains.TryGetValue(id,out var brain))throw new InvalidOperationException("This opponent has no AI profile");
        return state with {Melee=state.Melee! with {Field=f with {Brains=brains.SetItem(id,brain with {Profile=brain.Profile with {Tactic=tactic}})}}};
    }
    internal static void Validate(GameState s)
    {
        if(s.Melee!.Field!.Brains is not {} brains)return;
        var enemies=TacticalCombat.Enemies(s).ToArray();
        if(brains.Count!=enemies.Length || enemies.Any(e=>!brains.ContainsKey(e.Id)))throw new InvalidDataException("Incomplete enemy brains");
        foreach(var e in enemies)
        {
            var b=brains[e.Id];
            if(b is null || b.Profile is not {} p || !Enum.IsDefined(p.Tactic) || !Enum.IsDefined(b.Mode) || b.MaxHp<1 || e.Hp>b.MaxHp ||
                p.Vision is <1 or >16 || p.MemoryRounds is <0 or >20 || p.RetreatPercent is <0 or >100 ||
                (b.TargetId is null)!=(b.LastSeen is null) || (b.TargetId is null)!=(b.SeenRound is null) ||
                b.TargetId is not null && (!s.Party.Any(h=>h.Id==b.TargetId) || !TacticalCombat.Inside(s.Melee.Field,b.LastSeen!.Value) || s.Melee.Field.Walls.Contains(b.LastSeen.Value) || b.SeenRound<0 || b.SeenRound>s.Round))
                throw new InvalidDataException("Invalid enemy brain or memory");
        }
    }
    private sealed record Path(int Length,BattleCell First);
    private sealed record Approach(Hero Target,BattleCell Cell,Path Path,int Crowd,int Exposure,int Assigned);
    private static GameState At(GameState s,string id,BattleCell cell)=>s with {Melee=s.Melee! with {Field=s.Melee!.Field! with {Positions=s.Melee.Field.Positions.SetItem(id,cell)}}};
    private static Dictionary<BattleCell,Path> Paths(GameState s,string id)
    {
        var origin=s.Melee!.Field!.Positions[id];var result=new Dictionary<BattleCell,Path>{{origin,new(0,origin)}};var queue=new Queue<BattleCell>();queue.Enqueue(origin);
        while(queue.TryDequeue(out var cell))
        {
            var projected=At(s,id,cell);var path=result[cell];
            foreach(var next in TacticalCombat.Neighbors(cell))
                if(!result.ContainsKey(next) && TacticalCombat.CanStep(projected,id,next))
                {
                    result[next]=new(path.Length+1,path.Length==0?next:path.First);queue.Enqueue(next);
                }
        }
        return result;
    }
    private static int Health(Hero hero)=>(int)((long)hero.Hp*1000/hero.MaxHp);
    public static EnemyDecision Decide(GameState state,string id,bool allowMove=true)
    {
        Game.Validate(state);
        var field=state.Melee?.Field??throw new InvalidOperationException("Enemy AI requires a tactical field");
        if(field.Brains is not {} brains || !brains.TryGetValue(id,out var initial))throw new InvalidOperationException("Unknown enemy brain");
        var actor=TacticalCombat.Enemies(state).Single(e=>e.Id==id);var origin=field.Positions[id];var b=initial;
        EnemyDecision Result(TacticalAction action,EnemyAiMode mode,string reason,string? target=null,BattleCell? move=null)=>
            new(new(id,action,target,move),b with {Mode=mode},reason);
        if(actor.Hp==0)return Result(TacticalAction.Wait,EnemyAiMode.Idle,"dead actor");
        var visible=state.Party.Where(h=>h.Hp>0 && !h.Captive && TacticalCombat.Distance(origin,field.Positions[h.Id])<=b.Profile.Vision && TacticalCombat.Visible(state,id,h.Id)).ToArray();
        bool memory=b.TargetId is not null && state.Party.Any(h=>h.Id==b.TargetId && h.Hp>0 && !h.Captive) && state.Round-b.SeenRound!.Value<=b.Profile.MemoryRounds;
        if(!memory)b=b with {TargetId=null,LastSeen=null,SeenRound=null};
        if(b.Profile.Tactic==EnemyTactic.Skirmisher && (long)actor.Hp*100<=(long)b.MaxHp*b.Profile.RetreatPercent)
        {
            var threats=visible.Select(h=>field.Positions[h.Id]).Concat(memory && visible.Length==0?new[]{b.LastSeen!.Value}:[]).ToArray();
            if(threats.Length>0)
            {
                if(visible.Length>0){var t=visible.OrderBy(h=>TacticalCombat.Distance(origin,field.Positions[h.Id])).ThenBy(h=>h.Id,StringComparer.Ordinal).First();b=b with {TargetId=t.Id,LastSeen=field.Positions[t.Id],SeenRound=state.Round};}
                int now=threats.Min(t=>TacticalCombat.Distance(origin,t));
                var safe=TacticalCombat.Neighbors(origin).Where(c=>TacticalCombat.CanStep(state,id,c))
                    .Select(c=>(Cell:c,Distance:threats.Min(t=>TacticalCombat.Distance(c,t))))
                    .Where(c=>c.Distance>now).OrderByDescending(c=>c.Distance).ThenBy(c=>c.Cell.Y).ThenBy(c=>c.Cell.X).ToArray();
                if(allowMove && safe.Length>0)return Result(TacticalAction.Move,EnemyAiMode.Retreat,$"low HP {actor.Hp}/{b.MaxHp}; increase distance from threats",move:safe[0].Cell);
                // Cornered actors may still defend themselves; moving after an attack is forbidden.
                if(!visible.Any(h=>TacticalCombat.InReach(state,id,h.Id)))return Result(TacticalAction.Wait,EnemyAiMode.Retreat,"low HP; no safer step this turn");
            }
        }
        int Assigned(string target)=>brains.Count(p=>p.Key!=id && TacticalCombat.Alive(state,p.Key) && p.Value.TargetId==target &&
            p.Value.Mode is EnemyAiMode.Attack or EnemyAiMode.Pursue && p.Value.SeenRound is int seen && state.Round-seen<=p.Value.Profile.MemoryRounds);
        var adjacent=visible.Where(h=>TacticalCombat.InReach(state,id,h.Id));
        var victim=(b.Profile.Tactic==EnemyTactic.Hunter?adjacent.OrderBy(Health).ThenBy(h=>h.Id,StringComparer.Ordinal):
            b.Profile.Tactic==EnemyTactic.Flanker?adjacent.OrderBy(h=>Assigned(h.Id)).ThenBy(h=>h.Id,StringComparer.Ordinal):
            adjacent.OrderBy(h=>h.Id,StringComparer.Ordinal)).FirstOrDefault();
        if(victim is not null)
        {
            b=b with {TargetId=victim.Id,LastSeen=field.Positions[victim.Id],SeenRound=state.Round};
            return Result(TacticalAction.Attack,EnemyAiMode.Attack,$"visible adjacent target {victim.Id}"+(b.Profile.Tactic==EnemyTactic.Hunter?$"; health {victim.Hp}/{victim.MaxHp}":""),victim.Id);
        }
        if(!allowMove)
        {
            var last=visible.OrderBy(h=>h.Id,StringComparer.Ordinal).FirstOrDefault();
            if(last is not null)b=b with {TargetId=last.Id,LastSeen=field.Positions[last.Id],SeenRound=state.Round};
            return Result(TacticalAction.Wait,EnemyAiMode.Blocked,"no adjacent target; attack group cannot also move");
        }
        var paths=Paths(state,id);
        var approaches=new List<Approach>();
        foreach(var hero in visible)
        foreach(var pair in paths.Where(p=>p.Value.Length>0))
        {
            if(!TacticalCombat.InReach(At(state,id,pair.Key),id,hero.Id))continue;
            int crowd=TacticalCombat.Enemies(state).Count(e=>e.Id!=id && e.Hp>0 && TacticalCombat.Distance(field.Positions[e.Id],pair.Key)<=1);
            int exposed=visible.Count(h=>TacticalCombat.Distance(field.Positions[h.Id],pair.Key)<=1);
            int assigned=Assigned(hero.Id);
            approaches.Add(new(hero,pair.Key,pair.Value,crowd,exposed,assigned));
        }
        var chosen=(b.Profile.Tactic==EnemyTactic.Hunter?approaches.OrderBy(a=>Health(a.Target)).ThenBy(a=>a.Path.Length):
            b.Profile.Tactic==EnemyTactic.Flanker?approaches.OrderBy(a=>a.Assigned).ThenBy(a=>a.Path.Length+2*a.Crowd+2*a.Exposure):
            approaches.OrderBy(a=>a.Path.Length).ThenBy(a=>a.Path.Length))
            .ThenBy(a=>a.Target.Id,StringComparer.Ordinal).ThenBy(a=>a.Cell.Y).ThenBy(a=>a.Cell.X).FirstOrDefault();
        if(chosen is not null)
        {
            b=b with {TargetId=chosen.Target.Id,LastSeen=field.Positions[chosen.Target.Id],SeenRound=state.Round};
            return Result(TacticalAction.Move,EnemyAiMode.Pursue,$"approach {chosen.Target.Id}; path {chosen.Path.Length}"+(b.Profile.Tactic==EnemyTactic.Flanker?$"; assigned allies {chosen.Assigned}, crowd {chosen.Crowd}":""),chosen.Target.Id,chosen.Path.First);
        }
        if(visible.Length>0)
        {
            var t=visible.OrderBy(h=>TacticalCombat.Distance(origin,field.Positions[h.Id])).ThenBy(h=>h.Id,StringComparer.Ordinal).First();
            b=b with {TargetId=t.Id,LastSeen=field.Positions[t.Id],SeenRound=state.Round};
            return Result(TacticalAction.Wait,EnemyAiMode.Blocked,$"visible target {t.Id}, no reachable attack cell");
        }
        if(memory)
        {
            var seen=b.LastSeen!.Value;
            if(origin==seen){b=b with {TargetId=null,LastSeen=null,SeenRound=null};return Result(TacticalAction.Wait,EnemyAiMode.Idle,"last known cell searched; target lost");}
            if(paths.TryGetValue(seen,out var path) && path.Length>0)return Result(TacticalAction.Move,EnemyAiMode.Search,$"search last known cell ({seen.X},{seen.Y}); no tracking through walls",b.TargetId,path.First);
            return Result(TacticalAction.Wait,EnemyAiMode.Blocked,"last known cell currently unreachable");
        }
        return Result(TacticalAction.Wait,EnemyAiMode.Idle,"no visible target or active memory");
    }
}
