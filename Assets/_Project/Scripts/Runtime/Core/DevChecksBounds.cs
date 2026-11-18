using System.Collections;
using System.Linq;
using CarWash.Player;
using UnityEngine;
using UnityEngine.AI;

namespace CarWash.Core
{
    /// <summary>
    /// Play-area walls check, client side (-autotest -autotest-bounds; the hosting exe only observes). Tries to leave the lot
    /// every way a player could: run + sprint + jump outward from every edge (both car road openings included), jump off the
    /// dumpster over the north fence, a full charged throw of a prop outward, a ragdoll launched outward, and a critter NavMesh
    /// path to outside. Every check must end inside PlayArea. Then 8 eye-level screenshots looking out from the edges.
    /// Logged as [AUTOTEST] bounds:.
    /// </summary>
    public static class DevChecksBounds
    {
        static void Log(string m) => DevAutopilot.Log("bounds: " + m);
        static int _fails;

        public static IEnumerator HostIdle() { yield break; }

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            input.scripted = true;
            _fails = 0;
            yield return new WaitForSeconds(1.5f);
            float z = -15.5f; // road centre
            var runs = new (string name, Vector3 from, float yaw)[]
            {
                ("west road opening", new Vector3(-24f, 0.05f, z), 270f), ("east road opening", new Vector3(34f, 0.05f, z), 90f),
                ("west", new Vector3(-24f, 0.05f, 2f), 270f), ("east (dev yard end)", new Vector3(34f, 0.05f, 2f), 90f),
                ("south across the road", new Vector3(4f, 0.05f, -12f), 180f), ("north (dev yard end)", new Vector3(33f, 0.05f, 14f), 0f),
                ("north alley", new Vector3(-4f, 0.05f, 15f), 0f),
                ("SW corner", new Vector3(-24f, 0.05f, -17f), 225f), ("SE corner", new Vector3(34f, 0.05f, -17f), 135f),
                ("NW corner", new Vector3(-24f, 0.05f, 15f), 315f), ("NE corner", new Vector3(34f, 0.05f, 15f), 45f),
            };
            foreach (var r in runs) yield return RunOut(me, input, r.name, r.from, r.yaw);
            yield return DumpsterJump(me, input);
            yield return Throw(me, input, "throw west through the road opening", new Vector3(-23f, 0.05f, z), 270f);
            yield return Throw(me, input, "throw south over the wall", new Vector3(2f, 0.05f, -17.5f), 180f);
            yield return Launch(me, "ragdoll launched west down the road", new Vector3(-24f, 0.05f, z), new Vector3(-26f, 9f, 0f));
            yield return Launch(me, "ragdoll launched north over the fence", new Vector3(10f, 0.05f, 16.5f), new Vector3(0f, 14f, 22f));
            CritterPaths();
            yield return Screens(me);
            Log(_fails == 0 ? "PASS — nothing left the play area" : $"FAIL — {_fails} escapes");
            input.scripted = false;
        }

        static void Check(string what, Vector3 p)
        {
            bool inside = PlayArea.Contains(p, -0.6f) && p.y < PlayArea.WallHeight && p.y > -1.5f;
            if (!inside) _fails++;
            Log($"{what}: end {p.ToString("F1")} → {(inside ? "inside" : "OUTSIDE")}");
        }

        static IEnumerator RunOut(PlayerNet me, PlayerInputReader input, string name, Vector3 from, float yaw)
        {
            me.Motor.Teleport(from);
            me.Look.SetYaw(yaw); me.Look.SetPitch(0f);
            yield return new WaitForSeconds(0.3f);
            float t0 = Time.time, far = 0f;
            var dir = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            while (Time.time - t0 < 6f)
            {
                input.sMove = new Vector2(0f, 1f); input.sSprint = true;
                if (Time.frameCount % 20 == 0) input.sJump = true;
                me.Look.SetYaw(yaw);
                far = Mathf.Max(far, Vector3.Dot(me.transform.position - from, dir));
                yield return null;
            }
            input.sMove = Vector2.zero; input.sSprint = false;
            yield return new WaitForSeconds(0.4f);
            Check($"run+jump {name} (got {far:0.0} m out from the start)", me.transform.position);
        }

        static IEnumerator DumpsterJump(PlayerNet me, PlayerInputReader input)
        {
            // dumpster top 1.5 m by the north fence (2 m): stand on it and jump north repeatedly
            me.Motor.Teleport(new Vector3(16f, 1.6f, 15.2f));
            me.Look.SetYaw(0f);
            yield return new WaitForSeconds(0.4f);
            float t0 = Time.time, maxY = 0f;
            while (Time.time - t0 < 4f)
            {
                input.sMove = new Vector2(0f, 1f); input.sSprint = true;
                if (Time.frameCount % 15 == 0) input.sJump = true;
                maxY = Mathf.Max(maxY, me.transform.position.y);
                yield return null;
            }
            input.sMove = Vector2.zero; input.sSprint = false;
            yield return new WaitForSeconds(0.5f);
            Check($"jump off the dumpster over the north fence (peak y {maxY:0.0})", me.transform.position);
        }

        static IEnumerator Throw(PlayerNet me, PlayerInputReader input, string name, Vector3 from, float yaw)
        {
            var grabber = me.GetComponent<PlayerGrabber>();
            var prop = Object.FindObjectsByType<Grabbable>(FindObjectsSortMode.None)
                .Where(g => !g.IsHeld && g.Mass <= 4f && g.GetComponent<Tools.ToolBase>() == null && g.GetComponent<Critters.CritterBase>() == null)
                .OrderBy(g => (g.transform.position - from).sqrMagnitude).FirstOrDefault();
            if (prop == null) { Log($"{name}: no light prop found"); yield break; }
            // bring the prop to us (server owns it; teleport ourselves next to it, grab, then walk to the edge)
            var pp = prop.transform.position;
            me.Motor.Teleport(new Vector3(pp.x, 0.05f, pp.z) - new Vector3(0f, 0f, 1.5f));
            yield return new WaitForSeconds(0.3f);
            me.Look.LookAt(prop.GetComponent<Collider>().bounds.center);
            yield return new WaitForSeconds(0.2f);
            input.sGrab = true; yield return new WaitForSeconds(0.3f); input.sGrab = false;
            yield return new WaitForSeconds(0.5f);
            if (grabber.Held != prop) { Log($"{name}: could not grab {prop.name}"); yield break; }
            // carry it to the edge in small steps (a jump of more than 3 m would make the grab let go)
            var cur = me.transform.position;
            while ((new Vector3(from.x, 0, from.z) - new Vector3(cur.x, 0, cur.z)).magnitude > 0.4f)
            {
                cur = Vector3.MoveTowards(cur, from, 0.12f); cur.y = 0.05f;
                me.Motor.Teleport(cur);
                yield return new WaitForFixedUpdate();
            }
            yield return new WaitForSeconds(0.5f);
            if (grabber.Held != prop) { Log($"{name}: lost {prop.name} on the way"); yield break; }
            me.Look.SetYaw(yaw); me.Look.SetPitch(-35f);
            yield return new WaitForSeconds(0.3f);
            input.sGrab = true; yield return new WaitForSeconds(1.2f); // full charge
            float charge = grabber.Charge;
            input.sGrab = false;
            yield return new WaitForSeconds(4f);
            Check($"{name}: {prop.name} ({prop.Mass} kg, charge {charge:0.00})", prop.transform.position);
        }

        static IEnumerator Launch(PlayerNet me, string name, Vector3 from, Vector3 velocity)
        {
            var rag = me.GetComponent<PlayerRagdoll>();
            me.Motor.Teleport(from);
            yield return new WaitForSeconds(0.4f);
            rag.OwnerKnocked(velocity);
            float t0 = Time.time; var peak = from;
            while (Time.time - t0 < 3.2f) { if (rag.Hips.position.y > peak.y) peak = rag.Hips.position; yield return null; }
            Check($"{name} ({velocity.magnitude:0} m/s, peak y {peak.y:0.0})", rag.Hips.position);
            float tw = Time.time; while (rag.ControlsBody && Time.time - tw < 8f) yield return null;
            Check($"{name} — standing again", me.transform.position);
        }

        static void CritterPaths()
        {
            var inside = new Vector3(0f, 0f, -9f);
            foreach (var target in new[] { new Vector3(-34f, 0f, -15.5f), new Vector3(37.5f, 0f, -21f), new Vector3(-33f, 0f, 5f), new Vector3(0f, 0f, -21.5f) })
            {
                if (!NavMesh.SamplePosition(inside, out var a, 2f, NavMesh.AllAreas)) { Log("critter path: no NavMesh in the lot?"); _fails++; return; }
                bool hasEnd = NavMesh.SamplePosition(target, out var b, 1.5f, NavMesh.AllAreas);
                var path = new NavMeshPath();
                bool ok = hasEnd && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
                var end = ok ? path.corners[path.corners.Length - 1] : a.position;
                bool escaped = ok && !PlayArea.Contains(end, -0.6f);
                if (escaped) _fails++;
                Log($"critter NavMesh path lot → {target.ToString("F0")}: {(hasEnd ? path.status.ToString() : "no NavMesh out there")} → {(escaped ? "ESCAPES" : "blocked")}");
            }
        }

        static IEnumerator Screens(PlayerNet me)
        {
            var spots = new (Vector3 p, float yaw)[]
            {
                (new Vector3(-26.5f, 0.05f, -15.5f), 270f), (new Vector3(36.5f, 0.05f, -15.5f), 90f), (new Vector3(4f, 0.05f, -19.3f), 180f),
                (new Vector3(-26.5f, 0.05f, 16.8f), 315f), (new Vector3(36.5f, 0.05f, 16.8f), 45f), (new Vector3(-26.5f, 0.05f, 2f), 270f),
                (new Vector3(36.5f, 0.05f, 2f), 90f), (new Vector3(-6f, 0.05f, 16.8f), 0f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                me.Motor.Teleport(spots[i].p);
                me.Look.SetYaw(spots[i].yaw); me.Look.SetPitch(-6f);
                yield return new WaitForSeconds(0.6f);
                ScreenCapture.CaptureScreenshot($"autotest_bounds_{i}_{spots[i].yaw:000}.png");
                yield return null;
            }
        }
    }
}
