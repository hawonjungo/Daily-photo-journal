using Relifes.Api.Entities;

namespace Relifes.Api.Entries;

public record CurrencyTotal(string Currency, decimal Amount)
{
    /// <summary>One total per currency: amounts in different currencies are never added together.</summary>
    public static IReadOnlyList<CurrencyTotal> For(IEnumerable<Entry> entries) => entries
        .Where(e => e.Price is not null && e.Currency is not null)
        .GroupBy(e => e.Currency!)
        .Select(group => new CurrencyTotal(group.Key, group.Sum(e => e.Price!.Value)))
        .OrderBy(total => total.Currency, StringComparer.Ordinal)
        .ToList();
}
