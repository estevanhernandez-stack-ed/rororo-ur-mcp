using Labs626.UrMcp.Ipc;

namespace Labs626.UrMcp.Resolution;

/// <summary>
/// Turns the human words Claude passes ("Pokey", "the farm macro") into the ids the wire wants.
/// Exact-id passthrough first (case-insensitive), then case-insensitive display-name match; zero
/// or many matches throw a <see cref="ResolutionException"/> whose message lists the candidates,
/// so the operator can re-ask with a real name instead of guessing.
/// </summary>
public static class NameResolver
{
    public static HostAccount ResolveAccount(IReadOnlyList<HostAccount> accounts, string nameOrId)
    {
        var byId = accounts.FirstOrDefault(a => string.Equals(a.AccountId, nameOrId, StringComparison.OrdinalIgnoreCase));
        if (byId is not null) return byId;

        var matches = accounts
            .Where(a => string.Equals(a.DisplayName, nameOrId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new ResolutionException(
                $"No account named '{nameOrId}'. Known accounts: {Names(accounts.Select(a => a.DisplayName))}."),
            _ => throw new ResolutionException(
                $"'{nameOrId}' is ambiguous — {matches.Count} accounts share that name. " +
                $"Use an account id instead: {Names(matches.Select(m => $"{m.DisplayName} ({m.AccountId})"))}."),
        };
    }

    public static HostAccount ResolveMain(IReadOnlyList<HostAccount> accounts)
        => accounts.FirstOrDefault(a => a.IsMain)
           ?? throw new ResolutionException(
               "No account is marked as main in RoRoRo. Set one (right-click a row → Set as main), then try again.");

    public static BridgeMacro ResolveMacro(IReadOnlyList<BridgeMacro> macros, string nameOrId)
    {
        var byId = macros.FirstOrDefault(m => string.Equals(m.Id, nameOrId, StringComparison.OrdinalIgnoreCase));
        if (byId is not null) return byId;

        var matches = macros
            .Where(m => string.Equals(m.Name, nameOrId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new ResolutionException(
                $"No macro named '{nameOrId}'. Known macros: {Names(macros.Select(m => m.Name))}."),
            _ => throw new ResolutionException(
                $"'{nameOrId}' is ambiguous — {matches.Count} macros share that name. " +
                $"Use a macro id instead: {Names(matches.Select(m => $"{m.Name} ({m.Id})"))}."),
        };
    }

    private static string Names(IEnumerable<string> names)
    {
        var list = names.ToList();
        return list.Count == 0 ? "(none)" : string.Join(", ", list);
    }
}

/// <summary>An unknown or ambiguous name — the message is the tool result.</summary>
public sealed class ResolutionException : Exception
{
    public ResolutionException(string message) : base(message) { }
}
