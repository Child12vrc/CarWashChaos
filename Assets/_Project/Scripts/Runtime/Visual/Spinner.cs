using UnityEngine;

namespace CarWash.Visual
{
    /// <summary>Batch F #8: a part that keeps turning about one local axis (the airboat's propeller). Local, decorative.</summary>
    public class Spinner : MonoBehaviour
    {
        [SerializeField] private Vector3 _axis = Vector3.forward;
        [SerializeField] private float _degreesPerSecond = 720f;
        public void Init(Vector3 axis, float dps) { _axis = axis; _degreesPerSecond = dps; }
        void Update() => transform.Rotate(_axis, _degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
