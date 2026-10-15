using System.Text.Json;
using NeoWizardry.Core.Rules;

static class MonsterSequenceChecks
{
    public static int Run()
    {
        int checks=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
        void Reject<T>(Action action,string name) where T:Exception
        {
            bool rejected=false;try{action();}catch(T){rejected=true;}Check(rejected,name);
        }
        var a=new MonsterPhysicalChoice(0,OriginalAttackMode.Thrust,"hero-1");
        var b=new MonsterPhysicalChoice(2,OriginalAttackMode.Lash,"hero-2");
        MonsterPhysicalChoice[] candidates=[a,b,a];
        Check(OriginalMonsterAttackSequence.SelectChoice([],null) is null,"No eligible physical action requests no RNG");
        Check(OriginalMonsterAttackSequence.SelectChoice(candidates,0)==a && OriginalMonsterAttackSequence.SelectChoice(candidates,1)==b &&
            OriginalMonsterAttackSequence.SelectChoice(candidates,2)==a,"Candidate selection preserves ordered duplicate entries and target identity");
        var counts=Enumerable.Range(0,3).Select(i=>OriginalMonsterAttackSequence.SelectChoice(candidates,i)).GroupBy(c=>c).ToDictionary(g=>g.Key!,g=>g.Count());
        Check(counts[a]==2 && counts[b]==1,"Equal list-index probability must not become equal distinct-action probability");
        bool masksAgree=true;int maskCases=0;
        for(int mask=0;mask<=65535;mask++)
        {
            var modes=OriginalMonsterAttackSequence.Modes(mask);
            int rebuilt=modes.Sum(mode=>1<<(int)mode);
            masksAgree &= rebuilt==(mask&511) && modes.SequenceEqual(modes.OrderBy(m=>(int)m));maskCases++;
        }
        Check(masksAgree && maskCases==65536,"Every native word mask expands exactly the ordered low-nine monster mode bits");
        Check(OriginalMonsterAttackSequence.Modes(0).Count==0 && OriginalMonsterAttackSequence.Modes(0xfe00).Count==0,
            "Empty/unsupported masks produce no modes rather than an infinite cyclic sampler");
        Check(OriginalMonsterAttackSequence.Modes(0x105).SequenceEqual(new[]{OriginalAttackMode.Swing,OriginalAttackMode.Bash,OriginalAttackMode.Shoot}),
            "Sparse mask preserves mode ordinals rather than compacting them");
        var initial=OriginalMonsterAttackSequence.Start(2,3);
        var rejected=OriginalMonsterAttackSequence.BeginGroup(initial,false,null);
        Check(!rejected.Started && rejected.State.GroupsRemaining==1 && rejected.State.GroupsAttempted==1 && rejected.State.SwingsRemaining==0,
            "A failed group gate consumes group budget before requesting any swing draw");
        Check(initial.GroupsRemaining==2 && initial.GroupsAttempted==0,"Group start leaves input state immutable");
        var begun=OriginalMonsterAttackSequence.BeginGroup(rejected.State,true,2);
        Check(begun.Started && begun.State.GroupsRemaining==0 && begun.State.SwingsRemaining==3 && begun.State.GroupsAttempted==2,
            "Exclusive maximum-swings draw becomes one-based swing budget independently of groups");
        var afterMiss=OriginalMonsterAttackSequence.CompleteSwing(begun.State);
        Check(afterMiss.SwingsRemaining==2 && afterMiss.SwingsCompleted==1,"Completed miss still spends exactly one swing");
        var afterBlocked=OriginalMonsterAttackSequence.CompleteSwing(afterMiss);
        var finished=OriginalMonsterAttackSequence.CompleteSwing(afterBlocked);
        Check(finished.SwingsRemaining==0 && finished.SwingsCompleted==3 && finished.GroupsRemaining==0,
            "Miss/block/hit completion all use the same budget transition");
        Check(begun.State.SwingsRemaining==3 && begun.State.SwingsCompleted==0,"Completion never mutates the previous snapshot");
        var interrupted=OriginalMonsterAttackSequence.CompleteSwing(begun.State,true);
        Check(interrupted.SwingsRemaining==0 && interrupted.SwingsCompleted==1 && interrupted.GroupsRemaining==0,
            "Resolved interruption discards unfinished swings without fabricating completions");
        var moreGroups=OriginalMonsterAttackSequence.BeginGroup(OriginalMonsterAttackSequence.Start(2,2),true,1).State;
        var nextGroup=OriginalMonsterAttackSequence.BeginGroup(OriginalMonsterAttackSequence.CompleteSwing(moreGroups,true),true,0).State;
        Check(nextGroup.GroupsRemaining==0 && nextGroup.SwingsRemaining==1 && nextGroup.SwingsCompleted==1,
            "Cancelling current swings does not silently consume future group budget; outer controller owns stopping");
        foreach(int maximum in new[]{1,2,3,9,255})
        foreach(int draw in new[]{0,maximum-1}.Distinct())
            Check(OriginalMonsterAttackSequence.BeginGroup(OriginalMonsterAttackSequence.Start(1,maximum),true,draw).State.SwingsRemaining==draw+1,
                "Group swing allocation preserves exact minimum and maximum draw boundaries");
        var snapshot=JsonSerializer.Deserialize<MonsterAttackBudget>(JsonSerializer.Serialize(afterMiss))!;
        OriginalMonsterAttackSequence.Validate(snapshot);
        Check(snapshot==afterMiss && OriginalMonsterAttackSequence.CompleteSwing(snapshot)==afterBlocked,"Derived budget snapshot resumes exact continuation after roundtrip");
        var maxState=OriginalMonsterAttackSequence.Start(255,255);
        for(int group=0;group<255;group++)
        {
            maxState=OriginalMonsterAttackSequence.BeginGroup(maxState,true,254).State;
            for(int swing=0;swing<255;swing++)maxState=OriginalMonsterAttackSequence.CompleteSwing(maxState);
        }
        Check(maxState.GroupsAttempted==255 && maxState.SwingsCompleted==65025 && maxState.SwingsRemaining==0 && maxState.GroupsRemaining==0,
            "Full byte-sized group/swing product completes without byte counter wrapping");
        var selected=OriginalMonsterAttackSequence.SelectChoice([a],0)!;
        var hero=OriginalCharacterDefense.Build(new(50,0,0,0,false),new(new(0,5,0,0,0),0,0),new(0,0,false,false,false),new(0,0,0,false));
        var defense=OriginalCharacterDefense.Against(hero,CharacterHitLocation.Torso,selected.Mode,new(false,false,false));
        var attrs=new PhysicalCombatAttributes(80,80,80,80);
        var input=new MonsterPhysicalScoreInput(100,0,0,attrs,attrs,selected.Mode,defense.Hit,defense.Penetration,0,MonsterAttackFatiguePolicy.FullHitOnly);
        var score=OriginalMonsterScores.Build(input);
        Check(selected.TargetId=="hero-1" && score.HitScore==90 && score.PenetrationScore==85,
            "Selected attack/mode/target composes with character torso defense and monster scores");
        Check(OriginalPhysicalScores.Penetrates(score.PenetrationScore,84) && !OriginalPhysicalScores.Penetrates(score.PenetrationScore,85),
            "Series budget is independent of inclusive penetration result");
        Reject<ArgumentException>(()=>OriginalMonsterAttackSequence.SelectChoice([],0),"Empty list rejects spurious choice draw");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterAttackSequence.SelectChoice(candidates,null),"Nonempty list requires choice draw");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterAttackSequence.SelectChoice(candidates,-1),"Negative choice draw rejected");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterAttackSequence.SelectChoice(candidates,3),"Choice draw cannot reach count");
        Reject<ArgumentNullException>(()=>OriginalMonsterAttackSequence.SelectChoice(null!,null),"Eligible list required");
        Reject<ArgumentNullException>(()=>OriginalMonsterAttackSequence.SelectChoice([null!],0),"Candidate required");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterAttackSequence.SelectChoice([a,b with {AttackSlot=3}],0),"Validate even unselected attack slots");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterAttackSequence.SelectChoice([a with {Mode=(OriginalAttackMode)9}],0),"Invalid mode rejected");
        Reject<ArgumentException>(()=>OriginalMonsterAttackSequence.SelectChoice([a with {TargetId=" "}],0),"Target identity required");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterAttackSequence.Modes(-1),"Negative mask rejected");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterAttackSequence.Modes(65536),"Mask must retain word width");
        Reject<ArgumentException>(()=>OriginalMonsterAttackSequence.Start(1,0),"Positive groups require a resolved nonzero swing maximum");
        Reject<ArgumentException>(()=>OriginalMonsterAttackSequence.Start(256,1),"Group count must retain byte width");
        Reject<ArgumentException>(()=>OriginalMonsterAttackSequence.Start(1,256),"Swing maximum must retain byte width");
        Reject<ArgumentException>(()=>OriginalMonsterAttackSequence.Start(-1,1),"Negative budget rejected");
        Check(OriginalMonsterAttackSequence.Start(0,0).GroupsRemaining==0,"Zero budget has no attack and requires no allocation");
        Reject<InvalidOperationException>(()=>OriginalMonsterAttackSequence.BeginGroup(OriginalMonsterAttackSequence.Start(0,0),true,null),"No group can start after exhaustion");
        Reject<InvalidOperationException>(()=>OriginalMonsterAttackSequence.BeginGroup(begun.State,true,0),"Cannot overwrite unfinished swings");
        Reject<InvalidOperationException>(()=>OriginalMonsterAttackSequence.CompleteSwing(finished),"Finished state cannot complete twice");
        Reject<ArgumentException>(()=>OriginalMonsterAttackSequence.BeginGroup(initial,false,0),"Unavailable group accepts no RNG");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterAttackSequence.BeginGroup(initial,true,null),"Available group needs swing draw");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterAttackSequence.BeginGroup(initial,true,3),"Swing draw must be exclusive maximum");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterAttackSequence.BeginGroup(initial,true,-1),"Negative swing draw rejected");
        foreach(var invalid in new[]{initial with {GroupsAttempted=1},initial with {SwingsRemaining=1},afterMiss with {SwingsCompleted=int.MaxValue},
            afterMiss with {SwingsRemaining=4},afterMiss with {SwingsCompleted=-1},afterMiss with {GroupsRemaining=-1},
            afterMiss with {GroupsAttempted=-1},afterMiss with {GroupsAttempted=256},afterMiss with {GroupsRemaining=256}})
            Reject<ArgumentException>(()=>OriginalMonsterAttackSequence.Validate(invalid),"Reject inconsistent or overflowing derived budget state");
        Reject<ArgumentNullException>(()=>OriginalMonsterAttackSequence.Validate(null!),"Budget state required");
        return checks;
    }
}
