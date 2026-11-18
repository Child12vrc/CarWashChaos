using UnityEngine;

namespace CarWash.Visual
{
    /// <summary>M4.9 G9: the ceiling boom arm of a self-wash bay drifts slowly around its pivot (local, decorative).</summary>
    public class BoomSway : MonoBehaviour
    {
        [SerializeField] private float _amplitude = 35f, _period = 14f;
        float _phase;
        void Start() => _phase = (transform.position.x * 0.37f + transform.position.z * 0.11f) % 1f * Mathf.PI * 2f;
        void Update() => transform.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * Mathf.PI * 2f / _period + _phase) * _amplitude, 0f);
    }
}
