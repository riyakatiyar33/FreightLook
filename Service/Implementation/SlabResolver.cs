using Models.Constants;
using Service.Interface;

namespace Service.Implementation;

public sealed class SlabResolver : ISlabResolver
{
    private static readonly IReadOnlyDictionary<string, decimal> MinimumWeights = new Dictionary<string, decimal>
    {
        [SlabCodes.Minimum] = 0m,
        [SlabCodes.Plus45] = 45m,
        [SlabCodes.Plus100] = 100m,
        [SlabCodes.Plus250] = 250m,
        [SlabCodes.Plus300] = 300m,
        [SlabCodes.Plus500] = 500m,
        [SlabCodes.Plus1000] = 1000m,
    };

    public string? Resolve(decimal weight, IEnumerable<string> availableSlabCodes)
    {
        if (weight <= 0)
            return null;

        var available = availableSlabCodes
            .Where(MinimumWeights.ContainsKey)
            .ToHashSet();

        var numericSlabs = available
            .Where(code => code != SlabCodes.Minimum)
            .OrderBy(code => MinimumWeights[code])
            .ToList();

        // Highest numeric slab whose minimum weight is <= W.
        var match = numericSlabs.LastOrDefault(code => MinimumWeights[code] <= weight);
        if (match is not null)
            return match;

        // Below the lowest numeric slab -> M.
        if (available.Contains(SlabCodes.Minimum))
            return SlabCodes.Minimum;

        // Rate has no M slab: fall back to its lowest numeric slab (the minimum charge still applies).
        return numericSlabs.FirstOrDefault();
    }

    public decimal GetMinimumWeight(string slabCode) =>
        MinimumWeights.TryGetValue(slabCode, out var weight)
            ? weight
            : throw new ArgumentException($"Unknown slab code '{slabCode}'.", nameof(slabCode));
}
