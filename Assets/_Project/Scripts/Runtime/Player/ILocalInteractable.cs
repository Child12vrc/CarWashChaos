namespace CarWash.Player
{
    /// <summary>M4.9 G3: an E-interaction that only concerns the local player (opens a UI, no network call), e.g. the wardrobe locker.
    /// Found by PlayerInteractor like any IInteractable but needs no NetworkObject; <see cref="InteractLocal"/> runs on the owner.</summary>
    public interface ILocalInteractable : IInteractable
    {
        void InteractLocal(PlayerNet me);
    }
}
