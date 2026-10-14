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
Console.WriteLine($"PASS: {checks} core checks");
