using Godot;
using NeoWizardry.Core;

public partial class Main
{
    private RecipeCatalog? _recipeCatalog;
    private bool _recipesRequested;

    private void LoadRecipeCatalog()
    {
        var args=OS.GetCmdlineUserArgs();int index=Array.IndexOf(args,"--recipes");_recipesRequested=index>=0;
        if(_recipesRequested && index+1>=args.Length) { Say("No combination catalog path selected.");return; }
        string root=ExternalCatalogRoot(),local=Path.Combine(root,"local","recipes.json");
        string? path=_recipesRequested?args[index+1]:Godot.FileAccess.FileExists("user://recipes.json")?"user://recipes.json":System.IO.File.Exists(local)?local:Godot.FileAccess.FileExists("res://demo-items.recipes.json")?"res://demo-items.recipes.json":null;
        if(path is null)return;
        if(!path.StartsWith("user://") && !path.StartsWith("res://") && !Path.IsPathRooted(path))path=Path.GetFullPath(path,root);
        Try(()=>{
            string json=path.StartsWith("user://") || path.StartsWith("res://")?Godot.FileAccess.GetFileAsString(path):System.IO.File.ReadAllText(path);
            _recipeCatalog=RecipeCatalog.Parse(json);Say($"Loaded {_recipeCatalog.Count} combination reference records.");
        });
    }

    private void CheckRecipeReference()
    {
        if((_recipesRequested || Godot.FileAccess.FileExists("res://demo-items.recipes.json")) && _recipeCatalog is null)
            throw new Exception("Requested combination reference was not loaded");
        if(_recipeCatalog is null)return;
        _recipeCatalog.Validate();
        var existing=GetChildren().ToHashSet();int dialogsBefore=_dialogs;
        ShowCombinations();
        var dialog=GetChildren().OfType<AcceptDialog>().Single(d=>!existing.Contains(d));
        try
        {
            var search=(LineEdit)dialog.FindChildren("*","LineEdit",true,false).Single();
            var list=(ItemList)dialog.FindChildren("*","ItemList",true,false).Single();
            var flagged=(CheckButton)dialog.FindChildren("*","CheckButton",true,false).Single();
            if(list.ItemCount!=_recipeCatalog.Recipes.Count(r=>r.Issues.Length==0))throw new Exception("Flagged combinations were not hidden");
            search.Text="no-match-"+Guid.NewGuid();search.EmitSignal(LineEdit.SignalName.TextChanged,search.Text);
            if(list.ItemCount!=0)throw new Exception("Combination search did not filter");
            search.Text="";search.EmitSignal(LineEdit.SignalName.TextChanged,search.Text);flagged.ButtonPressed=true;
            if(list.ItemCount!=_recipeCatalog.Count)throw new Exception("Combination anomaly toggle failed");
            if(list.ItemCount>0)list.EmitSignal(ItemList.SignalName.ItemSelected,list.ItemCount-1);
        }
        finally { dialog.Free(); }
        if(_dialogs!=dialogsBefore)throw new Exception("Combination reference left a modal input lock");
        GD.Print($"PASS: combination reference {_recipeCatalog.Count} records, verified raw fields, search/anomaly filter and modal cleanup");
    }

    private void ShowCombinations()
    {
        if(_recipeCatalog is null) { Say("Import a combination reference as user://recipes.json or select it with --recipes. See the README.");return; }
        Input.MouseMode=Input.MouseModeEnum.Visible;_touchInput.Reset();_dialogs++;
        var dialog=new AcceptDialog { Theme=_hudTheme,Title=_recipeCatalog.Provenance.File.StartsWith("synthetic-")?"Synthetic combination reference":"Original combination reference",Unresizable=true,WrapControls=false };AddChild(dialog);
        dialog.Confirmed+=dialog.QueueFree;dialog.CloseRequested+=dialog.QueueFree;dialog.TreeExited+=()=>{_dialogs=Math.Max(0,_dialogs-1);_touchInput.Reset();};
        var body=new VBoxContainer();dialog.AddChild(body);
        var search=new LineEdit { PlaceholderText="Search result or ingredient name" };body.AddChild(search);
        var flagged=new CheckButton { Text="Include flagged entries" };body.AddChild(flagged);
        var count=new Label();body.AddChild(count);
        var list=new ItemList { AutoHeight=false,CustomMinimumSize=new(0,100),SizeFlagsVertical=Control.SizeFlags.ExpandFill };body.AddChild(list);
        var detail=new RichTextLabel { CustomMinimumSize=new(0,150),SizeFlagsVertical=Control.SizeFlags.ExpandFill };body.AddChild(detail);
        body.AddChild(new Label { Text="Read-only reference.\nCraft still uses test recipes; original item instances are not connected yet." });
        OriginalRecipe[] shown=[];
        void Details(int index)
        {
            var r=shown[index];
            string Ingredient(int i)=>$"{r.IngredientNames[i] ?? "Unknown ingredient"} [ID {r.IngredientIds[i]}]";
            string result=string.IsNullOrEmpty(r.OutputName)?"Unnamed result":r.OutputName;
            detail.Text=$"{result} [ID {r.OutputId}]\n{Ingredient(0)}\n+ {Ingredient(1)}\n\n{r.Skill}"+
                (r.SkillCode==255?"":$" ≥ {r.SkillThreshold}")+"\nIngredient order does not matter. Both must be identified.\nTwo stacks combine by the smaller quantity; other pairs use one instance each.\n"+
                (r.Issues.Length==0?"":$"\nFlagged: {string.Join(", ",r.Issues.Select(x=>x.Replace('_',' ')))}\n")+
                $"\nSource: {_recipeCatalog.Provenance.File}";
        }
        void Filter()
        {
            shown=_recipeCatalog.Recipes.Where(r=>(flagged.ButtonPressed || r.Issues.Length==0) &&
                (r.OutputName.Contains(search.Text,StringComparison.OrdinalIgnoreCase) || r.IngredientNames.Any(n=>n?.Contains(search.Text,StringComparison.OrdinalIgnoreCase)==true))).ToArray();
            count.Text=$"{shown.Length} shown · {_recipeCatalog.Recipes.Count(r=>r.Issues.Length>0)} flagged in source";
            list.Clear();foreach(var r in shown)list.AddItem($"{(string.IsNullOrEmpty(r.OutputName)?"Unnamed result":r.OutputName)} · {r.Skill}"+(r.SkillCode==255?"":$" {r.SkillThreshold}"));
            if(shown.Length>0) { list.Select(0);Details(0); }else detail.Text="No matching combinations.";
        }
        search.TextChanged+=_=>Filter();flagged.Toggled+=_=>Filter();list.ItemSelected+=i=>Details((int)i);
        var screen=GetViewport().GetVisibleRect().Size;var dimensions=new Vector2I((int)Math.Min(850,screen.X-32),(int)Math.Min(640,screen.Y-32));
        body.Position=new(12,12);body.Size=new(dimensions.X-24,dimensions.Y-64);Filter();dialog.PopupCentered(dimensions);
    }
}
