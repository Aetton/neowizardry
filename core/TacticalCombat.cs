using System.Collections.Immutable;
using NeoWizardry.Core.Rules;

namespace NeoWizardry.Core;

// Authored grid laboratory. Coordinates, reach, movement, AI and sight are NOT recovered Wiz8 rules.
public readonly record struct BattleCell(int X,int Y);
public sealed record TacticalOpponent(Enemy Actor,MeleeCharacterFacts Facts,string WeaponId,RestoredMagicFacts? Magic=null);
public sealed record TacticalField(int Width,int Height,ImmutableDictionary<string,BattleCell> Positions,
    ImmutableArray<BattleCell> Walls,ImmutableArray<TacticalOpponent> Opponents);
public enum TacticalAction { Attack, Move, Wait, Cast }
public sealed record TacticalOrder(string ActorId,TacticalAction Action,string? TargetId=null,BattleCell? Destination=null,RestoredCastOrder? Spell=null);

public static class TacticalCombat
{
    public static IEnumerable<Enemy> Enemies(GameState state)=>new[]{state.Enemy}.Concat(state.Melee?.Field?.Opponents.Select(o=>o.Actor)??[]);
    public static bool Alive(GameState state,string id)=>state.Party.Any(h=>h.Id==id && h.Hp>0 && !h.Captive) || Enemies(state).Any(e=>e.Id==id && e.Hp>0);
    public static int Distance(BattleCell a,BattleCell b)=>Math.Max(Math.Abs(a.X-b.X),Math.Abs(a.Y-b.Y));
    public static bool Inside(TacticalField f,BattleCell p)=>p.X>=0 && p.Y>=0 && p.X<f.Width && p.Y<f.Height;
    public static void ValidateStructure(GameState state)
    {
        var f=state.Melee!.Field!;
        if(f.Width is <4 or >16 || f.Height is <4 or >16 || f.Positions is null || f.Walls.IsDefault || f.Opponents.IsDefault || f.Opponents.Length>7 ||
            f.Opponents.Any(o=>o is null || o.Actor is null || o.Facts is null || o.Actor.Hp<0 || o.Actor.Speed<0 || o.Actor.Power!=0 || o.Actor.XpReward<0 || o.Actor.LootItemId is not null || o.Actor.Effects is {Count:>0} || o.Facts.Initiative is null) ||
            state.Melee.Opponent.Initiative is null || state.Melee.Party.Values.Any(p=>p.Initiative is null))throw new InvalidDataException("Invalid tactical actors or board");
        var ids=state.Party.Select(h=>h.Id).Concat(Enemies(state).Select(e=>e.Id)).ToArray();
        if(ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct().Count()!=ids.Length || f.Positions.Count!=ids.Length || ids.Any(id=>!f.Positions.ContainsKey(id)) ||
            f.Positions.Values.Any(p=>!Inside(f,p)) || f.Walls.Distinct().Count()!=f.Walls.Length || f.Walls.Any(p=>!Inside(f,p)) ||
            f.Positions.Any(p=>f.Walls.Contains(p.Value)) || ids.Where(id=>Alive(state,id)).Select(id=>f.Positions[id]).Distinct().Count()!=ids.Count(id=>Alive(state,id)))
            throw new InvalidDataException("Invalid tactical positions");
        foreach(var o in f.Opponents)
        {
            if(state.Items!.Items.SingleOrDefault(i=>i.Id==o.WeaponId) is not {Quantity:1} w || w.Location!=(state.RewardGranted?Inventory.BagLocation:new ItemLocation(o.Actor.Id,Equipment.WeaponSlot)))throw new InvalidDataException("Missing tactical opponent weapon");
            if((state.Melee.Magic is null)!=(o.Magic is null))throw new InvalidDataException("Mixed tactical magic profiles");
            if(o.Magic is not null)RestoredMagic.Validate(o.Magic);
        }
    }
    internal static MeleeCharacterFacts Facts(GameState s,string id)=>s.Melee!.Party.GetValueOrDefault(id)??(id==s.Enemy.Id?s.Melee.Opponent:s.Melee.Field!.Opponents.Single(o=>o.Actor.Id==id).Facts);
    internal static ItemDefinition Weapon(GameState s,string id,ItemCatalog c)=>s.Melee!.Field!.Opponents.FirstOrDefault(o=>o.Actor.Id==id) is {} o
        ?MeleeCombat.OrdinaryWeapon(c.Find(s.Items!.Items.Single(i=>i.Id==o.WeaponId).DefinitionId)):MeleeCombat.Weapon(s,id,c);
    internal static RestoredMagicFacts MagicFacts(GameState s,string id)=>s.Melee!.Magic?.GetValueOrDefault(id)??s.Melee.Field!.Opponents.Single(o=>o.Actor.Id==id).Magic??throw new InvalidOperationException("Missing target magic facts");
    private static bool Free(GameState s,BattleCell cell,string actor)=>Inside(s.Melee!.Field!,cell) && !s.Melee!.Field!.Walls.Contains(cell) &&
        !s.Melee!.Field!.Positions.Any(p=>p.Key!=actor && p.Value==cell && Alive(s,p.Key));
    // Supercover sight: diagonal corner cutting is blocked by either side cell.
    public static bool Visible(GameState s,string from,string to)
    {
        var f=s.Melee!.Field!;var a=f.Positions[from];var b=f.Positions[to];int x=a.X,y=a.Y;
        int dx=Math.Abs(b.X-x),dy=Math.Abs(b.Y-y),sx=Math.Sign(b.X-x),sy=Math.Sign(b.Y-y),ix=0,iy=0;
        bool Block(int cx,int cy)=>f.Walls.Contains(new(cx,cy)) || f.Positions.Any(p=>p.Key!=from && p.Key!=to && p.Value==new BattleCell(cx,cy) && Alive(s,p.Key));
        while(ix<dx || iy<dy)
        {
            int cmp=(1+2*ix)*dy-(1+2*iy)*dx;
            if(cmp==0){if(Block(x+sx,y)||Block(x,y+sy))return false;x+=sx;y+=sy;ix++;iy++;}
            else if(cmp<0){x+=sx;ix++;}else{y+=sy;iy++;}
            if(Block(x,y))return false;
        }
        return true;
    }
    private static bool CanStep(GameState s,string actor,BattleCell to)
    {
        var from=s.Melee!.Field!.Positions[actor];
        return Distance(from,to)==1 && Free(s,to,actor) && (from.X==to.X || from.Y==to.Y || Free(s,new(from.X,to.Y),actor) && Free(s,new(to.X,from.Y),actor));
    }
    private static bool InReach(GameState s,string a,string b)=>Distance(s.Melee!.Field!.Positions[a],s.Melee.Field.Positions[b])==1 && Visible(s,a,b);
    private static int Hp(GameState s,string id)=>s.Party.FirstOrDefault(h=>h.Id==id)?.Hp??Enemies(s).Single(e=>e.Id==id).Hp;
    private static GameState WithHp(GameState s,string id,int amount)
    {
        int i=Array.FindIndex(s.Party.ToArray(),h=>h.Id==id);
        if(i>=0)return s with {Party=s.Party.SetItem(i,s.Party[i] with {Hp=Math.Clamp(s.Party[i].Hp+amount,0,s.Party[i].MaxHp)})};
        if(id==s.Enemy.Id)return s with {Enemy=s.Enemy with {Hp=Math.Max(0,s.Enemy.Hp+amount)}};
        var f=s.Melee!.Field!;int j=Array.FindIndex(f.Opponents.ToArray(),o=>o.Actor.Id==id);var o=f.Opponents[j];
        return s with {Melee=s.Melee with {Field=f with {Opponents=f.Opponents.SetItem(j,o with {Actor=o.Actor with {Hp=Math.Max(0,o.Actor.Hp+amount)}})}}};
    }
    private static IEnumerable<BattleCell> Neighbors(BattleCell p)=>from y in Enumerable.Range(-1,3) from x in Enumerable.Range(-1,3) where x!=0 || y!=0 select new BattleCell(p.X+x,p.Y+y);
    private static TacticalOrder EnemyOrder(GameState s,string id)
    {
        var targets=s.Party.Where(h=>h.Hp>0 && !h.Captive).OrderBy(h=>Distance(s.Melee!.Field!.Positions[id],s.Melee.Field.Positions[h.Id])).ThenBy(h=>h.Id,StringComparer.Ordinal).ToArray();
        var near=targets.FirstOrDefault(t=>InReach(s,id,t.Id));if(near is not null)return new(id,TacticalAction.Attack,near.Id);
        // BFS chooses a reachable attack cell, rather than walking indefinitely into a wall.
        var origin=s.Melee!.Field!.Positions[id];var q=new Queue<(BattleCell Cell,BattleCell First)>();var visited=new HashSet<BattleCell>{origin};
        foreach(var n in Neighbors(origin).Where(n=>CanStep(s,id,n))){visited.Add(n);q.Enqueue((n,n));}
        while(q.TryDequeue(out var node))
        {
            var projected=s with {Melee=s.Melee with {Field=s.Melee.Field with {Positions=s.Melee.Field.Positions.SetItem(id,node.Cell)}}};
            if(targets.Any(t=>InReach(projected,id,t.Id)))return new(id,TacticalAction.Move,Destination:node.First);
            foreach(var n in Neighbors(node.Cell))if(!visited.Contains(n) && CanStep(projected,id,n)){visited.Add(n);q.Enqueue((n,node.First));}
        }
        return new(id,TacticalAction.Wait);
    }
    public static RoundResult Advance(GameState state,ItemCatalog catalog,IReadOnlyList<TacticalOrder>? orders=null,SpellCatalog? spells=null,IReadOnlyList<RestoredCastOrder>? casts=null)
    {
        MeleeCombat.Validate(state,catalog);if(state.Melee?.Field is null)throw new InvalidOperationException("Open a tactical encounter first");
        if(!Enemies(state).Any(e=>e.Hp>0) || !state.Party.Any(h=>h.Hp>0 && !h.Captive))return new(state,["Battle finished."]);
        var selected=new Dictionary<string,TacticalOrder>();var preparedCasts=new Dictionary<string,PreparedRestoredCast>();
        foreach(var order in (orders??[]).Concat((casts??[]).Select(c=>new TacticalOrder(c.CasterId,TacticalAction.Cast,c.TargetId,Spell:c))))
        {
            if(order is null || !state.Party.Any(h=>h.Id==order.ActorId && h.Hp>0 && !h.Captive) || !selected.TryAdd(order.ActorId,order) || !Enum.IsDefined(order.Action))throw new InvalidOperationException("One order per living free hero");
            if(order.Action==TacticalAction.Move && (order.Destination is not {} to || !CanStep(state,order.ActorId,to)))throw new InvalidOperationException("Move one cell into a free space; corners are blocked");
            if(order.Action==TacticalAction.Attack && (!Enemies(state).Any(e=>e.Id==order.TargetId && e.Hp>0) || !InReach(state,order.ActorId,order.TargetId!)))throw new InvalidOperationException("Select an adjacent visible living enemy");
            if(order.Action==TacticalAction.Cast)
            {
                if(order.Spell is null || order.Spell.CasterId!=order.ActorId)throw new InvalidOperationException("Invalid caster order");
                var c=RestoredMagic.Prepare(state,order.Spell,spells??throw new InvalidOperationException("Spell catalog required"));
                if(!Visible(state,order.ActorId,c.TargetId))throw new InvalidOperationException("Spell line of sight is blocked");
                preparedCasts.Add(order.ActorId,c);
            }
        }
        // Validate every weapon before consuming RNG, even if it will only be used for defense.
        var ids=state.Party.Where(h=>h.Hp>0 && !h.Captive).Select(h=>h.Id).Concat(Enemies(state).Where(e=>e.Hp>0).Select(e=>e.Id)).ToArray();
        if(state.Party.Any(h=>h.Effects is {Count:>0}) || state.Enemy.Effects is {Count:>0})throw new InvalidOperationException("Synthetic effects cannot enter tactical combat");
        foreach(var id in ids)_=MeleeCombat.Prepare(Facts(state,id),Weapon(state,id,catalog));
        var s=state;uint rng=s.RandomState;var events=new List<string>();
        var queue=new List<(string Id,int Time,int Remaining)>();
        foreach(var id in ids)
        {
            var f=Facts(s,id);var w=Weapon(s,id,catalog);bool other=Enemies(s).Any(e=>e.Id==id);
            if(!other && !selected.ContainsKey(id))
            {
                var t=Enemies(s).Where(e=>e.Hp>0 && InReach(s,id,e.Id)).OrderBy(e=>e.Id,StringComparer.Ordinal).FirstOrDefault();
                selected[id]=t is null?new(id,TacticalAction.Wait):new(id,TacticalAction.Attack,t.Id);
            }
            int count=!other && selected[id].Action!=TacticalAction.Attack?1:MeleeCombat.Prepare(f,w).Budget.Attacks;
            queue.Add((id,OriginalInitiative.FirstTime(OriginalInitiative.Character(f.Attributes.Speed,f.Attributes.Senses,f.Initiative!),preparedCasts.ContainsKey(id)?null:w.Initiative,Dice.Roll(ref rng,4)-1),count));
        }
        var enemyOrders=new Dictionary<string,TacticalOrder>();
        while(queue.Count>0 && Enemies(s).Any(e=>e.Hp>0) && s.Party.Any(h=>h.Hp>0 && !h.Captive))
        {
            var turn=queue.OrderBy(t=>t.Time).ThenBy(t=>Enemies(s).Any(e=>e.Id==t.Id)).ThenBy(t=>t.Id,StringComparer.Ordinal).First();queue.Remove(turn);
            if(!Alive(s,turn.Id))continue;
            bool enemy=Enemies(s).Any(e=>e.Id==turn.Id);
            if(enemy && !enemyOrders.ContainsKey(turn.Id))enemyOrders[turn.Id]=EnemyOrder(s,turn.Id);
            var order=enemy?enemyOrders[turn.Id]:selected[turn.Id];
            events.Add($"Queue {turn.Time}: {turn.Id} · {order.Action}");
            if(order.Action==TacticalAction.Wait)continue;
            if(order.Action==TacticalAction.Move)
            {
                if(order.Destination is {} to && CanStep(s,turn.Id,to))s=s with {Melee=s.Melee! with {Field=s.Melee!.Field! with {Positions=s.Melee.Field.Positions.SetItem(turn.Id,to)}}};
                else events.Add($"{turn.Id}: move cancelled, cell occupied or corner blocked");
                continue;
            }
            if(order.Action==TacticalAction.Cast)
            {
                var c=preparedCasts[turn.Id];
                if(!Alive(s,c.TargetId) || !Visible(s,turn.Id,c.TargetId)){events.Add("Spell cancelled: target unavailable or sight blocked; no mana spent");continue;}
                int i=Array.FindIndex(s.Party.ToArray(),h=>h.Id==turn.Id);var caster=s.Party[i];
                s=s with {Party=s.Party.SetItem(i,caster with {Mana=caster.Mana!.Select(m=>m.Realm==c.Spell.Realm?m with {Current=m.Current-c.ManaCost}:m).ToImmutableList()})};
                var outcome=RestoredMagic.Outcome(c,ref rng);int amount=0;string target=c.TargetId;
                if(outcome!=CastingOutcome.Fizzle)
                {
                    if(c.Spell.Id==6){var h=s.Party.Single(h=>h.Id==target);amount=h.Hp==h.MaxHp?0:Math.Min(h.MaxHp-h.Hp,RestoredMagic.Amount(c,ref rng));s=WithHp(s,target,amount);}
                    else{if(outcome==CastingOutcome.Backfire)target=turn.Id;amount=Math.Min(Hp(s,target),RestoredMagic.Damage(c,MagicFacts(s,target),ref rng).Damage);s=WithHp(s,target,-amount);}
                }
                events.Add($"{turn.Id} → {target}: {c.Spell.Name} PL{c.Power}, {outcome}, amount {amount}, mana {c.ManaCost}");continue;
            }
            if(order.TargetId is not {} targetId || !Alive(s,targetId) || !InReach(s,turn.Id,targetId)){events.Add("Attack cancelled: target dead or out of reach");continue;}
            var facts=Facts(s,turn.Id);var weapon=Weapon(s,turn.Id,catalog);var prepared=MeleeCombat.Prepare(facts,weapon);
            int swings=OriginalCombatRules.AllocateSwings(prepared.Budget.MaximumSwings,weapon.BonusSwings,Dice.Roll(ref rng,prepared.Budget.MaximumSwings)-1).AllocatedSwings;
            for(int n=0;n<swings && Alive(s,targetId);n++)
            {
                var df=Facts(s,targetId);var armor=OriginalDefenseAdapter.Prepare(new(MainHand:Weapon(s,targetId,catalog))) with {Body=df.BodyArmor};
                var defense=OriginalCharacterDefense.Build(new(df.Attributes.Speed,df.Stealth,0,0,false),armor,new(0,0,false,false,false),new(0,0,0,false));
                var loc=OriginalCharacterDefense.SelectLocation(Dice.Roll(ref rng,100)-1);var thresholds=OriginalCharacterDefense.Against(defense,loc,OriginalAttackMode.Swing,new(false,false,false));
                var input=new OrdinaryPhysicalScoreInput(prepared.Budget.BaseAttackRating,prepared.Hand.ToHit,0,0,0,facts.Attributes,df.Attributes,OriginalAttackMode.Swing,thresholds.Hit,thresholds.Penetration,ModernWeapon:prepared.Weapon.ModernWeapon);
                var scores=OriginalPhysicalScores.OrdinaryCharacter(input);int contact=Dice.Roll(ref rng,100)-1;
                var outcome=OriginalMeleeStrike.Contact(scores,contact);int[] draws=outcome==PhysicalStrikeOutcome.Damage?Enumerable.Range(0,prepared.Weapon.Damage.Dice).Select(_=>Dice.Roll(ref rng,prepared.Weapon.Damage.Sides)-1).ToArray():[];
                var strike=OriginalMeleeStrike.Resolve(input,prepared.Weapon.Damage,prepared.Hand.DamagePercent,0,0,contact,draws);int amount=Math.Min(Hp(s,targetId),strike.Damage?.Damage??0);
                s=WithHp(s,targetId,-amount);events.Add($"{turn.Id} → {targetId} · {loc}: {strike.Outcome}, {amount} damage [d100 {contact+1}; hit {scores.HitScore}; pen {scores.PenetrationScore}]");
            }
            if(turn.Remaining>1 && Alive(s,turn.Id) && OriginalInitiative.NextAttackTime(turn.Time,0,turn.Time,turn.Remaining-1) is int next)queue.Add((turn.Id,next,turn.Remaining-1));
        }
        s=s with {Round=checked(s.Round+1),RandomState=rng};
        if(!Enemies(s).Any(e=>e.Hp>0) && !s.RewardGranted && s.Party.Any(h=>h.Hp>0 && !h.Captive))
        {
            int reward=checked(Enemies(s).Sum(e=>e.XpReward));var eligible=s.Party.Where(h=>h.Hp>0 && !h.Captive).Select(h=>h.Id).ToArray();int j=0;
            s=s with {Party=s.Party.Select(h=>eligible.Contains(h.Id)?h with {Xp=checked(h.Xp+reward/eligible.Length+(j++<reward%eligible.Length?1:0))}:h).ToImmutableArray(),RewardGranted=true};
            var items=s.Items!;
            foreach(var id in new[]{s.Melee!.OpponentWeaponId}.Concat(s.Melee.Field!.Opponents.Select(o=>(string?)o.WeaponId)).OfType<string>())items=ItemInventory.Move(items,id,Inventory.BagLocation,Inventory.Rules(catalog));
            // Secondary opponents retain their identity; their weapon is now owned by the party.
            s=s with {Items=items,Melee=s.Melee with {OpponentWeaponId=null}};events.Add($"Victory: {reward} scenario XP; all owned weapons transferred once.");
        }
        Game.Validate(s);return new(s,events.ToImmutableArray());
    }
}
