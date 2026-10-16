namespace Service.Interface;

public interface ISlabResolver
{
    /// <summary>
    /// Picks the slab that applies to <paramref name="weight"/> from the slabs a rate actually has:
    /// the highest numeric slab whose minimum weight is &lt;= weight; below the lowest numeric slab, M.
    /// Returns null when no slab can be applied.
    /// </summary>
    string? Resolve(decimal weight, IEnumerable<string> availableSlabCodes);

    /// <summary>Minimum weight (kg) for a slab code: M = 0, +45 = 45, ... Throws for unknown codes.</summary>
    decimal GetMinimumWeight(string slabCode);
}
