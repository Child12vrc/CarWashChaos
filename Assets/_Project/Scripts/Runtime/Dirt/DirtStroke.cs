using Unity.Netcode;
using UnityEngine;

namespace CarWash.Dirt
{
    public enum StrokeType : byte { Clean = 0, ReDirt = 1, Foam = 2 }

    /// <summary>
    /// One brush stamp in the vehicle's OBJECT space (TECH S04). Strength per channel: x mud, y grease, z spots (re-dirt only), w towel.
    /// Network-serializable for DirtNetSync (half precision would be enough; floats keep it simple).
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

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref pos);
            s.SerializeValue(ref radius);
            s.SerializeValue(ref strength);
            s.SerializeValue(ref type);
            s.SerializeValue(ref source);
        }
    }
}
