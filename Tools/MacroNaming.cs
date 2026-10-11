using System.Text;
using System.Text.RegularExpressions;
using Labs626.UrMcp.Ipc;

namespace Labs626.UrMcp.Tools;

/// <summary>
/// Pure parsing, filtering and grouping for list_macros. Understands the library's naming
/// convention <c>&lt;WHERE&gt; | &lt;ROUTE&gt; n/N | &lt;what&gt; [account] (notes)</c>. Names that
/// don't follow it still list fine, under "Ungrouped".
/// </summary>
public static class MacroNaming
{
    public const string Ungrouped = "Ungrouped";
    public const int HintThreshold = 60;
    private const string Separator = " | ";

    private static readonly Regex RouteStep = new(
        @"^(?<route>.*?)\s*\b(?<n>\d+)/(?<total>\d+)\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public sealed record Parsed(string Group, string? Route, int? Step);

    public static Parsed Parse(string name)
    {
        var first = name.IndexOf(Separator, StringComparison.Ordinal);
        if (first < 0) return new Parsed(Ungrouped, null, null);

        var group = name[..first].Trim();
        if (group.Length == 0) group = Ungrouped;

        var rest = name[(first + Separator.Length)..];
        var second = rest.IndexOf(Separator, StringComparison.Ordinal);
        // A route segment only counts when a "what" segment follows it.
        if (second < 0) return new Parsed(group, null, null);

        var m = RouteStep.Match(rest[..second]);
        if (!m.Success || !int.TryParse(m.Groups["n"].Value, out var step))
            return new Parsed(group, null, null);
        return new Parsed(group, m.Groups["route"].Value.Trim(), step);
    }

    /// <summary>Every whitespace-separated word of <paramref name="query"/> must appear in the name, any case.</summary>
    public static bool MatchesQuery(string name, string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        return query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .All(w => name.Contains(w, StringComparison.OrdinalIgnoreCase));
    }

    public static bool MatchesGroup(string name, string? group)
    {
        if (string.IsNullOrWhiteSpace(group)) return true;
        return string.Equals(Parse(name).Group, group.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<BridgeMacro> Filter(IEnumerable<BridgeMacro> macros, string? query, string? group)
        => macros.Where(m => MatchesQuery(m.Name, query) && MatchesGroup(m.Name, group)).ToList();

    /// <summary>Groups alphabetical with Ungrouped last; inside a group, route steps (by route, then n) before the rest (by name).</summary>
    public static IReadOnlyList<(string Group, IReadOnlyList<BridgeMacro> Macros)> GroupAndOrder(IEnumerable<BridgeMacro> macros)
    {
        return macros
            .Select(m => (Macro: m, P: Parse(m.Name)))
            .GroupBy(x => x.P.Group, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => string.Equals(g.Key, Ungrouped, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => (
                g.Key,
                (IReadOnlyList<BridgeMacro>)g
                    .OrderBy(x => x.P.Step is null ? 1 : 0)
                    .ThenBy(x => x.P.Route ?? "", StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.P.Step ?? 0)
                    .ThenBy(x => x.Macro.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.Macro)
                    .ToList()))
            .ToList();
    }

    public static string Render(IReadOnlyList<BridgeMacro> all, string? query, string? group)
    {
        var hasQuery = !string.IsNullOrWhiteSpace(query);
        var hasGroup = !string.IsNullOrWhiteSpace(group);
        var hits = Filter(all, query, group);

        if (hits.Count == 0)
        {
            var criteria = new List<string>();
            if (hasQuery) criteria.Add($"matching \"{query!.Trim()}\"");
            if (hasGroup) criteria.Add($"in group \"{group!.Trim()}\"");
            var sb0 = new StringBuilder();
            sb0.AppendLine($"No macros {string.Join(" ", criteria)}. {all.Count} macro(s) exist in these groups:");
            foreach (var (g, ms) in GroupAndOrder(all)) sb0.AppendLine($"- {g} ({ms.Count})");
            return sb0.ToString().TrimEnd();
        }

        var groups = GroupAndOrder(hits);
        var sb = new StringBuilder();
        if (!hasQuery && !hasGroup && all.Count > HintThreshold)
            sb.AppendLine("Hint: list_macros accepts query= and group= to narrow a long library.");

        sb.Append($"{hits.Count} macro(s) in {groups.Count} group{(groups.Count == 1 ? "" : "s")}");
        if (hasQuery) sb.Append($", matching \"{query!.Trim()}\"");
        if (hasGroup) sb.Append($", in group \"{group!.Trim()}\"");
        sb.AppendLine(":");
        foreach (var (g, ms) in groups)
        {
            sb.AppendLine($"## {g} ({ms.Count})");
            foreach (var m in ms) sb.AppendLine($"- {m.Name} | id {m.Id}");
        }
        return sb.ToString().TrimEnd();
    }
}
