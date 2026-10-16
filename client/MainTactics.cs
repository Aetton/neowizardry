using System.Collections.Immutable;
using Godot;
using NeoWizardry.Core;

public partial class Main
{
    private void NewTactical(int heroes,int enemies)
    {
        _state=MeleeTraining.CreateTactical(heroes,enemies,_catalog!,_spellCatalog);_autosaveBlocked=false;
        _selectedHeroId=_state.Party[0].Id;Refresh();Say($"Tactical laboratory: {heroes} heroes, {enemies} opponents. Movement and sight use authored grid rules.");
    }
    private void NewMonsterBattle(int heroes,int count,MonsterCatalog monsters,int definitionId)
    {
        _state=MonsterCombat.Create(heroes,count,_catalog!,monsters,definitionId);_autosaveBlocked=false;_selectedHeroId=_state.Party[0].Id;Refresh();
        Say("Monster physical slice: native HP/stats/initiative/attack budgets/defense; authored grid AI. Magic and XP/loot are not connected in this mode.");
    }
    private void ShowMonsterRoster(int heroes,int count)
    {
        var catalog=_monsterCatalog??MonsterCombat.FixtureCatalog();var ready=catalog.Monsters.Where(MonsterCombat.Supported).ToArray();
        if(ready.Length==0){Say("No complete physical profiles in this catalog. Inspect Monster reference for blockers.");return;}
        _dialogs++;_touchInput.Reset();Input.MouseMode=Input.MouseModeEnum.Visible;
        var dialog=new AcceptDialog {Name="MonsterRoster",Theme=_hudTheme,Title="Mixed monster roster"};AddChild(dialog);
        dialog.Confirmed+=dialog.QueueFree;dialog.CloseRequested+=dialog.QueueFree;dialog.TreeExited+=()=>{_dialogs=Math.Max(0,_dialogs-1);_touchInput.Reset();};
        var body=new VBoxContainer();dialog.AddChild(body);var choices=new List<OptionButton>();
        body.AddChild(new Label {Text=$"Choose {count} enemies · {ready.Length}/{catalog.Count} physical profiles ready",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        int start=Math.Max(0,Array.FindIndex(ready,m=>m.Id==282));
        for(int i=0;i<count;i++)
        {
            var choose=new OptionButton {Name=$"Roster{i}",FitToLongestItem=false,CustomMinimumSize=new(250,0)};foreach(var row in ready)choose.AddItem($"{row.Id}: {row.Label}");choose.Select((start+i)%ready.Length);body.AddChild(choose);choices.Add(choose);
        }
        var begin=Button(body,"Start mixed battle",()=>Try(()=>
        {
            _state=MonsterCombat.CreateRoster(heroes,_catalog!,catalog,choices.Select(c=>ready[c.Selected].Id).ToArray());
            _autosaveBlocked=false;_selectedHeroId=_state.Party[0].Id;Refresh();dialog.Hide();dialog.QueueFree();ShowTactics();
        }));begin.Name="StartMonsterRoster";
        var screen=GetViewport().GetVisibleRect().Size;dialog.PopupCentered(new Vector2I((int)Math.Min(380,screen.X-24),(int)Math.Min(Math.Min(620,150+count*42),screen.Y-24)));
    }
    private void ShowTactics()
    {
        if(_state.Melee?.Field is null){Say("Menu → New tactical battle to test multiple opponents and positions.");return;}
        Input.MouseMode=Input.MouseModeEnum.Visible;_touchInput.Reset();_dialogs++;
        var dialog=new AcceptDialog {Name="TacticalDialog",Theme=_hudTheme,Title="Tactical field · authored movement rules",Unresizable=true};AddChild(dialog);
        dialog.Confirmed+=dialog.QueueFree;dialog.CloseRequested+=dialog.QueueFree;dialog.TreeExited+=()=>{_dialogs=Math.Max(0,_dialogs-1);_touchInput.Reset();};
        var scroll=new ScrollContainer();dialog.AddChild(scroll);var body=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};scroll.AddChild(body);
        body.AddChild(new Label {Text="Choose hero and order, then tap a cell.\nOne step or adjacent melee; spells need clear sight.\nUnordered heroes attack nearby enemies or wait.\nAI: Brute closes in; Hunter targets wounded;\nFlanker spreads out; Skirmisher retreats at low HP.",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        if(_state.Melee.Field.Monsters is not null)body.AddChild(new Label {Text="Monster physical test: spellcasting and XP/loot are not connected.",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        var actor=new OptionButton {Name="TacticalActor"};foreach(var h in _state.Party)actor.AddItem(h.Name);body.AddChild(actor);
        var mode=new OptionButton();foreach(var text in new[]{"Move","Attack","Spell"})mode.AddItem(text);body.AddChild(mode);
        OriginalSpell[] magic=_state.Melee.Magic is null?[]:_spellCatalog?.Spells.Where(RestoredMagic.SupportedSpell).ToArray()??[];
        var spell=new OptionButton();foreach(var s in magic)spell.AddItem(s.Name);spell.Disabled=magic.Length==0;body.AddChild(spell);
        var power=new OptionButton();for(int i=1;i<=7;i++)power.AddItem($"Power {i}");body.AddChild(power);
        var feedback=new Label {AutowrapMode=TextServer.AutowrapMode.WordSmart};body.AddChild(feedback);
        void Attempt(Action action){try{action();feedback.Text="";}catch(Exception e){feedback.Text=e.Message;Say(e.Message);}}
        var aiLabel=new Label {Name="EnemyAiStatus",AutowrapMode=TextServer.AutowrapMode.WordSmart};body.AddChild(aiLabel);
        var inspect=new OptionButton {Name="EnemyAiActor"};foreach(var e in TacticalCombat.Enemies(_state))inspect.AddItem(e.Id);body.AddChild(inspect);
        var tactic=new OptionButton {Name="EnemyAiTactic"};foreach(var t in Enum.GetValues<EnemyTactic>())tactic.AddItem(t.ToString());body.AddChild(tactic);
        var grid=new GridContainer {Columns=_state.Melee.Field.Width,Name="TacticalGrid"};body.AddChild(grid);
        var lastDecisions=new Label {Name="EnemyAiReasons",AutowrapMode=TextServer.AutowrapMode.WordSmart};
        var orders=new Dictionary<string,TacticalOrder>();var summary=new Label {AutowrapMode=TextServer.AutowrapMode.WordSmart};
        void Render()
        {
            foreach(var child in grid.GetChildren()){grid.RemoveChild(child);child.QueueFree();}
            var f=_state.Melee!.Field!;int selected=actor.Selected;string heroId=_state.Party[selected].Id;
            for(int y=0;y<f.Height;y++)for(int x=0;x<f.Width;x++)
            {
                var cell=new BattleCell(x,y);bool wall=f.Walls.Contains(cell);
                string? id=f.Positions.Where(p=>p.Value==cell).OrderByDescending(p=>TacticalCombat.Alive(_state,p.Key)).Select(p=>p.Key).FirstOrDefault();
                int index=Array.FindIndex(_state.Party.ToArray(),h=>h.Id==id);var enemy=TacticalCombat.Enemies(_state).FirstOrDefault(e=>e.Id==id);
                string text=wall?"▩":id is null?"·":index>=0?$"P{index+1}\n{_state.Party[index].Hp}":$"E{Array.FindIndex(TacticalCombat.Enemies(_state).ToArray(),e=>e.Id==id)+1}\n{enemy!.Hp}";
                var b=new Button {Text=text,Name=$"Cell{x}_{y}",CustomMinimumSize=new(36,40),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,Disabled=wall,TooltipText=$"({x},{y}) {id}"};
                b.Modulate=wall?new Color(.5f,.5f,.5f):id==heroId?new Color(1,.85f,.3f):index>=0?new Color(.5f,.8f,1):enemy is not null?new Color(1,.55f,.5f):Colors.White;
                b.Pressed+=()=>Attempt(()=>
                {
                    var h=_state.Party[actor.Selected];if(h.Hp==0 || h.Captive)throw new InvalidOperationException("Select a living free hero");
                    var action=(TacticalAction)(mode.Selected==0?1:mode.Selected==1?0:3);
                    if(action is TacticalAction.Cast or TacticalAction.Attack && id is null)throw new InvalidOperationException("Select an occupied target cell");
                    if(action==TacticalAction.Cast && magic.Length==0)throw new InvalidOperationException(_state.Melee.Magic is null?"Original casting is not connected in this encounter.":"Load the original spell catalog first.");
                    orders[h.Id]=new(h.Id,action,id,action==TacticalAction.Move?cell:null,action==TacticalAction.Cast?new(h.Id,magic[spell.Selected].Id,power.Selected+1,id):null);Render();
                });grid.AddChild(b);
            }
            var brains=f.Brains;
            aiLabel.Text=brains is null?"Legacy pursuit AI. Start a new battle for behavior profiles.":
                string.Join("\n",TacticalCombat.Enemies(_state).Select((e,i)=>{var b=brains[e.Id];return $"E{i+1} {(MonsterCombat.Actor(_state,e.Id) is {} native?MonsterCombat.Compile(native.Record).Label:e.Id)}: {b.Profile.Tactic} · {b.Mode} · target {b.TargetId??"—"} · seen {b.LastSeen?.ToString()??"—"}";}));
            tactic.Disabled=brains is null;
            if(brains is not null)tactic.Select((int)brains[TacticalCombat.Enemies(_state).ElementAt(inspect.Selected).Id].Profile.Tactic);
            summary.Text=$"Round {_state.Round} · {TacticalCombat.Enemies(_state).Count(e=>e.Hp>0)} enemies alive\n"+
                string.Join("\n",orders.Values.Select(o=>$"{o.ActorId}: {o.Action} {o.TargetId} {o.Destination} {(o.Spell is {} cast?$"spell {cast.SpellId} PL{cast.Power}":"")}"))+"\n"+
                string.Join(" · ",_state.Party.Select((h,i)=>$"P{i+1} {h.Name}"))+"\nP = party, E = enemy; number below = HP. ▩ = obstacle.\nOrders are cleared when this window closes; positions are saved.";
        }
        actor.ItemSelected+=_=>Render();inspect.ItemSelected+=_=>Render();
        tactic.ItemSelected+=index=>Attempt(()=>
        {
            _state=EnemyAi.SetTactic(_state,TacticalCombat.Enemies(_state).ElementAt(inspect.Selected).Id,(EnemyTactic)index);Render();
        });
        var controls=new HBoxContainer();body.AddChild(controls);
        Button(controls,"Wait",()=>{var h=_state.Party[actor.Selected];orders[h.Id]=new(h.Id,TacticalAction.Wait);Render();});
        Button(controls,"Clear",()=>{orders.Clear();Render();});
        var resolve=Button(controls,"Resolve round",()=>Attempt(()=>
        {
            var result=TacticalCombat.Advance(_state,_catalog!,orders.Values.ToArray(),_spellCatalog);_state=result.State;
            lastDecisions.Text=string.Join("\n",result.Events.Where(e=>e.StartsWith("AI ")));
            foreach(var e in result.Events)Say(e);orders.Clear();Refresh();Render();
        }));resolve.Name="TacticalResolve";
        Button(body,"Save positions and combat",()=>Attempt(()=>{SaveTo(SavePath);Say("Tactical battle saved.");}));body.AddChild(lastDecisions);body.AddChild(summary);Render();
        var screen=GetViewport().GetVisibleRect().Size;var dimensions=new Vector2I((int)Math.Min(520,screen.X-24),(int)Math.Min(740,screen.Y-24));
        scroll.Position=new(10,10);scroll.Size=new(dimensions.X-20,dimensions.Y-62);dialog.PopupCentered(dimensions);
    }
    private void CheckTacticalInterface()
    {
        var original=_state;NewTactical(3,3);ShowTactics();
        var dialog=GetChildren().OfType<AcceptDialog>().Single(d=>d.Name=="TacticalDialog");
        IEnumerable<Node> Walk(Node node)=>node.GetChildren().SelectMany(c=>new[]{c}.Concat(Walk(c)));
        if(Walk(dialog).OfType<Button>().Count(b=>b.Name.ToString().StartsWith("Cell"))!=64)throw new Exception("Tactical grid is incomplete");
        Walk(dialog).OfType<Button>().Single(b=>b.Name=="Cell2_0").EmitSignal(Godot.Button.SignalName.Pressed);
        Walk(dialog).OfType<Button>().Single(b=>b.Name=="TacticalResolve").EmitSignal(Godot.Button.SignalName.Pressed);
        if(_state.Round!=1 || _state.Melee!.Field!.Positions["hero-1"]!=new BattleCell(2,0) || TacticalCombat.Enemies(_state).Count()!=3 || _state.Melee!.Field!.Positions["Training Duelist"].X!=5)throw new Exception("Tactical UI round did not move opponents");
        if(_state.Melee.Field.Brains is null || !_state.Melee.Field.Brains.Values.Any(b=>b.TargetId is not null))throw new Exception("Enemy AI did not perceive or pursue the party");
        var inspect=Walk(dialog).OfType<OptionButton>().Single(b=>b.Name=="EnemyAiActor");inspect.Select(1);inspect.EmitSignal(OptionButton.SignalName.ItemSelected,1L);
        var tactic=Walk(dialog).OfType<OptionButton>().Single(b=>b.Name=="EnemyAiTactic");tactic.Select(3);tactic.EmitSignal(OptionButton.SignalName.ItemSelected,3L);
        if(_state.Melee.Field.Brains["Training Duelist 2"].Profile.Tactic!=EnemyTactic.Skirmisher)throw new Exception("Enemy AI profile selector did not commit");
        GD.Print("PASS: enemy AI perception, profile selector and tactical saved state");
        GD.Print("PASS: tactical UI renders 64 cells and resolves three independent opponents");
        var saved=Game.Save(_state);var continuation=Game.Save(TacticalCombat.Advance(_state,_catalog!).State);
        _state=Game.Load(saved,_catalog);if(Game.Save(TacticalCombat.Advance(_state,_catalog!).State)!=continuation)throw new Exception("Tactical client save continuation failed");
        RemoveChild(dialog);dialog.Free();_state=original;Refresh();
    }
    private void CheckMonsterInterface()
    {
        var original=_state;var catalog=_monsterCatalog?.Monsters.Any(m=>m.Id==282 && MonsterCombat.Supported(m))==true?_monsterCatalog:MonsterCombat.FixtureCatalog();
        NewMonsterBattle(3,3,catalog!,catalog!.Count==1?0:282);ShowTactics();
        var dialog=GetChildren().OfType<AcceptDialog>().Single(d=>d.Name=="TacticalDialog");
        IEnumerable<Node> Walk(Node node)=>node.GetChildren().SelectMany(c=>new[]{c}.Concat(Walk(c)));
        Walk(dialog).OfType<Button>().Single(b=>b.Name=="TacticalResolve").EmitSignal(Godot.Button.SignalName.Pressed);
        if(_state.Round!=1 || _state.Melee!.Opponent is not null || _state.Melee.Field!.Monsters?.Count!=3 || _state.Items!.Items.Any(i=>i.Location.ContainerId.StartsWith("monster-")))throw new Exception("Monster battle UI did not use native unarmed actors");
        RemoveChild(dialog);dialog.Free();
        ShowCombatFacts();GetChildren().OfType<AcceptDialog>().Last().Free();
        var saved=Game.Save(_state);var expected=Game.Save(TacticalCombat.Advance(_state,_catalog!).State);
        if(Game.Save(TacticalCombat.Advance(Game.Load(saved,_catalog),_catalog!).State)!=expected)throw new Exception("Monster UI save continuation failed");
        ShowMonsterRoster(3,2);
        var roster=GetChildren().OfType<AcceptDialog>().Single(d=>d.Name=="MonsterRoster");
        Walk(roster).OfType<Button>().Single(b=>b.Name=="StartMonsterRoster").EmitSignal(Godot.Button.SignalName.Pressed);
        if(_state.Melee!.Field!.Monsters?.Count!=2 || _state.Melee.Field.Monsters.Values.Any(m=>m.Record.ProfileVersion!=2))throw new Exception("Mixed roster UI did not create general profiles");
        RemoveChild(roster);roster.Free();
        var mixed=GetChildren().OfType<AcceptDialog>().Single(d=>d.Name=="TacticalDialog");RemoveChild(mixed);mixed.Free();
        GD.Print("PASS: mixed monster roster UI and general physical profiles");
        _state=original;Refresh();GD.Print("PASS: monster actor battle, native combat facts UI and save continuation");
    }

}
