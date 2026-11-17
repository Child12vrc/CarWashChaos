using UnityEngine;

namespace CarWash.Player
{
    /// <summary>
    /// P3 ragdoll pose codec. Quaternions are packed "smallest three" into 32 bits (2-bit index of the dropped largest
    /// component + 3 × 10-bit components in [-1/√2, 1/√2]) → ~0.1-0.2° error. A full pose = hips world position
    /// (3 floats) + 11 bone WORLD rotations (hips first) = 12 + 11·4 = 56 bytes. Pure → EditMode tested.
    /// </summary>
    public static class RagdollPose
    {
        public const int BoneCount = 11;           // world rotations, [0] = hips
        public const int BytesPerPose = 12 + BoneCount * 4;
        const float Range = 0.70710678f;           // 1/√2
        const int Bits = 10, Max = (1 << Bits) - 1;

        public static uint Pack(Quaternion q)
        {
            q = Normalize(q);
            float[] c = { q.x, q.y, q.z, q.w };
            int largest = 0;
            for (int i = 1; i < 4; i++) if (Mathf.Abs(c[i]) > Mathf.Abs(c[largest])) largest = i;
            float sign = c[largest] < 0f ? -1f : 1f;
            uint packed = (uint)largest << 30;
            int shift = 20;
            for (int i = 0; i < 4; i++)
            {
                if (i == largest) continue;
                float v = Mathf.Clamp(c[i] * sign, -Range, Range);
                uint u = (uint)Mathf.RoundToInt((v + Range) / (2f * Range) * Max);
                packed |= u << shift;
                shift -= Bits;
            }
            return packed;
        }

        public static Quaternion Unpack(uint packed)
        {
            int largest = (int)(packed >> 30);
            float[] c = new float[4];
            int shift = 20;
            float sum = 0f;
            for (int i = 0; i < 4; i++)
            {
                if (i == largest) continue;
                uint u = (packed >> shift) & Max;
                c[i] = u / (float)Max * 2f * Range - Range;
                sum += c[i] * c[i];
                shift -= Bits;
            }
            c[largest] = Mathf.Sqrt(Mathf.Max(0f, 1f - sum));
            return new Quaternion(c[0], c[1], c[2], c[3]);
        }

        static Quaternion Normalize(Quaternion q)
        {
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return m < 1e-6f ? Quaternion.identity : new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
        }
    }
}
