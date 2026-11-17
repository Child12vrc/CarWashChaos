using UnityEngine;

namespace CarWash.Player
{
    /// <summary>Pure ground-movement math (Quake-style friction/accel). EditMode tested.</summary>
    public static class MotorMath
    {
        /// <summary>Speed below which friction uses this value as control speed (stops crawling).</summary>
        public const float StopSpeed = 1.5f;

        public static Vector3 ApplyFriction(Vector3 horizontalVel, float friction, float dt)
        {
            float speed = horizontalVel.magnitude;
            if (speed < 0.001f) return Vector3.zero;
            float control = Mathf.Max(speed, StopSpeed);
            float newSpeed = Mathf.Max(0f, speed - control * friction * dt);
            return horizontalVel * (newSpeed / speed);
        }

        public static Vector3 Accelerate(Vector3 horizontalVel, Vector3 wishDir, float wishSpeed, float accel, float dt)
        {
            float current = Vector3.Dot(horizontalVel, wishDir);
            float add = wishSpeed - current;
            if (add <= 0f) return horizontalVel;
            float accelSpeed = Mathf.Min(accel * wishSpeed * dt, add);
            return horizontalVel + wishDir * accelSpeed;
        }

        /// <summary>Exponential decay of external (knockback) velocity at `rate` per second.</summary>
        public static Vector3 DecayKnockback(Vector3 ext, float rate, float dt)
        {
            ext *= Mathf.Exp(-rate * dt);
            return ext.sqrMagnitude < 0.0004f ? Vector3.zero : ext;
        }

        /// <summary>Simulates release-and-coast from `speed` on a surface; returns stopping distance (used by tests/tuning).</summary>
        public static float StoppingDistance(float speed, float friction, float dt = 1f / 60f)
        {
            var v = new Vector3(speed, 0f, 0f);
            float dist = 0f;
            for (int i = 0; i < 6000 && v.sqrMagnitude > 0.0001f; i++)
            {
                v = ApplyFriction(v, friction, dt);
                dist += v.magnitude * dt;
            }
            return dist;
        }
    }
}
