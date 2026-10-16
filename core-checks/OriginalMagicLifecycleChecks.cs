using System.Collections.Immutable;
using NeoWizardry.Core;
using NeoWizardry.Core.Rules;

static class OriginalMagicLifecycleChecks
{
    public static int Run(SpellCatalog spells)
    {
        if(!spells.Spells.Any(s=>s.Id==6 && RestoredMagic.SupportedSpell(s)))return 0;
        int count=0;void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;}
        void Reject(Action action,string message){bool yes=false;try{action();}catch(InvalidOperationException){yes=true;}Check(yes,message);}
        var cat=MeleeTraining.Catalog();var start=MeleeTraining.Create(1,cat,spells);
        start=start with {Party=start.Party.SetItem(0,start.Party[0] with {Hp=5000,MaxHp=5000}),Enemy=start.Enemy with {Hp=5000}};
        var order=new RestoredCastOrder("hero-1",5,7);var prepared=RestoredMagic.Prepare(start,order,spells);
        Check(prepared.Risk.FailureThreshold==14 && prepared.ManaCost==14,"Default Bishop exposes native risky PL7");
        var found=new HashSet<CastingOutcome>();
        for(uint seed=1;seed<=160;seed++)
        {
            uint rng=seed;found.Add(RestoredMagic.Outcome(prepared,ref rng));
            var state=start with {RandomState=seed};string before=Game.Save(state);
            var result=MeleeCombat.Advance(state,cat,3,[order],spells);
            Check(Game.Save(state)==before,"Risky round leaves input snapshot intact");
            Check(result.State.Party[0].Mana!.Single(m=>m.Realm=="Fire").Current==28,"Success, fizzle and backfire pay the same ordinary cost");
            Check(Game.Save(MeleeCombat.Advance(Game.Load(before,cat),cat,3,[order],spells).State)==Game.Save(result.State),"Every risky outcome resumes after save/load");
            var entry=result.Events.Single(e=>e.Contains("Energy Blast PL7"));
            if(entry.Contains("Fizzle") || entry.Contains("Backfire"))Check(result.State.Enemy.Hp==5000,"Failed offensive cast never damages original enemy target");
        }
        Check(found.SetEquals(new[]{CastingOutcome.Success,CastingOutcome.Fizzle,CastingOutcome.Backfire}),"All three native outcomes covered");
        var f=start.Melee!.Magic!["hero-1"] with {BookSkill=80,RealmSkills=[80,80,80,80,80,80]};
        var safe=start with {Melee=start.Melee with {Magic=start.Melee.Magic.SetItem("hero-1",f)}};
        var heal=new RestoredCastOrder("hero-1",6,7,"hero-1");var packet=RestoredMagic.Prepare(safe,heal,spells);
        uint safeRng=42;_=RestoredMagic.Outcome(packet,ref safeRng);Check(safeRng==42,"Safe heal consumes no failure roll");
        uint a=57,b=57;int normal=RestoredMagic.Amount(packet,ref a);
        int boosted=RestoredMagic.Amount(packet with {Facts=f with {PowerCastEnabled=true,PowerCastSkill=100}},ref b);
        Check(boosted==normal+normal*26/100 && a==b,"Heal Wounds uses 28d4 and full integer Power Cast without resistance draws");
        var wounded=safe with {Party=safe.Party.SetItem(0,safe.Party[0] with {Hp=4997})};
        var restored=MeleeCombat.Advance(wounded,cat,3,[heal],spells);
        Check(restored.Events.Any(e=>e.Contains("Heal Wounds PL7") && e.Contains("+3 HP")),"Successful heal clamps to missing HP");
        Check(restored.State.Party[0].Mana!.Single(m=>m.Realm=="Divine").Current==21,"Healing pays only Divine mana");
        var full=MeleeCombat.Advance(safe,cat,3,[heal],spells);
        Check(full.Events.Any(e=>e.Contains("Heal Wounds PL7") && e.Contains("+0 HP")),"Full-health healing has no healing dice");
        Check(full.State.Party[0].Mana!.Single(m=>m.Realm=="Divine").Current==21,"Full-health cast still pays mana");
        var riskyHeal=RestoredMagic.Prepare(start,heal,spells);bool fizzled=false;
        for(uint seed=1;seed<=160;seed++)
        {
            uint rng=seed;var outcome=RestoredMagic.Outcome(riskyHeal,ref rng);
            Check(outcome!=CastingOutcome.Backfire,"Native Heal Wounds classifier disables backfire");fizzled|=outcome==CastingOutcome.Fizzle;
        }
        Check(fizzled,"Risky healing can fizzle");
        Reject(()=>RestoredMagic.Prepare(start,heal with {TargetId=start.Enemy.Id},spells),"Healing rejects enemy target");
        Reject(()=>RestoredMagic.Prepare(start,order with {TargetId="hero-1"},spells),"Offensive order cannot manually choose caster as enemy");
        var pair=MeleeTraining.Create(2,cat,spells);var allyHeal=new RestoredCastOrder("hero-1",6,1,"hero-2");
        Check(RestoredMagic.Prepare(pair,allyHeal,spells).TargetId=="hero-2","Party member is a valid healing target");
        var dead=pair with {Party=pair.Party.SetItem(1,pair.Party[1] with {Hp=0})};
        Reject(()=>RestoredMagic.Prepare(dead,allyHeal,spells),"Heal Wounds does not resurrect");
        return count;
    }
}
