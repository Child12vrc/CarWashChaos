namespace CarWash.Dirt
{
    /// <summary>TECH S04: GPU UV2 painter (default) or vertex-color fallback.</summary>
    public interface IDirtBackend
    {
        void AddStroke(in DirtStroke stroke);
        /// <summary>1 = spotless (dirt surface only; trash handled by Vehicle).</summary>
        float CleanPct { get; }
        float MudPct { get; }
        float GreasePct { get; }
        bool HasReadback { get; }
        byte[] Serialize();
        void Deserialize(byte[] data);
    }
}
