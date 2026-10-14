namespace NeoWizardry.Core;

public sealed record ItemStack(int ItemId, int Count);

// Shared party bag. Equipped copies live in hero slots, not in bag quantities.
public static class Inventory
{
    public static int Count(GameState state, int itemId) =>
        (state.Bag ?? []).FirstOrDefault(s => s.ItemId == itemId)?.Count ?? 0;

    public static GameState Add(GameState state, int itemId, int count, ItemCatalog catalog)
    {
        Game.Validate(state); catalog.Validate(); catalog.Find(itemId);
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        var bag = (state.Bag ?? []).ToDictionary(s => s.ItemId, s => s.Count);
        bag[itemId] = checked(bag.GetValueOrDefault(itemId) + count);
        return state with { Bag = bag.OrderBy(s => s.Key).Select(s => new ItemStack(s.Key,s.Value)).ToArray() };
    }

    public static GameState Remove(GameState state, int itemId, int count)
    {
        Game.Validate(state);
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        int owned = Count(state,itemId);
        if (owned < count) throw new InvalidOperationException("Not enough items in the party bag.");
        return state with { Bag = (state.Bag ?? []).Select(s => s.ItemId == itemId ? s with { Count=owned-count } : s)
            .Where(s => s.Count > 0).ToArray() };
    }
}
