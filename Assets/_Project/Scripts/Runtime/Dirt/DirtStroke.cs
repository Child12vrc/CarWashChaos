using Unity.Netcode;
using UnityEngine;

namespace CarWash.Dirt
{
    public enum StrokeType : byte { Clean = 0, ReDirt = 1, Foam = 2 }

    /// <summary>
    /// One brush stamp in the vehicle's OBJECT space (TECH S04). Strength per channel: x mud, y grease, z spots (re-dirt only), w towel.
    /// Network-serializable for DirtNetSync. M4.7 N2: sent as half floats (18 B instead of 34 B: object-space positions on a
    /// car are within ±10 m → ~5 mm steps); the server paints the same quantized stroke (Quantized) so every peer matches.
    /// </summary>
    public struct DirtStroke : INetworkSerializable
    {
        public Vector3 pos;
        public float radius;
        public Vector4 strength;
        public StrokeType type;
        /// <summary>P1: client that caused it (clientId+1, 0 = server/none) — that client already painted it locally (prediction).</summary>
        public byte source;

        public DirtStroke(Vector3 objectPos, float radius, Vector4 strength, StrokeType type)
        {
            pos = objectPos; this.radius = radius; this.strength = strength; this.type = type; source = 0;
        }

        /// <summary>The stroke as every receiver will decode it (half precision) — the server paints this one too.</summary>
        public DirtStroke Quantized()
        {
            static float H(float v) => Mathf.HalfToFloat(Mathf.FloatToHalf(v));
            var q = this;
            q.pos = new Vector3(H(pos.x), H(pos.y), H(pos.z)); q.radius = H(radius);
            q.strength = new Vector4(H(strength.x), H(strength.y), H(strength.z), H(strength.w));
            return q;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            ushort px = 0, py = 0, pz = 0, r = 0, sx = 0, sy = 0, sz = 0, sw = 0;
            if (s.IsWriter)
            {
                px = Mathf.FloatToHalf(pos.x); py = Mathf.FloatToHalf(pos.y); pz = Mathf.FloatToHalf(pos.z); r = Mathf.FloatToHalf(radius);
                sx = Mathf.FloatToHalf(strength.x); sy = Mathf.FloatToHalf(strength.y); sz = Mathf.FloatToHalf(strength.z); sw = Mathf.FloatToHalf(strength.w);
            }
            s.SerializeValue(ref px); s.SerializeValue(ref py); s.SerializeValue(ref pz); s.SerializeValue(ref r);
            s.SerializeValue(ref sx); s.SerializeValue(ref sy); s.SerializeValue(ref sz); s.SerializeValue(ref sw);
            s.SerializeValue(ref type);
            s.SerializeValue(ref source);
            if (s.IsReader)
            {
                pos = new Vector3(Mathf.HalfToFloat(px), Mathf.HalfToFloat(py), Mathf.HalfToFloat(pz)); radius = Mathf.HalfToFloat(r);
                strength = new Vector4(Mathf.HalfToFloat(sx), Mathf.HalfToFloat(sy), Mathf.HalfToFloat(sz), Mathf.HalfToFloat(sw));
            }
        }
    }
}
