#nullable enable
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Pair;
using Content.Server._Moffstation.CharacterSelection;
using Content.Server._Moffstation.Preferences;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Maps;
using Content.Shared.Preferences;
using Content.Shared.Antag;
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
    private static readonly ProtoId<JobPrototype> Passenger = "Passenger";
    private static readonly ProtoId<JobPrototype> Engineer = "StationEngineer";
    private static readonly ProtoId<JobPrototype> Captain = "Captain";
    private const string AgeGated = "MoffMultiCharacterAgeGatedJob";
    private const string OffStationJob = "MoffMultiCharacterOffStationJob";
    private const string TimeGated = "MoffMultiCharacterTimeGatedJob";
    private const string DeptJobOffered = "MoffMultiCharacterDeptJobOffered";
    private const string DeptJobEnabled = "MoffMultiCharacterDeptJobEnabled";
    private const string TestAntag = "MoffMultiCharacterTestAntag";
    private const string TestAntagRule = "MoffMultiCharacterTestAntagRule";
    private const string OtherTestAntag = "MoffMultiCharacterOtherTestAntag";
    private const string OtherTestAntagRule = "MoffMultiCharacterOtherTestAntagRule";
    private const string SelfSpawnAntag = "MoffMultiCharacterSelfSpawnAntag";
    private const string SelfSpawnRule = "MoffMultiCharacterSelfSpawnAntagRule";

    private const string Map = "MoffMultiCharacterTestMap";

    /// Offers one job the test user can never hold, so a fallback that ignores preferences has
    /// nothing legitimate to hand them.
    private const string TimeGatedMap = "MoffMultiCharacterTimeGatedMap";

    /// Offers one job of a test department that the user never enabled, so only a department fallback
    /// can fill it. Command is deliberately not used: it is not a primary department, so
    /// SharedJobSystem.TryGetPrimaryDepartment finds nothing for its jobs.
    private const string DepartmentMap = "MoffMultiCharacterDepartmentMap";

    /// A real map, because AntagRandomSpawn needs a tile with breathable atmosphere to spawn on.
    private const string AtmosMap = "MoffMultiCharacterAtmosMap";

    private const string SlotZeroName = "Slot Zero Guy";
    private const string SlotOneName = "Slot One Guy";

    [TestPrototypes]
    private static readonly string MultiCharTestMap = @$"
- type: playTimeTracker
  id: PlayTimeMoffMultiCharacterAgeGated

- type: job
  id: {AgeGated}
  name: job-name-passenger
  description: job-description-passenger
  playTimeTracker: PlayTimeMoffMultiCharacterAgeGated
  startingGear: PassengerGear
  icon: ""JobIconPassenger""
  supervisors: job-supervisors-everyone
  requirements:
  - !type:AgeRequirement
    requiredAge: 30

- type: playTimeTracker
  id: PlayTimeMoffMultiCharacterOffStation

- type: job
  id: {OffStationJob}
  name: job-name-passenger
  description: job-description-passenger
  playTimeTracker: PlayTimeMoffMultiCharacterOffStation
  startingGear: PassengerGear
  icon: ""JobIconPassenger""
  supervisors: job-supervisors-everyone

- type: playTimeTracker
  id: PlayTimeMoffMultiCharacterTimeGated

- type: job
  id: {TimeGated}
  name: job-name-passenger
  description: job-description-passenger
  playTimeTracker: PlayTimeMoffMultiCharacterTimeGated
  startingGear: PassengerGear
  icon: ""JobIconPassenger""
  supervisors: job-supervisors-everyone
  requirements:
  - !type:OverallPlaytimeRequirement
    time: 36000

- type: playTimeTracker
  id: PlayTimeMoffMultiCharacterDeptOffered

- type: playTimeTracker
  id: PlayTimeMoffMultiCharacterDeptEnabled

- type: job
  id: {DeptJobOffered}
  name: job-name-passenger
  description: job-description-passenger
  playTimeTracker: PlayTimeMoffMultiCharacterDeptOffered
  startingGear: PassengerGear
  icon: ""JobIconPassenger""
  supervisors: job-supervisors-everyone

- type: job
  id: {DeptJobEnabled}
  name: job-name-passenger
  description: job-description-passenger
  playTimeTracker: PlayTimeMoffMultiCharacterDeptEnabled
  startingGear: PassengerGear
  icon: ""JobIconPassenger""
  supervisors: job-supervisors-everyone

- type: department
  id: MoffMultiCharacterTestDepartment
  name: department-Cargo
  description: department-Cargo-description
  color: ""#ffffff""
  roles:
  - {DeptJobOffered}
  - {DeptJobEnabled}

- type: antagSpecifier
  id: {TestAntag}
  prefRoles:
  - Traitor

- type: entity
  abstract: false
  parent: BaseUnweightedAntagRule
  id: {TestAntagRule}
  components:
  - type: AntagSelection
    selectionTime: PrePlayerSpawn
    antags:
    - !type:FixedAntagCount
      proto: {TestAntag}
      count: 1

- type: antagSpecifier
  id: {OtherTestAntag}
  multiAntagSetting: All
  prefRoles:
  - Thief

- type: entity
  abstract: false
  parent: BaseUnweightedAntagRule
  id: {OtherTestAntagRule}
  components:
  - type: AntagSelection
    selectionTime: PrePlayerSpawn
    antags:
    - !type:FixedAntagCount
      proto: {OtherTestAntag}
      count: 1

# Spawns its own body before jobs are assigned, so its antags never reach AssignJobs at all.
- type: antagSpecifier
  id: {SelfSpawnAntag}
  prefRoles:
  - Nukeops

- type: entity
  abstract: false
  parent: BaseUnweightedAntagRule
  id: {SelfSpawnRule}
  components:
  - type: AntagRandomSpawn
  - type: AntagLoadProfileRule
    preserveName: true
  - type: AntagSelection
    selectionTime: PrePlayerSpawn
    antags:
    - !type:FixedAntagCount
      proto: {SelfSpawnAntag}
      count: 1

- type: gameMap
  id: {AtmosMap}
  mapName: {AtmosMap}
  mapPath: /Maps/Test/dev_map.yml
  minPlayers: 0
  stations:
    Dev:
      stationProto: StandardNanotrasenStation
      components:
        - type: StationNameSetup
          mapNameTemplate: ""Dev""
        - type: StationJobs
          availableJobs:
            {Passenger}: [ -1, -1 ]

- type: gameMap
  id: {TimeGatedMap}
  mapName: {TimeGatedMap}
  mapPath: /Maps/Test/empty.yml
  minPlayers: 0
  stations:
    Empty:
      stationProto: StandardNanotrasenStation
      components:
        - type: StationNameSetup
          mapNameTemplate: ""Empty""
        - type: StationJobs
          availableJobs:
            {TimeGated}: [ 1, 1 ]

- type: gameMap
  id: {DepartmentMap}
  mapName: {DepartmentMap}
  mapPath: /Maps/Test/empty.yml
  minPlayers: 0
  stations:
    Empty:
      stationProto: StandardNanotrasenStation
      components:
        - type: StationNameSetup
          mapNameTemplate: ""Empty""
        - type: StationJobs
          availableJobs:
            {DeptJobOffered}: [ 1, 1 ]

- type: gameMap
  id: {Map}
  mapName: {Map}
  mapPath: /Maps/Test/empty.yml
  minPlayers: 0
  stations:
    Empty:
      stationProto: StandardNanotrasenStation
      components:
        - type: StationNameSetup
          mapNameTemplate: ""Empty""
        - type: StationJobs
          availableJobs:
            {Passenger}: [ -1, -1 ]
            {Engineer}: [ 5, 5 ]
            {Captain}: [ 1, 1 ]
            {AgeGated}: [ 5, 5 ]
";

    public override PoolSettings PoolSettings => new()
    {
        DummyTicker = false,
        Connected = true,
        InLobby = true,
    };

    /// <summary>
    /// Two characters taking one job each, named so the spawned entity identifies the winner.
    /// </summary>
    private async Task SetupTwoCharacters(
        TestPair pair,
        ProtoId<JobPrototype> slotZeroJob,
        ProtoId<JobPrototype> slotOneJob)
    {
        var prefMan = pair.Server.ResolveDependency<IServerPreferencesManager>();
        var user = pair.Client.User!.Value;

        var slotZero = new HumanoidCharacterProfile()
            .WithName(SlotZeroName)
            .WithJobPriorities(new Dictionary<ProtoId<JobPrototype>, JobPriority>
            {
                [slotZeroJob] = JobPriority.Medium,
            });

        var slotOne = new HumanoidCharacterProfile()
            .WithName(SlotOneName)
            .WithJobPriorities(new Dictionary<ProtoId<JobPrototype>, JobPriority>
            {
                [slotOneJob] = JobPriority.Medium,
            });

        await pair.Server.WaitPost(() => prefMan.SetProfile(user, 0, slotZero).Wait());
        await pair.Server.WaitPost(() => prefMan.SetProfile(user, 1, slotOne).Wait());
    }

    private async Task SetGlobalPriorities(
        TestPair pair,
        params (ProtoId<JobPrototype> Job, JobPriority Priority)[] priorities)
    {
        var selection = pair.Server.ResolveDependency<MoffCharacterSelectionManager>();
        var user = pair.Client.User!.Value;

        var dict = new Dictionary<ProtoId<JobPrototype>, JobPriority>();
        foreach (var (job, priority) in priorities)
        {
            dict[job] = priority;
        }

        await pair.Server.WaitPost(() => selection.SetJobPriorities(user, dict).Wait());
    }

    private async Task SetSlotEnabled(TestPair pair, int slot, bool enabled)
    {
        var selection = pair.Server.ResolveDependency<MoffCharacterSelectionManager>();
        var user = pair.Client.User!.Value;

        await pair.Server.WaitPost(() =>
        {
            // GetState hands back a throwaway default when nothing is cached.
            Assert.That(selection.TryGetState(user, out var state),
                Is.True,
                "Selection state was not loaded for the test user.");

            state.EnabledSlots[slot] = enabled;
        });
    }

    /// <summary>
    /// Resets the state of the test
    /// </summary>
    [SetUp]
    public async Task ResetSelectionState()
    {
        var pair = Pair;
        var selection = pair.Server.ResolveDependency<MoffCharacterSelectionManager>();
        var user = pair.Client.User!.Value;

        await pair.Server.WaitPost(() =>
        {
            if (!selection.TryGetState(user, out var state))
                return;

            state.EnabledSlots.Clear();
            state.JobPriorities.Clear();
        });
    }

    /// <summary>
    /// Automatic preference resetting only covers slot 0, so slot 1 would leak between tests.
    /// </summary>
    [TearDown]
    public async Task CleanupSecondCharacter()
    {
        var pair = Pair;
        var prefMan = pair.Server.ResolveDependency<IServerPreferencesManager>();
        var selection = pair.Server.ResolveDependency<MoffCharacterSelectionManager>();
        var user = pair.Client.User!.Value;

        await pair.Server.WaitPost(() => prefMan.SetProfile(user, 1, new HumanoidCharacterProfile()).Wait());

        // Deactivating is what isolates: an inactive slot contributes no candidates.
        await pair.Server.WaitPost(() =>
        {
            if (selection.TryGetState(user, out var state))
            {
                state.EnabledSlots.Clear();
                state.EnabledSlots[1] = false;
                state.JobPriorities.Clear();
            }
        });

        // SetProfile also pins the selected slot, so writing slot 1 above left the selection on it.
        // Put it back or the next test to take this pooled pair inherits a selected index of 1 --
        // which is what JobPriorityTest asserts against.
        await pair.Server.WaitPost(() =>
        {
            if (prefMan.GetPreferences(user).Characters.TryGetValue(0, out var slotZero))
                prefMan.SetProfile(user, 0, slotZero).Wait();
        });
    }

    private async Task StartRound(TestPair pair)
    {
        var ticker = pair.Server.System<GameTicker>();
        ticker.ToggleReadyAll(true);
        await pair.Server.WaitPost(() => ticker.StartRound());
        await pair.RunTicksSync(10);
    }

    /// <summary>Asserts the assigned job and which character spawned.</summary>
    private void AssertJobAndCharacter(TestPair pair, ProtoId<JobPrototype> job, string characterName)
    {
        var jobSys = pair.Server.System<SharedJobSystem>();
        var mindSys = pair.Server.System<MindSystem>();
        var ticker = pair.Server.System<GameTicker>();
        var user = pair.Client.User!.Value;

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
        Assert.That(ticker.PlayerGameStatuses[user], Is.EqualTo(PlayerGameStatus.JoinedGame));

        var uid = pair.Server.PlayerMan.SessionsDict.GetValueOrDefault(user)?.AttachedEntity;
        Assert.That(pair.Server.EntMan.EntityExists(uid));

        var mind = mindSys.GetMind(uid!.Value);
        Assert.That(jobSys.MindTryGetJobId(mind, out var actualJob));
        Assert.That(actualJob, Is.EqualTo(job));

        Assert.That(pair.Server.EntMan.GetComponent<MetaDataComponent>(uid.Value).EntityName,
            Is.EqualTo(characterName),
            "The wrong character was spawned for the assigned job.");
    }

    /// <summary>
    /// A job on a non-selected character still makes the player eligible, and that one spawns.
    /// </summary>
    [Test]
    public async Task SpawnsCharacterMatchingAssignedJob()
    {
        var pair = Pair;
        pair.Server.CfgMan.SetCVar(CCVars.GameMap, Map);
        var ticker = pair.Server.System<GameTicker>();

        await SetupTwoCharacters(pair, Passenger, Engineer);

        // Only Engineer has a priority, and only slot 1 will take it.
        await SetGlobalPriorities(pair, (Engineer, JobPriority.High));

        await StartRound(pair);

        AssertJobAndCharacter(pair, Engineer, SlotOneName);

        await pair.Server.WaitPost(() => ticker.RestartRound());
    }

    /// <summary>Inactive characters are skipped even when selected and job-eligible.</summary>
    [Test]
    public async Task InactiveCharacterIsNotSpawned()
    {
        var pair = Pair;
        pair.Server.CfgMan.SetCVar(CCVars.GameMap, Map);
        var ticker = pair.Server.System<GameTicker>();

        await SetupTwoCharacters(pair, Engineer, Engineer);

        // Slot 0 is selected and wants Engineer, but is inactive.
        await SetGlobalPriorities(pair, (Engineer, JobPriority.High));
        await SetSlotEnabled(pair, 0, false);

        await StartRound(pair);

        AssertJobAndCharacter(pair, Engineer, SlotOneName);

        await pair.Server.WaitPost(() => ticker.RestartRound());
    }

    /// <summary>
    /// An inactive character contributes no candidate jobs, even when it is the selected one. If it
    /// did, the player would be assigned a job no active character can fill.
    /// </summary>
    [Test]
    public async Task InactiveSelectedCharacterContributesNoJobs()
    {
        var pair = Pair;
        pair.Server.CfgMan.SetCVar(CCVars.GameMap, Map);
        var ticker = pair.Server.System<GameTicker>();

        await SetupTwoCharacters(pair, Captain, Passenger);
        await SetGlobalPriorities(pair, (Captain, JobPriority.High), (Passenger, JobPriority.Medium));
        await SetSlotEnabled(pair, 0, false);

        await StartRound(pair);

        AssertJobAndCharacter(pair, Passenger, SlotOneName);

        await pair.Server.WaitPost(() => ticker.RestartRound());
    }

    /// <summary>
    /// Jobs contributed by a non-selected character are still subject to role timers, and a player
    /// who fails them falls back to a job they can hold rather than not spawning at all.
    /// </summary>
    [Test]
    public async Task RoleTimersApplyToOtherCharactersJobs()
    {
        var pair = Pair;
        pair.Server.CfgMan.SetCVar(CCVars.GameMap, Map);
        var ticker = pair.Server.System<GameTicker>();

        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, true);

        await SetupTwoCharacters(pair, Passenger, Captain);
        await SetGlobalPriorities(pair, (Captain, JobPriority.High), (Passenger, JobPriority.Medium));

        await StartRound(pair);

        // The test user has no playtime, so Captain is filtered out before jobs are assigned.
        AssertJobAndCharacter(pair, Passenger, SlotZeroName);

        await pair.Server.WaitPost(() => ticker.RestartRound());
    }

    /// <summary>High outranks Medium even across different characters.</summary>
    [Test]
    public async Task GlobalPriorityAppliesAcrossCharacters()
    {
        var pair = Pair;
        pair.Server.CfgMan.SetCVar(CCVars.GameMap, Map);
        var ticker = pair.Server.System<GameTicker>();

        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        await SetupTwoCharacters(pair, Passenger, Captain);

        await SetGlobalPriorities(pair, (Passenger, JobPriority.Medium), (Captain, JobPriority.High));

        await StartRound(pair);

        AssertJobAndCharacter(pair, Captain, SlotOneName);

        await pair.Server.WaitPost(() => ticker.RestartRound());

        // Flip the preference; the other character should win.
        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));
        await SetGlobalPriorities(pair, (Passenger, JobPriority.High), (Captain, JobPriority.Medium));

        await StartRound(pair);

        AssertJobAndCharacter(pair, Passenger, SlotZeroName);

        await pair.Server.WaitPost(() => ticker.RestartRound());
    }

    /// <summary>
    /// Which character fills a job still has to respect the job's age and species gates when role
    /// timers are off, or the pick would ignore them entirely.
    /// </summary>
    [Test]
    public async Task AgeRequirementAppliesWithRoleTimersOff()
    {
        var pair = Pair;
        pair.Server.CfgMan.SetCVar(CCVars.GameMap, Map);
        var ticker = pair.Server.System<GameTicker>();

        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        var prefMan = pair.Server.ResolveDependency<IServerPreferencesManager>();
        var user = pair.Client.User!.Value;

        // Both characters want the same age gated job; only the older one may hold it.
        var young = new HumanoidCharacterProfile()
            .WithName(SlotZeroName)
            .WithAge(20)
            .WithJobPriorities(new Dictionary<ProtoId<JobPrototype>, JobPriority>
            {
                [AgeGated] = JobPriority.Medium,
            });

        var old = new HumanoidCharacterProfile()
            .WithName(SlotOneName)
            .WithAge(60)
            .WithJobPriorities(new Dictionary<ProtoId<JobPrototype>, JobPriority>
            {
                [AgeGated] = JobPriority.Medium,
            });

        await pair.Server.WaitPost(() => prefMan.SetProfile(user, 0, young).Wait());
        await pair.Server.WaitPost(() => prefMan.SetProfile(user, 1, old).Wait());

        await SetGlobalPriorities(pair, (AgeGated, JobPriority.High));

        await StartRound(pair);

        AssertJobAndCharacter(pair, AgeGated, SlotOneName);

        await pair.Server.WaitPost(() => ticker.RestartRound());
    }

    // ---------------------------------------------------------------------------------------------
    // Antag <-> job selection ordering.
    //
    // A player opts in to an antag if *any* active character wants it, so job candidacy has to be
    // narrowed to the characters that can actually fill that antag. Otherwise they get assigned a job
    // only an antag-incompatible character wants, no character can be both, and they are dropped to
    // the lobby with neither a job nor an antag.
    // ---------------------------------------------------------------------------------------------

    /// <summary>Two characters, each with its own job and its own antag opt-in.</summary>
    private async Task SetupTwoCharactersWithAntags(
        TestPair pair,
        ProtoId<JobPrototype> slotZeroJob,
        ProtoId<AntagPrototype>[] slotZeroAntags,
        ProtoId<JobPrototype> slotOneJob,
        ProtoId<AntagPrototype>[] slotOneAntags)
    {
        var prefMan = pair.Server.ResolveDependency<IServerPreferencesManager>();
        var user = pair.Client.User!.Value;

        var slotZero = new HumanoidCharacterProfile()
            .WithName(SlotZeroName)
            .WithJobPriorities(new Dictionary<ProtoId<JobPrototype>, JobPriority>
            {
                [slotZeroJob] = JobPriority.Medium,
            })
            .WithAntagPreferences(slotZeroAntags);

        var slotOne = new HumanoidCharacterProfile()
            .WithName(SlotOneName)
            .WithJobPriorities(new Dictionary<ProtoId<JobPrototype>, JobPriority>
            {
                [slotOneJob] = JobPriority.Medium,
            })
            .WithAntagPreferences(slotOneAntags);

        await pair.Server.WaitPost(() => prefMan.SetProfile(user, 0, slotZero).Wait());
        await pair.Server.WaitPost(() => prefMan.SetProfile(user, 1, slotOne).Wait());
    }

    /// <summary>Adds (but does not start) a rule; PrePlayerSpawn pre-selection only needs it added.</summary>
    private async Task<EntityUid> AddAntagRule(TestPair pair, string ruleId)
    {
        var ticker = pair.Server.System<GameTicker>();
        var rule = EntityUid.Invalid;
        await pair.Server.WaitPost(() => rule = ticker.AddGameRule(ruleId));
        return rule;
    }

    /// <summary>How many minds the rule actually handed the antag role to.</summary>
    private int AssignedAntagCount(TestPair pair, EntityUid rule, string antagProto)
    {
        var antagSys = pair.Server.System<AntagSelectionSystem>();
        var comp = pair.Server.EntMan.GetComponent<AntagSelectionComponent>(rule);
        return antagSys.GetAssignedAntagCount((rule, comp), antagProto);
    }

    /// <summary>
    /// Whether the player still holds this rule's antag slot, i.e. it was not wiped on a failed
    /// spawn. Deliberately not IsAssignedAntag, which also answers true for any leftover antag mind
    /// role and would hide a rule that never pre-selected at all.
    /// </summary>
    private bool StillPreSelected(TestPair pair, EntityUid rule)
    {
        var comp = pair.Server.EntMan.GetComponent<AntagSelectionComponent>(rule);
        var session = pair.Server.PlayerMan.SessionsDict[pair.Client.User!.Value];

        foreach (var (_, sessions) in comp.PreSelectedSessions)
        {
            if (sessions.Contains(session))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Restarts and lets the restart actually land. Without the tick sync the next test picks up a
    /// pooled pair mid-restart, and its player can still be holding the previous round's antag role
    /// -- which silently blocks the next pre-selection through the MultiAntagSetting check.
    /// </summary>
    private async Task EndRound(TestPair pair)
    {
        var ticker = pair.Server.System<GameTicker>();
        await pair.Server.WaitPost(() => ticker.RestartRound());
        await pair.RunTicksSync(10);
    }

    /// <summary>
    /// The bug this all exists for. Slot zero wants Traitor and only Passenger; slot one wants no
    /// antag but takes Engineer, which is the better job. Before the fix the player was pre-selected
    /// as a traitor off slot zero, assigned Engineer off slot one, and then dropped entirely because
    /// no single character could be both.
    /// </summary>
    [Test]
    public async Task AntagNarrowsJobsToCompatibleCharacters()
    {
        var pair = Pair;
        pair.Server.CfgMan.SetCVar(CCVars.GameMap, Map);
        var ticker = pair.Server.System<GameTicker>();

        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        await SetupTwoCharactersWithAntags(pair, Passenger, ["Traitor"], Engineer, []);
        await SetGlobalPriorities(pair, (Engineer, JobPriority.High), (Passenger, JobPriority.Medium));

        var rule = await AddAntagRule(pair, TestAntagRule);

        await StartRound(pair);

        // Engineer is off the table entirely: only the traitor-capable character contributes jobs.
        AssertJobAndCharacter(pair, Passenger, SlotZeroName);
        Assert.That(AssignedAntagCount(pair, rule, TestAntag), Is.EqualTo(1), "The antag role was not assigned.");

        await EndRound(pair);
    }

    /// <summary>
    /// The narrowing must cost nothing when no antag is in play, or every non-antag player would lose
    /// the jobs their other characters offer.
    /// </summary>
    [Test]
    public async Task NoAntagMeansNoNarrowing()
    {
        var pair = Pair;
        pair.Server.CfgMan.SetCVar(CCVars.GameMap, Map);
        var ticker = pair.Server.System<GameTicker>();

        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        // Identical setup to the test above, but no antag rule is running.
        await SetupTwoCharactersWithAntags(pair, Passenger, ["Traitor"], Engineer, []);
        await SetGlobalPriorities(pair, (Engineer, JobPriority.High), (Passenger, JobPriority.Medium));

        await StartRound(pair);

        AssertJobAndCharacter(pair, Engineer, SlotOneName);

        await EndRound(pair);
    }

    /// <summary>
    /// A pre-selected antag whose only compatible character wants a job it cannot get falls back to
    /// the overflow job and keeps the antag, instead of being dropped to the lobby.
    /// </summary>
    [Test]
    public async Task PreSelectedAntagWithNoJobGetsOverflow()
    {
        var pair = Pair;
        pair.Server.CfgMan.SetCVar(CCVars.GameMap, Map);
        var ticker = pair.Server.System<GameTicker>();
        var prefMan = pair.Server.ResolveDependency<IServerPreferencesManager>();
        var user = pair.Client.User!.Value;

        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        // No fallback broadening, so nothing can hand them a job they did not ask for and the
        // overflow path is the only thing that can keep them out of the lobby. Set directly rather
        // than via OverrideCVar, which does not reliably reach a pooled pair.
        var originalFallback = pair.Server.CfgMan.GetCVar(CCVars.GameMinimumJobFallback);
        pair.Server.CfgMan.SetCVar(CCVars.GameMinimumJobFallback, MinimumJobFallback.None);

        // The traitor-capable character wants only a job this station does not offer, and explicitly
        // asked to stay in the lobby rather than take an overflow job. Holding the antag outranks
        // that preference, so it should still spawn -- as Passenger, which it never enabled.
        var slotZero = new HumanoidCharacterProfile()
            .WithName(SlotZeroName)
            .WithJobPriorities(new Dictionary<ProtoId<JobPrototype>, JobPriority>
            {
                [OffStationJob] = JobPriority.Medium,
            })
            .WithAntagPreferences(["Traitor"])
            .WithPreferenceUnavailable(PreferenceUnavailableMode.StayInLobby);

        var slotOne = new HumanoidCharacterProfile()
            .WithName(SlotOneName)
            .WithJobPriorities(new Dictionary<ProtoId<JobPrototype>, JobPriority>
            {
                [Engineer] = JobPriority.Medium,
            });

        await pair.Server.WaitPost(() => prefMan.SetProfile(user, 0, slotZero).Wait());
        await pair.Server.WaitPost(() => prefMan.SetProfile(user, 1, slotOne).Wait());

        await SetGlobalPriorities(pair, (OffStationJob, JobPriority.High), (Engineer, JobPriority.High));

        var rule = await AddAntagRule(pair, TestAntagRule);

        try
        {
            await StartRound(pair);

            Assert.That(StillPreSelected(pair, rule), Is.True, "The antag slot was wiped instead of falling back.");
            // Passenger is the station's overflow job, and slot zero never enabled it.
            AssertJobAndCharacter(pair, Passenger, SlotZeroName);
        }
        finally
        {
            pair.Server.CfgMan.SetCVar(CCVars.GameMinimumJobFallback, originalFallback);
        }

        await EndRound(pair);
    }

    /// <summary>
    /// Two stacking antags wanted by different characters must not both be pre-selected -- no single
    /// character could fill both, and the player would spawn with neither.
    /// </summary>
    [Test]
    public async Task IncompatibleAntagCombinationIsNotPreSelected()
    {
        var pair = Pair;
        pair.Server.CfgMan.SetCVar(CCVars.GameMap, Map);
        var ticker = pair.Server.System<GameTicker>();

        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        await SetupTwoCharactersWithAntags(pair, Passenger, ["Traitor"], Engineer, ["Thief"]);
        await SetGlobalPriorities(pair, (Passenger, JobPriority.High), (Engineer, JobPriority.High));

        var first = await AddAntagRule(pair, TestAntagRule);
        var second = await AddAntagRule(pair, OtherTestAntagRule);

        await StartRound(pair);

        var firstCount = AssignedAntagCount(pair, first, TestAntag);
        var secondCount = AssignedAntagCount(pair, second, OtherTestAntag);

        Assert.Multiple(() =>
        {
            Assert.That(firstCount + secondCount,
                Is.EqualTo(1),
                "Exactly one of the two incompatible antags should have been filled.");

            // Whichever won, the player is in the round holding a job rather than stuck in the lobby.
            Assert.That(ticker.PlayerGameStatuses[pair.Client.User!.Value],
                Is.EqualTo(PlayerGameStatus.JoinedGame));
        });

        await EndRound(pair);
    }

    /// <summary>
    /// Makes <paramref name="slot"/> the lobby-selected character. SetProfile moves the selection to
    /// whichever slot it wrote, so re-writing the slot you want selected is how you pin it.
    /// </summary>
    private async Task SelectSlot(TestPair pair, int slot)
    {
        var prefMan = pair.Server.ResolveDependency<IServerPreferencesManager>();
        var user = pair.Client.User!.Value;

        var profile = (HumanoidCharacterProfile) prefMan.GetPreferences(user).Characters[slot];
        await pair.Server.WaitPost(() => prefMan.SetProfile(user, slot, profile).Wait());

        Assert.That(prefMan.GetPreferences(user).SelectedCharacterIndex,
            Is.EqualTo(slot),
            "Failed to pin the selected character slot.");
    }

    /// <summary>The job the player was assigned, or null when they were left in the lobby.</summary>
    private ProtoId<JobPrototype>? AssignedJob(TestPair pair)
    {
        var jobSys = pair.Server.System<SharedJobSystem>();
        var mindSys = pair.Server.System<MindSystem>();
        var user = pair.Client.User!.Value;

        var uid = pair.Server.PlayerMan.SessionsDict.GetValueOrDefault(user)?.AttachedEntity;

        if (!pair.Server.EntMan.EntityExists(uid))
            return null;

        var mind = mindSys.GetMind(uid!.Value);
        return jobSys.MindTryGetJobId(mind, out var job) ? job : null;
    }

    /// <summary>The character the player is actually attached to, and whether they hold a job.</summary>
    private (string Name, ProtoId<JobPrototype>? Job) SpawnedCharacter(TestPair pair)
    {
        var jobSys = pair.Server.System<SharedJobSystem>();
        var mindSys = pair.Server.System<MindSystem>();
        var user = pair.Client.User!.Value;

        var uid = pair.Server.PlayerMan.SessionsDict.GetValueOrDefault(user)?.AttachedEntity;
        Assert.That(pair.Server.EntMan.EntityExists(uid), $"{user} is not attached to an entity.");

        var mind = mindSys.GetMind(uid!.Value);
        var job = jobSys.MindTryGetJobId(mind, out var actualJob) ? actualJob : null;

        return (pair.Server.EntMan.GetComponent<MetaDataComponent>(uid.Value).EntityName, job);
    }

    /// <summary>
    /// Antags that spawn their own body (pirates, nukies, stowaways) are pulled out of the player
    /// pool before jobs are assigned, so none of the job-candidacy narrowing applies to them. The
    /// character they spawn as still has to be one that opted in to the antag, not whichever
    /// character happens to be selected in the lobby.
    /// </summary>
    [Test]
    public async Task SelfSpawningAntagUsesCharacterThatWantsIt()
    {
        var pair = Pair;
        pair.Server.CfgMan.SetCVar(CCVars.GameMap, AtmosMap);

        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        // Slot zero is the lobby-selected character and wants no antags at all. Only slot one opted
        // in, so slot one is the only character that may fill the role.
        await SetupTwoCharactersWithAntags(pair, Passenger, [], Passenger, ["Nukeops"]);
        await SetGlobalPriorities(pair, (Passenger, JobPriority.High));

        // The whole point: the selected character is the one that does NOT want the antag.
        await SelectSlot(pair, 0);

        var rule = await AddAntagRule(pair, SelfSpawnRule);

        await StartRound(pair);

        Assert.That(StillPreSelected(pair, rule), Is.True, "The player was not pre-selected for the antag.");

        var (name, job) = SpawnedCharacter(pair);

        Assert.Multiple(() =>
        {
            // No job proves this really went down the self-spawn path rather than the normal one,
            // where the job narrowing would have picked slot one for unrelated reasons.
            Assert.That(job, Is.Null, "Expected the antag to spawn its own body instead of taking a job.");
            Assert.That(name,
                Is.EqualTo(SlotOneName),
                "The antag spawned as the lobby-selected character, which never opted in to the antag.");
        });

        await EndRound(pair);
    }

    // ---------------------------------------------------------------------------------------------
    // The fallback ladder. StationJobsGetCandidatesEvent is subtractive: subscribers narrow the jobs
    // seeded from the player's roster. When a Moff subscriber instead *replaced* that list, asking it
    // about one job answered "does this player have any playable job at all", which silently disabled
    // playtime and whitelist enforcement on the ignoring-preferences fallback.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Runs job assignment for the test user against a throwaway station built from one of this
    /// fixture's test maps, and returns the job they were assigned.
    /// </summary>
    /// <remarks>
    /// Deliberately not a whole round: the spawn-side character pick has guards of its own, which
    /// would hide what the assignment actually decided. The station is deleted again -- leaving it
    /// behind adds a spawnable station to the pooled server and changes where later tests spawn.
    /// </remarks>
    private async Task<ProtoId<JobPrototype>?> AssignJobOnTestStation(
        TestPair pair,
        string gameMap,
        MinimumJobFallback fallback)
    {
        var protoMan = pair.Server.ResolveDependency<IPrototypeManager>();
        var prefMan = pair.Server.ResolveDependency<IServerPreferencesManager>();
        var stationSys = pair.Server.System<StationSystem>();
        var stationJobs = pair.Server.System<StationJobsSystem>();
        var user = pair.Client.User!.Value;
        var proto = protoMan.Index<GameMapPrototype>(gameMap);

        var originalFallback = pair.Server.CfgMan.GetCVar(CCVars.GameMinimumJobFallback);
        pair.Server.CfgMan.SetCVar(CCVars.GameMinimumJobFallback, fallback);

        ProtoId<JobPrototype>? assigned = null;

        try
        {
            await pair.Server.WaitPost(() =>
            {
                var station = stationSys.InitializeNewStation(proto.Stations["Empty"], null, "Empty", proto);

                try
                {
                    var profiles = new Dictionary<NetUserId, HumanoidCharacterProfile>
                    {
                        [user] = prefMan.GetPreferences(user).SelectedCharacter,
                    };

                    assigned = stationJobs.AssignJobs(profiles, [station]).GetValueOrDefault(user).Item1;
                }
                finally
                {
                    pair.Server.EntMan.DeleteEntity(station);
                }
            });
        }
        finally
        {
            pair.Server.CfgMan.SetCVar(CCVars.GameMinimumJobFallback, originalFallback);
        }

        return assigned;
    }

    /// <summary>
    /// <see cref="MinimumJobFallback.AnyEligiblePlayer"/> ignores what a player asked for, never what
    /// they are allowed to hold. A job they lack the playtime for must stay out of reach.
    /// </summary>
    /// <remarks>
    /// Fails when <c>StationJobsGetCandidatesEvent</c> is answered by replacing the job list rather
    /// than narrowing it: asking it about one job then means "does this player have any playable job
    /// at all", which is true for anybody and lets this fallback bypass playtime entirely.
    /// </remarks>
    [Test]
    [Description("The ignoring-preferences job fallback still enforces playtime requirements.")]
    public async Task AnyEligiblePlayerFallbackRespectsPlaytime()
    {
        var pair = Pair;

        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, true);

        // Passenger is deliberately a job this station does not offer. It leaves the player with a
        // non-empty roster, which is what made the single-job check answer "yes" for the gated job.
        await SetupTwoCharacters(pair, Passenger, Passenger);
        await SetGlobalPriorities(pair, (Passenger, JobPriority.High));

        var assigned = await AssignJobOnTestStation(pair, TimeGatedMap, MinimumJobFallback.AnyEligiblePlayer);

        Assert.That(assigned,
            Is.Not.EqualTo((ProtoId<JobPrototype>?) TimeGated),
            "A fallback handed out a job the player has no playtime for.");
    }

    /// <summary>
    /// <see cref="MinimumJobFallback.SameDepartment"/> has to match on the player-global priority. It
    /// used to read the selected character's own value -- which the editor flattens to Medium for
    /// every enabled job -- so a job the player rated Never still dragged them into its department.
    /// </summary>
    [Test]
    [Description("The same-department job fallback matches on the player-global priority, not the character's own.")]
    public async Task SameDepartmentFallbackUsesGlobalPriority()
    {
        var pair = Pair;

        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        // Both characters have the sibling job enabled -- same department as the only job this station
        // offers -- but the player rates it Never, so it cannot be their bridge into the department.
        await SetupTwoCharacters(pair, DeptJobEnabled, DeptJobEnabled);
        await SetGlobalPriorities(pair, (Passenger, JobPriority.High));

        var assigned = await AssignJobOnTestStation(pair, DepartmentMap, MinimumJobFallback.SameDepartment);

        Assert.That(assigned,
            Is.Not.EqualTo((ProtoId<JobPrototype>?) DeptJobOffered),
            "The department fallback matched on a job the player rated Never.");
    }

    // ---------------------------------------------------------------------------------------------
    // The committed character. Everything downstream of spawning reads it, so it has to stop being
    // true the moment the player stops embodying it.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Ghosting releases the committed character. While it was held for the whole round, a player who
    /// spawned and then ghosted was stuck offering only their first character's antag preferences, so
    /// a ghost role their other characters opted in to was unreachable.
    /// </summary>
    [Test]
    [Description("Ghosting releases the committed character, so the player offers every active character again.")]
    public async Task RejoinAfterGhostingIsNotPinnedToFirstCharacter()
    {
        var pair = Pair;
        pair.Server.CfgMan.SetCVar(CCVars.GameMap, Map);
        var roster = pair.Server.System<MoffCharacterRosterSystem>();
        var mindSys = pair.Server.System<MindSystem>();
        var ghostSys = pair.Server.System<GhostSystem>();
        var user = pair.Client.User!.Value;

        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        // Only slot one opted in to Thief, and slot zero is the one that will spawn.
        await SetupTwoCharactersWithAntags(pair, Passenger, [], Engineer, ["Thief"]);
        await SetGlobalPriorities(pair, (Passenger, JobPriority.High), (Engineer, JobPriority.Medium));

        await StartRound(pair);

        AssertJobAndCharacter(pair, Passenger, SlotZeroName);

        var session = pair.Server.PlayerMan.SessionsDict[user];

        Assert.That(roster.TryGetCommittedCharacter(user)?.Name,
            Is.EqualTo(SlotZeroName),
            "Spawning did not commit the character that spawned.");

        // While embodying slot zero they can only be the antags slot zero wants.
        Assert.That(roster.GetAntagPreferences(session), Does.Not.Contain(new ProtoId<AntagPrototype>("Thief")));

        await pair.Server.WaitPost(() =>
        {
            var mind = mindSys.GetMind(session.AttachedEntity!.Value);
            ghostSys.OnGhostAttempt(mind!.Value, canReturnGlobal: false, forced: true);
        });
        await pair.RunTicksSync(5);

        Assert.Multiple(() =>
        {
            Assert.That(roster.TryGetCommittedCharacter(user),
                Is.Null,
                "Ghosting left the player pinned to the character they had spawned as.");
            Assert.That(roster.GetAntagPreferences(session),
                Does.Contain(new ProtoId<AntagPrototype>("Thief")),
                "A ghost could not offer the antag preferences of their other characters.");
        });

        await EndRound(pair);
    }

    /// <summary>
    /// A late join may take any slot, enabled or not, on any job it can see. The slot-enabled flag
    /// only governs who is eligible at round start, and the late join window lists every character --
    /// so the joingame command must not gate on it.
    /// </summary>
    [Test]
    [Description("A late join can spawn a character whose slot is disabled.")]
    public async Task LateJoinAcceptsDisabledSlot()
    {
        var pair = Pair;
        pair.Server.CfgMan.SetCVar(CCVars.GameMap, Map);
        var ticker = pair.Server.System<GameTicker>();
        var conHost = pair.Server.ResolveDependency<IConsoleHost>();
        var user = pair.Client.User!.Value;

        await OverrideCVar(Side.Server, CCVars.GameRoleTimers, false);

        // Slot 1 is disabled, so it contributes nothing at round start -- but late joining as it must
        // still work.
        await SetupTwoCharacters(pair, Passenger, Engineer);
        await SetGlobalPriorities(pair, (Passenger, JobPriority.High), (Engineer, JobPriority.Medium));
        await SetSlotEnabled(pair, 1, false);

        // Start the round with nobody readied, so the player is left in the lobby to late join from.
        await pair.Server.WaitPost(() => ticker.ToggleReadyAll(false));
        await pair.Server.WaitPost(() => ticker.StartRound());
        await pair.RunTicksSync(10);

        Assert.That(ticker.PlayerGameStatuses[user], Is.Not.EqualTo(PlayerGameStatus.JoinedGame));

        var station = EntityUid.Invalid;
        await pair.Server.WaitPost(() =>
        {
            var query = pair.Server.EntMan.EntityQueryEnumerator<StationJobsComponent>();
            if (query.MoveNext(out var uid, out _))
                station = uid;
        });
        Assert.That(station, Is.Not.EqualTo(EntityUid.Invalid), "No station to late join to.");

        var session = pair.Server.PlayerMan.SessionsDict[user];
        var netStation = pair.Server.EntMan.GetNetEntity(station);

        await pair.Server.WaitPost(() =>
            conHost.ExecuteCommand(session, $"joingame 1 {Engineer} {netStation}"));
        await pair.RunTicksSync(10);

        AssertJobAndCharacter(pair, Engineer, SlotOneName);

        await EndRound(pair);
    }
}
