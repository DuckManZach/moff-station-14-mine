using Robust.Shared.Serialization;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Preferences.Loadouts;

public sealed partial class LoadoutPrototype : ISerializationHooks
{
    /// Equips the entity whose ID matches this loadout's into this slot when equipment is empty.
    [DataField]
    public string? Slot;

    void ISerializationHooks.AfterDeserialization()
    {
        if (Slot != null && Equipment.Count == 0)
            Equipment[Slot] = ID;
    }
}
