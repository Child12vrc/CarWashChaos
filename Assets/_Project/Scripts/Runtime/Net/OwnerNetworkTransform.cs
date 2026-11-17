using Unity.Netcode.Components;

namespace CarWash.Net
{
    /// <summary>Owner-authoritative NetworkTransform (TECH S01: player AuthorityMode = Owner).</summary>
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
