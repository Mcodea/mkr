using Mkr.Core;

// Tests of the dependency engine (ADR 0009). Pure logic: runs anywhere. Every scenario states WHAT must be obsolete and what must NOT be.
var failures = 0;
void Check(string name, Func<bool> body)
{
    try
    {
        if (body()) Console.WriteLine("ok    " + name);
        else { failures++; Console.WriteLine("FAIL  " + name); }
    }
    catch (Exception error) { failures++; Console.WriteLine("FAIL  " + name + " : " + error.Message); }
}

var t0 = new DateTimeOffset(2026, 11, 28, 9, 0, 0, TimeSpan.Zero);
ProjectConfiguration Initial() => ProjectConfiguration.Empty
    .Apply(ConfigurationFacet.Dataset, "data-1", t0, "alice").Apply(ConfigurationFacet.SignalMapping, "map-1", t0, "alice")
    .Apply(ConfigurationFacet.UnitsAndConversions, "units-1", t0, "alice").Apply(ConfigurationFacet.IdentificationWindow, "win-1", t0, "alice")
    .Apply(ConfigurationFacet.Preprocessing, "pre-1", t0, "alice").Apply(ConfigurationFacet.IdentificationMethod, "fopdt-1", t0, "alice")
    .Apply(ConfigurationFacet.ProcessParameters, "proc-1", t0, "alice").Apply(ConfigurationFacet.ControlStrategy, "pi-1", t0, "alice")
    .Apply(ConfigurationFacet.ManufacturerBlock, "pidff-1", t0, "alice").Apply(ConfigurationFacet.PidParameters, "pid-1", t0, "alice")
    .Apply(ConfigurationFacet.OperatingLimits, "lim-1", t0, "alice");

// A complete chain: model -> tuning -> simulation, tuning -> block conversion, all of them -> report. Snapshots are taken from the initial configuration.
IReadOnlyDictionary<ConfigurationFacet, string> Snapshot(ProjectConfiguration cfg, ResultKind kind) =>
    DependencyRules.FacetsOf(kind).Where(cfg.Current.ContainsKey).ToDictionary(f => f, f => cfg.Current[f]);
var model = Guid.Parse("00000000-0000-0000-0000-000000000001");
var tuning = Guid.Parse("00000000-0000-0000-0000-000000000002");
var simulation = Guid.Parse("00000000-0000-0000-0000-000000000003");
var conversion = Guid.Parse("00000000-0000-0000-0000-000000000004");
var report = Guid.Parse("00000000-0000-0000-0000-000000000005");
IReadOnlyList<ResultNode> Chain(ProjectConfiguration cfg, bool validated = true) => new[]
{
    new ResultNode(model, ResultKind.Model, t0, Snapshot(cfg, ResultKind.Model), Array.Empty<Guid>(), validated),
    new ResultNode(tuning, ResultKind.Tuning, t0, Snapshot(cfg, ResultKind.Tuning), new[] { model }, validated),
    new ResultNode(simulation, ResultKind.Simulation, t0, Snapshot(cfg, ResultKind.Simulation), new[] { tuning }, validated),
    new ResultNode(conversion, ResultKind.BlockConversion, t0, Snapshot(cfg, ResultKind.BlockConversion), new[] { tuning }, validated),
    new ResultNode(report, ResultKind.Report, t0, Snapshot(cfg, ResultKind.Report), new[] { model, tuning, simulation, conversion }, validated),
};
HashSet<Guid> ObsoleteAfter(ConfigurationFacet facet, string fingerprint)
{
    var original = Initial();
    var nodes = Chain(original);
    var changed = original.Apply(facet, fingerprint, t0.AddHours(1), "bob", "test");
    return DependencyEngine.Evaluate(changed, nodes.ToArray()).Values.Where(s => s.Status == ResultStatus.Obsolete).Select(s => s.Id).ToHashSet();
}

Check("fingerprints are canonical: order never matters, any change does", () =>
    Fingerprint.Of(("a", "1"), ("b", "2")) == Fingerprint.Of(("b", "2"), ("a", "1")) &&
    Fingerprint.Of(("a", "1"), ("b", "2")) != Fingerprint.Of(("a", "1"), ("b", "3")) &&
    Fingerprint.Of(("a", "1")) != Fingerprint.Of(("a", "1"), ("b", "")) &&
    Fingerprint.Of(("a", "1")).Length == 64);

Check("nothing changed: every validated result is validated for the domain and may be exported", () =>
{
    var all = DependencyEngine.Evaluate(Initial(), Chain(Initial()).ToArray());
    return all.Count == 5 && all.Values.All(s => s.Status == ResultStatus.ValidatedForDomain && s.OperationalExportAllowed && s.Reasons.Count == 0 && s.Action is null);
});

Check("replacing the active dataset makes EVERYTHING derived from it obsolete (cascade) and keeps the reason", () =>
{
    var obsolete = ObsoleteAfter(ConfigurationFacet.Dataset, "data-2");
    return obsolete.SetEquals(new[] { model, tuning, simulation, conversion, report });
});

Check("changing the window, units, mapping, preprocessing or method invalidates the model and its descendants only", () =>
    new[] { (ConfigurationFacet.IdentificationWindow, "win-2"), (ConfigurationFacet.UnitsAndConversions, "units-2"), (ConfigurationFacet.SignalMapping, "map-2"),
            (ConfigurationFacet.Preprocessing, "pre-2"), (ConfigurationFacet.IdentificationMethod, "sopdt-1") }
        .All(c => ObsoleteAfter(c.Item1, c.Item2).SetEquals(new[] { model, tuning, simulation, conversion, report })));

Check("changing the control strategy invalidates the tuning and its descendants but NOT the model (independent step)", () =>
{
    var obsolete = ObsoleteAfter(ConfigurationFacet.ControlStrategy, "pid-strategy");
    return obsolete.SetEquals(new[] { tuning, simulation, conversion, report }) && !obsolete.Contains(model);
});

Check("changing the manufacturer block invalidates only the conversion (and the report): the tuning and the simulation stay valid", () =>
{
    var obsolete = ObsoleteAfter(ConfigurationFacet.ManufacturerBlock, "pi_b-1");
    return obsolete.SetEquals(new[] { conversion, report });
});

Check("changing the PID parameters invalidates the simulation and the conversion but neither the model nor the tuning", () =>
{
    var obsolete = ObsoleteAfter(ConfigurationFacet.PidParameters, "pid-2");
    return obsolete.SetEquals(new[] { simulation, conversion, report });
});

Check("changing the operating limits invalidates the tuning and the simulation (and what derives from them), not the model nor the conversion's own facets", () =>
{
    var obsolete = ObsoleteAfter(ConfigurationFacet.OperatingLimits, "lim-2");
    return obsolete.SetEquals(new[] { tuning, simulation, conversion, report }) && !obsolete.Contains(model);
});

Check("changing the process parameters invalidates the tuning and the simulation, not the model", () =>
{
    var obsolete = ObsoleteAfter(ConfigurationFacet.ProcessParameters, "proc-2");
    return obsolete.Contains(tuning) && obsolete.Contains(simulation) && !obsolete.Contains(model);
});

Check("the reason says WHICH facet changed, from what to what, when and by whom, and the action to redo", () =>
{
    var original = Initial();
    var changed = original.Apply(ConfigurationFacet.IdentificationWindow, "win-2", t0.AddHours(2), "bob", "fenêtre élargie");
    var all = DependencyEngine.Evaluate(changed, Chain(original).ToArray());
    var m = all[model];
    var reason = m.Reasons.Single();
    var downstream = all[tuning].Reasons.Single();
    return m.Status == ResultStatus.Obsolete && reason is { Cause: ObsolescenceCause.FacetChanged, Facet: ConfigurationFacet.IdentificationWindow, PreviousFingerprint: "win-1",
               CurrentFingerprint: "win-2", ChangedBy: "bob" } && reason.ChangedAt == t0.AddHours(2) && m.Action == "recompute_identification" &&
           downstream is { Cause: ObsolescenceCause.UpstreamObsolete } && downstream.Upstream == model && all[tuning].Action == "recompute_tuning";
});

Check("an obsolete result may NOT be exported or finally validated, nor anything built on it", () =>
{
    var original = Initial();
    var changed = original.Apply(ConfigurationFacet.IdentificationWindow, "win-2", t0.AddHours(1), "bob");
    var all = DependencyEngine.Evaluate(changed, Chain(original).ToArray());
    return all.Values.All(s => !s.OperationalExportAllowed);
});

Check("independent results stay exportable when another branch is obsolete", () =>
{
    var original = Initial();
    var changed = original.Apply(ConfigurationFacet.ManufacturerBlock, "pi_b-1", t0.AddHours(1), "bob");
    var all = DependencyEngine.Evaluate(changed, Chain(original).ToArray());
    return all[model].OperationalExportAllowed && all[tuning].OperationalExportAllowed && all[simulation].OperationalExportAllowed &&
           !all[conversion].OperationalExportAllowed && !all[report].OperationalExportAllowed;
});

Check("history is kept: the old result is not modified, a recalculation is a NEW result, reverting restores currency", () =>
{
    var original = Initial();
    var oldNodes = Chain(original);
    var changed = original.Apply(ConfigurationFacet.IdentificationWindow, "win-2", t0.AddHours(1), "bob");
    var newModel = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    var recomputed = oldNodes.Append(new ResultNode(newModel, ResultKind.Model, t0.AddHours(2), Snapshot(changed, ResultKind.Model), Array.Empty<Guid>(), true)).ToArray();
    var all = DependencyEngine.Evaluate(changed, recomputed);
    var reverted = changed.Apply(ConfigurationFacet.IdentificationWindow, "win-1", t0.AddHours(3), "bob", "retour");
    var afterRevert = DependencyEngine.Evaluate(reverted, recomputed);
    return all[model].Status == ResultStatus.Obsolete && all[newModel].Status == ResultStatus.ValidatedForDomain && all.Count == recomputed.Length &&
           changed.History.Count == original.History.Count + 1 && reverted.History.Count == changed.History.Count + 1 &&
           afterRevert[model].Status == ResultStatus.ValidatedForDomain && afterRevert[newModel].Status == ResultStatus.Obsolete;
});

Check("re-applying an identical value changes nothing and adds no history", () =>
{
    var original = Initial();
    var again = original.Apply(ConfigurationFacet.Dataset, "data-1", t0.AddHours(5), "carol");
    return ReferenceEquals(original, again) && again.History.Count == original.History.Count;
});

Check("a result recorded before dependencies existed is 'to verify', never obsolete, keeps its own gate and is flagged", () =>
{
    var legacy = new ResultNode(model, ResultKind.Model, t0, null, Array.Empty<Guid>(), true);
    var status = DependencyEngine.Evaluate(Initial().Apply(ConfigurationFacet.Dataset, "data-9", t0.AddHours(1), "bob"), new[] { legacy })[model];
    return status.Status == ResultStatus.ToVerify && status.LegacyWithoutDependencyRecord && status.OperationalExportAllowed && status.Reasons.Count == 0;
});

Check("a result that did not pass its own gate is never exportable, whatever the configuration", () =>
{
    var all = DependencyEngine.Evaluate(Initial(), Chain(Initial(), validated: false).ToArray());
    return all.Values.All(s => s.Status == ResultStatus.ToVerify && !s.OperationalExportAllowed);
});

Check("a blocked result is Blocked and not exportable", () =>
{
    var blocked = new ResultNode(model, ResultKind.Model, t0, Snapshot(Initial(), ResultKind.Model), Array.Empty<Guid>(), true, Blocked: true);
    var status = DependencyEngine.Evaluate(Initial(), new[] { blocked })[model];
    return status.Status == ResultStatus.Blocked && !status.OperationalExportAllowed;
});

Check("a missing upstream result makes the result obsolete (validity cannot be proven)", () =>
{
    var orphan = new ResultNode(tuning, ResultKind.Tuning, t0, Snapshot(Initial(), ResultKind.Tuning), new[] { model }, true);
    var status = DependencyEngine.Evaluate(Initial(), new[] { orphan })[tuning];
    return status.Status == ResultStatus.Obsolete && status.Reasons.Single().Cause == ObsolescenceCause.UpstreamMissing && !status.OperationalExportAllowed;
});

Check("the outcome never depends on the order of the results (deterministic)", () =>
{
    var original = Initial();
    var changed = original.Apply(ConfigurationFacet.ControlStrategy, "x", t0.AddHours(1), "bob");
    var nodes = Chain(original);
    var a = DependencyEngine.Evaluate(changed, nodes.ToArray());
    var b = DependencyEngine.Evaluate(changed, nodes.Reverse().ToArray());
    return a.Keys.All(k => a[k].Status == b[k].Status && a[k].Reasons.Count == b[k].Reasons.Count && a[k].OperationalExportAllowed == b[k].OperationalExportAllowed);
});

Check("a dependency cycle is an error, not an endless loop", () =>
{
    var a = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    var b = Guid.Parse("00000000-0000-0000-0000-0000000000b2");
    try
    {
        DependencyEngine.Evaluate(Initial(), new[]
        {
            new ResultNode(a, ResultKind.Tuning, t0, Snapshot(Initial(), ResultKind.Tuning), new[] { b }, true),
            new ResultNode(b, ResultKind.Tuning, t0, Snapshot(Initial(), ResultKind.Tuning), new[] { a }, true),
        });
        return false;
    }
    catch (InvalidDataException) { return true; }
});

Check("two results with the same identifier are refused", () =>
{
    try { DependencyEngine.Evaluate(Initial(), new[] { new ResultNode(model, ResultKind.Model, t0, null, Array.Empty<Guid>(), true), new ResultNode(model, ResultKind.Model, t0, null, Array.Empty<Guid>(), true) }); return false; }
    catch (InvalidDataException) { return true; }
});

Check("a facet the rule table does not list for a kind never invalidates it (the report depends on upstream results only)", () =>
{
    var original = Initial();
    var lone = new ResultNode(report, ResultKind.Report, t0, Snapshot(original, ResultKind.Report).Concat(new[] { KeyValuePair.Create(ConfigurationFacet.Dataset, "data-1") }).ToDictionary(p => p.Key, p => p.Value),
        Array.Empty<Guid>(), true);
    var changed = original.Apply(ConfigurationFacet.Dataset, "data-2", t0.AddHours(1), "bob");
    return DependencyEngine.Evaluate(changed, new[] { lone })[report].Status == ResultStatus.ValidatedForDomain;
});

Check("the rule table has a version and covers every kind", () =>
    DependencyEngine.RulesVersion == "1.0.0" && Enum.GetValues<ResultKind>().All(kind => DependencyRules.FacetsOf(kind) is not null));

Console.WriteLine(failures == 0 ? "Orchestration (dépendances) : tous les contrôles passent." : $"{failures} contrôle(s) en échec.");
return failures == 0 ? 0 : 1;
