using System.Numerics;
using Content.Shared._Moffstation.Shuttles.Components;
using Content.Shared._Moffstation.Shuttles.Systems;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._Moffstation.Shuttles.Systems;

/// <summary>
/// Gives every FTL destination map a zone, and places it off to one side of whatever the biggest grid on that map is.
/// </summary>
public sealed class FTLZoneSystem : SharedFTLZoneSystem
{
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FTLDestinationComponent, ComponentStartup>(OnDestinationStartup);
    }

    /// <summary>
    /// Every destination map funnels through this, including ones that add the component directly instead of going
    /// through <c>TryAddFTLDestination</c> (salvage expeditions).
    /// </summary>
    private void OnDestinationStartup(Entity<FTLDestinationComponent> ent, ref ComponentStartup args)
    {
        EnsureComp<FTLZoneComponent>(ent);
    }

    protected override Vector2? ResolveOrigin(Entity<FTLZoneComponent> zone)
    {
        if (zone.Comp.Origin is { } existing)
            return existing;

        if (!TryComp<MapComponent>(zone.Owner, out var map))
            return null;

        // Resolved lazily rather than at startup because a destination can be registered before its grids are
        // loaded - salvage marks the map long before the dungeon finishes generating.
        Entity<MapGridComponent>? largest = null;
        foreach (var grid in Maps.GetAllGrids(map.MapId))
        {
            if (largest is not { } current || grid.Comp.LocalAABB.Size.Length() > current.Comp.LocalAABB.Size.Length())
                largest = grid;
        }

        if (largest is not { } reference)
            return null;

        var distance = reference.Comp.LocalAABB.Size.Length() / 2f +
                       _random.NextFloat(zone.Comp.MinOffset, zone.Comp.MaxOffset);

        var resolved = XformSystem.GetWorldPosition(reference.Owner) + _random.NextAngle().ToVec() * distance;

        zone.Comp.Origin = resolved;
        Dirty(zone);

        return resolved;
    }
}
