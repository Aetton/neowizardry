using Godot;
using NeoWizardry.Core;
using System.Security.Cryptography;

public partial class WorldPreview
{
    private NativeWorldItems? _nativeItems;
    private Label? _itemHint;
    private readonly Dictionary<int,Node3D> _itemVisuals=new();
    private void InitializeWorldItems(string folder)
    {
        if(!GameMode)return;
        _nativeItems=NativeWorldItems.Parse(File.ReadAllText(Path.Combine(folder,"monastery1.nwitems.json")),_nativeEncounter!,_expeditionItems);
        _expedition=WorldItemPickup.Attach(_expedition!,_nativeItems,_expeditionItems);
        var scenes=new Dictionary<int,Node3D>();
        foreach(var member in _nativeItems.Members.Where(m=>!m.Hidden))
        {
            if(!scenes.TryGetValue(member.DefinitionId,out var template))
            {
                string path=Path.Combine(folder,member.MeshFile);
                using(var stream=File.OpenRead(path))if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(member.MeshSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("World item model hash mismatch");
                using var document=new GltfDocument();using var state=new GltfState();
                if(document.AppendFromFile(path,state)!=Error.Ok)throw new InvalidDataException("World item model import failed");
                template=document.GenerateScene(state) as Node3D??throw new InvalidDataException("World item model scene missing");
                ConfigureTextures(state,false);scenes.Add(member.DefinitionId,template);
            }
            var visual=(Node3D)template.Duplicate();visual.Position=NativePosition(member.Position);AddChild(visual);
            _itemVisuals.Add(member.LocationId,visual);
        }
        foreach(var template in scenes.Values)template.Free();
    }
    private void MigratePreviousPlayerItems()
    {
        if(_nativeItems is null)return;
        int at=Array.IndexOf(Args,"--world");
        string previousHash=ExplorationCheckpointFile.ContentHash(Path.GetFullPath(Args[at+1]),includeItems:false);
        string previous=Path.Combine(Path.GetDirectoryName(_expeditionPath)!,previousHash+".json");
        if(!File.Exists(previous))return;
        var old=WorldExpedition.Load(ExplorationCheckpointFile.Read(previous,WorldExpedition.MaximumBytes),
            previousHash,CheckpointLimits,_expeditionItems,_expeditionMonsters,_nativeEncounter);
        var upgraded=WorldItemPickup.Attach(old,_nativeItems,_expeditionItems);
        upgraded=upgraded with {World=upgraded.World with {ContentHash=_contentHash}};
        string json=WorldExpedition.Save(upgraded,_contentHash,CheckpointLimits,_expeditionItems,_expeditionMonsters,_nativeEncounter,_nativeItems);
        ExplorationCheckpointFile.Write(_expeditionPath,json,WorldExpedition.MaximumBytes);
    }
    private void RefreshWorldItems()
    {
        foreach(var (id,visual) in _itemVisuals)visual.Visible=_expedition?.CollectedItems?.Contains(id)!=true;
    }
    private NativeWorldItem? TargetWorldItem()
    {
        if(_nativeItems is null || _expedition is null)return null;
        return _nativeItems.Members.Where(m=>!m.Hidden && !_expedition.CollectedItems!.Contains(m.LocationId))
            .Where(m=>
            {
                Vector3 target=NativePosition(m.Position)+Vector3.Up*0.15f,delta=target-_view.GlobalPosition;
                if(delta.Length()>2.7f || delta.Length()<0.01f || (-_view.GlobalBasis.Z).Dot(delta.Normalized())<0.65f)return false;
                var query=PhysicsRayQueryParameters3D.Create(_view.GlobalPosition,target,1);
                return GetWorld3D().DirectSpaceState.IntersectRay(query).Count==0;
            }).OrderBy(m=>_view.GlobalPosition.DistanceSquaredTo(NativePosition(m.Position))).FirstOrDefault();
    }
    private void UpdatePlayerItemHint()
    {
        if(_itemHint is null)return;
        var source=_gameReady && !_gamePaused && !BattleOpen && _expedition?.Phase!=WorldEncounterPhase.Defeat?TargetWorldItem():null;
        _itemHint.Visible=source is not null;
        _itemHint.Position=new(18,GetViewport().GetVisibleRect().Size.Y-124);
        if(source is not null)_itemHint.Text=$"E — подобрать: {ItemName(source)} ×{source.Instance(_expeditionItems,"hint").Quantity}";
    }
    private string ItemName(NativeWorldItem item)=>item.Identified?_expeditionItems.Find(item.DefinitionId).Name:"Неопознанный предмет";
    private bool TryCollectWorldItem()
    {
        var source=TargetWorldItem();if(source is null)return false;
        try
        {
            var next=WorldItemPickup.Take(_expedition!,source.LocationId,_expeditionItems);
            var item=next.Game.Items!.Items.Single(i=>!_expedition!.Game.Items!.Items.Any(old=>old.Id==i.Id));
            _expedition=next;RefreshWorldItems();RefreshPlayerHud();SavePlayerGame();
            Notice($"Подобрано: {ItemName(source)} ×{item.Quantity}. Откройте инвентарь — I.");
        }
        catch(Exception error){Notice("Не удалось подобрать: "+error.Message);}
        return true;
    }
    private void CheckPreviousItemMigration()
    {
        string oldPath=_expeditionPath;
        string folder=Path.Combine(Path.GetTempPath(),"neo-item-migration-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            _expeditionPath=Path.Combine(folder,"current.json");
            int at=Array.IndexOf(Args,"--world");
            string previousHash=ExplorationCheckpointFile.ContentHash(Path.GetFullPath(Args[at+1]),includeItems:false);
            var old=_expedition! with {WorldItems=null,CollectedItems=null,World=_expedition.World with {ContentHash=previousHash}};
            string previous=Path.Combine(folder,previousHash+".json");
            string json=WorldExpedition.Save(old,previousHash,CheckpointLimits,_expeditionItems,_expeditionMonsters,_nativeEncounter);
            ExplorationCheckpointFile.Write(previous,json,WorldExpedition.MaximumBytes);
            MigratePreviousPlayerItems();
            var migrated=WorldExpedition.Load(File.ReadAllText(_expeditionPath),_contentHash,CheckpointLimits,_expeditionItems,_expeditionMonsters,_nativeEncounter,_nativeItems);
            if(Game.Save(migrated.Game)!=Game.Save(old.Game) || migrated.Phase!=old.Phase || migrated.World.Position!=old.World.Position || migrated.World.Rotation!=old.World.Rotation ||
               migrated.CollectedItems is not {Count:0} || File.ReadAllText(previous)!=json)
                throw new InvalidDataException("Item upgrade changed previous native campaign or rewrote old save");
            GD.Print("PASS: previous native campaign migrates with exact party/items/RNG/XP, encounter phase and position; old save retained");
        }
        finally{_expeditionPath=oldPath;Directory.Delete(folder,true);}
    }
    private void CheckNativeWorldItems()
    {
        if(_nativeItems is null || _expedition is null || _itemVisuals.Count!=_nativeItems.Members.Count(m=>!m.Hidden))throw new InvalidDataException("Native world item models missing");
        var source=_nativeItems.Members.First(m=>!m.Hidden);
        var original=_expedition;var cameraPosition=_view.GlobalPosition;var cameraRotation=_view.Rotation;
        try
        {
            // Exercise the same range/aim/LOS and E path as ordinary player input.
            Vector3 target=NativePosition(source.Position)+Vector3.Up*0.15f;
            _view.GlobalPosition=target+Vector3.Up*0.9f;_view.LookAt(target,Vector3.Forward);
            if(TargetWorldItem()?.LocationId!=source.LocationId)throw new InvalidDataException("Native pickup cannot be targeted through player interaction");
            PlayerInteract();
            if(!_expedition.CollectedItems!.Contains(source.LocationId) || _itemVisuals[source.LocationId].Visible)throw new InvalidDataException("E did not collect native item");
            var expected=source.Instance(_expeditionItems,"expected");var actual=_expedition.Game.Items!.Items.Last();
            if((actual with {Id="expected"})!=expected || _expedition.Game.RandomState!=original.Game.RandomState)throw new InvalidDataException("Pickup rerolled native quantity, identification, charges or RNG");
            SaveExpedition();LoadExpedition();
            if(!_expedition.CollectedItems!.Contains(source.LocationId) || _itemVisuals[source.LocationId].Visible || TargetWorldItem()?.LocationId==source.LocationId)throw new InvalidDataException("Collected native item respawned after load");
            bool rejected=false;try{WorldItemPickup.Take(_expedition,source.LocationId,_expeditionItems);}catch(InvalidOperationException){rejected=true;}
            if(!rejected)throw new InvalidDataException("Duplicate native pickup allowed");
            GD.Print($"PASS: native floor items {_nativeItems.Members.Length}, {_itemVisuals.Count} visible original models; E pickup preserves quantity/charges/identification/RNG, save/reload and no duplicate");
        }
        finally{_expedition=original;_view.GlobalPosition=cameraPosition;_view.Rotation=cameraRotation;RefreshWorldItems();SaveExpedition();}
    }
}
