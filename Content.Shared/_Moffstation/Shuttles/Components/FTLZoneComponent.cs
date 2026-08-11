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

    /// <summary>
    /// What the zone is drawn in on the console. Science's #6b57c8 pushed towards magenta and brightened, since it
    /// sits on a near-black radar backdrop.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public Color Color = Color.FromHex("#d866eb");
}
