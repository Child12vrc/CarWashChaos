using System.Collections.Generic;
using UnityEngine;

namespace CarWash.Net
{
    /// <summary>
    /// P1: judges how a replicated object moves on screen from per-frame samples. Jumps = a frame step much larger than
    /// the typical step (teleport-like pops); rubber-band = the object moves back against its recent travel direction
    /// (snapped back by a correction). Pure → EditMode tested.
    /// </summary>
    public class MotionStats
    {
        readonly List<Vector3> _pos = new List<Vector3>();
        readonly List<float> _dt = new List<float>();

        public void Add(Vector3 p, float dt) { _pos.Add(p); _dt.Add(dt); }
        public int Samples => _pos.Count;

        public float Distance
        {
            get { float d = 0f; for (int i = 1; i < _pos.Count; i++) d += Flat(_pos[i] - _pos[i - 1]).magnitude; return d; }
        }

        /// <summary>Largest single-frame step expressed as speed (m/s).</summary>
        public float MaxStepSpeed
        {
            get { float m = 0f; for (int i = 1; i < _pos.Count; i++) m = Mathf.Max(m, Flat(_pos[i] - _pos[i - 1]).magnitude / Mathf.Max(1e-4f, _dt[i])); return m; }
        }

        /// <summary>Frames whose step speed exceeds `limit` m/s (e.g. 2× the max run speed) → visible pops.</summary>
        public int Jumps(float limit)
        {
            int n = 0;
            for (int i = 1; i < _pos.Count; i++) if (Flat(_pos[i] - _pos[i - 1]).magnitude / Mathf.Max(1e-4f, _dt[i]) > limit) n++;
            return n;
        }

        /// <summary>
        /// Rubber-band events: a step of at least `minStep` m that points more than 120° away from the average direction
        /// of the previous `window` steps (which moved at least 3·minStep in total).
        /// </summary>
        public int Reversals(float minStep = 0.03f, int window = 6)
        {
            int n = 0;
            for (int i = window + 1; i < _pos.Count; i++)
            {
                var step = Flat(_pos[i] - _pos[i - 1]);
                if (step.magnitude < minStep) continue;
                var travel = Flat(_pos[i - 1] - _pos[i - 1 - window]);
                if (travel.magnitude < minStep * 3f) continue;
                if (Vector3.Angle(step, travel) > 120f) n++;
            }
            return n;
        }

        /// <summary>Fraction of frames in which the object moved at all (≈1 = updated every rendered frame; ≈tick/fps = stair-stepping).</summary>
        public float MovingFrameRatio
        {
            get
            {
                if (_pos.Count < 2) return 0f;
                int moving = 0;
                for (int i = 1; i < _pos.Count; i++) if (Flat(_pos[i] - _pos[i - 1]).sqrMagnitude > 1e-8f) moving++;
                return moving / (float)(_pos.Count - 1);
            }
        }

        /// <summary>Median step speed over frames that moved (m/s).</summary>
        public float MedianMovingSpeed
        {
            get
            {
                var s = new List<float>();
                for (int i = 1; i < _pos.Count; i++)
                {
                    float d = Flat(_pos[i] - _pos[i - 1]).magnitude;
                    if (d > 1e-4f) s.Add(d / Mathf.Max(1e-4f, _dt[i]));
                }
                if (s.Count == 0) return 0f;
                s.Sort();
                return s[s.Count / 2];
            }
        }

        public string Summary(float jumpLimit) =>
            $"{Distance:0.0}m moving {MovingFrameRatio:P0} of {Samples - 1} frames, median {MedianMovingSpeed:0.0} max {MaxStepSpeed:0.0} m/s, jumps {Jumps(jumpLimit)}, rubberband {Reversals()}";

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
