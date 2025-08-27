using System.Linq;
using Content.Server.Worldgen.Components.Debris;
using Content.Shared.Maps;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Worldgen.Systems.Debris;

/// <summary>
///     This handles populating simple structures, simply using a loot table for each tile.
/// </summary>
public sealed class SimpleFloorPlanPopulatorSystem : BaseWorldSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;

    private static readonly ProtoId<TagPrototype> PirateBlacklistTag = "PirateSaleBlacklist";

    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<SimpleFloorPlanPopulatorComponent, LocalStructureLoadedEvent>(OnFloorPlanBuilt);
    }

    private void OnFloorPlanBuilt(EntityUid uid, SimpleFloorPlanPopulatorComponent component,
        LocalStructureLoadedEvent args)
    {
        var placeables = new List<string?>(4);
        var grid = Comp<MapGridComponent>(uid);
        var enumerator = _map.GetAllTilesEnumerator(uid, grid);
        while (enumerator.MoveNext(out var tile))
        {
            var coords = _map.GridTileToLocal(uid, grid, tile.Value.GridIndices);
            var selector = _turf.GetContentTileDefinition(tile.Value).ID;
            if (!component.Caches.TryGetValue(selector, out var cache))
                continue;

            placeables.Clear();
            cache.GetSpawns(_random, ref placeables);

            foreach (var proto in placeables)
            {
                if (proto is null)
                    continue;

                var ent = Spawn(proto, coords);
                _tagSystem.AddTag(ent, PirateBlacklistTag);
            }
        }
    }
}

