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
[TestOf(typeof(MoffCharacterRosterSystem))]
public sealed class MultiCharacterTest : GameTest
{
    [SidedDependency(Side.Server)] private readonly IConsoleHost _sConHost = default!;
    [SidedDependency(Side.Server)] private readonly IServerPreferencesManager _sPrefs = default!;
    [SidedDependency(Side.Server)] private readonly MoffCharacterSelectionManager _sSelection = default!;

    [SidedDependency(Side.Server)] private readonly AntagSelectionSystem _sAntag = default!;
    [SidedDependency(Side.Server)] private readonly GameTicker _sTicker = default!;
    [SidedDependency(Side.Server)] private readonly GhostSystem _sGhost = default!;
    [SidedDependency(Side.Server)] private readonly MindSystem _sMind = default!;
    [SidedDependency(Side.Server)] private readonly MoffCharacterRosterSystem _sRoster = default!;
    [SidedDependency(Side.Server)] private readonly SharedJobSystem _sJobs = default!;
    [SidedDependency(Side.Server)] private readonly StationJobsSystem _sStationJobs = default!;
    [SidedDependency(Side.Server)] private readonly StationSystem _sStations = default!;

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
            if (_sSelection.TryGetState(User, out var state) && state is { } selection)
            {
                selection.EnabledSlots.Clear();
                selection.JobPriorities.Clear();
            }
        });
    }

    /// Automatic preference resetting only covers slot 0, so slot 1 would leak between tests.
    public override async Task DoTeardown()
    {
        await Server.WaitPost(() => _sPrefs.SetProfile(User, 1, new HumanoidCharacterProfile()).Wait());

        // An inactive slot contributes no candidates.
        await Server.WaitPost(() =>
        {
            if (_sSelection.TryGetState(User, out var state) && state is { } selection)
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
            if (_sPrefs.GetPreferences(User).Characters.TryGetValue(0, out var slotZero))
                _sPrefs.SetProfile(User, 0, slotZero).Wait();
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
            await Server.WaitPost(() => _sPrefs.SetProfile(User, index, profile).Wait());
        }
    }

    private async Task SetGlobalPriorities(params (ProtoId<JobPrototype> Job, JobPriority Priority)[] priorities)
    {
        var dict = new Dictionary<ProtoId<JobPrototype>, JobPriority>();

        foreach (var (job, priority) in priorities)
        {
            dict[job] = priority;
        }

        await Server.WaitPost(() => _sSelection.SetJobPriorities(User, dict).Wait());
    }

    private async Task SetSlotEnabled(int slot, bool enabled)
    {
        await Server.WaitAssertion(() =>
        {
            Assume.That(_sSelection.TryGetState(User, out var state), Is.True, "Selection state was not loaded.");
            state!.Value.EnabledSlots[slot] = enabled;
        });
    }

    /// SetProfile moves the selection to whichever slot it wrote, so rewriting a slot pins it.
    private async Task SelectSlot(int slot)
    {
        var profile = _sPrefs.GetPreferences(User).Characters[slot];
        await Server.WaitPost(() => _sPrefs.SetProfile(User, slot, profile).Wait());

        Assume.That(_sPrefs.GetPreferences(User).SelectedCharacterIndex, Is.EqualTo(slot), "Failed to pin the slot.");
    }

    private async Task StartRound(string map = Map)
    {
        await OverrideCVar(Side.Server, CCVars.GameMap, map);
        _sTicker.ToggleReadyAll(true);
        await Server.WaitPost(() => _sTicker.StartRound());
        await Pair.RunTicksSync(10);
    }

    /// Without the tick sync the next test takes a pooled pair mid-restart whose player still holds the
    /// last round's antag role, blocking the next pre-selection.
    private async Task EndRound()
    {
        await Server.WaitPost(() => _sTicker.RestartRound());
        await Pair.RunTicksSync(10);
    }

    private (string? Name, ProtoId<JobPrototype>? Job) Spawned()
    {
        if (ServerSession?.AttachedEntity is not { } uid || !SEntMan.EntityExists(uid))
            return (null, null);

        var mind = _sMind.GetMind(uid);
        var job = _sJobs.MindTryGetJobId(mind, out var id) ? id : null;

        return (SComp<MetaDataComponent>(uid).EntityName, job);
    }

    private void AssertSpawned(ProtoId<JobPrototype> job, string name)
    {
        var (actualName, actualJob) = Spawned();

        Assume.That(_sTicker.RunLevel, Is.EqualTo(GameRunLevel.InRound), "The round never started.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_sTicker.PlayerGameStatuses[User], Is.EqualTo(PlayerGameStatus.JoinedGame));
            Assert.That(actualJob, Is.EqualTo((ProtoId<JobPrototype>?) job), "The wrong job was assigned.");
            Assert.That(actualName, Is.EqualTo(name), "The wrong character was spawned.");
        }
    }

    /// Adds (but does not start) a rule; PrePlayerSpawn pre-selection only needs it added.
    private async Task<EntityUid> AddAntagRule(string ruleId)
    {
        var rule = EntityUid.Invalid;
        await Server.WaitPost(() => rule = _sTicker.AddGameRule(ruleId));
        return rule;
    }

    private int AssignedAntagCount(EntityUid rule, string antagProto)
    {
        return _sAntag.GetAssignedAntagCount(SEntity<AntagSelectionComponent>(rule), antagProto);
    }

    /// IsAssignedAntag also answers true for a leftover antag mind role, hiding a rule that never pre-selected.
    private bool StillPreSelected(EntityUid rule)
    {
        foreach (var (_, sessions) in SComp<AntagSelectionComponent>(rule).PreSelectedSessions)
        {
            if (sessions.Contains(ServerSession!))
                return true;
        }

        return false;
    }

    /// Assigns jobs against a throwaway station without running a round. the spawn-side character
    /// pick has guards of its own that would hide what the assignment decided. The station is deleted
    /// again; leaving it behind changes where later tests spawn.
    private async Task<ProtoId<JobPrototype>?> AssignJobOnTestStation(string gameMap, MinimumJobFallback fallback)
    {
        var proto = SProtoMan.Index<GameMapPrototype>(gameMap);

        // The fallback is a parameter, so this cannot be the EnsureCVar attribute.
        await OverrideCVar(Side.Server, CCVars.GameMinimumJobFallback, fallback);

        ProtoId<JobPrototype>? assigned = null;

        await Server.WaitPost(() =>
        {
            var station = _sStations.InitializeNewStation(proto.Stations["Empty"], null, "Empty", proto);

            try
            {
                var profiles = new Dictionary<NetUserId, HumanoidCharacterProfile>
                {
                    [User] = _sPrefs.GetPreferences(User).SelectedCharacter,
                };

                assigned = _sStationJobs.AssignJobs(profiles, [station]).GetValueOrDefault(User).Item1;
            }
            finally
            {
                SDeleteNow(station);
            }
        });

        return assigned;
    }

    [Test]
    [Description("A job on a non-selected character makes the player eligible, and that character spawns.")]
    public async Task SpawnsCharacterMatchingAssignedJob()
    {
        await SetProfiles(Character(SlotZeroName, Passenger), Character(SlotOneName, Engineer));
        await SetGlobalPriorities((Engineer, JobPriority.High));
        await StartRound();

        AssertSpawned(Engineer, SlotOneName);
        await EndRound();
    }

    [Test]
    [Description("An inactive character is skipped even when it is selected and job-eligible.")]
    public async Task InactiveCharacterIsNotSpawned()
    {
        await SetProfiles(Character(SlotZeroName, Engineer), Character(SlotOneName, Engineer));
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
        await SetProfiles(Character(SlotZeroName, Captain), Character(SlotOneName, Passenger));
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
        await SetProfiles(Character(SlotZeroName, Passenger), Character(SlotOneName, Captain));
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
        await SetProfiles(Character(SlotZeroName, Passenger), Character(SlotOneName, Captain));
        await SetGlobalPriorities((Passenger, JobPriority.Medium), (Captain, JobPriority.High));
        await StartRound();

        AssertSpawned(Captain, SlotOneName);
        await EndRound();

        Assume.That(_sTicker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby), "The round did not end.");
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
        await SetProfiles(Character(SlotZeroName, Passenger, [Traitor]), Character(SlotOneName, Engineer));
        await SetGlobalPriorities((Engineer, JobPriority.High), (Passenger, JobPriority.Medium));

        var rule = await AddAntagRule(TraitorRule);
        await StartRound();

        // Engineer is off the table: only the traitor-capable character contributes jobs.
        AssertSpawned(Passenger, SlotZeroName);
        Assert.That(AssignedAntagCount(rule, TraitorAntag), Is.EqualTo(1), "The antag role was not assigned.");

        await EndRound();
    }

    [Test]
    [Description("The narrowing costs nothing when no antag is in play.")]
    public async Task NoAntagMeansNoNarrowing()
    {
        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        await SetProfiles(Character(SlotZeroName, Passenger, [Traitor]), Character(SlotOneName, Engineer));
        await SetGlobalPriorities((Engineer, JobPriority.High), (Passenger, JobPriority.Medium));
        await StartRound();

        AssertSpawned(Engineer, SlotOneName);
        await EndRound();
    }

    [Test]
    [Description("A pre-selected antag with no obtainable job takes the overflow job and keeps the antag.")]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GameMinimumJobFallback), MinimumJobFallback.None)]
    public async Task PreSelectedAntagWithNoJobGetsOverflow()
    {
        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        // The traitor-capable character wants only a job this station does not offer, and asked to stay
        // in the lobby rather than take an overflow job. Holding the antag outranks that preference.
        await SetProfiles(
            Character(SlotZeroName, OffStation, [Traitor])
                .WithPreferenceUnavailable(PreferenceUnavailableMode.StayInLobby),
            Character(SlotOneName, Engineer));

        await SetGlobalPriorities((OffStation, JobPriority.High), (Engineer, JobPriority.High));
        var rule = await AddAntagRule(TraitorRule);
        await StartRound();

        Assert.That(StillPreSelected(rule), Is.True, "The antag slot was wiped instead of falling back.");
        // Passenger is the station's overflow job, and slot zero never enabled it.
        AssertSpawned(Passenger, SlotZeroName);

        await EndRound();
    }

    [Test]
    [Description("Two stacking antags wanted by different characters are not both pre-selected.")]
    public async Task IncompatibleAntagCombinationIsNotPreSelected()
    {
        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);
        await SetProfiles(Character(SlotZeroName, Passenger, [Traitor]), Character(SlotOneName, Engineer, [Thief]));
        await SetGlobalPriorities((Passenger, JobPriority.High), (Engineer, JobPriority.High));

        var first = await AddAntagRule(TraitorRule);
        var second = await AddAntagRule(ThiefRule);
        await StartRound();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(AssignedAntagCount(first, TraitorAntag) + AssignedAntagCount(second, ThiefAntag),
                Is.EqualTo(1),
                "Exactly one of the two incompatible antags should have been filled.");

            Assert.That(_sTicker.PlayerGameStatuses[User], Is.EqualTo(PlayerGameStatus.JoinedGame));
        }

        await EndRound();
    }

    [Test]
    [Description("An antag that spawns its own body still uses a character that opted in to it.")]
    public async Task SelfSpawningAntagUsesCharacterThatWantsIt()
    {
        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        await SetProfiles(Character(SlotZeroName, Passenger), Character(SlotOneName, Passenger, [Nukeops]));
        await SetGlobalPriorities((Passenger, JobPriority.High));

        await SelectSlot(0);

        var rule = await AddAntagRule(SelfSpawnRule);
        await StartRound(AtmosMap);

        Assert.That(StillPreSelected(rule), Is.True, "The player was not pre-selected for the antag.");

        var (name, job) = Spawned();

        using (Assert.EnterMultipleScope())
        {
            // No job means this took the self-spawn path; the normal one would have picked slot one anyway.
            Assert.That(job, Is.Null, "Expected the antag to spawn its own body instead of taking a job.");
            Assert.That(name, Is.EqualTo(SlotOneName), "The antag spawned as a character that never opted in.");
        }

        await EndRound();
    }

    [Test]
    [Description("The ignoring-preferences job fallback still enforces playtime requirements.")]
    [TestOf(typeof(StationJobsSystem))]
    public async Task AnyEligiblePlayerFallbackRespectsPlaytime()
    {
        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, true);

        // Passenger is not offered here, so the player keeps a non-empty roster.
        await SetProfiles(Character(SlotZeroName, Passenger), Character(SlotOneName, Passenger));
        await SetGlobalPriorities((Passenger, JobPriority.High));

        var assigned = await AssignJobOnTestStation(TimeGatedMap, MinimumJobFallback.AnyEligiblePlayer);

        Assert.That(assigned, Is.Null, $"A fallback handed out {assigned}, which the player has no playtime for.");
    }

    [Test]
    [Description("The same-department job fallback matches on the player-global priority, not the character's own.")]
    [TestOf(typeof(StationJobsSystem))]
    public async Task SameDepartmentFallbackUsesGlobalPriority()
    {
        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        await SetProfiles(Character(SlotZeroName, DeptEnabled), Character(SlotOneName, DeptEnabled));
        await SetGlobalPriorities((Passenger, JobPriority.High));

        var assigned = await AssignJobOnTestStation(DeptMap, MinimumJobFallback.SameDepartment);

        Assert.That(assigned, Is.Null, $"The department fallback handed out {assigned}, which the player rated Never.");
    }

    [Test]
    [Description("Ghosting releases the committed character, so the player offers every active character again.")]
    public async Task RejoinAfterGhostingIsNotPinnedToFirstCharacter()
    {
        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        await SetProfiles(Character(SlotZeroName, Passenger), Character(SlotOneName, Engineer, [Thief]));
        await SetGlobalPriorities((Passenger, JobPriority.High), (Engineer, JobPriority.Medium));
        await StartRound();

        AssertSpawned(Passenger, SlotZeroName);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_sRoster.TryGetCommittedCharacter(User)?.Name,
                Is.EqualTo(SlotZeroName),
                "Spawning did not commit the character that spawned.");
            // While taking slot zero they can only be the antags slot zero wants.
            Assert.That(_sRoster.GetAntagPreferences(ServerSession!), Does.Not.Contain(Thief));
        }

        await Server.WaitPost(() =>
        {
            var mind = _sMind.GetMind(ServerSession!.AttachedEntity!.Value);
            _sGhost.OnGhostAttempt(mind!.Value, canReturnGlobal: false, forced: true);
        });
        await Pair.RunTicksSync(5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_sRoster.TryGetCommittedCharacter(User),
                Is.Null,
                "Ghosting left the player pinned to the character they had spawned as.");
            Assert.That(_sRoster.GetAntagPreferences(ServerSession!),
                Does.Contain(Thief),
                "A ghost could not offer the antag preferences of their other characters.");
        }

        await EndRound();
    }

    [Test]
    [Description("A late join can spawn a character whose slot is disabled.")]
    public async Task LateJoinAcceptsDisabledSlot()
    {
        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        await SetProfiles(Character(SlotZeroName, Passenger), Character(SlotOneName, Engineer));
        await SetGlobalPriorities((Passenger, JobPriority.High), (Engineer, JobPriority.Medium));
        await SetSlotEnabled(1, false);

        await OverrideCVar(Side.Server, CCVars.GameMap, Map);
        await Server.WaitPost(() => _sTicker.ToggleReadyAll(false));
        await Server.WaitPost(() => _sTicker.StartRound());
        await Pair.RunTicksSync(10);

        Assume.That(_sTicker.PlayerGameStatuses[User],
            Is.Not.EqualTo(PlayerGameStatus.JoinedGame),
            "Player already joined at round start.");

        var station = EntityUid.Invalid;
        await Server.WaitPost(() =>
        {
            var query = SEntMan.EntityQueryEnumerator<StationJobsComponent>();
            if (query.MoveNext(out var uid, out _))
                station = uid;
        });
        Assume.That(station, Is.Not.EqualTo(EntityUid.Invalid), "No station to late join to.");

        var netStation = SEntMan.GetNetEntity(station);
        await Server.WaitPost(() => _sConHost.ExecuteCommand(ServerSession, $"joingame 1 {Engineer} {netStation}"));
        await Pair.RunTicksSync(10);

        AssertSpawned(Engineer, SlotOneName);
        await EndRound();
    }
}
