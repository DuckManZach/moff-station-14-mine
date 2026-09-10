#nullable enable
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Pair;
using Content.Server._Moffstation.Preferences;
using Content.Server.GameTicking;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Antag;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Moffstation.Round;

/// <summary>
/// Job selection is per character, job priority is per player, and only active characters spawn.
/// </summary>
[TestFixture]
public sealed class MultiCharacterTest : GameTest
{
    private static readonly ProtoId<JobPrototype> Passenger = "Passenger";
    private static readonly ProtoId<JobPrototype> Engineer = "StationEngineer";
    private static readonly ProtoId<JobPrototype> Captain = "Captain";
    private const string AgeGated = "MoffMultiCharacterAgeGatedJob";
    private const string OffStationJob = "MoffMultiCharacterOffStationJob";
    private const string TestAntag = "MoffMultiCharacterTestAntag";
    private const string TestAntagRule = "MoffMultiCharacterTestAntagRule";
    private const string OtherTestAntag = "MoffMultiCharacterOtherTestAntag";
    private const string OtherTestAntagRule = "MoffMultiCharacterOtherTestAntagRule";

    private const string Map = "MoffMultiCharacterTestMap";

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
}
