using Content.Shared.Hands.Components;

namespace Content.Shared.Storage.EntitySystems;

public abstract partial class SharedStorageSystem
{
    /// <summary>
    /// Puts an item stored in this storage into the player's hand, playing the storage's remove sound.
    /// </summary>
    public bool PlayerTakeOutItem(Entity<StorageComponent?> storage, Entity<HandsComponent?> player, EntityUid item)
    {
        if (!Resolve(storage, ref storage.Comp, false)
            || !storage.Comp.Container.Contains(item)
            || !_sharedHandsSystem.TryPickupAnyHand(player, item, handsComp: player.Comp))
            return false;

        if (storage.Comp.StorageRemoveSound != null
            && !_tag.HasTag(player, storage.Comp.SilentStorageUserTag))
        {
            Audio.PlayPredicted(storage.Comp.StorageRemoveSound, storage, player, _audioParams);
        }

        return true;
    }
}
