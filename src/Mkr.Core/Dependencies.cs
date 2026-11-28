using System.Security.Cryptography;
using System.Text;

namespace Mkr.Core;

// Automatic obsolescence of results by their REAL dependencies (ADR 0009, cahier directeur §25 point 12 and §26).
// Pure logic: no I/O, no clock, no localisation. Obsolescence is DERIVED by comparing the fingerprints a result recorded at calculation time with the
// project's active configuration; it is never stored, so a stale flag cannot make an outdated result look current.

/// <summary>The facets of the project configuration a result can depend on. Imported original data stay immutable: <see cref="Dataset"/> is the SELECTION of the import (id + SHA-256).</summary>
public enum ConfigurationFacet
{
    Dataset, SignalMapping, UnitsAndConversions, IdentificationWindow, Preprocessing, IdentificationMethod,
    ProcessParameters, ControlStrategy, ManufacturerBlock, PidParameters, OperatingLimits
}

public enum ResultKind { Model, Tuning, Simulation, BlockConversion, Report }

/// <summary>The eight statuses of cahier directeur §26.</summary>
public enum ResultStatus { NotStarted, InProgress, ToComplete, ToVerify, ValidatedForDomain, Blocked, NotApplicable, Obsolete }

public enum ObsolescenceCause { FacetChanged, UpstreamObsolete, UpstreamMissing }

/// <summary>One change of a configuration facet, kept forever (append-only history).</summary>
public sealed record ConfigurationChange(ConfigurationFacet Facet, string? PreviousFingerprint, string Fingerprint, DateTimeOffset At, string Actor, string? Reason);

/// <summary>Canonical fingerprints: SHA-256 of "key=value" lines sorted by key, so the order in which inputs are listed never matters.</summary>
public static class Fingerprint
{
    public static string Of(IEnumerable<KeyValuePair<string, string>> inputs)
    {
        var text = string.Join("\n", inputs.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    public static string Of(params (string Key, string Value)[] inputs) => Of(inputs.Select(i => KeyValuePair.Create(i.Key, i.Value)));
}

/// <summary>The project's ACTIVE configuration and its append-only history. Immutable: <see cref="Apply"/> returns a new instance.</summary>
public sealed class ProjectConfiguration
{
    public static readonly ProjectConfiguration Empty = new(new Dictionary<ConfigurationFacet, string>(), Array.Empty<ConfigurationChange>());

    private ProjectConfiguration(IReadOnlyDictionary<ConfigurationFacet, string> current, IReadOnlyList<ConfigurationChange> history)
    {
        Current = current;
        History = history;
    }

    public IReadOnlyDictionary<ConfigurationFacet, string> Current { get; }
    public IReadOnlyList<ConfigurationChange> History { get; }

    /// <summary>Sets a facet. Re-applying the same fingerprint is a no-op (no history entry), so identical saves never create noise.</summary>
    public ProjectConfiguration Apply(ConfigurationFacet facet, string fingerprint, DateTimeOffset at, string actor, string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(fingerprint)) throw new ArgumentException("A fingerprint is required.", nameof(fingerprint));
        Current.TryGetValue(facet, out var previous);
        if (previous == fingerprint) return this;
        var current = new Dictionary<ConfigurationFacet, string>(Current) { [facet] = fingerprint };
        var history = History.Append(new ConfigurationChange(facet, previous, fingerprint, at, actor, reason)).ToArray();
        return new ProjectConfiguration(current, history);
    }

    /// <summary>The last recorded change of a facet whose new fingerprint is the given one (when and by whom the active value was set), or null.</summary>
    public ConfigurationChange? LastChangeTo(ConfigurationFacet facet, string fingerprint) =>
        History.LastOrDefault(change => change.Facet == facet && change.Fingerprint == fingerprint);
}

/// <summary>A stored result as the engine sees it. <see cref="FacetSnapshot"/> is null for a result recorded before dependencies were (a legacy result).</summary>
public sealed record ResultNode(Guid Id, ResultKind Kind, DateTimeOffset CalculatedAt, IReadOnlyDictionary<ConfigurationFacet, string>? FacetSnapshot,
    IReadOnlyList<Guid> UpstreamResults, bool ValidatedByItsOwnGate, bool Blocked = false);

public sealed record ObsolescenceReason(ObsolescenceCause Cause, ConfigurationFacet? Facet, Guid? Upstream, string? PreviousFingerprint, string? CurrentFingerprint,
    DateTimeOffset? ChangedAt, string? ChangedBy);

/// <param name="Action">Code of the step to redo (recompute_identification, recompute_tuning, rerun_simulation, reconvert_block, regenerate_report) ; null when nothing is to redo.</param>
/// <param name="LegacyWithoutDependencyRecord">True when the result predates dependency recording: neither obsolescence nor freshness can be asserted for it.</param>
public sealed record NodeStatus(Guid Id, ResultStatus Status, IReadOnlyList<ObsolescenceReason> Reasons, bool OperationalExportAllowed, string? Action,
    bool LegacyWithoutDependencyRecord = false);

/// <summary>Which configuration facets each kind of result depends on. Explicit, versioned and tested: a facet not listed for a kind NEVER invalidates it.</summary>
public static class DependencyRules
{
    public const string Version = "1.0.0";

    private static readonly IReadOnlyDictionary<ResultKind, ConfigurationFacet[]> Table = new Dictionary<ResultKind, ConfigurationFacet[]>
    {
        [ResultKind.Model] = new[] { ConfigurationFacet.Dataset, ConfigurationFacet.SignalMapping, ConfigurationFacet.UnitsAndConversions,
            ConfigurationFacet.IdentificationWindow, ConfigurationFacet.Preprocessing, ConfigurationFacet.IdentificationMethod },
        [ResultKind.Tuning] = new[] { ConfigurationFacet.ProcessParameters, ConfigurationFacet.ControlStrategy, ConfigurationFacet.OperatingLimits },
        [ResultKind.Simulation] = new[] { ConfigurationFacet.ProcessParameters, ConfigurationFacet.PidParameters, ConfigurationFacet.OperatingLimits },
        [ResultKind.BlockConversion] = new[] { ConfigurationFacet.ManufacturerBlock, ConfigurationFacet.PidParameters },
        [ResultKind.Report] = Array.Empty<ConfigurationFacet>(),   // a report depends on its upstream results only
    };

    public static IReadOnlyList<ConfigurationFacet> FacetsOf(ResultKind kind) => Table[kind];

    public static string? ActionFor(ResultKind kind) => kind switch
    {
        ResultKind.Model => "recompute_identification",
        ResultKind.Tuning => "recompute_tuning",
        ResultKind.Simulation => "rerun_simulation",
        ResultKind.BlockConversion => "reconvert_block",
        ResultKind.Report => "regenerate_report",
        _ => null,
    };
}

public static class DependencyEngine
{
    /// <summary>Version of the decision rules (table + propagation). Bump it whenever the behaviour changes.</summary>
    public const string RulesVersion = DependencyRules.Version;

    /// <summary>Evaluates every result. Deterministic: the order of <paramref name="nodes"/> never changes the outcome. A dependency cycle throws InvalidDataException.</summary>
    public static IReadOnlyDictionary<Guid, NodeStatus> Evaluate(ProjectConfiguration configuration, IReadOnlyCollection<ResultNode> nodes)
    {
        var byId = new Dictionary<Guid, ResultNode>();
        foreach (var node in nodes)
            if (!byId.TryAdd(node.Id, node)) throw new InvalidDataException("Two results share the same identifier.");
        var done = new Dictionary<Guid, NodeStatus>();
        var visiting = new HashSet<Guid>();
        foreach (var node in nodes.OrderBy(n => n.Id)) Resolve(node, configuration, byId, done, visiting);
        return done;
    }

    private static NodeStatus Resolve(ResultNode node, ProjectConfiguration configuration, IReadOnlyDictionary<Guid, ResultNode> byId,
        Dictionary<Guid, NodeStatus> done, HashSet<Guid> visiting)
    {
        if (done.TryGetValue(node.Id, out var known)) return known;
        if (!visiting.Add(node.Id)) throw new InvalidDataException("Cycle in the dependencies of results.");
        var reasons = new List<ObsolescenceReason>();

        if (node.FacetSnapshot is { } snapshot)
            foreach (var facet in DependencyRules.FacetsOf(node.Kind))
            {
                if (!snapshot.TryGetValue(facet, out var recorded)) continue;               // the result did not record this facet: nothing to compare
                configuration.Current.TryGetValue(facet, out var active);
                if (active == recorded) continue;
                var change = active is null ? null : configuration.LastChangeTo(facet, active);
                reasons.Add(new ObsolescenceReason(ObsolescenceCause.FacetChanged, facet, null, recorded, active, change?.At, change?.Actor));
            }

        var upstreamAllowExport = true;
        foreach (var upstreamId in node.UpstreamResults.OrderBy(id => id))
        {
            if (!byId.TryGetValue(upstreamId, out var upstream))
            {
                reasons.Add(new ObsolescenceReason(ObsolescenceCause.UpstreamMissing, null, upstreamId, null, null, null, null));
                upstreamAllowExport = false;
                continue;
            }
            var upstreamStatus = Resolve(upstream, configuration, byId, done, visiting);
            if (upstreamStatus.Status == ResultStatus.Obsolete)
                reasons.Add(new ObsolescenceReason(ObsolescenceCause.UpstreamObsolete, null, upstreamId, null, null, null, null));
            upstreamAllowExport &= upstreamStatus.OperationalExportAllowed;
        }

        var legacy = node.FacetSnapshot is null;
        ResultStatus status;
        if (reasons.Count > 0) status = ResultStatus.Obsolete;
        else if (node.Blocked) status = ResultStatus.Blocked;
        else if (legacy) status = ResultStatus.ToVerify;                                   // cannot assert freshness: not obsolete, not "validated for the domain"
        else status = node.ValidatedByItsOwnGate ? ResultStatus.ValidatedForDomain : ResultStatus.ToVerify;

        // Operational export and final validation: never for an obsolete or blocked result, nor one whose upstream results are not allowed ;
        // a legacy result keeps today's behaviour (its own gate) and is flagged so the interface says no dependency was recorded.
        var allowed = status is ResultStatus.ValidatedForDomain or ResultStatus.ToVerify && node.ValidatedByItsOwnGate && upstreamAllowExport;
        var result = new NodeStatus(node.Id, status, reasons, allowed, status == ResultStatus.Obsolete ? DependencyRules.ActionFor(node.Kind) : null, legacy);
        visiting.Remove(node.Id);
        done[node.Id] = result;
        return result;
    }
}
