using Content.Shared.Preferences.Loadouts;
using Robust.Shared.Prototypes;

namespace Content.Shared._Moffstation.Preferences.Loadouts;

public static class MoffLoadouts
{
    /// <summary>
    /// Loadout applied to every character on top of their job loadout. A group the job loadout already fills is
    /// dropped from it at spawn, so the job's selection wins.
    /// </summary>
    public static readonly ProtoId<RoleLoadoutPrototype> Universal = "MoffUniversal";
}
