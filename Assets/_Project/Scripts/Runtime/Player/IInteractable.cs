namespace CarWash.Player
{
    /// <summary>Something the player can use with E (TECH S02). Interact runs on the SERVER.</summary>
    public interface IInteractable
    {
        string Prompt { get; }
        bool CanInteract(PlayerNet player);
        void Interact(PlayerNet player);
    }
}
