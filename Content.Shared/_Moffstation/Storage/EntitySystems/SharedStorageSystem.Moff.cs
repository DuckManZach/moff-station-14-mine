namespace Content.Shared.Storage.EntitySystems;

public abstract partial class SharedStorageSystem
{
    /// <summary>
    /// Puts a stored item into the player's hand, playing the storage's remove sound.
    /// </summary>
    public bool PlayerTakeOutItem(Entity<StorageComponent> storage, EntityUid player, EntityUid item)
    {
        if (!_sharedHandsSystem.TryPickupAnyHand(player, item))
            return false;

        if (storage.Comp.StorageRemoveSound != null
            && !_tag.HasTag(player, storage.Comp.SilentStorageUserTag))
        {
            Audio.PlayPredicted(storage.Comp.StorageRemoveSound, storage, player, _audioParams);
        }

        return true;
    }
}
