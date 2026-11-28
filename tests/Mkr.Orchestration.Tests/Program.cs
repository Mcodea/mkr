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
    .Apply(ConfigurationFacet.OperatingLimits, "lim-1", t0, "alice").Apply(ConfigurationFacet.AlgorithmVersions, "algos-1", t0, "alice");

IReadOnlyDictionary<ConfigurationFacet, string> Snapshot(ProjectConfiguration cfg, ResultKind kind) =>
    DependencyRules.FacetsOf(kind).Where(cfg.Current.ContainsKey).ToDictionary(f => f, f => cfg.Current[f]);
var model = Guid.Parse("00000000-0000-0000-0000-000000000001");
var tuning = Guid.Parse("00000000-0000-0000-0000-000000000002");
var simulation = Guid.Parse("00000000-0000-0000-0000-000000000003");
var conversion = Guid.Parse("00000000-0000-0000-0000-000000000004");
var report = Guid.Parse("00000000-0000-0000-0000-000000000005");

// A node whose validation proof is genuinely bound to its revision, data and assumptions.
ResultNode Node(Guid id, ResultKind kind, ProjectConfiguration cfg, Guid[] upstream, bool accepted = true, bool withProof = true, string? revision = null)
{
    var rev = revision ?? $"rev-{id:N}";
    var node = new ResultNode(id, kind, t0, Snapshot(cfg, kind), upstream, rev, "data-fp", "assumptions-fp");
    return withProof ? node with { Proof = ValidationProof.Issue(id, rev, "data-fp", "assumptions-fp", "criteria-1.0", accepted, t0) } : node;
}
IReadOnlyList<ResultNode> Chain(ProjectConfiguration cfg, bool accepted = true) => new[]
{
    Node(model, ResultKind.Model, cfg, Array.Empty<Guid>(), accepted),
    Node(tuning, ResultKind.Tuning, cfg, new[] { model }, accepted),
    Node(simulation, ResultKind.Simulation, cfg, new[] { tuning }, accepted),
    Node(conversion, ResultKind.BlockConversion, cfg, new[] { tuning }, accepted),
    Node(report, ResultKind.Report, cfg, new[] { model, tuning, simulation, conversion }, accepted),
};
HashSet<Guid> ObsoleteAfter(ConfigurationFacet facet, string fingerprint)
{
    var original = Initial();
    var nodes = Chain(original);
    var changed = original.Apply(facet, fingerprint, t0.AddHours(1), "bob", "test");
    return DependencyEngine.Evaluate(changed, nodes.ToArray()).Values.Where(s => s.Status == ResultStatus.Obsolete).Select(s => s.Id).ToHashSet();
}
var everything = new[] { model, tuning, simulation, conversion, report };

Check("fingerprints are canonical: order never matters, any change does", () =>
    Fingerprint.Of(("a", "1"), ("b", "2")) == Fingerprint.Of(("b", "2"), ("a", "1")) &&
    Fingerprint.Of(("a", "1"), ("b", "2")) != Fingerprint.Of(("a", "1"), ("b", "3")) &&
    Fingerprint.Of(("a", "1")) != Fingerprint.Of(("a", "1"), ("b", "")) &&
    Fingerprint.Of(("a", "1")).Length == 64);

Check("nothing changed: every validated result is Valid, validated for the domain, and may be exported", () =>
{
    var all = DependencyEngine.Evaluate(Initial(), Chain(Initial()).ToArray());
    return all.Count == 5 && all.Values.All(s => s.Status == ResultStatus.ValidatedForDomain && s.Validity == ValidityClass.Valid && s.ProofProblem == ProofProblem.None &&
                                                 s.OperationalExportAllowed && s.Reasons.Count == 0 && s.Action is null);
});

Check("replacing the active dataset makes EVERYTHING derived from it obsolete (cascade)", () => ObsoleteAfter(ConfigurationFacet.Dataset, "data-2").SetEquals(everything));

Check("a NEW import that the study does not use changes nothing: the dataset SELECTION is unchanged, so no result is obsolete and no history entry is added", () =>
{
    var original = Initial();
    var all = DependencyEngine.Evaluate(original, Chain(original).ToArray());
    var again = original.Apply(ConfigurationFacet.Dataset, "data-1", t0.AddHours(3), "carol", "import of another file, not selected");
    return ReferenceEquals(original, again) && all.Values.All(s => s.Validity == ValidityClass.Valid);
});

Check("changing the window, units, mapping, preprocessing or method invalidates the model and its descendants only", () =>
    new[] { (ConfigurationFacet.IdentificationWindow, "win-2"), (ConfigurationFacet.UnitsAndConversions, "units-2"), (ConfigurationFacet.SignalMapping, "map-2"),
            (ConfigurationFacet.Preprocessing, "pre-2"), (ConfigurationFacet.IdentificationMethod, "sopdt-1") }
        .All(c => ObsoleteAfter(c.Item1, c.Item2).SetEquals(everything)));

Check("a change of algorithm or criteria version that affects results invalidates what was computed with the old one (and what derives from it)", () =>
    ObsoleteAfter(ConfigurationFacet.AlgorithmVersions, "algos-2").SetEquals(everything));

Check("changing the control strategy invalidates the tuning and its descendants but NOT the model (independent step)", () =>
{
    var obsolete = ObsoleteAfter(ConfigurationFacet.ControlStrategy, "pid-strategy");
    return obsolete.SetEquals(new[] { tuning, simulation, conversion, report }) && !obsolete.Contains(model);
});

Check("changing the manufacturer block invalidates only the conversion (and the report): the tuning and the simulation stay valid", () =>
    ObsoleteAfter(ConfigurationFacet.ManufacturerBlock, "pi_b-1").SetEquals(new[] { conversion, report }));

Check("changing the PID parameters invalidates the simulation and the conversion but neither the model nor the tuning", () =>
    ObsoleteAfter(ConfigurationFacet.PidParameters, "pid-2").SetEquals(new[] { simulation, conversion, report }));

Check("changing the operating limits invalidates the tuning and the simulation (and what derives from them), not the model", () =>
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
    return m.Status == ResultStatus.Obsolete && m.Validity == ValidityClass.Obsolete && reason is { Cause: ObsolescenceCause.FacetChanged, Facet: ConfigurationFacet.IdentificationWindow,
               PreviousFingerprint: "win-1", CurrentFingerprint: "win-2", ChangedBy: "bob" } && reason.ChangedAt == t0.AddHours(2) && m.Action == "recompute_identification" &&
           downstream is { Cause: ObsolescenceCause.UpstreamObsolete } && downstream.Upstream == model && all[tuning].Action == "recompute_tuning";
});

Check("an obsolete result may NOT be exported or finally validated, nor anything built on it", () =>
{
    var original = Initial();
    var changed = original.Apply(ConfigurationFacet.IdentificationWindow, "win-2", t0.AddHours(1), "bob");
    return DependencyEngine.Evaluate(changed, Chain(original).ToArray()).Values.All(s => !s.OperationalExportAllowed);
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
    var recomputed = oldNodes.Append(Node(newModel, ResultKind.Model, changed, Array.Empty<Guid>())).ToArray();
    var all = DependencyEngine.Evaluate(changed, recomputed);
    var reverted = changed.Apply(ConfigurationFacet.IdentificationWindow, "win-1", t0.AddHours(3), "bob", "retour");
    var afterRevert = DependencyEngine.Evaluate(reverted, recomputed);
    return all[model].Status == ResultStatus.Obsolete && all[newModel].Validity == ValidityClass.Valid && all.Count == recomputed.Length &&
           changed.History.Count == original.History.Count + 1 && reverted.History.Count == changed.History.Count + 1 &&
           afterRevert[model].Validity == ValidityClass.Valid && afterRevert[newModel].Validity == ValidityClass.Obsolete;
});

Check("re-applying an identical value changes nothing and adds no history", () =>
{
    var original = Initial();
    var again = original.Apply(ConfigurationFacet.Dataset, "data-1", t0.AddHours(5), "carol");
    return ReferenceEquals(original, again) && again.History.Count == original.History.Count;
});

Check("the four classes are told apart: valid, not validated, refused, obsolete", () =>
{
    var cfg = Initial();
    var valid = Node(model, ResultKind.Model, cfg, Array.Empty<Guid>());
    var notValidated = Node(tuning, ResultKind.Tuning, cfg, Array.Empty<Guid>(), withProof: false);
    var refusedNode = Node(simulation, ResultKind.Simulation, cfg, Array.Empty<Guid>(), accepted: false);
    var obsolete = Node(conversion, ResultKind.BlockConversion, cfg, Array.Empty<Guid>());
    var changed = cfg.Apply(ConfigurationFacet.ManufacturerBlock, "other", t0.AddHours(1), "bob");
    var all = DependencyEngine.Evaluate(changed, new[] { valid, notValidated, refusedNode, obsolete });
    return all[model].Validity == ValidityClass.Valid && all[tuning].Validity == ValidityClass.NotValidated && all[simulation].Validity == ValidityClass.Refused &&
           all[conversion].Validity == ValidityClass.Obsolete && all[tuning].ProofProblem == ProofProblem.Missing;
});

Check("a refused or unproven result may never be exported", () =>
{
    var cfg = Initial();
    var refusedNode = Node(model, ResultKind.Model, cfg, Array.Empty<Guid>(), accepted: false);
    var unproven = Node(tuning, ResultKind.Tuning, cfg, Array.Empty<Guid>(), withProof: false);
    var all = DependencyEngine.Evaluate(cfg, new[] { refusedNode, unproven });
    return !all[model].OperationalExportAllowed && !all[tuning].OperationalExportAllowed;
});

Check("the proof is bound to the exact revision: a changed revision, data or assumptions make it DoesNotMatchRevision, not a free pass", () =>
{
    var cfg = Initial();
    var good = Node(model, ResultKind.Model, cfg, Array.Empty<Guid>());
    var revised = good with { RevisionFingerprint = "rev-changed" };
    var otherData = good with { DataFingerprint = "other-data" };
    var otherAssumptions = good with { AssumptionsFingerprint = "other-assumptions" };
    var all = new[] { revised, otherData, otherAssumptions }.Select((n, i) => n with { Id = Guid.Parse($"00000000-0000-0000-0000-0000000001{i:00}"), Proof = good.Proof }).ToArray();
    // a proof for another RESULT is refused too
    var moved = good with { Id = Guid.Parse("00000000-0000-0000-0000-000000000199") };
    var statuses = DependencyEngine.Evaluate(cfg, all.Append(moved).ToArray());
    return statuses.Values.All(s => s.ProofProblem == ProofProblem.DoesNotMatchRevision && s.Validity == ValidityClass.NotValidated && !s.OperationalExportAllowed);
});

Check("a forged or altered proof is not authentic (the seal detects it)", () =>
{
    var cfg = Initial();
    var good = Node(model, ResultKind.Model, cfg, Array.Empty<Guid>());
    var flipped = good with { Proof = good.Proof! with { Accepted = !good.Proof!.Accepted } };
    var edited = good with { Proof = good.Proof! with { CriteriaVersion = "criteria-9.9" } };
    var s = DependencyEngine.Evaluate(cfg, new[] { flipped });
    var e = DependencyEngine.Evaluate(cfg, new[] { edited });
    return s[model].ProofProblem == ProofProblem.NotAuthentic && !s[model].OperationalExportAllowed && e[model].ProofProblem == ProofProblem.NotAuthentic && good.Proof!.IsAuthentic;
});

Check("a legacy result (no dependency record, no proof object) is 'to verify', never obsolete; its stored decision still governs export, and it is flagged", () =>
{
    var cfg = Initial().Apply(ConfigurationFacet.Dataset, "data-9", t0.AddHours(1), "bob");
    var accepted = new ResultNode(model, ResultKind.Model, t0, null, Array.Empty<Guid>(), "r", "d", "a", LegacyAccepted: true);
    var refusedLegacy = new ResultNode(tuning, ResultKind.Tuning, t0, null, Array.Empty<Guid>(), "r", "d", "a", LegacyAccepted: false);
    var s = DependencyEngine.Evaluate(cfg, new[] { accepted, refusedLegacy });
    return s[model] is { Status: ResultStatus.ToVerify, LegacyWithoutDependencyRecord: true, OperationalExportAllowed: true } && s[model].Reasons.Count == 0 &&
           !s[tuning].OperationalExportAllowed;
});

Check("a result that did not pass its own gate is never exportable, whatever the configuration", () =>
{
    var all = DependencyEngine.Evaluate(Initial(), Chain(Initial(), accepted: false).ToArray());
    return all.Values.All(s => s.Validity == ValidityClass.Refused && !s.OperationalExportAllowed);
});

Check("a blocked result is Blocked and not exportable", () =>
{
    var blocked = Node(model, ResultKind.Model, Initial(), Array.Empty<Guid>()) with { Blocked = true };
    var status = DependencyEngine.Evaluate(Initial(), new[] { blocked })[model];
    return status.Status == ResultStatus.Blocked && !status.OperationalExportAllowed;
});

Check("a missing upstream result makes the result obsolete (validity cannot be proven)", () =>
{
    var orphan = Node(tuning, ResultKind.Tuning, Initial(), new[] { model });
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
    return a.Keys.All(k => a[k].Status == b[k].Status && a[k].Validity == b[k].Validity && a[k].Reasons.Count == b[k].Reasons.Count && a[k].OperationalExportAllowed == b[k].OperationalExportAllowed);
});

Check("a dependency cycle is an error, not an endless loop", () =>
{
    var a = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    var b = Guid.Parse("00000000-0000-0000-0000-0000000000b2");
    try
    {
        DependencyEngine.Evaluate(Initial(), new[] { Node(a, ResultKind.Tuning, Initial(), new[] { b }), Node(b, ResultKind.Tuning, Initial(), new[] { a }) });
        return false;
    }
    catch (InvalidDataException) { return true; }
});

Check("two results with the same identifier are refused", () =>
{
    var one = Node(model, ResultKind.Model, Initial(), Array.Empty<Guid>());
    try { DependencyEngine.Evaluate(Initial(), new[] { one, one }); return false; }
    catch (InvalidDataException) { return true; }
});

Check("a facet the rule table does not list for a kind never invalidates it (the report depends on upstream results only)", () =>
{
    var original = Initial();
    var lone = Node(report, ResultKind.Report, original, Array.Empty<Guid>()) with
    {
        FacetSnapshot = Snapshot(original, ResultKind.Report).Concat(new[] { KeyValuePair.Create(ConfigurationFacet.Dataset, "data-1") }).ToDictionary(p => p.Key, p => p.Value)
    };
    var changed = original.Apply(ConfigurationFacet.Dataset, "data-2", t0.AddHours(1), "bob");
    return DependencyEngine.Evaluate(changed, new[] { lone })[report].Status == ResultStatus.ValidatedForDomain;
});

Check("the rule table has a version, covers every kind and every facet is used by at least one kind", () =>
    DependencyEngine.RulesVersion == "1.1.0" && Enum.GetValues<ResultKind>().All(kind => DependencyRules.FacetsOf(kind) is not null) &&
    Enum.GetValues<ConfigurationFacet>().All(f => Enum.GetValues<ResultKind>().Any(k => DependencyRules.FacetsOf(k).Contains(f))));

// ---- Dépendances enregistrées avec le résultat (tranche 2) ----
Check("dependencies: a result records ONLY the facets its kind depends on, survives a JSON round trip, and is invalidated exactly like the in-memory node", () =>
{
    var original = Initial();
    var captured = ResultDependencies.Capture(ResultKind.Model, original, Array.Empty<Guid>(), "rev", "data", "hyp");
    var json = System.Text.Json.JsonSerializer.Serialize(captured);
    var back = System.Text.Json.JsonSerializer.Deserialize<ResultDependencies>(json)!;
    if (back.Facets.Keys.Contains(nameof(ConfigurationFacet.PidParameters)) || !back.Facets.ContainsKey(nameof(ConfigurationFacet.IdentificationWindow))) return false;
    var node = back.ToNode(model, ResultKind.Model, t0);
    var same = DependencyEngine.Evaluate(original, new[] { node })[model];
    var changed = DependencyEngine.Evaluate(original.Apply(ConfigurationFacet.IdentificationWindow, "win-2", t0.AddHours(1), "bob"), new[] { node })[model];
    var unrelated = DependencyEngine.Evaluate(original.Apply(ConfigurationFacet.PidParameters, "pid-9", t0.AddHours(1), "bob"), new[] { node })[model];
    return same.Status != ResultStatus.Obsolete && changed.Status == ResultStatus.Obsolete && unrelated.Status != ResultStatus.Obsolete;
});

// ---- Parcours : un modèle accepté mais obsolète n'ouvre aucune étape aval et dit quoi faire ----
Check("journey: an accepted model that is obsolete blocks the downstream steps with the obsolescence message; a valid one opens them", () =>
{
    var valid = StudyJourney.Evaluate(new ProjectFacts(true, 2, 1, 1, 0));
    var obsolete = StudyJourney.Evaluate(new ProjectFacts(true, 2, 1, 1, 1));
    var mixed = StudyJourney.Evaluate(new ProjectFacts(true, 2, 2, 2, 1));
    return valid[7].State == StepState.Ready && obsolete[4].State == StepState.Ready && obsolete[5].State == StepState.Blocked && obsolete[7].State == StepState.Blocked &&
           obsolete[7].Detail == Loc.T("core.modele_obsolete_recalculez") && mixed[7].State == StepState.Ready && obsolete[7].Detail != Loc.T("core.aucun_modele_accepte_a_la");
});

Check("journey: facts without obsolescence information behave exactly as before (legacy projects)", () =>
    StudyJourney.Evaluate(new ProjectFacts(true, 1, 1, 1)).Select(step => step.State).SequenceEqual(StudyJourney.Evaluate(new ProjectFacts(true, 1, 1, 1, 0)).Select(step => step.State)));

// ---- Advisor : one plain conclusion, why, and what to do (no threshold is ever asked from the user) ----
var none = new RecourseOptions(false, false, false);
var all3 = new RecourseOptions(true, true, true);
ConclusionInput In(DataSufficiency d, ModelVerdict m, TuningVerdict t, RecourseOptions? r = null, NodeStatus? status = null) => new(status, d, m, t, r ?? none);

Check("advisor: a good case gives 'tuning recommended' with the next action", () =>
    Advisor.Conclude(In(DataSufficiency.Sufficient, ModelVerdict.Accepted, TuningVerdict.Recommended)) is { Conclusion: AdvisorConclusion.TuningRecommended, Action: "simulate_then_validate" });

Check("advisor: an unstable tuning is NEVER recommended, whatever the model says, and the recourse order is alternative model, robust tuning, complementary trial, then no recommendation", () =>
    Advisor.Conclude(In(DataSufficiency.Sufficient, ModelVerdict.Accepted, TuningVerdict.NotRecommendedUnstable, all3)) is { Conclusion: AdvisorConclusion.TuningNotRecommended, Action: "try_alternative_model" } &&
    Advisor.Conclude(In(DataSufficiency.Sufficient, ModelVerdict.Accepted, TuningVerdict.NotRecommendedUnstable, new RecourseOptions(false, true, true))) is { Action: "use_more_robust_tuning" } &&
    Advisor.Conclude(In(DataSufficiency.Sufficient, ModelVerdict.Accepted, TuningVerdict.NotRecommendedUnstable, new RecourseOptions(false, false, true))) is { Conclusion: AdvisorConclusion.ComplementaryTestNeeded, Action: "run_complementary_trial" } &&
    Advisor.Conclude(In(DataSufficiency.Sufficient, ModelVerdict.Accepted, TuningVerdict.NotRecommendedUnstable, none)) is { Conclusion: AdvisorConclusion.TuningNotRecommended, Action: "no_recommendation_possible" });

Check("advisor: insufficient robustness behaves like instability (no recommendation, recourse first)", () =>
    Advisor.Conclude(In(DataSufficiency.Sufficient, ModelVerdict.Accepted, TuningVerdict.NotRecommendedInsufficientRobustness, new RecourseOptions(false, true, false))) is
        { Conclusion: AdvisorConclusion.TuningNotRecommended, Explanation: "robustness_insufficient", Action: "use_more_robust_tuning" });

Check("advisor: high uncertainty seeks a way out before a definitive refusal", () =>
    Advisor.Conclude(In(DataSufficiency.Sufficient, ModelVerdict.RefusedUncertain, TuningVerdict.NotComputed, new RecourseOptions(false, false, true))) is
        { Conclusion: AdvisorConclusion.ComplementaryTestNeeded, Action: "run_complementary_trial" });

Check("advisor: insufficient data asks for a complementary trial", () =>
    Advisor.Conclude(In(DataSufficiency.Insufficient, ModelVerdict.NotValidatedYet, TuningVerdict.NotComputed)) is { Conclusion: AdvisorConclusion.ComplementaryTestNeeded, Explanation: "data_insufficient" });

Check("advisor: a model not representative proposes an alternative model first", () =>
    Advisor.Conclude(In(DataSufficiency.Sufficient, ModelVerdict.RefusedNotRepresentative, TuningVerdict.NotComputed, all3)) is { Conclusion: AdvisorConclusion.ModelNotRepresentative, Action: "try_alternative_model" });

Check("advisor: an accepted model without tuning is 'model usable' and says to compute the tuning", () =>
    Advisor.Conclude(In(DataSufficiency.Sufficient, ModelVerdict.Accepted, TuningVerdict.NotComputed)) is { Conclusion: AdvisorConclusion.ModelUsable, Action: "compute_tuning" });

Check("advisor: an OBSOLETE result is shown as 'to recompute' with the facet that changed, even if its content looked good", () =>
{
    var original = Initial();
    var changed = original.Apply(ConfigurationFacet.IdentificationWindow, "win-2", t0.AddHours(1), "bob");
    var status = DependencyEngine.Evaluate(changed, Chain(original).ToArray())[model];
    var r = Advisor.Conclude(In(DataSufficiency.Sufficient, ModelVerdict.Accepted, TuningVerdict.Recommended, all3, status));
    return r is { Conclusion: AdvisorConclusion.ResultObsoleteRecompute, Explanation: "obsolete_facet_changed", Action: "recompute_identification", Facet: ConfigurationFacet.IdentificationWindow };
});

Check("advisor: every combination yields a conclusion WITH an explanation and an action (exhaustive)", () =>
{
    var count = 0;
    foreach (var d in Enum.GetValues<DataSufficiency>())
        foreach (var m in Enum.GetValues<ModelVerdict>())
            foreach (var t in Enum.GetValues<TuningVerdict>())
                foreach (var alt in new[] { false, true })
                    foreach (var rob in new[] { false, true })
                        foreach (var trial in new[] { false, true })
                        {
                            var r = Advisor.Conclude(In(d, m, t, new RecourseOptions(alt, rob, trial)));
                            if (string.IsNullOrWhiteSpace(r.Explanation) || string.IsNullOrWhiteSpace(r.Action)) return false;
                            // never recommend a tuning that is unstable or not robust, or whose model is not accepted
                            if (r.Conclusion == AdvisorConclusion.TuningRecommended && (t != TuningVerdict.Recommended || m != ModelVerdict.Accepted)) return false;
                            count++;
                        }
    return count == 3 * 4 * 5 * 8;
});

Console.WriteLine(failures == 0 ? "Orchestration (dépendances) : tous les contrôles passent." : $"{failures} contrôle(s) en échec.");
return failures == 0 ? 0 : 1;

