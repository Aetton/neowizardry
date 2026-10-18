using Godot;
using NeoWizardry.Core;

public partial class WorldPreview
{
    private const float PlayerAttackRange=8;
    private Label? _combatHint;
    private Button? _menuAttack;
    private (Vector3? Target,float Distance,string Reason) PlayerEncounterTarget()
    {
        if(_expedition?.Phase!=WorldEncounterPhase.Ready)return(null,0,"Эта схватка уже завершена или идёт сейчас.");
        var candidates=_encounterCreatures.Values.Select(visual=>visual.InteractionBounds)
            .Select(bounds=>(Center:bounds.GetCenter(),Top:new Vector3(bounds.GetCenter().X,bounds.End.Y+0.05f,bounds.GetCenter().Z)))
            .OrderBy(p=>p.Center.DistanceSquaredTo(_view.GlobalPosition)).ToArray();
        float nearest=candidates.Length==0?float.PositiveInfinity:candidates[0].Center.DistanceTo(_view.GlobalPosition);
        foreach(var candidate in candidates)
        {
            if(candidate.Center.DistanceTo(_view.GlobalPosition)>PlayerAttackRange)continue;
            foreach(var target in new[]{candidate.Center,candidate.Top})
            {
                using var query=PhysicsRayQueryParameters3D.Create(_view.GlobalPosition,target,1);query.HitBackFaces=true;
                if(GetWorld3D().DirectSpaceState.IntersectRay(query).Count==0)return(target,nearest,"");
            }
        }
        return(null,nearest,nearest>PlayerAttackRange?$"До противников {nearest:0.0} м. Подойди ближе: атака доступна с {PlayerAttackRange:0} м.":"Путь к противникам закрыт препятствием. Обойди его, чтобы начать бой.");
    }
    private void RequestPlayerAttack()
    {
        var target=PlayerEncounterTarget();
        if(target.Target is null){Notice(target.Reason);return;}
        _beginEncounter=true;
    }
    private bool LookingAtPlayerEncounter()
    {
        return _expedition?.Phase==WorldEncounterPhase.Ready && _encounterCreatures.Values.Any(visual=>
            (-_view.GlobalBasis.Z).Dot((visual.InteractionBounds.GetCenter()-_view.GlobalPosition).Normalized())>0.8f);
    }
    private void UpdatePlayerCombatHint()
    {
        if(_menuAttack is not null)_menuAttack.Disabled=_expedition?.Phase!=WorldEncounterPhase.Ready;
        if(_combatHint is null)return;
        _combatHint.Visible=_gameReady && !_gamePaused && _expedition?.Phase==WorldEncounterPhase.Ready;
        if(!_combatHint.Visible)return;
        var target=PlayerEncounterTarget();
        string name=_expeditionMonsters.Monsters.Single(m=>m.Id==_expedition!.MonsterDefinitionId).Label;
        _combatHint.Position=new(18,GetViewport().GetVisibleRect().Size.Y-158);
        _combatHint.Text=target.Target is not null?$"B — атаковать · {name} ×{_encounterCreatures.Count} · {target.Distance:0.0} м":$"{name} ×{_encounterCreatures.Count} · {target.Reason}";
    }
    private async System.Threading.Tasks.Task CheckPlayerAttackInput()
    {
        var before=_expedition!;var position=_view.GlobalPosition;var rotation=_view.Rotation;var walkerPosition=_walker!.GlobalPosition;var walkerVelocity=_walker.Velocity;
        bool paused=_gamePaused,menu=_gameMenu.Visible;_gamePaused=false;_gameMenu.Hide();
        try
        {
            // Physical B must work even when the logical key is Cyrillic И.
            _view.GlobalPosition=_encounterAnchor+Vector3.Up*100;_walker!.GlobalPosition=_view.GlobalPosition-Vector3.Up*WorldWalker.EyeOffset;_walker.Velocity=Vector3.Zero;
            Input.ParseInputEvent(new InputEventKey {Pressed=true,PhysicalKeycode=Key.B,Keycode=(Key)0x0418,Unicode=0x0438});
            Input.ParseInputEvent(new InputEventKey {Pressed=false,PhysicalKeycode=Key.B,Keycode=(Key)0x0418});
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            if(_expedition!.Phase!=WorldEncounterPhase.Ready || !(_gameNotice?.Text.Contains("Подойди ближе")??false))throw new InvalidDataException("Physical B/far attack feedback failed");
            _view.GlobalPosition=_encounterAnchor+Vector3.Up*100;_walker!.GlobalPosition=_view.GlobalPosition-Vector3.Up*WorldWalker.EyeOffset;_walker.Velocity=Vector3.Zero;Notice("");
            _view.LookAt(_creature!.InteractionBounds.GetCenter());
            Input.ParseInputEvent(new InputEventKey {Pressed=true,PhysicalKeycode=Key.E,Keycode=(Key)0x0423,Unicode=0x0443});
            Input.ParseInputEvent(new InputEventKey {Pressed=false,PhysicalKeycode=Key.E,Keycode=(Key)0x0423});
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            if(_expedition.Phase!=WorldEncounterPhase.Ready || !(_gameNotice?.Text.Contains("Подойди ближе")??false))throw new InvalidDataException($"Physical E/range feedback failed: {_gameNotice?.Text}, {_expedition.Phase}, {_view.GlobalPosition}");
            // Find a real standable, visible approach several metres from the source group.
            bool found=false;
            for(int ring=0;ring<3 && !found;ring++)for(int i=0;i<24 && !found;i++)
            {
                float angle=i*Mathf.Tau/24,distance=ring==0?5:ring==1?2:0.3f;
                try
                {
                    var floor=PlayerFloor(_encounterAnchor+new Vector3(Mathf.Cos(angle)*distance,0,Mathf.Sin(angle)*distance),3);
                    _view.GlobalPosition=floor+Vector3.Up*1.25f;
                    if(PlayerEncounterTarget().Target is not Vector3 target)continue;
                    _walker!.GlobalPosition=floor+Vector3.Up*(WorldWalker.Height/2+0.05f);_walker.Velocity=Vector3.Zero;
                    await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
                    if(PlayerEncounterTarget().Target is not Vector3 settledTarget)continue;
                    _view.LookAt(settledTarget);found=true;
                }
                catch(InvalidDataException){}
            }
            if(!found)throw new InvalidDataException($"No walkable visible player approach: camera={_view.GlobalPosition}, bounds={_creature!.InteractionBounds}, query={PlayerEncounterTarget()}");
            UpdatePlayerCombatHint();
            if(!(_combatHint?.Text.StartsWith("B — атаковать")??false))throw new InvalidDataException("Attack affordance missing");
            Input.ParseInputEvent(new InputEventKey {Pressed=true,PhysicalKeycode=Key.B,Keycode=(Key)0x0418,Unicode=0x0438});
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            if(_expedition.Phase!=WorldEncounterPhase.Active || !_battlePanel.Visible)throw new InvalidDataException($"Real physical attack failed: phase={_expedition.Phase}, requested={_beginEncounter}, paused={_gamePaused}, notice={_gameNotice?.Text}, camera={_view.GlobalPosition}, target={PlayerEncounterTarget()}");
            GD.Print("PASS: player attack: physical B/E with Cyrillic layout, visible range/reason, walkable source approach and battle UI through dispatched input");
        }
        finally
        {
            Input.ParseInputEvent(new InputEventKey {Pressed=false,PhysicalKeycode=Key.B,Keycode=(Key)0x0418});
            _beginEncounter=false;_expedition=before;_view.GlobalPosition=position;_view.Rotation=rotation;
            _walker!.GlobalPosition=walkerPosition;_walker.Velocity=walkerVelocity;
            _gamePaused=paused;_gameMenu.Visible=menu;RefreshExpedition();
            if(!paused)ResumePlayerWalking();
        }
    }
}
