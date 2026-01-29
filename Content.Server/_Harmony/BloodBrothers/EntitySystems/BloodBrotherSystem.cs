using Content.Server._Harmony.GameTicking.Rules;
using Content.Server.Mind;
using Content.Shared._Harmony.BloodBrothers.Components;
using Content.Shared._Harmony.BloodBrothers.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Content.Shared.Zombies;
using Robust.Shared.Timing;

namespace Content.Server._Harmony.BloodBrothers.EntitySystems;

public sealed class BloodBrotherSystem : SharedBloodBrotherSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly BloodBrotherRuleSystem _bloodBrotherRule = default!;

    private TimeSpan _nextIconRefresh;
    private HashSet<EntityUid> ConvertableEntities = new();
    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<BloodBrotherConvertableRequestEvent>(OnConvertableRequest);

        _nextIconRefresh = TimeSpan.Zero;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextIconRefresh)
            return;

        var query = EntityQueryEnumerator<InitialBloodBrotherComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            _nextIconRefresh = _timing.CurTime + comp.IconRefreshRate;

            var players = _mindSystem.GetAliveHumans();
            foreach (var player in players)
            {
                // We clear them because for cleanup purposes, if they aren't in GetAliveHumans they won't be updated again.
                ConvertableEntities.Clear();
                if (_bloodBrotherRule.IsConvertable(player))
                {
                    ConvertableEntities.Remove(player);
                }
                else
                {
                    ConvertableEntities.Add(player.Owner);
                }
            }
        }
    }

    private void OnConvertableRequest(BloodBrotherConvertableRequestEvent ev, EntitySessionEventArgs args)
    {
        var response = new BloodBrotherConvertableResponseEvent
        {
            Eligible = ConvertableEntities,
        };

        RaiseNetworkEvent(response, ev.Sender);
    }
}
