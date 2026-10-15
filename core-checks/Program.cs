using NeoWizardry.Core.Research;
using NeoWizardry.Core;

int checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
void Throws(Action action, string name)
{
    bool threw = false;
    try { action(); } catch (Exception) { threw = true; }
    Check(threw, name);
}
for (int size = 1; size <= 8; size++)
{
    var start = Game.Create(size);
    var state = start;
    for (int i = 0; i < 20 && state.Enemy.Hp > 0; i++) state = Game.Attack(state).State;
    Check(state.Enemy.Hp == 0, $"Victory {size}");
    Check(state.Party.Sum(h => h.Xp) == 120, $"XP conservation {size}");
    Check(Game.Attack(state).State.Party.Sum(h => h.Xp) == 120, $"No duplicate XP {size}");
    Check(start.Enemy.Hp == 32 && start.Party.All(h => h.Xp == 0 && h.Hp == 36), "Immutable transition");
    Check(Game.Save(Game.Load(Game.Save(state))) == Game.Save(state), "Save roundtrip");
    var captive = Game.CaptureFirst(start);
    Check(Game.Escape(Game.Load(Game.Save(captive))).Party.All(h => !h.Captive), "Escape without second hero");
    if (size == 1) Check(Game.Attack(captive).State.Round == 0, "Captive cannot fight");
    Check(Game.Save(Game.Attack(start).State) == Game.Save(Game.Attack(start).State), "Deterministic replay");
}
var solo = Game.Create(1);
var spark=new SpellOrder("hero-1","test-spark",2,solo.Enemy.Id);
var sparked=Game.Cast(solo,spark).State;
Check(sparked.Party[0].Mana!.Single(p=>p.Realm=="Fire").Current==8 && sparked.Party[0].Mana!.Single(p=>p.Realm=="Divine").Current==12,"Spell spends only its realm mana");
Check(sparked.Enemy.Hp==19 && sparked.Round==1,"Power-two synthetic spell replaces the physical attack");
Check(solo.Enemy.Hp==32 && solo.Party[0].Mana!.All(p=>p.Current==12),"Casting leaves source state unchanged");
var wounded=solo with { Party=[solo.Party[0] with { Hp=10 }] };
var healed=Game.Cast(wounded,new("hero-1","test-mend",2,"hero-1")).State;
Check(healed.Party[0].Hp==20 && healed.Enemy.Hp==32,"Healing uses its turn and enemy still acts");
Check(healed.Party[0].Mana!.Single(p=>p.Realm=="Divine").Current==8,"Self healing preserves mana spending");
var nearFull=solo with { Party=[solo.Party[0] with { Hp=35 }],Enemy=solo.Enemy with { Power=0 } };
Check(Game.Cast(nearFull,new("hero-1","test-mend",3,"hero-1")).State.Party[0].Hp==36,"Healing is capped at maximum HP");
foreach(int power in new[]{0,4})Throws(()=>Game.Cast(solo,spark with { Power=power }),"Reject unsupported spell power");
Throws(()=>Game.Cast(solo,spark with { SpellId="unknown" }),"Reject unknown spell");
Throws(()=>Game.Cast(solo,spark with { HeroId="unknown" }),"Reject unknown caster");
Throws(()=>Game.Cast(solo,spark with { TargetId="hero-1" }),"Offensive spell rejects friendly target");
Throws(()=>Game.Cast(solo,new("hero-1","test-mend",1,"hero-1")),"Reject healing a full-health target");
Throws(()=>Game.Cast(solo,new("hero-1","test-mend",1,solo.Enemy.Id)),"Healing rejects enemy target");
Throws(()=>Game.Cast(solo with { Party=[solo.Party[0] with { Spellbook=[] }] },spark),"Reject unlearned spell");
Throws(()=>Game.Cast(solo with { Party=[solo.Party[0] with { Mana=[new("Fire",0,12)] }] },spark),"Insufficient mana rejects the round");
Throws(()=>Game.Cast(Game.CaptureFirst(solo),spark),"Captive cannot cast");
Throws(()=>Game.Cast(solo with { Enemy=solo.Enemy with { Hp=0 } },spark),"No casting after battle");
var killedCaster=Game.Cast(solo with { Party=[solo.Party[0] with { Hp=1,Speed=0 }] },spark).State;
Check(killedCaster.Enemy.Hp==32 && killedCaster.RandomState==solo.RandomState && killedCaster.Party[0].Mana!.All(p=>p.Current==12),"Killed queued caster spends no mana or rolls");
var doomedTarget=Game.Create(2);
doomedTarget=doomedTarget with { Party=[doomedTarget.Party[0] with { Hp=1,Speed=0 },doomedTarget.Party[1] with { Speed=0 }] };
var cancelledHeal=Game.Cast(doomedTarget,new("hero-2","test-mend",1,"hero-1")).State;
Check(cancelledHeal.Party[0].Hp==0 && cancelledHeal.Party[1].Mana!.All(p=>p.Current==12) && cancelledHeal.RandomState==doomedTarget.RandomState,"Target killed before healing causes no resurrection or mana loss");
var earlyKill=Game.Create(2) with { Enemy=solo.Enemy with { Hp=1 } };
var skippedCast=Game.Cast(earlyKill,new("hero-2","test-spark",1,solo.Enemy.Id)).State;
Check(skippedCast.Party[1].Mana!.All(p=>p.Current==12) && skippedCast.RandomState==earlyKill.RandomState,"Earlier ally victory skips queued spell without costs");
Check(Game.Save(Game.Cast(sparked,spark).State)==Game.Save(Game.Cast(Game.Load(Game.Save(sparked)),spark).State),"Magic RNG and mana continue identically after save/load");
Throws(()=>Game.Validate(solo with { Party=[solo.Party[0] with { Mana=[new("Fire",13,12)] }] }),"Reject invalid mana quantities");
Throws(()=>Game.Validate(solo with { Party=[solo.Party[0] with { Mana=[new("Fire",1,12),new("Fire",2,12)] }] }),"Reject duplicate mana realms");
Throws(()=>Game.Validate(solo with { Party=[solo.Party[0] with { Spellbook=["test-spark","test-spark"] }] }),"Reject duplicate learned spells");
var preMagic=System.Text.Json.Nodes.JsonNode.Parse(Game.Save(solo))!;
foreach(var h in preMagic["Party"]!.AsArray()){h!.AsObject().Remove("Mana");h.AsObject().Remove("Spellbook");}
var oldMagic=Game.Load(preMagic.ToJsonString());
Check(oldMagic.Party[0].Mana is null && oldMagic.Party[0].Spellbook is null,"Old saves do not gain retroactive spells or mana");
for(int size=1;size<=8;size++)
{
    var magicParty=Game.Create(size);
    magicParty=magicParty with { Party=magicParty.Party.Select(h=>h with { Profession="Mage" }).ToArray() };
    for(int i=0;i<20 && magicParty.Enemy.Hp>0;i++)
        magicParty=magicParty.Party[0].Mana!.Single(p=>p.Realm=="Fire").Current>=2
            ? Game.Cast(magicParty,new("hero-1","test-spark",1,magicParty.Enemy.Id)).State : Game.Attack(magicParty).State;
    Check(magicParty.Enemy.Hp==0 && magicParty.Party.Sum(h=>h.Xp)==120,$"Magic-oriented party {size} retains conserved XP");
}
Throws(() => Game.Create(0), "No empty party");
Throws(() => Game.Create(9), "UI limit enforced");
Throws(() => Game.Load(Game.Save(solo).Replace("\"Schema\": 2", "\"Schema\": 99")), "Reject unknown save version");
Throws(() => Game.Validate(solo with { Party = [solo.Party[0], solo.Party[0]] }), "Reject duplicate IDs");
Throws(() => Game.Escape(solo), "Escape requires capture");
var fast = solo with { Party = [solo.Party[0] with { Speed = 0, Hp = 1 }] };
var dead = Game.Attack(fast).State;
Check(dead.Party[0].Hp == 0 && dead.Enemy.Hp == 32, "Dead actor cannot execute queued attack");
// These validate the published model and reported level breakpoints, not the original executable.
foreach (var (multiplier, lower, upper) in new[] { (4,18,19), (3,24,26), (2,37,38) })
{
    Check(LegacyResearch.SingleClassAttackScore(125,125,100,100,lower,multiplier)<600, "Candidate score below breakpoint");
    Check(LegacyResearch.SingleClassAttackScore(125,125,100,100,upper,multiplier)>600, "Candidate score above breakpoint");
}
Check(LegacyResearch.SingleClassAttackScore(125,125,100,100,25,3)==599.9999m,
    "Published decimal approximation straddles reported level-25 threshold");
Check(LegacyResearch.SingleClassAttackScore(125,125,100,125,14,4)<600, "Primary weapon candidate below breakpoint");
Check(LegacyResearch.SingleClassAttackScore(125,125,100,125,15,4)>600, "Primary weapon candidate above breakpoint");
Throws(() => LegacyResearch.SingleClassAttackScore(126,0,0,0,1,4), "Reject uncapped effective attributes");
Throws(() => LegacyResearch.SingleClassAttackScore(0,0,0,0,0,4), "Reject invalid level");
Throws(() => LegacyResearch.SingleClassAttackScore(0,0,0,0,1,5), "Reject unknown profession coefficient");
var restricted = new ItemDefinition(77,"Restricted test staff",0,0,32,32,3,new string('a',64),5,0,2,1,1,2,4,7,50,false,0);
var catalog = new ItemCatalog(2,"DATABASES/ITEMS.DBS",new Provenance("archive","PATCH.001",new string('b',64)),269,1,[restricted]);
catalog.Validate();
foreach(int bonus in new[]{256,-257,int.MaxValue,int.MinValue})
{
    var wideBonus=catalog with { Items=[restricted with { BonusSwings=bonus }] };
    Check(ItemCatalog.Parse(System.Text.Json.JsonSerializer.Serialize(wideBonus,
        new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy=System.Text.Json.JsonNamingPolicy.SnakeCaseLower })).Items[0].BonusSwings==bonus,
        "Item catalogs retain signed dword bonus independently of executable allocation validation");
}
var ownedSolo=Inventory.Add(solo,77,1,catalog);
var equipped = Equipment.Equip(ownedSolo,solo.Party[0].Id,77,catalog);
Check(Inventory.Count(equipped,77)==0 && Inventory.Count(ownedSolo,77)==1,"Equip moves one copy out of bag without mutation");
Throws(()=>Equipment.Equip(solo,"hero-1",77,catalog),"Cannot equip an unowned weapon");
var returned=Equipment.Unequip(equipped,"hero-1",catalog);
Check(Equipment.ItemId(returned,returned.Party[0].Id) is null && Inventory.Count(returned,77)==1,"Unequip returns exactly one copy");
Check(Game.Save(Equipment.Unequip(returned,"hero-1",catalog))==Game.Save(returned),"Repeated unequip creates no copy");
Check(Game.Save(Equipment.Equip(equipped,"hero-1",77,catalog))==Game.Save(equipped),"Repeated equip creates no copy");
var common=restricted with { RaceMask=65535 };
var commonCatalog=catalog with { Items=[common] };
var shared=Inventory.Add(Game.Create(2),77,1,commonCatalog);
shared=Equipment.Equip(shared,"hero-1",77,commonCatalog);
Throws(()=>Equipment.Equip(shared,"hero-2",77,commonCatalog),"Two heroes cannot share one equipped copy");
shared=Equipment.Equip(Equipment.Unequip(shared,"hero-1",commonCatalog),"hero-2",77,commonCatalog);
Check(Equipment.ItemId(shared,shared.Party[0].Id) is null && Equipment.ItemId(shared,shared.Party[1].Id)==77 && Inventory.Count(shared,77)==0,"Transfer through shared bag conserves copy");
var spare=restricted with { Id=78,Name="Replacement fixture" };
var swapCatalog=catalog with { Items=[restricted,spare],Count=2 };
var swapped=Equipment.Equip(Inventory.Add(equipped,78,1,swapCatalog),"hero-1",78,swapCatalog);
Check(Equipment.ItemId(swapped,swapped.Party[0].Id)==78 && Inventory.Count(swapped,77)==1 && Inventory.Count(swapped,78)==0,"Weapon swap returns old weapon and consumes replacement");
string beforeSwap=Game.Save(equipped);
Throws(()=>Equipment.Equip(equipped,"hero-1",78,swapCatalog),"Unavailable replacement rejected");
Check(Game.Save(equipped)==beforeSwap,"Rejected swap leaves original weapon and bag intact");
Throws(()=>Inventory.Remove(ownedSolo,77,2,catalog),"Insufficient quantity rejected");
Throws(()=>Inventory.Add(solo,77,0,catalog),"Zero addition rejected");
Throws(()=>Inventory.Add(solo,999,1,catalog),"Unknown bag item rejected");
Throws(()=>Inventory.Add(solo,77,int.MaxValue,catalog),"Unsafe number of individual instances rejected before allocation");
Throws(()=>Game.Validate(ownedSolo with { Items=ownedSolo.Items! with {Items=[ownedSolo.Items.Items[0],ownedSolo.Items.Items[0]]} }),"Duplicate bag stacks rejected");
Throws(()=>Game.Validate(ownedSolo with { Items=ownedSolo.Items! with {Items=[ownedSolo.Items.Items[0] with {Quantity=-1}]} }),"Negative bag quantity rejected");
Check(Game.Save(Game.Load(Game.Save(swapped)))==Game.Save(swapped),"Bag and equipment roundtrip together");
Throws(()=>Equipment.ValidateLoadedEquipment(ownedSolo,null,null),"Bag-only save requires catalog");
Throws(()=>Equipment.ValidateLoadedEquipment(ownedSolo,catalog,new string('c',64)),"Bag-only save rejects changed catalog hash");
Throws(()=>Game.Attack(solo with { Enemy=solo.Enemy with { LootItemId=999 } },catalog),"Unknown configured loot rejected before battle");
var lootBattle=solo with { Enemy=solo.Enemy with { Hp=1,LootItemId=77 } };
var wonLoot=Game.Attack(lootBattle,catalog).State;
Check(wonLoot.RewardGranted && Inventory.Count(wonLoot,77)==1,"Victory grants configured test loot");
Check(Inventory.Count(Game.Attack(Game.Load(Game.Save(wonLoot)),catalog).State,77)==1,"Reloaded victory cannot duplicate loot");
var legacyBagSave=System.Text.Json.Nodes.JsonNode.Parse(Game.Save(equipped))!;
legacyBagSave["Schema"]=1;legacyBagSave.AsObject().Remove("Items");legacyBagSave.AsObject().Remove("NextItemSequence");
legacyBagSave["Party"]![0]!["EquippedItemId"]=77;
var legacyEquipment=Game.Load(legacyBagSave.ToJsonString(),catalog);
Check(Inventory.Count(legacyEquipment,77)==0 && Inventory.Count(Equipment.Unequip(legacyEquipment,"hero-1",catalog),77)==1,"Old equipped save retains its single owned copy");
// Synthetic crafting invariants: exact quantities, no equipped-item consumption, actor gates.
var craftCatalog=swapCatalog with { Count=5,Items=[restricted,spare with { Name="Test herb",TypeCode=19 },restricted with { Id=79,Name="Test vial",TypeCode=19 },restricted with { Id=80,Name="Test tonic",TypeCode=15 },restricted with { Id=81,Name="Tempered test staff",DamageConstant=3 }],Provenance=catalog.Provenance with { File=Crafting.FixtureSource } };
var craftStart=Inventory.Add(Inventory.Add(Inventory.Add(solo,78,4,craftCatalog),79,2,craftCatalog),77,1,craftCatalog) with { KnownRecipes=["test-tonic","test-staff"] };
var tonicOrder=new CraftOrder("hero-1","test-tonic");
var tonic=Crafting.Craft(craftStart,tonicOrder,craftCatalog);
Check(Inventory.Count(tonic,78)==2 && Inventory.Count(tonic,79)==1 && Inventory.Count(tonic,80)==1,"Craft consumes exact recipe quantities");
Check(Inventory.Count(craftStart,78)==4 && Inventory.Count(craftStart,80)==0,"Craft immutable source");
Check(tonic.Round==craftStart.Round && tonic.RandomState==craftStart.RandomState,"Fixture crafting spends no combat turn or random roll");
var improved=Crafting.Craft(tonic,new("hero-1","test-staff"),craftCatalog);
Check(Inventory.Count(improved,77)==0 && Inventory.Count(improved,80)==0 && Inventory.Count(improved,81)==1,"Craft chain consumes intermediate and produces correct result");
Check(Equipment.ItemId(Equipment.Equip(improved,"hero-1",81,craftCatalog),"hero-1")==81,"Crafted weapon is a real owned copy");
var worn=Equipment.Equip(tonic,"hero-1",77,craftCatalog);
var beforeCraft=Game.Save(worn);
Throws(()=>Crafting.Craft(worn,new("hero-1","test-staff"),craftCatalog),"Equipped copy cannot be consumed by crafting");
Check(Game.Save(worn)==beforeCraft,"Rejected craft preserves equipment and ingredients");
Throws(()=>Crafting.Craft(craftStart,tonicOrder with { HeroId="unknown" },craftCatalog),"Unknown crafter rejected");
Throws(()=>Crafting.Craft(craftStart,tonicOrder with { RecipeId="unknown" },craftCatalog),"Unknown recipe rejected");
Throws(()=>Crafting.Craft(craftStart with { KnownRecipes=null },tonicOrder,craftCatalog),"Unlearned recipe rejected");
Throws(()=>Crafting.Craft(Game.CaptureFirst(craftStart),tonicOrder,craftCatalog),"Captive crafter rejected");
Throws(()=>Crafting.Craft(craftStart with { Party=[craftStart.Party[0] with { Hp=0 }] },tonicOrder,craftCatalog),"Dead crafter rejected");
foreach(var skills in new SkillValue[]?[]{null,[],[new("test-craft",19)]})
    Throws(()=>Crafting.Craft(tonic with { Party=[tonic.Party[0] with { Skills=skills }] },new("hero-1","test-staff"),craftCatalog),"Skill gate cannot be bypassed");
Check(Inventory.Count(Crafting.Craft(tonic with { Party=[tonic.Party[0] with { Skills=[new("test-craft",20)] }] },new("hero-1","test-staff"),craftCatalog),81)==1,"Exact skill threshold permitted");
Throws(()=>Crafting.Craft(tonic,tonicOrder,craftCatalog with { Provenance=craftCatalog.Provenance with { File="original" } }),"Synthetic recipes cannot apply to original catalog IDs");
Throws(()=>Crafting.Craft(craftStart with {Items=craftStart.Items! with {Capacity=craftStart.Items.Items.Length}},tonicOrder,craftCatalog),"Craft rejects full instance capacity before ingredient loss");
Throws(()=>Crafting.Craft(improved,new("hero-1","test-staff"),craftCatalog),"Recipe cannot duplicate result without ingredients");
Check(Game.Save(Game.Load(Game.Save(improved)))==Game.Save(improved),"Recipes, skill gates and crafted ownership survive save");
Throws(()=>Crafting.ValidateRecipe(Crafting.Recipes[0] with { Ingredients=[new(78,1),new(78,1)] },craftCatalog),"Duplicate recipe ingredients rejected");
Throws(()=>Crafting.ValidateRecipe(Crafting.Recipes[0] with { Result=new(80,0) },craftCatalog),"Empty recipe result rejected");
Throws(()=>Game.Validate(solo with { KnownRecipes=["unknown"] }),"Unknown saved recipe rejected");
Throws(()=>Game.Validate(solo with { KnownRecipes=["test-tonic","test-tonic"] }),"Duplicate saved recipe rejected");
Throws(()=>Game.Validate(solo with { Party=[solo.Party[0] with { Skills=[new("test-craft",-1)] }] }),"Negative skill rejected");
for(int size=1;size<=8;size++)
{
    var c=craftStart with { Party=Game.Create(size).Party };
    Check(Inventory.Count(Crafting.Craft(c,tonicOrder,craftCatalog),80)==1,$"Craft works without required companions for party {size}");
}
// Effect durations include the application round; only direct spells draw dice.
var wardOrder=new SpellOrder("hero-1","test-ward",1,"hero-1");
var warded=Game.Cast(solo,wardOrder).State;
Check(warded.Party[0].Hp==35 && warded.Party[0].Effects!.Single().Rounds==2,"Ward reduces damage and ticks on application round");
Check(warded.RandomState==solo.RandomState,"Status spell consumes no fabricated success dice");
var ward2=Game.Attack(warded).State;var ward3=Game.Attack(ward2).State;var ward4=Game.Attack(ward3).State;
Check(ward2.Party[0].Hp==34 && ward3.Party[0].Hp==33 && ward3.Party[0].Effects!.Length==0 && ward4.Party[0].Hp==30,"Ward expires after exactly three rounds");
var bound=Game.Cast(solo,new("hero-1","test-bind",1,solo.Enemy.Id)).State;
Check(bound.Party[0].Hp==36 && bound.Enemy.Effects!.Length==0,"Fast binding skips only this round enemy action");
Check(Game.Attack(bound).State.Party[0].Hp==33,"Enemy acts normally after binding expires");
var lateBind=Game.Cast(solo with { Party=[solo.Party[0] with { Speed=0 }] },new("hero-1","test-bind",1,solo.Enemy.Id)).State;
Check(lateBind.Party[0].Hp==33 && lateBind.Enemy.Effects!.Length==0,"Late binding cannot retroactively skip an enemy action");
var burned=Game.Cast(solo,new("hero-1","test-burn",2,solo.Enemy.Id)).State;
Check(burned.Enemy.Hp==30 && burned.Enemy.Effects!.Single().Rounds==2,"Burn ticks at round end including cast round");
var burn2=Game.Attack(burned).State;var burn3=Game.Attack(burn2).State;var burn4=Game.Attack(burn3).State;
Check(burn2.Enemy.Hp==21 && burn3.Enemy.Hp==12 && burn3.Enemy.Effects!.Length==0 && burn4.Enemy.Hp==5,"Periodic damage stops after exact duration");
var burnWin=Game.Cast(solo with { Enemy=solo.Enemy with { Hp=2 } },new("hero-1","test-burn",2,solo.Enemy.Id)).State;
Check(burnWin.RewardGranted && burnWin.Party[0].Xp==120 && Game.Attack(burnWin).State.Party[0].Xp==120,"Periodic damage victory grants XP once");
var dual=Game.Create(2);var orders=new[]{wardOrder,new SpellOrder("hero-2","test-burn",1,dual.Enemy.Id)};
var dualRound=Game.CastRound(dual,orders).State;
Check(dualRound.Party[0].Hp==35 && dualRound.Enemy.Hp==31 && dualRound.Round==1,"Both casters replace physical attacks in one round");
Check(dualRound.Party[0].Mana!.Single(m=>m.Realm=="Divine").Current==10 && dualRound.Party[1].Mana!.Single(m=>m.Realm=="Fire").Current==10,"Each caster spends independent realm pool");
Check(Game.Save(Game.CastRound(dual,orders.Reverse().ToArray()).State)==Game.Save(dualRound),"Order submission sequence does not change initiative");
Check(Game.Save(Game.Attack(Game.Load(Game.Save(dualRound))).State)==Game.Save(Game.Attack(dualRound).State),"Saved effects resume exact continuation");
Throws(()=>Game.CastRound(dual,[wardOrder,wardOrder]),"Duplicate caster orders rejected atomically");
Throws(()=>Game.CastRound(dual,[]),"Empty casting plan rejected");
Throws(()=>Game.CastRound(dual,[wardOrder,new("hero-2","unknown",1,dual.Enemy.Id)]),"One invalid plan order rejects entire round");
Check(dual.Round==0 && dual.Party.All(h=>h.Mana!.All(m=>m.Current==12)),"Rejected plan spends no mana or turn");
var refreshed=Magic.Apply([new("ward",6,1)],new("ward",2,3));
Check(refreshed.Length==1 && refreshed[0].Magnitude==6 && refreshed[0].Rounds==3,"Repeated status refreshes duration without stacking or weakening");
Throws(()=>Game.Validate(solo with { Party=[solo.Party[0] with { Effects=[new("burn",1,2)] }] }),"Wrong target effect rejected");
Throws(()=>Game.Validate(solo with { Enemy=solo.Enemy with { Effects=[new("bind",1,0)] } }),"Expired effect rejected in saves");
Throws(()=>Game.Validate(solo with { Enemy=solo.Enemy with { Effects=[new("burn",1,2),new("burn",1,2)] } }),"Duplicate saved effects rejected");
var preFeatures=System.Text.Json.Nodes.JsonNode.Parse(Game.Save(solo))!;
preFeatures.AsObject().Remove("KnownRecipes");foreach(var h in preFeatures["Party"]!.AsArray()) { h!.AsObject().Remove("Skills");h.AsObject().Remove("Effects"); }
preFeatures["Enemy"]!.AsObject().Remove("Effects");
var preFeaturesLoaded=Game.Load(preFeatures.ToJsonString());
Check(preFeaturesLoaded.KnownRecipes is null && preFeaturesLoaded.Party[0].Skills is null && preFeaturesLoaded.Party[0].Effects is null,"Old saves acquire no invented recipe knowledge, skills or effects");
uint diceState = Dice.DefaultSeed;
var diceResults = Enumerable.Range(0,6).Select(_ => Dice.Roll(ref diceState,6)).ToArray();
Check(diceResults.SequenceEqual(new[]{5,6,3,2,1,5}) && diceState==3050220688u,"Stable prototype RNG vector");
uint rejectionState=1165839077u;
Check(Dice.Roll(ref rejectionState,255)==19 && rejectionState==587785823u,"Incomplete die bucket rejected before return");
Throws(()=>Dice.Roll(ref diceState,0),"Reject zero-sided die");
Throws(()=>Dice.Roll(ref diceState,256),"Reject unsupported die");
for(int sides=1;sides<=255;sides++)
    for(int roll=0;roll<100;roll++)
    {
        int value=Dice.Roll(ref diceState,sides);
        if(value<1 || value>sides) throw new Exception("Die bounds");
    }
Check(true,"All supported die bounds");
var armedRound=Game.Attack(equipped,catalog);
int armedDamage=equipped.Enemy.Hp-armedRound.State.Enemy.Hp;
Check(armedDamage is >=3 and <=9 && armedRound.State.RandomState!=equipped.RandomState,"Imported 2d4+1 damage used");
Check(armedRound.Events[0].Contains("Restricted test staff") && armedRound.Events[0].Contains("2d4 + 1"),"Combat log identifies weapon roll");
Check(equipped.Enemy.Hp==32 && equipped.RandomState==Dice.DefaultSeed,"Weapon attack leaves source state intact");
Check(Game.Save(Game.Attack(armedRound.State,catalog).State)==
    Game.Save(Game.Attack(Game.Load(Game.Save(armedRound.State)),catalog).State),"Save resumes exact weapon roll sequence");
Throws(()=>Game.Attack(equipped),"Armed combat requires item catalog");
var initiativeState=equipped with { Party=[equipped.Party[0] with { Speed=3,Hp=1 }],Enemy=equipped.Enemy with { Speed=4 } };
Check(Game.Attack(initiativeState,catalog).State.Enemy.Hp<32,"Weapon initiative moves hero before lethal enemy");
var slowCatalog=catalog with { Items=[restricted with { Initiative=-8 }] };
var skipped=Game.Attack(equipped with { Party=[equipped.Party[0] with { Hp=1 }] },slowCatalog).State;
Check(skipped.Enemy.Hp==32 && skipped.RandomState==equipped.RandomState,"Dead queued weapon attack consumes no dice");
Check(Game.Attack(equipped with { Enemy=equipped.Enemy with { Hp=0 } },catalog).State.RandomState==equipped.RandomState,"Finished combat consumes no dice");
Check(Game.Attack(Game.CaptureFirst(equipped),catalog).State.RandomState==equipped.RandomState,"Captive consumes no dice");
Check(Game.Attack(solo).State.RandomState==solo.RandomState,"Unarmed fixture consumes no dice");
Throws(()=>Equipment.Equip(solo,"hero-1",77,catalog with { Items=[restricted with { DamageDice=0,DamageSides=0 }] }),"Percentage weapon cannot become flat melee damage");
var oldRngSave=System.Text.Json.Nodes.JsonNode.Parse(Game.Save(solo))!;
oldRngSave.AsObject().Remove("RandomState");
Check(Game.Load(oldRngSave.ToJsonString()).RandomState==Dice.DefaultSeed,"Old saves acquire deterministic seed");
for(int size=1;size<=8;size++)
{
    var partyBattle=Equipment.Equip(Inventory.Add(Game.Create(size),77,1,catalog),"hero-1",77,catalog);
    for(int i=0;i<20 && partyBattle.Enemy.Hp>0;i++) partyBattle=Game.Attack(partyBattle,catalog).State;
    Check(partyBattle.Enemy.Hp==0 && partyBattle.Party.Sum(h=>h.Xp)==120,$"Weapon combat and XP conservation for party {size}");
}
Check(Equipment.ItemId(equipped,equipped.Party[0].Id)==77 && Equipment.ItemId(solo,solo.Party[0].Id) is null,"Equipment immutable transition");
Check(Equipment.ItemId(Game.Load(Game.Save(equipped)),"hero-1")==77,"Equipment save roundtrip");
Equipment.ValidateLoadedEquipment(equipped,catalog,catalog.Provenance.Sha256);
Throws(()=>Equipment.ValidateLoadedEquipment(equipped,null,null),"Reject equipment without catalog");
Throws(()=>Equipment.ValidateLoadedEquipment(equipped,catalog,new string('c',64)),"Reject changed source database");
Throws(()=>Equipment.Equip(solo,solo.Party[0].Id,999,catalog),"Reject unknown item");
Throws(()=>Equipment.Equip(Game.Create(2),"hero-2",77,catalog),"Enforce race restriction");
Check(Equipment.Denial(solo.Party[0] with { Profession="Mage" },restricted)=="Profession restriction.","Enforce class restriction");
Check(Equipment.Denial(solo.Party[0],restricted with { GenderMask=2 })=="Gender restriction.","Enforce gender restriction");
Throws(()=>Equipment.Equip(Game.CaptureFirst(solo),"hero-1",77,catalog),"Captive cannot equip");
Throws(()=> (catalog with { Items=[restricted,restricted],Count=2 }).Validate(),"Reject duplicate catalog IDs");
Throws(()=> (catalog with { Count=2 }).Validate(),"Reject catalog count mismatch");
Check((restricted with { DamageConstant=3,DamageDice=0,DamageSides=0 }).DamageDescription.Contains("30%"),"Zero dice encode bonus rather than flat damage");
var snakeJson=System.Text.Json.JsonSerializer.Serialize(catalog,new System.Text.Json.JsonSerializerOptions{PropertyNamingPolicy=System.Text.Json.JsonNamingPolicy.SnakeCaseLower});
Check(ItemCatalog.Parse(snakeJson).Find(77).Name==restricted.Name,"Python/C# snake-case schema");
var oldSave=System.Text.Json.Nodes.JsonNode.Parse(Game.Save(solo))!;
oldSave["Schema"]=1;oldSave.AsObject().Remove("Items");oldSave.AsObject().Remove("NextItemSequence");
foreach(var member in oldSave["Party"]!.AsArray()) { member!.AsObject().Remove("Gender");member.AsObject().Remove("EquippedItemId"); }
var migrated=Game.Load(oldSave.ToJsonString());
Check(migrated.Party[0].Gender=="Male" && Equipment.ItemId(migrated,migrated.Party[0].Id) is null,"Iteration-001 saves remain readable");
var spellRaw=new string('0',1408);
var spellHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Convert.FromHexString(spellRaw))).ToLowerInvariant();
var reference=new OriginalSpell(1,"Fixture ember","Synthetic reference entry, not an original spell.",0,1,0,-1,3,1,[1,7,0,0],720,30,1,4,2,6,spellHash,spellRaw);
var referenceCatalog=new SpellCatalog(1,"DATABASES/SPELLTABLES.DBS",new("loose","synthetic-spell-reference",new string('a',64)),"DATABASES/SPELLDESC.DBS",new("loose","synthetic-spell-descriptions",new string('b',64)),704,1,[reference]);
referenceCatalog.Validate();
var referenceJson=System.Text.Json.JsonSerializer.Serialize(referenceCatalog,new System.Text.Json.JsonSerializerOptions{PropertyNamingPolicy=System.Text.Json.JsonNamingPolicy.SnakeCaseLower});
Check(SpellCatalog.Parse(referenceJson).Spells[0].SchoolFlags[1]==7,"Spell reference preserves unknown school flag and zero cost");
Check(reference.Realm=="Fire" && reference.Target=="One enemy","Source-backed spell code labels");
Check((reference with { RealmCode=99 }).Realm=="Unknown code 99","Unknown original code is not guessed");
Throws(()=> (referenceCatalog with { DescriptionProvenance=referenceCatalog.DescriptionProvenance with { Sha256="invalid" } }).Validate(),"Spell descriptions require independent valid provenance");
Throws(()=> (referenceCatalog with { Spells=[reference with { RawRecordHex=new string('f',1408) }] }).Validate(),"Raw spell record hash mismatch rejected");
Throws(()=> (referenceCatalog with { Count=2,Spells=[reference,reference] }).Validate(),"Duplicate spell reference IDs rejected");
var nativeRecipe=new NativeCombination(2,0,1,1,26,15);
var nativeA=new NativeIngredient("a",0,1,7,true);
var nativeB=new NativeIngredient("b",1,1,3,true);
var nativePlan=NativeCombinations.Plan(nativeRecipe,nativeA,nativeB,15);
Check(nativePlan==new NativeCombinationPlan(2,3,3,4,0,3,"second"),"Native two-stack batch consumes minimum and replaces smaller stack");
Check(NativeCombinations.Plan(nativeRecipe,nativeB,nativeA,15)==new NativeCombinationPlan(2,3,3,0,4,3,"first"),"Native ingredient order does not alter batch");
Check(NativeCombinations.Plan(nativeRecipe,nativeA with {Quantity=3},nativeB,15).Destination=="second","Native equal stacks replace second slot");
Check(NativeCombinations.Plan(nativeRecipe,nativeA,nativeB with {Mode=2,Quantity=1},15)==new NativeCombinationPlan(2,1,1,6,0,1,"second"),"Charged ingredient is consumed as one whole instance, not one charge");
Check(NativeCombinations.Plan(nativeRecipe,nativeA with {Mode=0,Quantity=1},nativeB,15).Destination=="first","Nonstack first ingredient becomes result destination");
Check(NativeCombinations.Plan(nativeRecipe with {OutputMode=0},nativeA,nativeB,15).OutputQuantity==1,"Nonstack result is one instance even for a larger batch");
Check(NativeCombinations.Plan(nativeRecipe with {SkillCode=255,SkillThreshold=255},nativeA,nativeB,0).OutputQuantity==3,"Native no-skill sentinel bypasses threshold");
Check(NativeCombinations.Plan(nativeRecipe with {FirstId=0,SecondId=0},nativeA,nativeA with {InstanceId="other"},15).OutputQuantity==7,"Repeated ingredient IDs require two instances, not different definitions");
Throws(()=>NativeCombinations.Plan(nativeRecipe,nativeA,nativeB,14),"Native threshold boundary rejects one below");
Throws(()=>NativeCombinations.Plan(nativeRecipe,nativeA with {Identified=false},nativeB,15),"Native unidentified first ingredient rejected");
Throws(()=>NativeCombinations.Plan(nativeRecipe,nativeA,nativeB with {Identified=false},15),"Native unidentified second ingredient rejected");
Throws(()=>NativeCombinations.Plan(nativeRecipe,nativeA,nativeB with {ItemId=99},15),"Native wrong ingredient rejected");
Throws(()=>NativeCombinations.Plan(nativeRecipe with {SecondId=0},nativeA,nativeA,15),"One native instance cannot occupy both ingredient slots");
Throws(()=>NativeCombinations.Plan(nativeRecipe,nativeA with {Quantity=0},nativeB,15),"Empty native stack rejected");
Throws(()=>NativeCombinations.Plan(nativeRecipe,nativeA with {Quantity=256},nativeB,15),"Native byte-sized quantity bounded");
Throws(()=>NativeCombinations.Plan(nativeRecipe,nativeA with {Mode=2},nativeB,15),"Charged item count does not stand for charge quantity");
Throws(()=>NativeCombinations.Plan(nativeRecipe with {SkillCode=254},nativeA,nativeB,15),"Unknown signed skill code is not executed");
Throws(()=>NativeCombinations.Plan(nativeRecipe with {OutputMode=5},nativeA,nativeB,15),"Unknown output mode is not executed");
Check(nativeA.Quantity==7 && nativeB.Quantity==3,"Native planning preserves source instances");
var recipeBytes=new byte[269];System.Text.Encoding.Unicode.GetBytes("Fixture compound").CopyTo(recipeBytes,0);
System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(recipeBytes.AsSpan(0xb9),0);
System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(recipeBytes.AsSpan(0xbd),1);
recipeBytes[0xc9]=26;recipeBytes[0xca]=15;
var recipeReference=new OriginalRecipe(2,"Fixture compound",[0,1],["Fixture A","Fixture B"],26,15,new string('0',16),[],
    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(recipeBytes)),Convert.ToHexString(recipeBytes));
var recipeReferenceCatalog=new RecipeCatalog(1,"reference_only","DATABASES/ITEMS.DBS",new("loose","synthetic-combination-reference",new string('c',64)),269,3,1,new(){{"-1",2}},[recipeReference]);
var recipeReferenceJson=System.Text.Json.JsonSerializer.Serialize(recipeReferenceCatalog,new System.Text.Json.JsonSerializerOptions{PropertyNamingPolicy=System.Text.Json.JsonNamingPolicy.SnakeCaseLower});
Check(RecipeCatalog.Parse(recipeReferenceJson).Recipes[0].Skill=="Alchemy","Combination reference parses with source-backed skill name");
Throws(()=> (recipeReferenceCatalog with {Recipes=[recipeReference with {SkillThreshold=16}]}).Validate(),"Combination metadata must match its raw bytes");
Throws(()=> (recipeReferenceCatalog with {Recipes=[recipeReference with {RawRecordHex=new string('f',538)}]}).Validate(),"Combination raw record hash checked");
Throws(()=> (recipeReferenceCatalog with {Recipes=[recipeReference with {Issues=["unnamed_output"]}]}).Validate(),"Combination anomaly flags cannot hide or invent invalid rows");
Throws(()=> (recipeReferenceCatalog with {ItemCount=4}).Validate(),"Combination counts include inactive pairs");
checks+=NativeItemChecks.Run(restricted);
checks+=NativeInventoryChecks.Run(restricted);
checks+=ItemInventoryChecks.Run(restricted);
checks+=InventoryIntegrationChecks.Run(catalog,craftCatalog);
checks+=OriginalFormulaChecks.Run();
checks+=PhysicalScoreChecks.Run();
if(args.Length==2 && args[0]=="--fixture")
{
    File.WriteAllText(args[1],System.Text.Json.JsonSerializer.Serialize(catalog with { Count=5, Items=[restricted, restricted with { Id=78,Name="Test herb",TypeCode=19 }, restricted with { Id=79,Name="Test vial",TypeCode=19 }, restricted with { Id=80,Name="Test tonic",TypeCode=15 }, restricted with { Id=81,Name="Tempered test staff",DamageConstant=3 }], Provenance=new Provenance("loose","synthetic-test-catalog",catalog.Provenance.Sha256) },new System.Text.Json.JsonSerializerOptions{PropertyNamingPolicy=System.Text.Json.JsonNamingPolicy.SnakeCaseLower}));
    File.WriteAllText(Path.ChangeExtension(args[1],".spells.json"),referenceJson);
    File.WriteAllText(Path.ChangeExtension(args[1],".recipes.json"),recipeReferenceJson);
}
else if(args.Length==2 && args[0]=="--spell-formulas")
{
    var spells=SpellCatalog.Parse(File.ReadAllText(args[1]));int scenarios=0;
    foreach(var spell in spells.Spells.Where(s=>s.Level>0))
    {
        int previous=-1;
        for(int power=1;power<=7;power++)
        {
            var risk=NeoWizardry.Core.Rules.OriginalMagicRules.OrdinaryCastingRisk(new(100,100,spell.Level,spell.Cost,power,20));
            if(risk.FailureThreshold<previous)throw new Exception("Power must not lower ordinary casting risk");
            previous=risk.FailureThreshold;scenarios++;
        }
    }
    Check(scenarios>0,"Private positive-level spell metadata exercises ordinary-risk arithmetic");
    Console.WriteLine($"PASS: {scenarios} original spell metadata/power scenarios; selected book and caster level are supplied fixtures, not actor eligibility");
}
else if(args.Length==2 && args[0]=="--recipes")
{
    var realRecipes=RecipeCatalog.Parse(File.ReadAllText(args[1]));
    Check(realRecipes.Recipes.Count(r=>r.Issues.Length==0)>0,"Private combination catalog has named valid references");
    Console.WriteLine($"PASS: real imported combination catalog ({realRecipes.Count} records)");
}
else if(args.Length==3 && args[0]=="--native-flow")
{
    var items=ItemCatalog.Parse(File.ReadAllText(args[1]));var recipes=RecipeCatalog.Parse(File.ReadAllText(args[2]));
    int simulated=0;
    foreach(var recipe in recipes.Recipes.Where(r=>r.Issues.Length==0))
    {
        var inventory=NativeInventory.Create(items,10);
        for(int i=0;i<2;i++)
        {
            var definition=items.Find(recipe.IngredientIds[i]);
            var instance=NativeItemFactory.Create(definition,[],useMaximum:true,forceIdentified:true);
            if(definition.Instance!.Mode==1)instance=instance with {CountRaw=1};
            inventory=NativeInventory.Add(inventory,i,$"ingredient-{i}",instance,items);
        }
        string before=NativeInventory.Save(inventory,items);var output=items.Find(recipe.OutputId);
        var draws=Enumerable.Repeat(0,output.Instance!.AmountSides>0?output.Instance.AmountDice:0).ToArray();
        var result=NativeInventory.SimulateCombination(inventory,0,1,recipe.SkillThreshold,draws,"result",[],items,recipes);
        if(result.State.Entries.Length!=1 || result.State.Entries[0].Item.ItemId!=recipe.OutputId || NativeInventory.Save(inventory,items)!=before)
            throw new Exception("Original local combination simulation failed");
        var restored=NativeInventory.Load(NativeInventory.Save(result.State,items),items);
        if(restored.Entries[0]!=result.State.Entries[0])throw new Exception("Original local result snapshot changed after save/load");
        var rules=recipe.IngredientIds.Append(recipe.OutputId).Distinct()
            .Select(id=>OriginalItemAdapter.Rule(items.Find(id))).ToDictionary(rule=>rule.DefinitionId);
        var typed=ItemInventory.Create(2);var location=new ItemLocation("party");
        foreach(var entry in inventory.Entries)
            typed=ItemInventory.Add(typed,OriginalItemAdapter.Convert(items.Find(entry.Item.ItemId),entry.Item,entry.InstanceId,location).Item,rules);
        string typedBefore=ItemInventory.Save(typed,rules);
        var typedOutput=OriginalItemAdapter.Convert(output,result.State.Entries[0].Item,"result",location).Item;
        var typedResult=ItemInventory.Transform(typed,[new("ingredient-0",result.Plan.FirstConsumed),new("ingredient-1",result.Plan.SecondConsumed)],
            [typedOutput],rules);
        if(typedResult.Items.Single()!=typedOutput || ItemInventory.Save(typed,rules)!=typedBefore ||
            ItemInventory.Save(ItemInventory.Load(ItemInventory.Save(typedResult,rules),rules),rules)!=ItemInventory.Save(typedResult,rules))
            throw new Exception("Clean inventory combination transaction or save/load failed");
        simulated++;
    }
    Check(simulated>0,"Original local recipe flow covered named references");
    Console.WriteLine($"PASS: local native simulation, clean inventory transaction and save/load for {simulated} original combinations (actor/time/progression excluded)");
}
else if(args.Length>0)
{
    var real=ItemCatalog.Parse(File.ReadAllText(args[0]));
    if(real.Schema==3)
    {
        Check(real.Items.All(i=>i.Instance is not null),"Real item records include native instance fields");
        var generated=real.Items.Select(i=>NativeItemFactory.Create(i,[],useMaximum:true)).ToArray();
        Check(generated.All(i=>NativeItemInstance.Decode(i.Encode())==i),"All imported native creation maxima roundtrip through 12-byte slots");
        var stackDefinitions=real.Items.Where(i=>i.Instance!.Mode==1).ToArray();
        bool stackConservation=stackDefinitions.All(i=>{
            var oversized=NativeItemFactory.Create(i,[],useMaximum:true) with {CountRaw=255};
            var normalized=NativeStacks.Normalize(oversized,i,254);
            return normalized.Kept.CountRaw+normalized.Overflow.Sum(s=>(int)s.CountRaw)==255 &&
                normalized.Kept.CountRaw<=i.Instance!.MaxStack && normalized.Overflow.All(s=>s.CountRaw>0 && s.CountRaw<=i.Instance!.MaxStack);
        });
        Check(stackConservation,"All imported stack definitions split without losing units");
        Console.WriteLine($"PASS: stack overflow conservation for {stackDefinitions.Length} original definitions");
        Console.WriteLine($"PASS: native fields and local maximum creation for {generated.Length} original definitions");
    }
    var cane=real.Items.Single(i=>i.Name=="Cane of Corpus");
    Check(Equipment.Denial(solo.Party[0],cane) is null,"Real faerie ninja weapon allowed");
    Check(Equipment.Denial(Game.Create(2).Party[1],cane)=="Race restriction.","Real human denied exclusive weapon");
    Check(real.Find(0).DamageDescription=="1d4 + 0","Real dagger dice parsed");
    var caneState=Equipment.Equip(Inventory.Add(solo with { Enemy=solo.Enemy with { Hp=1000 } },cane.Id,1,real),"hero-1",cane.Id,real);
    var caneRound=Game.Attack(caneState,real);
    Check(1000-caneRound.State.Enemy.Hp is >=12 and <=42 && caneRound.Events[0].Contains("6d6 + 6"),"Real Cane of Corpus base dice used by combat");
    Console.WriteLine($"PASS: real imported item catalog ({real.Count} records)");
}
Console.WriteLine($"PASS: {checks} core checks");
var touch=new NeoWizardry.Input.MobileControls();
var origin=System.Numerics.Vector2.Zero;
Check(touch.Press(0,origin,true,false,origin),"Move pointer claimed");
touch.Drag(0,new(0,-60),new(0,-60));Check(touch.Movement.Y < -0.99f,"Stick forward");
Check(touch.Press(1,new(200,0),false,true,origin),"Independent look pointer");
touch.Drag(1,new(230,10),new(30,10));Check(touch.ConsumeLook()==new System.Numerics.Vector2(30,10),"Look delta queued once");
Check(touch.ConsumeLook()==origin,"Look delta consumed");
Check(!touch.Press(2,origin,true,false,origin),"Third pointer cannot steal stick");
touch.Release(1);Check(touch.Movement.Y < -0.99f,"Look release preserves movement");
touch.Release(0);Check(touch.Movement==origin,"Release stops movement");
Check(!touch.Press(3,origin,false,false,origin),"UI touch is not claimed");
touch.Press(0,origin,true,false,origin);touch.Drag(0,new(1000,1000),origin);Check(touch.Movement.Length()<=1.001f,"Stick magnitude clamped");
touch.Reset();Check(touch.MovePointer is null && touch.LookPointer is null && touch.Movement==origin,"Background resets ownership");
Console.WriteLine($"PASS: {checks} checks including mobile gestures");
