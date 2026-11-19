using Shoko.Abstractions.Filtering.Services;

namespace Shoko.Plugin.OfflineImporter.Tests;

/// <summary>
/// Scores as the server's fuzzy search does, in short: a case-insensitive
/// substring match is exact, at its index, with no length difference at the
/// start; otherwise a name within the allowed edits is a fuzzy match.
/// </summary>
internal sealed class FakeFuzzySearchService : IFuzzySearchService
{
    public bool FuzzyMatchesAnyName(string query, IReadOnlySet<string> names)
        => FuzzyScoreAnyName(query, names) is not null;

    public (bool isNotExact, int index, double distance, int lengthDiff)? FuzzyScoreAnyName(string query, IReadOnlySet<string> names)
    {
        var normalizedQuery = query.Trim().ToLowerInvariant();
        if (normalizedQuery.Length is 0)
            return null;

        var maxErrors = normalizedQuery.Length switch { < 4 => 0, < 8 => 1, _ => 2 };
        (bool, int, double, int)? best = null;
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var normalizedName = name.Trim().ToLowerInvariant();
            (bool, int, double, int)? current = null;
            var index = normalizedName.IndexOf(normalizedQuery, StringComparison.Ordinal);
            if (index >= 0)
            {
                current = (false, index, 0.0, index is 0 ? 0 : Math.Abs(normalizedQuery.Length - normalizedName.Length));
            }
            else if (maxErrors > 0 && Distance(normalizedQuery, normalizedName) is var distance && distance <= maxErrors)
            {
                current = (true, 0, (double)distance / normalizedQuery.Length, Math.Abs(normalizedQuery.Length - normalizedName.Length));
            }

            if (current is not null && (best is null || Comparer<(bool, int, double, int)>.Default.Compare(current.Value, best.Value) < 0))
                best = current;
        }

        return best;
    }

    public void InvalidateCache() { }

    private static int Distance(string a, string b)
    {
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var current = new int[b.Length + 1];
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            previous = current;
        }

        return previous[b.Length];
    }
}
