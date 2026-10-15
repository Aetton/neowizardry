using System.Collections.Frozen;
using System.Runtime.CompilerServices;

namespace NeoWizardry.Core;

// The source catalog owns immutable definitions. A record copy has a different
// identity and therefore cannot inherit a stale index or rules from its parent.
internal sealed class CompiledItemCatalog
{
    private static readonly ConditionalWeakTable<ItemCatalog,Lazy<CompiledItemCatalog>> Cache=new();
    internal static CompiledItemCatalog Get(ItemCatalog catalog)=>Cache.GetValue(catalog,
        c=>new Lazy<CompiledItemCatalog>(()=>new(c))).Value;
    internal FrozenDictionary<int,ItemDefinition> Definitions {get;}
    private readonly Lazy<FrozenDictionary<int,ItemInventoryRule>> rules;
    internal FrozenDictionary<int,ItemInventoryRule> Rules=>rules.Value;
    private CompiledItemCatalog(ItemCatalog catalog)
    {
        catalog.ValidateSource();
        Definitions=catalog.Items.ToFrozenDictionary(d=>d.Id);
        // Unknown native modes may be inspected as metadata, but never executed.
        rules=new(()=>catalog.Items.Select(d=>d.Instance is not null?OriginalItemAdapter.Rule(d):
            new ItemInventoryRule(d.Id,d.TypeCode is 0 or 1?1:int.MaxValue)).ToFrozenDictionary(r=>r.DefinitionId));
    }
}
