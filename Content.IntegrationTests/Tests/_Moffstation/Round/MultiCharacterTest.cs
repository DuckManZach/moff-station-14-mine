#nullable enable
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Moffstation.CharacterSelection;
using Content.Server._Moffstation.Preferences;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Maps;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Moffstation.Round;

/// <summary>
/// Job selection is per character, job priority is per player, and only active characters spawn.
/// </summary>
[TestFixture]
[TestOf(typeof(MoffCharacterRosterSystem))]
public sealed class MultiCharacterTest : GameTest
{
    [SidedDependency(Side.Server)] private readonly IConsoleHost _conHost = default!;
    [SidedDependency(Side.Server)] private readonly IServerPreferencesManager _prefs = default!;
    [SidedDependency(Side.Server)] private readonly MoffCharacterSelectionManager _selection = default!;

    [SidedDependency(Side.Server)] private readonly AntagSelectionSystem _antag = default!;
    [SidedDependency(Side.Server)] private readonly GameTicker _ticker = default!;
    [SidedDependency(Side.Server)] private readonly GhostSystem _ghost = default!;
    [SidedDependency(Side.Server)] private readonly MindSystem _mind = default!;
    [SidedDependency(Side.Server)] private readonly MoffCharacterRosterSystem _roster = default!;
    [SidedDependency(Side.Server)] private readonly SharedJobSystem _jobs = default!;
    [SidedDependency(Side.Server)] private readonly StationJobsSystem _stationJobs = default!;
    [SidedDependency(Side.Server)] private readonly StationSystem _stations = default!;

    private static readonly ProtoId<JobPrototype> Passenger = "Passenger";
    private static readonly ProtoId<JobPrototype> Engineer = "StationEngineer";
    private static readonly ProtoId<JobPrototype> Captain = "Captain";
    private static readonly ProtoId<AntagPrototype> Traitor = "Traitor";
    private static readonly ProtoId<AntagPrototype> Thief = "Thief";
    private static readonly ProtoId<AntagPrototype> Nukeops = "Nukeops";

    private const string AgeGated = "MoffMultiCharAgeGated";
    private const string OffStation = "MoffMultiCharOffStation";
    private const string TimeGated = "MoffMultiCharTimeGated";
    private const string DeptOffered = "MoffMultiCharDeptOffered";
    private const string DeptEnabled = "MoffMultiCharDeptEnabled";

    private const string TraitorAntag = "MoffMultiCharTraitorAntag";
    private const string TraitorRule = "MoffMultiCharTraitorRule";
    private const string ThiefAntag = "MoffMultiCharThiefAntag";
    private const string ThiefRule = "MoffMultiCharThiefRule";
    private const string SelfSpawnAntag = "MoffMultiCharSelfSpawnAntag";
    private const string SelfSpawnRule = "MoffMultiCharSelfSpawnRule";

    private const string Map = "MoffMultiCharMap";

    /// Offers only a job nobody can hold, so a fallback that ignores preferences has nothing to give.
    private const string TimeGatedMap = "MoffMultiCharTimeGatedMap";

    /// Offers only a job of a test department. Command is unusable here: it is not a primary
    /// department, so TryGetPrimaryDepartment finds nothing for its jobs.
    private const string DeptMap = "MoffMultiCharDeptMap";

    /// A real map, because AntagRandomSpawn needs a tile with breathable atmosphere.
    private const string AtmosMap = "MoffMultiCharAtmosMap";

    private const string SlotZeroName = "Slot Zero Guy";
    private const string SlotOneName = "Slot One Guy";

    private NetUserId User => Client.User!.Value;

    [TestPrototypes]
    private static readonly string Prototypes =
        JobProto(AgeGated, "  requirements:\n  - !type:AgeRequirement\n    requiredAge: 30") +
        JobProto(OffStation) +
        JobProto(TimeGated, "  requirements:\n  - !type:OverallPlaytimeRequirement\n    time: 36000") +
        JobProto(DeptOffered) +
        JobProto(DeptEnabled) + @$"
- type: department
  id: MoffMultiCharTestDepartment
  name: department-Cargo
  description: department-Cargo-description
  color: ""#ffffff""
  roles:
  - {DeptOffered}
  - {DeptEnabled}
" +
        AntagRuleProto(TraitorRule, TraitorAntag, Traitor) +
        AntagRuleProto(ThiefRule, ThiefAntag, Thief, antagExtra: "  multiAntagSetting: All\n") +
        // Spawns its own body before jobs are assigned, so its antags never reach AssignJobs at all.
        AntagRuleProto(SelfSpawnRule,
            SelfSpawnAntag,
            Nukeops,
            ruleExtra: "  - type: AntagRandomSpawn\n  - type: AntagLoadProfileRule\n    preserveName: true\n") +
        MapProto(AtmosMap, $"            {Passenger}: [ -1, -1 ]", "/Maps/Test/dev_map.yml", "Dev") +
        MapProto(TimeGatedMap, $"            {TimeGated}: [ 1, 1 ]") +
        MapProto(DeptMap, $"            {DeptOffered}: [ 1, 1 ]") +
        MapProto(Map,
            $"            {Passenger}: [ -1, -1 ]\n" +
            $"            {Engineer}: [ 5, 5 ]\n" +
            $"            {Captain}: [ 1, 1 ]\n" +
            $"            {AgeGated}: [ 5, 5 ]");

    public override PoolSettings PoolSettings => new()
    {
        DummyTicker = false,
        Connected = true,
        InLobby = true,
    };

    /// Each job needs its own playtime tracker; one tracker may not serve two jobs.
    private static string JobProto(string id, string requirements = "") => @$"
- type: playTimeTracker
  id: PlayTime{id}

- type: job
  id: {id}
  name: job-name-passenger
  description: job-description-passenger
  playTimeTracker: PlayTime{id}
  startingGear: PassengerGear
  icon: ""JobIconPassenger""
  supervisors: job-supervisors-everyone
{requirements}
";

    /// <paramref name="station"/> has to match the station baked into the map file.
    private static string MapProto(
        string id,
        string jobs,
        string path = "/Maps/Test/empty.yml",
        string station = "Empty") => @$"
- type: gameMap
  id: {id}
  mapName: {id}
  mapPath: {path}
  minPlayers: 0
  stations:
    {station}:
      stationProto: StandardNanotrasenStation
      components:
        - type: StationNameSetup
          mapNameTemplate: ""{station}""
        - type: StationJobs
          availableJobs:
{jobs}
";

    private static string AntagRuleProto(
        string ruleId,
        string antagId,
        string prefRole,
        string antagExtra = "",
        string ruleExtra = "") => @$"
- type: antagSpecifier
  id: {antagId}
{antagExtra}  prefRoles:
  - {prefRole}

- type: entity
  abstract: false
  parent: BaseUnweightedAntagRule
  id: {ruleId}
  components:
{ruleExtra}  - type: AntagSelection
    selectionTime: PrePlayerSpawn
    antags:
    - !type:FixedAntagCount
      proto: {antagId}
      count: 1
";

    public override async Task DoSetup()
    {
        await base.DoSetup();

        await Server.WaitPost(() =>
        {
            if (_selection.TryGetState(User, out var state) && state is { } selection)
            {
                selection.EnabledSlots.Clear();
                selection.JobPriorities.Clear();
            }
        });
    }

    /// Automatic preference resetting only covers slot 0, so slot 1 would leak between tests.
    public override async Task DoTeardown()
    {
        await Server.WaitPost(() => _prefs.SetProfile(User, 1, new HumanoidCharacterProfile()).Wait());

        // Deactivating is what isolates: an inactive slot contributes no candidates.
        await Server.WaitPost(() =>
        {
            if (_selection.TryGetState(User, out var state) && state is { } selection)
            {
                selection.EnabledSlots.Clear();
                selection.EnabledSlots[1] = false;
                selection.JobPriorities.Clear();
            }
        });

        // SetProfile also pins the selected slot, so writing slot 1 above left the selection on it.
        // Put it back, or the next test to take this pooled pair inherits a selected index of 1.
        await Server.WaitPost(() =>
        {
            if (_prefs.GetPreferences(User).Characters.TryGetValue(0, out var slotZero))
                _prefs.SetProfile(User, 0, slotZero).Wait();
        });

        await base.DoTeardown();
    }

    private static HumanoidCharacterProfile Character(
        string name,
        ProtoId<JobPrototype> job,
        ProtoId<AntagPrototype>[]? antags = null)
    {
        return new HumanoidCharacterProfile()
            .WithName(name)
            .WithJobPriorities(new Dictionary<ProtoId<JobPrototype>, JobPriority> { [job] = JobPriority.Medium })
            .WithAntagPreferences(antags ?? []);
    }

    private async Task SetProfiles(params HumanoidCharacterProfile[] profiles)
    {
        for (var slot = 0; slot < profiles.Length; slot++)
        {
            var (index, profile) = (slot, profiles[slot]);
            await Server.WaitPost(() => _prefs.SetProfile(User, index, profile).Wait());
        }
    }

    private Task SetupTwoCharacters(ProtoId<JobPrototype> zero, ProtoId<JobPrototype> one)
    {
        return SetupTwoCharactersWithAntags(zero, [], one, []);
    }

    private Task SetupTwoCharactersWithAntags(
        ProtoId<JobPrototype> zeroJob,
        ProtoId<AntagPrototype>[] zeroAntags,
        ProtoId<JobPrototype> oneJob,
        ProtoId<AntagPrototype>[] oneAntags)
    {
        return SetProfiles(Character(SlotZeroName, zeroJob, zeroAntags), Character(SlotOneName, oneJob, oneAntags));
    }

    private async Task SetGlobalPriorities(params (ProtoId<JobPrototype> Job, JobPriority Priority)[] priorities)
    {
        var dict = new Dictionary<ProtoId<JobPrototype>, JobPriority>();

        foreach (var (job, priority) in priorities)
        {
            dict[job] = priority;
        }

        await Server.WaitPost(() => _selection.SetJobPriorities(User, dict).Wait());
    }

    private async Task SetSlotEnabled(int slot, bool enabled)
    {
        await Server.WaitPost(() =>
        {
            Assert.That(_selection.TryGetState(User, out var state), Is.True, "Selection state was not loaded.");
            state!.Value.EnabledSlots[slot] = enabled;
        });
    }

    /// SetProfile moves the selection to whichever slot it wrote, so rewriting a slot pins it.
    private async Task SelectSlot(int slot)
    {
        var profile = _prefs.GetPreferences(User).Characters[slot];
        await Server.WaitPost(() => _prefs.SetProfile(User, slot, profile).Wait());

        Assert.That(_prefs.GetPreferences(User).SelectedCharacterIndex, Is.EqualTo(slot), "Failed to pin the slot.");
    }

    private async Task StartRound(string map = Map)
    {
        Server.CfgMan.SetCVar(CCVars.GameMap, map);
        _ticker.ToggleReadyAll(true);
        await Server.WaitPost(() => _ticker.StartRound());
        await Pair.RunTicksSync(10);
    }

    /// Without the tick sync the next test takes a pooled pair mid-restart whose player still holds the
    /// last round's antag role, blocking the next pre-selection.
    private async Task EndRound()
    {
        await Server.WaitPost(() => _ticker.RestartRound());
        await Pair.RunTicksSync(10);
    }

    private (string? Name, ProtoId<JobPrototype>? Job) Spawned()
    {
        if (ServerSession?.AttachedEntity is not { } uid || !SEntMan.EntityExists(uid))
            return (null, null);

        var mind = _mind.GetMind(uid);
        var job = _jobs.MindTryGetJobId(mind, out var id) ? id : null;

        return (SEntMan.GetComponent<MetaDataComponent>(uid).EntityName, job);
    }

    private void AssertSpawned(ProtoId<JobPrototype> job, string name)
    {
        var (actualName, actualJob) = Spawned();

        Assert.Multiple(() =>
        {
            Assert.That(_ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            Assert.That(_ticker.PlayerGameStatuses[User], Is.EqualTo(PlayerGameStatus.JoinedGame));
            Assert.That(actualJob, Is.EqualTo((ProtoId<JobPrototype>?) job), "The wrong job was assigned.");
            Assert.That(actualName, Is.EqualTo(name), "The wrong character was spawned.");
        });
    }

    /// Adds (but does not start) a rule; PrePlayerSpawn pre-selection only needs it added.
    private async Task<EntityUid> AddAntagRule(string ruleId)
    {
        var rule = EntityUid.Invalid;
        await Server.WaitPost(() => rule = _ticker.AddGameRule(ruleId));
        return rule;
    }

    private int AssignedAntagCount(EntityUid rule, string antagProto)
    {
        return _antag.GetAssignedAntagCount((rule, SEntMan.GetComponent<AntagSelectionComponent>(rule)), antagProto);
    }

    /// Deliberately not IsAssignedAntag, which also answers true for a leftover antag mind role and
    /// would hide a rule that never pre-selected.
    private bool StillPreSelected(EntityUid rule)
    {
        foreach (var (_, sessions) in SEntMan.GetComponent<AntagSelectionComponent>(rule).PreSelectedSessions)
        {
            if (sessions.Contains(ServerSession!))
                return true;
        }

        return false;
    }

    /// Assigns jobs against a throwaway station without running a round -- the spawn-side character
    /// pick has guards of its own that would hide what the assignment decided. The station is deleted
    /// again; leaving it behind changes where later tests spawn.
    private async Task<ProtoId<JobPrototype>?> AssignJobOnTestStation(string gameMap, MinimumJobFallback fallback)
    {
        var proto = SProtoMan.Index<GameMapPrototype>(gameMap);
        var original = Server.CfgMan.GetCVar(CCVars.GameMinimumJobFallback);
        Server.CfgMan.SetCVar(CCVars.GameMinimumJobFallback, fallback);

        ProtoId<JobPrototype>? assigned = null;

        try
        {
            await Server.WaitPost(() =>
            {
                var station = _stations.InitializeNewStation(proto.Stations["Empty"], null, "Empty", proto);

                try
                {
                    var profiles = new Dictionary<NetUserId, HumanoidCharacterProfile>
                    {
                        [User] = _prefs.GetPreferences(User).SelectedCharacter,
                    };

                    assigned = _stationJobs.AssignJobs(profiles, [station]).GetValueOrDefault(User).Item1;
                }
                finally
                {
                    SEntMan.DeleteEntity(station);
                }
            });
        }
        finally
        {
            Server.CfgMan.SetCVar(CCVars.GameMinimumJobFallback, original);
        }

        return assigned;
    }

    [Test]
    [Description("A job on a non-selected character makes the player eligible, and that character spawns.")]
    public async Task SpawnsCharacterMatchingAssignedJob()
    {
        await SetupTwoCharacters(Passenger, Engineer);
        await SetGlobalPriorities((Engineer, JobPriority.High));
        await StartRound();

        AssertSpawned(Engineer, SlotOneName);
        await EndRound();
    }

    [Test]
    [Description("An inactive character is skipped even when it is selected and job-eligible.")]
    public async Task InactiveCharacterIsNotSpawned()
    {
        await SetupTwoCharacters(Engineer, Engineer);
        await SetGlobalPriorities((Engineer, JobPriority.High));
        await SetSlotEnabled(0, false);
        await StartRound();

        AssertSpawned(Engineer, SlotOneName);
        await EndRound();
    }

    [Test]
    [Description("An inactive character contributes no candidate jobs, even when it is the selected one.")]
    public async Task InactiveSelectedCharacterContributesNoJobs()
    {
        await SetupTwoCharacters(Captain, Passenger);
        await SetGlobalPriorities((Captain, JobPriority.High), (Passenger, JobPriority.Medium));
        await SetSlotEnabled(0, false);
        await StartRound();

        AssertSpawned(Passenger, SlotOneName);
        await EndRound();
    }

    [Test]
    [Description("Jobs contributed by another character are still subject to role timers.")]
    public async Task RoleTimersApplyToOtherCharactersJobs()
    {
        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, true);
        await SetupTwoCharacters(Passenger, Captain);
        await SetGlobalPriorities((Captain, JobPriority.High), (Passenger, JobPriority.Medium));
        await StartRound();

        // The test user has no playtime, so Captain is filtered out before jobs are assigned.
        AssertSpawned(Passenger, SlotZeroName);
        await EndRound();
    }

    [Test]
    [Description("High outranks Medium even across different characters.")]
    public async Task GlobalPriorityAppliesAcrossCharacters()
    {
        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);
        await SetupTwoCharacters(Passenger, Captain);
        await SetGlobalPriorities((Passenger, JobPriority.Medium), (Captain, JobPriority.High));
        await StartRound();

        AssertSpawned(Captain, SlotOneName);
        await EndRound();

        // Flip the preference; the other character should win.
        Assert.That(_ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));
        await SetGlobalPriorities((Passenger, JobPriority.High), (Captain, JobPriority.Medium));
        await StartRound();

        AssertSpawned(Passenger, SlotZeroName);
        await EndRound();
    }

    [Test]
    [Description("Picking which character fills a job respects the job's age gate when role timers are off.")]
    public async Task AgeRequirementAppliesWithRoleTimersOff()
    {
        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        // Both characters want the same age gated job; only the older one may hold it.
        await SetProfiles(Character(SlotZeroName, AgeGated).WithAge(20),
            Character(SlotOneName, AgeGated).WithAge(60));

        await SetGlobalPriorities((AgeGated, JobPriority.High));
        await StartRound();

        AssertSpawned(AgeGated, SlotOneName);
        await EndRound();
    }

    // A player opts in to an antag if any active character wants it, so job candidacy has to be
    // narrowed to the characters that can fill it -- otherwise they get a job only an
    // antag-incompatible character wants, and are dropped with neither a job nor an antag.

    [Test]
    [Description("Pre-selecting an antag narrows job candidacy to the characters that can hold it.")]
    public async Task AntagNarrowsJobsToCompatibleCharacters()
    {
        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);
        await SetupTwoCharactersWithAntags(Passenger, [Traitor], Engineer, []);
        await SetGlobalPriorities((Engineer, JobPriority.High), (Passenger, JobPriority.Medium));

        var rule = await AddAntagRule(TraitorRule);
        await StartRound();

        // Engineer is off the table entirely: only the traitor-capable character contributes jobs.
        AssertSpawned(Passenger, SlotZeroName);
        Assert.That(AssignedAntagCount(rule, TraitorAntag), Is.EqualTo(1), "The antag role was not assigned.");

        await EndRound();
    }

    [Test]
    [Description("The narrowing costs nothing when no antag is in play.")]
    public async Task NoAntagMeansNoNarrowing()
    {
        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        // Identical setup to the test above, but no antag rule is running.
        await SetupTwoCharactersWithAntags(Passenger, [Traitor], Engineer, []);
        await SetGlobalPriorities((Engineer, JobPriority.High), (Passenger, JobPriority.Medium));
        await StartRound();

        AssertSpawned(Engineer, SlotOneName);
        await EndRound();
    }

    [Test]
    [Description("A pre-selected antag with no obtainable job takes the overflow job and keeps the antag.")]
    public async Task PreSelectedAntagWithNoJobGetsOverflow()
    {
        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        // No fallback broadening, so the overflow path is the only thing that can keep them out of the
        // lobby. Set directly rather than via OverrideCVar, which does not reliably reach a pooled pair.
        var originalFallback = Server.CfgMan.GetCVar(CCVars.GameMinimumJobFallback);
        Server.CfgMan.SetCVar(CCVars.GameMinimumJobFallback, MinimumJobFallback.None);

        // The traitor-capable character wants only a job this station does not offer, and asked to stay
        // in the lobby rather than take an overflow job. Holding the antag outranks that preference.
        await SetProfiles(
            Character(SlotZeroName, OffStation, [Traitor])
                .WithPreferenceUnavailable(PreferenceUnavailableMode.StayInLobby),
            Character(SlotOneName, Engineer));

        await SetGlobalPriorities((OffStation, JobPriority.High), (Engineer, JobPriority.High));
        var rule = await AddAntagRule(TraitorRule);

        try
        {
            await StartRound();

            Assert.That(StillPreSelected(rule), Is.True, "The antag slot was wiped instead of falling back.");
            // Passenger is the station's overflow job, and slot zero never enabled it.
            AssertSpawned(Passenger, SlotZeroName);
        }
        finally
        {
            Server.CfgMan.SetCVar(CCVars.GameMinimumJobFallback, originalFallback);
        }

        await EndRound();
    }

    [Test]
    [Description("Two stacking antags wanted by different characters are not both pre-selected.")]
    public async Task IncompatibleAntagCombinationIsNotPreSelected()
    {
        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);
        await SetupTwoCharactersWithAntags(Passenger, [Traitor], Engineer, [Thief]);
        await SetGlobalPriorities((Passenger, JobPriority.High), (Engineer, JobPriority.High));

        var first = await AddAntagRule(TraitorRule);
        var second = await AddAntagRule(ThiefRule);
        await StartRound();

        Assert.Multiple(() =>
        {
            Assert.That(AssignedAntagCount(first, TraitorAntag) + AssignedAntagCount(second, ThiefAntag),
                Is.EqualTo(1),
                "Exactly one of the two incompatible antags should have been filled.");

            // Whichever won, the player is in the round holding a job rather than stuck in the lobby.
            Assert.That(_ticker.PlayerGameStatuses[User], Is.EqualTo(PlayerGameStatus.JoinedGame));
        });

        await EndRound();
    }

    [Test]
    [Description("An antag that spawns its own body still uses a character that opted in to it.")]
    public async Task SelfSpawningAntagUsesCharacterThatWantsIt()
    {
        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        // Slot zero wants no antags, so slot one is the only character that may fill the role.
        await SetupTwoCharactersWithAntags(Passenger, [], Passenger, [Nukeops]);
        await SetGlobalPriorities((Passenger, JobPriority.High));

        // The whole point: the selected character is the one that does NOT want the antag.
        await SelectSlot(0);

        var rule = await AddAntagRule(SelfSpawnRule);
        await StartRound(AtmosMap);

        Assert.That(StillPreSelected(rule), Is.True, "The player was not pre-selected for the antag.");

        var (name, job) = Spawned();

        Assert.Multiple(() =>
        {
            // No job proves this took the self-spawn path rather than the normal one, where the job
            // narrowing would have picked slot one for unrelated reasons.
            Assert.That(job, Is.Null, "Expected the antag to spawn its own body instead of taking a job.");
            Assert.That(name, Is.EqualTo(SlotOneName), "The antag spawned as a character that never opted in.");
        });

        await EndRound();
    }

    // StationJobsGetCandidatesEvent is subtractive; a subscriber that replaced the list instead made
    // "is this job allowed" answer "does this player have any playable job at all".

    [Test]
    [Description("The ignoring-preferences job fallback still enforces playtime requirements.")]
    public async Task AnyEligiblePlayerFallbackRespectsPlaytime()
    {
        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, true);

        // Passenger is deliberately a job this station does not offer. It leaves the player with a
        // non-empty roster, which is what made the single-job check answer "yes" for the gated job.
        await SetupTwoCharacters(Passenger, Passenger);
        await SetGlobalPriorities((Passenger, JobPriority.High));

        var assigned = await AssignJobOnTestStation(TimeGatedMap, MinimumJobFallback.AnyEligiblePlayer);

        Assert.That(assigned,
            Is.Not.EqualTo((ProtoId<JobPrototype>?) TimeGated),
            "A fallback handed out a job the player has no playtime for.");
    }

    [Test]
    [Description("The same-department job fallback matches on the player-global priority, not the character's own.")]
    public async Task SameDepartmentFallbackUsesGlobalPriority()
    {
        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        // Both characters have the sibling job enabled -- same department as the only job this station
        // offers -- but the player rates it Never, so it cannot be their bridge into the department.
        await SetupTwoCharacters(DeptEnabled, DeptEnabled);
        await SetGlobalPriorities((Passenger, JobPriority.High));

        var assigned = await AssignJobOnTestStation(DeptMap, MinimumJobFallback.SameDepartment);

        Assert.That(assigned,
            Is.Not.EqualTo((ProtoId<JobPrototype>?) DeptOffered),
            "The department fallback matched on a job the player rated Never.");
    }

    [Test]
    [Description("Ghosting releases the committed character, so the player offers every active character again.")]
    public async Task RejoinAfterGhostingIsNotPinnedToFirstCharacter()
    {
        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        // Only slot one opted in to Thief, and slot zero is the one that will spawn.
        await SetupTwoCharactersWithAntags(Passenger, [], Engineer, [Thief]);
        await SetGlobalPriorities((Passenger, JobPriority.High), (Engineer, JobPriority.Medium));
        await StartRound();

        AssertSpawned(Passenger, SlotZeroName);

        Assert.Multiple(() =>
        {
            Assert.That(_roster.TryGetCommittedCharacter(User)?.Name,
                Is.EqualTo(SlotZeroName),
                "Spawning did not commit the character that spawned.");
            // While embodying slot zero they can only be the antags slot zero wants.
            Assert.That(_roster.GetAntagPreferences(ServerSession!), Does.Not.Contain(Thief));
        });

        await Server.WaitPost(() =>
        {
            var mind = _mind.GetMind(ServerSession!.AttachedEntity!.Value);
            _ghost.OnGhostAttempt(mind!.Value, canReturnGlobal: false, forced: true);
        });
        await Pair.RunTicksSync(5);

        Assert.Multiple(() =>
        {
            Assert.That(_roster.TryGetCommittedCharacter(User),
                Is.Null,
                "Ghosting left the player pinned to the character they had spawned as.");
            Assert.That(_roster.GetAntagPreferences(ServerSession!),
                Does.Contain(Thief),
                "A ghost could not offer the antag preferences of their other characters.");
        });

        await EndRound();
    }

    [Test]
    [Description("A late join can spawn a character whose slot is disabled.")]
    public async Task LateJoinAcceptsDisabledSlot()
    {
        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        // Slot 1 contributes nothing at round start, but late joining as it must still work.
        await SetupTwoCharacters(Passenger, Engineer);
        await SetGlobalPriorities((Passenger, JobPriority.High), (Engineer, JobPriority.Medium));
        await SetSlotEnabled(1, false);

        // Start the round with nobody readied, so the player is left in the lobby to late join from.
        Server.CfgMan.SetCVar(CCVars.GameMap, Map);
        await Server.WaitPost(() => _ticker.ToggleReadyAll(false));
        await Server.WaitPost(() => _ticker.StartRound());
        await Pair.RunTicksSync(10);

        Assert.That(_ticker.PlayerGameStatuses[User], Is.Not.EqualTo(PlayerGameStatus.JoinedGame));

        var station = EntityUid.Invalid;
        await Server.WaitPost(() =>
        {
            var query = SEntMan.EntityQueryEnumerator<StationJobsComponent>();
            if (query.MoveNext(out var uid, out _))
                station = uid;
        });
        Assert.That(station, Is.Not.EqualTo(EntityUid.Invalid), "No station to late join to.");

        var netStation = SEntMan.GetNetEntity(station);
        await Server.WaitPost(() => _conHost.ExecuteCommand(ServerSession, $"joingame 1 {Engineer} {netStation}"));
        await Pair.RunTicksSync(10);

        AssertSpawned(Engineer, SlotOneName);
        await EndRound();
    }

}
