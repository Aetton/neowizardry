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
Throws(() => Game.Create(0), "No empty party");
Throws(() => Game.Create(9), "UI limit enforced");
Throws(() => Game.Load(Game.Save(solo).Replace("\"Schema\": 1", "\"Schema\": 99")), "Reject unknown save version");
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
var equipped = Equipment.Equip(solo,solo.Party[0].Id,77,catalog);
Check(equipped.Party[0].EquippedItemId==77 && solo.Party[0].EquippedItemId is null,"Equipment immutable transition");
Check(Game.Load(Game.Save(equipped)).Party[0].EquippedItemId==77,"Equipment save roundtrip");
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
foreach(var member in oldSave["Party"]!.AsArray()) { member!.AsObject().Remove("Gender");member.AsObject().Remove("EquippedItemId"); }
var migrated=Game.Load(oldSave.ToJsonString());
Check(migrated.Party[0].Gender=="Male" && migrated.Party[0].EquippedItemId is null,"Iteration-001 saves remain readable");
if(args.Length==2 && args[0]=="--fixture")
    File.WriteAllText(args[1],System.Text.Json.JsonSerializer.Serialize(catalog with { Provenance=new Provenance("loose","synthetic-test-catalog",catalog.Provenance.Sha256) },new System.Text.Json.JsonSerializerOptions{PropertyNamingPolicy=System.Text.Json.JsonNamingPolicy.SnakeCaseLower}));
else if(args.Length>0)
{
    var real=ItemCatalog.Parse(File.ReadAllText(args[0]));
    var cane=real.Items.Single(i=>i.Name=="Cane of Corpus");
    Check(Equipment.Denial(solo.Party[0],cane) is null,"Real faerie ninja weapon allowed");
    Check(Equipment.Denial(Game.Create(2).Party[1],cane)=="Race restriction.","Real human denied exclusive weapon");
    Check(real.Find(0).DamageDescription=="1d4 + 0","Real dagger dice parsed");
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
