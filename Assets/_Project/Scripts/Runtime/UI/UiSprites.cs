using UnityEngine;

namespace CarWash.UI
{
    /// <summary>
    /// M4.9 G6: small procedural UI sprites (no art file needed): an anti-aliased ring for radial timers, a filled disc,
    /// an edge arrow and an angry face. Created once, cached. Listed in docs/ART_REQUESTS.md as replaceable.
    /// </summary>
    public static class UiSprites
    {
        static Sprite _ring, _disc, _arrow, _angry;

        static Texture2D Tex(int s) => new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "UiSprite" };
        static Sprite Make(Texture2D t) { t.Apply(false, true); return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f); }

        /// <summary>White ring, outer radius = edge, thickness ≈ 16 % of the size.</summary>
        public static Sprite Ring
        {
            get
            {
                if (_ring != null) return _ring;
                const int S = 128; var t = Tex(S); var px = new Color32[S * S];
                float c = (S - 1) * 0.5f, ro = S * 0.5f - 1.5f, ri = ro - S * 0.16f;
                for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(ro - d + 0.5f) * Mathf.Clamp01(d - ri + 0.5f);
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
                t.SetPixels32(px); return _ring = Make(t);
            }
        }

        public static Sprite Disc
        {
            get
            {
                if (_disc != null) return _disc;
                const int S = 128; var t = Tex(S); var px = new Color32[S * S];
                float c = (S - 1) * 0.5f, r = S * 0.5f - 1.5f;
                for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d + 0.5f) * 255));
                }
                t.SetPixels32(px); return _disc = Make(t);
            }
        }

        /// <summary>A white arrow pointing up (+Y); rotate the Image to aim.</summary>
        public static Sprite Arrow
        {
            get
            {
                if (_arrow != null) return _arrow;
                const int S = 64; var t = Tex(S); var px = new Color32[S * S];
                for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                {
                    // triangle: tip (32, 60), base y = 8 from x 6..58; soft edge
                    float fy = (y - 8f) / 52f;                 // 0 at base … 1 at tip
                    float half = 26f * (1f - fy);
                    float inside = Mathf.Min(half - Mathf.Abs(x - 31.5f), y - 8f, 60f - y);
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(inside + 0.5f) * 255));
                }
                t.SetPixels32(px); return _arrow = Make(t);
            }
        }

        /// <summary>Angry face (red disc, slanted brows, frown) — shown when a customer is about to leave.</summary>
        public static Sprite Angry
        {
            get
            {
                if (_angry != null) return _angry;
                const int S = 96; var t = Tex(S); var px = new Color32[S * S];
                float c = (S - 1) * 0.5f, r = S * 0.5f - 2f;
                var face = new Color(0.95f, 0.3f, 0.22f); var dark = new Color(0.25f, 0.05f, 0.05f);
                for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    var col = Color.Lerp(face, face * 0.75f, Mathf.Clamp01((d - r * 0.7f) / (r * 0.3f)));
                    float u = (x - c) / r, v = (y - c) / r;            // -1..1, v up
                    // eyes
                    if (Dist(u, v, -0.34f, 0.12f) < 0.11f || Dist(u, v, 0.34f, 0.12f) < 0.11f) col = dark;
                    // brows: slanting down toward the middle
                    if (Seg(u, v, -0.62f, 0.42f, -0.14f, 0.26f) < 0.07f || Seg(u, v, 0.62f, 0.42f, 0.14f, 0.26f) < 0.07f) col = dark;
                    // frown: an arc
                    float m = Dist(u, v, 0f, -0.75f);
                    if (Mathf.Abs(m - 0.42f) < 0.065f && v > -0.42f && v < -0.25f) col = dark;
                    col.a = a;
                    px[y * S + x] = col;
                }
                t.SetPixels32(px); return _angry = Make(t);
            }
        }

        static float Dist(float u, float v, float x, float y) => Mathf.Sqrt((u - x) * (u - x) + (v - y) * (v - y));
        static float Seg(float u, float v, float ax, float ay, float bx, float by)
        {
            var p = new Vector2(u, v); var a = new Vector2(ax, ay); var b = new Vector2(bx, by);
            float h = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
            return (p - a - (b - a) * h).magnitude;
        }
    }
}
