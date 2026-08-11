using Robust.Shared.GameStates;

namespace Content.Shared._Moffstation.Shuttles.Components;

/// <summary>
/// Marks an entity as its map's FTL zone. Shuttles must be inside the zone to FTL out, and console FTLs into the map
/// arrive inside it. One per map; position is just the entity's transform.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FTLZoneComponent : Component
{
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float Radius = 96f;
}
