using Content.Server.Mind;
using Content.Shared._Harmony.BloodBrothers.Components;
using Content.Shared._Harmony.BloodBrothers.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Zombies;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Server._Harmony.BloodBrothers.EntitySystems;

public sealed class BloodBrotherSystem : SharedBloodBrotherSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;

    private TimeSpan _nextIconRefresh;
    public Dictionary<MindComponent, bool> ConvertableList = new();
    public override void Initialize()
    {
        base.Initialize();

        _nextIconRefresh = TimeSpan.Zero;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<InitialBloodBrotherComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < _nextIconRefresh)
                return;
            {
                _nextIconRefresh = _timing.CurTime + comp.IconRefreshRate;

                var players = _mindSystem.GetAliveHumans();
                foreach (var player in players)
                {
                    if (!IsConvertable(player))
                    {
                        RemCompDeferred<BloodBrotherConvertableComponent>(player);
                        continue;
                    }
                    EnsureComp<BloodBrotherConvertableComponent>(player);
                }
            }
        }
    }

    private bool IsConvertable(EntityUid uid)
    {
        if (!_mindSystem.TryGetMind(uid, out _, out var targetMind) || targetMind.UserId == null)
        {
            return false;
        }

        // Target is already a blood brother
        if (HasComp<BloodBrotherComponent>(uid))
        {
            return false;
        }

        if (!HasComp<HumanoidAppearanceComponent>(uid))
        {
            return false;
        }

        if (HasComp<ZombieComponent>(uid))
        {
            return false;
        }

        if (!_mobStateSystem.IsAlive(uid))
        {
            return false;
        }

        return true;
    }
}
