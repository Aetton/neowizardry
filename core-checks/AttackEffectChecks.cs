using System.Text.Json;
using NeoWizardry.Core;
using NeoWizardry.Core.Rules;

static class AttackEffectChecks
{
    public static int Run(MonsterCatalog? originals=null)
    {
        int checks=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
        void Reject(Action action,string name){bool failed=false;try{action();}catch(ArgumentException){failed=true;}catch(InvalidOperationException){failed=true;}catch(InvalidDataException){failed=true;}catch(OverflowException){failed=true;}Check(failed,name);}
        if(originals is not null)
        {
            int withEffects=0;
            foreach(var row in originals.Monsters)
            {
                var raw=Convert.FromHexString(row.RawRecordHex);var reference=MonsterAttackInspector.Inspect(row);
                Check(reference.DefinitionLevel==raw[0x24f] && reference.EffectStrength==raw[0x24f]+Math.Min((int)raw[0x24f],15),"Every original effect strength follows its raw source level");
                Check(reference.Attacks.Length==Enumerable.Range(0,3).Count(s=>raw[0xe7+34*s]!=0),"Inspector retains every enabled original slot, including blocked ones");
                foreach(var a in reference.Attacks)
                    Check(a.Effects.Select(e=>e.Probability).SequenceEqual(raw[(0xe7+34*a.Slot+5)..(0xe7+34*a.Slot+21)].Select(b=>(int)b)) && a.SecondaryParameter==raw[0xe7+34*a.Slot+27],"Every original effect chance/parameter matches its unmodified record");
                if(reference.Attacks.Any(a=>a.Effects.Any(e=>e.Probability!=0)))
                {withEffects++;Check(!MonsterCombat.Supported(row),"Recovered effect requests never silently unlock missing status consumers");}
            }
            Console.WriteLine($"PASS: attack effects inspected for {originals.Count} original records, {withEffects} with enabled payloads; admission remains conservative");
            return checks;
        }
        bool gates=true;for(int probability=1;probability<=255;probability++)for(int draw=0;draw<100;draw++)gates &= OriginalAttackEffects.Triggers(probability,draw)==(draw<probability);
        Check(gates && !OriginalAttackEffects.Triggers(0,null),"All unsigned-byte chance values and strict d100 boundaries, including >100");
        Reject(()=>OriginalAttackEffects.Triggers(0,0),"Disabled payload requests no chance draw");Reject(()=>OriginalAttackEffects.Triggers(100,null),"Guaranteed payload still requests a chance draw");
        foreach(int bad in new[]{-1,100})Reject(()=>OriginalAttackEffects.Triggers(25,bad),"Invalid effect chance roll");
        int?[] conditionIds=[15,16,7,9,2,null,17,12,6,null,null,null,null,null,4,11];
        int?[] realms=[2,1,1,5,1,null,3,0,4,null,null,null,null,null,2,4];
        for(int i=0;i<16;i++)
        {
            var route=OriginalAttackEffects.Route(i);Check(route?.ConditionId==conditionIds[i] && route?.RealmIndex==realms[i],"Native switch routes match the recovered condition/realm constants");
            if(route is null)Reject(()=>OriginalAttackEffects.Request(i,20,3),"Special/resource dispatch cannot masquerade as an ordinary condition");
        }
        Check(OriginalAttackEffects.Strength(0)==0 && OriginalAttackEffects.Strength(14)==28 && OriginalAttackEffects.Strength(15)==30 && OriginalAttackEffects.Strength(16)==31 && OriginalAttackEffects.Strength(255)==270,"Effect strength cap applies to added level contribution only");
        bool durations=true;
        for(int level=0;level<=255;level++)for(int gate=0;gate<4;gate++)
        {
            int strength=OriginalAttackEffects.Strength(level),basis=strength/3+1;
            var common=OriginalAttackEffects.DurationPlan(strength,gate);
            int low=OriginalAttackEffects.Duration(common,Enumerable.Repeat(0,common.Dice).ToArray());
            int high=OriginalAttackEffects.Duration(common,Enumerable.Repeat(1,common.Dice).ToArray());
            durations &= common.Dice==(gate==1?0:basis) && low==(gate==1?2:Math.Min(12,basis+2)) && high==(gate==1?2:Math.Min(12,2*basis+2));
            foreach(int parameter in new[]{0,1,100,255})
            {
                var secondary=OriginalAttackEffects.DurationPlan(strength,gate,true,parameter);int count=unchecked((byte)(3*basis));
                durations &= secondary.Dice==(gate==1?0:count) && OriginalAttackEffects.Duration(secondary,Enumerable.Repeat(0,secondary.Dice).ToArray())==(gate==1?2:parameter+1+count);
            }
        }
        Check(durations,"All 256 definition levels and four duration gates, secondary parameters and byte-sized dice overflow");
        Check(OriginalAttackEffects.Duration(OriginalAttackEffects.DurationPlan(270,0,true,255),Enumerable.Repeat(1,17).ToArray())==290,"High-level secondary stores 273 dice as native byte 17 and remains unclamped");
        Reject(()=>OriginalAttackEffects.Duration(OriginalAttackEffects.DurationPlan(20,0),[]),"Missing duration draws cannot be silently generated");
        Reject(()=>OriginalAttackEffects.Duration(OriginalAttackEffects.DurationPlan(0,0),[2]),"Duration die rejects an out-of-range d2");
        Reject(()=>OriginalAttackEffects.Duration(new(1,1,2,2,true),[0]),"Gate-two path accepts no forged fallback dice");
        var ordinary=OriginalAttackEffects.Request(0,30,12);Check(ordinary==new AttackConditionRequest(15,2,30,12,0),"Condition request retains source strength and duration");
        var permanent=OriginalAttackEffects.Request(4,30,2);Check(permanent.Duration is null,"Permanent condition uses no invented 9999-round timer");
        var secondaryRequest=OriginalAttackEffects.Request(2,30,12,101,77);Check(secondaryRequest.Duration==101 && secondaryRequest.SecondaryParameter==77,"Secondary second duration replaces the consumed common stage and retains its parameter");
        Reject(()=>OriginalAttackEffects.Request(2,30,12),"Secondary cannot skip its separate second stage");Reject(()=>OriginalAttackEffects.Request(0,30,12,3),"Ordinary condition accepts no secondary duration");
        int[] offsets=[0,1,2,3,4,4,5,6,7,8,9,10,12,14,16,20,18,22,24,22];bool resistance=true;
        for(int id=0;id<20;id++)for(int raw=-10;raw<=110;raw++)foreach(var scale in Enum.GetValues<PhysicalScoreScale>())
        {
            var score=OriginalAttackEffects.Resistance(10,10,raw-offsets[id],id,scale);
            int expected=scale==PhysicalScoreScale.Unscaled?raw:scale==PhysicalScoreScale.SixtyPercent?raw*3*20/100:raw*7*20/100;
            resistance &= score.RawScore==raw && score.ScaledScore==expected && score.Threshold==Math.Clamp(expected,5,95);
            for(int draw=0;draw<100;draw++)resistance &= OriginalAttackEffects.Resisted(score,draw)==(draw<Math.Clamp(expected,5,95));
        }
        Check(resistance,"Every condition offset, score scale, clamp and strict binary-resistance boundary");
        Check(OriginalAttackEffects.Resistance(12,10,30,7).RawScore==42,"Condition resistance combines level difference, realm resistance and condition offset");
        Reject(()=>OriginalAttackEffects.Resistance(0,0,int.MaxValue,19),"Condition resistance preserves checked arithmetic");
        Reject(()=>OriginalAttackEffects.Resisted(new(0,0,0),0),"Malformed resistance cannot bypass the five-percent floor");
        var fixture=GeneralMonsterChecks.Fixture(b=>{b[0x24f]=16;b[0xec]=101;b[0xe7+7]=25;b[0xe7+27]=33;});
        var inspected=MonsterAttackInspector.Inspect(fixture.Monsters[0]);Check(inspected.EffectStrength==31 && inspected.Attacks[0].Effects[0].Probability==101 && inspected.Attacks[0].Effects[2].Probability==25 && inspected.Attacks[0].SecondaryParameter==33,"Portable raw fixture connects typed effect fields and unsigned parameters");
        Check(!MonsterCombat.Supported(fixture.Monsters[0]),"Inspection never enables incomplete status execution");
        Check(JsonSerializer.Deserialize<MonsterEffectReference>(JsonSerializer.Serialize(inspected))!.Attacks[0].Effects[2].Condition==inspected.Attacks[0].Effects[2].Condition,"Effect reference facts roundtrip without changing native route");
        Reject(()=>MonsterAttackInspector.Inspect(fixture.Monsters[0] with {RecordSha256=new string('0',64)}),"Inspector refuses forged raw digest");
        var effect=new AttackConditionInput(0,25,30,10,30);
        Check(OriginalAttackEffects.ResolveCondition(effect,[25]).Outcome==AttackConditionOutcome.NotTriggered,"Chance equality stops before duration/resistance draws");
        var resolved=OriginalAttackEffects.ResolveCondition(effect,[24,1,99]);
        Check(resolved.Outcome==AttackConditionOutcome.Requested && resolved.Request!.Duration==2 && resolved.Draws.Select(d=>d.Stage).SequenceEqual(new[]{"chance","common-duration-gate","resistance"}),"Selected condition executes complete chance/duration/resistance request chain");
        Check(OriginalAttackEffects.ResolveCondition(effect,[24,1,0]).Outcome==AttackConditionOutcome.Resisted,"Resistance blocks request after consuming duration");
        var immune=OriginalAttackEffects.ResolveCondition(effect with {Immune=true},[24,1]);Check(immune.Outcome==AttackConditionOutcome.Immune && immune.Resistance is null,"Resolved immunity suppresses resistance draw but preserves earlier duration gate");
        Check(OriginalAttackEffects.ResolveCondition(effect with {Probability=0},[]).Draws.Length==0,"Disabled condition consumes no chain draws");
        var forever=OriginalAttackEffects.ResolveCondition(effect with {EffectIndex=4},Enumerable.Repeat(0,13).Append(99).ToArray());
        Check(forever.Request is {Duration:null} && forever.Draws.Count(d=>d.Exclusive==2)==11,"Permanent request still consumes common fallback duration dice");
        var second=OriginalAttackEffects.ResolveCondition(effect with {EffectIndex=2,SecondaryParameter=77},[0,1,1,99]);
        Check(second.Request is {Duration:2,SecondaryParameter:77} && second.Draws.Select(d=>d.Stage).SequenceEqual(new[]{"chance","common-duration-gate","secondary-duration-gate","resistance"}),"Effect two consumes separate duration gates in native order");
        Reject(()=>OriginalAttackEffects.ResolveCondition(effect,[0,1]),"Missing resistance draw fails without partial mutable state");
        Reject(()=>OriginalAttackEffects.ResolveCondition(effect,[99,0]),"Failed chance rejects unused duration draws");
        Reject(()=>OriginalAttackEffects.ResolveCondition(effect with {EffectIndex=12},[0]),"Resource branch rejected before an ordinary-condition execution");
        return checks;
    }
}
