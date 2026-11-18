using UnityEngine;

namespace CarWash.Core
{
    /// <summary>P7: scene content that only exists in the editor / Development builds (the dev yard). Removed in release.</summary>
    public class DevOnlyObject : MonoBehaviour
    {
        void Awake()
        {
            if (!DevFlags.DevContent) Destroy(gameObject);
        }
    }
}
