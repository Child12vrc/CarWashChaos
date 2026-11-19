using System.Collections.Generic;
using System.IO;
using System.Linq;
using CarWash.Core;
using CarWash.Vehicles;
using UnityEditor;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>
    /// M4.6 D3: car interiors. Until the user's interior models arrive, every vehicle gets a procedural cabin under "Interior":
    /// carpet floor, two front seats (driver on the left), a rear bench when the cabin is long enough, dashboard, steering
    /// wheel + column, centre console, and an inner trim panel (+ armrest) on each side door that swings with the door.
    /// Trash spots on the seats / floor tell VehicleSpawner where interior trash lies.
    /// Swap-in: if Art/Models/Interiors/Interior_&lt;id&gt;.glb (or Interior_Default.glb) exists, it is used instead (origin = the
    /// cabin floor centre, +Z forward, scaled to the cabin width), and the same trash spots are generated over it.
    /// No colliders (doors, washing and trash stay as before).
    /// </summary>
    public static class VehicleInteriorBuilder
    {
        public const string ModelDir = "Assets/_Project/Art/Models/Interiors";

        /// <summary>Cabin box in vehicle-local space: x half-width, floor / belt / roof heights, rear / front z.</summary>
        public struct Cabin { public float halfW, floorY, beltY, roofY, rearZ, frontZ; }

        static readonly HashSet<string> NoCabin = new HashSet<string> { "V08", "B03", "B04" };

        public static Transform[] Build(Transform root, string id, Cabin c)
        {
            var group = new GameObject("Interior") { layer = Layers.Vehicle }.transform;
            group.SetParent(root, false);
            string custom = new[] { $"{ModelDir}/Interior_{id}.glb", $"{ModelDir}/Interior_Default.glb" }.FirstOrDefault(File.Exists);
            // Batch F #8: the tank, airboat and UFO have no cabin — no procedural seats inside the hull / dome (ART_IMPORT)
            if (custom == null && NoCabin.Contains(id)) return System.Array.Empty<Transform>();
            if (custom != null && AssetDatabase.LoadAssetAtPath<GameObject>(custom) != null)
            {
                var m = GlbKit.Model(custom, group, Layers.Vehicle, new Vector3(0f, c.floorY, (c.rearZ + c.frontZ) * 0.5f));
                var b = GlbKit.LocalBounds(m, group);
                m.transform.localScale = Vector3.one * (c.halfW * 2f / Mathf.Max(0.1f, b.size.x));
                GlbAnchors(group, m.transform, c);
            }
            else Procedural(group, id, c);
            // v2 vehicles (Batch F #7) carry their own inner door cards (DoorDetail_* under each door): no InteriorDoors / DoorTrim
            bool v2Cards = root.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("DoorDetail_"));
            if (!v2Cards && !GlbDoorCards(root, id)) DoorTrim(root);
            return TrashSpots(group, c);
        }

        public static Transform Deep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform ch in t) { var r = Deep(ch, name); if (r != null) return r; }
            return null;
        }

        /// <summary>
        /// Blender interiors (docs/ART_IMPORT.md, Batch F #1): the same anchors the procedural cabin makes, from the model's
        /// empties — "DriverSeat" = cushion top under <c>Socket_DriverHips</c> (the hip joint is 0.09 m above it), its "Roof"
        /// child at the headliner, and "SteeringWheel" / "Hand_L" / "Hand_R" refs used by CustomerDriver.
        /// </summary>
        static void GlbAnchors(Transform group, Transform model, Cabin c)
        {
            var hips = Deep(model, "Socket_DriverHips");
            if (hips == null) { Debug.LogWarning($"[Interior] {model.name}: no Socket_DriverHips"); return; }
            var seat = new GameObject("DriverSeat") { layer = Layers.Vehicle }.transform; seat.SetParent(group, false);
            seat.position = hips.position - group.up * (GlbHipHeight * model.lossyScale.y);
            seat.localRotation = Quaternion.identity; // facing the vehicle's +Z like the procedural seat
            var roof = new GameObject("Roof") { layer = Layers.Vehicle }.transform; roof.SetParent(seat, false);
            roof.localPosition = new Vector3(0f, c.roofY - seat.localPosition.y - 0.05f, 0f);
        }

        /// <summary>The Socket_DriverHips joint height above the cushion in the Blender interiors.</summary>
        public const float GlbHipHeight = 0.09f;

        /// <summary>InteriorDoors_&lt;id&gt;.glb: inner door cards authored in the vehicle frame with their origin at each door
        /// pivot → parented under the matching Door_* node (local pose 0, scale 1) so they swing with the door.</summary>
        static bool GlbDoorCards(Transform root, string id)
        {
            string path = $"{ModelDir}/InteriorDoors_{id}.glb";
            if (!File.Exists(path) || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return false;
            var holder = new GameObject("InteriorDoorsTmp").transform; holder.SetParent(root, false);
            var m = GlbKit.Model(path, holder, Layers.Vehicle, Vector3.zero);
            int n = 0;
            foreach (var panel in m.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("DoorPanel_")).ToArray())
            {
                var door = Deep(root, "Door_" + panel.name.Substring("DoorPanel_".Length));
                if (door == null) { Debug.LogWarning($"[Interior] {id}: no {("Door_" + panel.name.Substring(10))} for {panel.name}"); continue; }
                panel.SetParent(door, false);
                panel.localPosition = Vector3.zero; panel.localRotation = Quaternion.identity; panel.localScale = Vector3.one;
                foreach (var r in panel.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                n++;
            }
            Object.DestroyImmediate(holder.gameObject);
            return n > 0;
        }

        static Material M(string n, Color col, float smooth = 0.15f) => GrayboxMaterials.Get("Interior_" + n, col, smooth);

        static GameObject Box(Transform p, string n, Vector3 c, Vector3 s, Material m, Vector3 euler = default)
        {
            var go = GrayboxMaterials.Box(p, n, c, s, m, collider: false, layer: Layers.Vehicle);
            go.transform.localRotation = Quaternion.Euler(euler);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static void Procedural(Transform g, string id, Cabin c)
        {
            // seat fabric colour varies a little per vehicle type
            var palette = new[] { new Color(0.25f, 0.27f, 0.32f), new Color(0.38f, 0.3f, 0.24f), new Color(0.22f, 0.24f, 0.22f), new Color(0.32f, 0.22f, 0.22f) };
            int pick = id.Sum(ch => ch) % palette.Length; // stable per id
            // M4.7 A1: seat fabric weave (fabric_pattern_05: normal + AO/roughness only, colour = tint) and a dirty carpet floor
            var fabric = GrayboxMaterials.Pbr("Interior_Seat" + pick, palette[pick], "fabric_pattern_05", 0f, useAlbedo: false); // prefab content: plain cube UVs
            var carpet = GrayboxMaterials.Pbr("Interior_Carpet", new Color(0.62f, 0.62f, 0.64f), "dirty_carpet", 0f);
            var plastic = M("Dash", new Color(0.09f, 0.09f, 0.1f), 0.35f);
            var trim = M("Trim", new Color(0.55f, 0.55f, 0.57f), 0.4f);
            float len = c.frontZ - c.rearZ, w = c.halfW * 2f;
            float seatH = Mathf.Clamp((c.beltY - c.floorY) * 0.45f, 0.25f, 0.42f);
            Box(g, "Floor", new Vector3(0, c.floorY, (c.rearZ + c.frontZ) * 0.5f), new Vector3(w, 0.04f, len), carpet);
            // dashboard across the front, a little under the belt line, with the instrument hood on the driver's side
            float dashZ = c.frontZ - 0.18f;
            Box(g, "Dashboard", new Vector3(0, c.beltY - 0.12f, dashZ), new Vector3(w, 0.26f, 0.36f), plastic);
            Box(g, "DashLower", new Vector3(0, (c.floorY + c.beltY - 0.25f) * 0.5f, c.frontZ - 0.1f), new Vector3(w, c.beltY - 0.25f - c.floorY, 0.2f), plastic);
            float driverX = -c.halfW * 0.48f;
            Box(g, "InstrumentHood", new Vector3(driverX, c.beltY + 0.04f, dashZ - 0.02f), new Vector3(0.36f, 0.08f, 0.2f), plastic);
            // steering wheel: a thin disc facing the driver + column
            var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(wheel.GetComponent<Collider>());
            wheel.name = "SteeringWheel"; wheel.layer = Layers.Vehicle;
            wheel.transform.SetParent(g, false);
            wheel.transform.localPosition = new Vector3(driverX, c.beltY - 0.02f, dashZ - 0.36f);
            wheel.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
            wheel.transform.localScale = new Vector3(0.36f, 0.015f, 0.36f);
            wheel.GetComponent<MeshRenderer>().sharedMaterial = plastic;
            Box(g, "Column", new Vector3(driverX, c.beltY - 0.07f, dashZ - 0.22f), new Vector3(0.06f, 0.06f, 0.26f), plastic, new Vector3(-20f, 0, 0));
            // front seats + console
            float frontSeatZ = dashZ - 0.75f;
            foreach (float sx in new[] { -1f, 1f })
                Seat(g, sx < 0 ? "SeatDriver" : "SeatPassenger", new Vector3(sx * c.halfW * 0.48f, c.floorY, frontSeatZ), Mathf.Min(0.52f, c.halfW * 0.8f), seatH, fabric);
            // M4.8 F4: where the customer sits (cushion top, facing forward) — CustomerDriver puts a character here
            var seat = new GameObject("DriverSeat") { layer = Layers.Vehicle }.transform; seat.SetParent(g, false);
            seat.localPosition = new Vector3(driverX, c.floorY + seatH, frontSeatZ - 0.04f);
            var roof = new GameObject("Roof") { layer = Layers.Vehicle }.transform; roof.SetParent(seat, false);
            roof.localPosition = new Vector3(0f, c.roofY - (c.floorY + seatH) - 0.05f, 0f); // headliner: the driver's head stays under it
            Box(g, "Console", new Vector3(0, c.floorY + seatH * 0.6f, frontSeatZ + 0.25f), new Vector3(0.2f, seatH * 1.1f, 0.7f), plastic);
            // rear bench when the cabin is long enough
            if (frontSeatZ - c.rearZ > 0.9f)
                Seat(g, "RearBench", new Vector3(0, c.floorY, Mathf.Max(c.rearZ + 0.4f, frontSeatZ - 0.95f)), w - 0.12f, seatH, fabric);
            // rear parcel shelf / bulkhead so the cabin reads as closed from the windows
            Box(g, "RearPanel", new Vector3(0, (c.floorY + c.beltY) * 0.5f, c.rearZ + 0.03f), new Vector3(w, c.beltY - c.floorY, 0.05f), trim);
        }

        static void Seat(Transform g, string n, Vector3 at, float width, float seatH, Material m)
        {
            var s = new GameObject(n) { layer = Layers.Vehicle }.transform; s.SetParent(g, false); s.localPosition = at;
            Box(s, "Base", new Vector3(0, seatH * 0.5f, 0), new Vector3(width, seatH, 0.5f), m);
            Box(s, "Back", new Vector3(0, seatH + 0.32f, -0.28f), new Vector3(width, 0.64f, 0.12f), m, new Vector3(-12f, 0, 0));
            Box(s, "Headrest", new Vector3(0, seatH + 0.72f, -0.34f), new Vector3(Mathf.Min(width, 0.28f), 0.16f, 0.1f), m, new Vector3(-12f, 0, 0));
        }

        /// <summary>Inner trim panel + armrest on each side door (children of the hinge node → they swing with the door).</summary>
        static void DoorTrim(Transform root)
        {
            var trim = M("DoorTrim", new Color(0.3f, 0.3f, 0.33f), 0.3f);
            var arm = M("Armrest", new Color(0.18f, 0.18f, 0.2f));
            foreach (var door in root.GetComponentsInChildren<VehicleDoor>(true))
            {
                if (door.IsTrunk) continue;
                var t = door.transform;
                var mf = t.GetComponent<MeshFilter>();
                Bounds b;
                if (mf != null && mf.sharedMesh != null) b = mf.sharedMesh.bounds;
                else { var bc = t.GetComponent<BoxCollider>(); if (bc == null) continue; b = new Bounds(bc.center, bc.size); }
                // the car's centre is inward: sign of the door's local x in the vehicle
                float inward = -Mathf.Sign(root.InverseTransformPoint(t.position).x);
                var inner = new Vector3(b.center.x + inward * (b.extents.x + 0.012f), b.center.y - b.size.y * 0.12f, b.center.z);
                Box(t, "InnerTrim", inner, new Vector3(0.02f, b.size.y * 0.62f, b.size.z * 0.86f), trim).layer = t.gameObject.layer;
                Box(t, "Armrest", inner + new Vector3(inward * 0.04f, b.size.y * 0.05f, 0f), new Vector3(0.07f, 0.05f, b.size.z * 0.4f), arm).layer = t.gameObject.layer;
            }
        }

        static Transform[] TrashSpots(Transform g, Cabin c)
        {
            float seatTop = c.floorY + Mathf.Clamp((c.beltY - c.floorY) * 0.45f, 0.25f, 0.42f) + 0.05f;
            float dashZ = c.frontZ - 0.18f, frontSeatZ = dashZ - 0.75f;
            var pts = new List<Vector3>
            {
                new Vector3(c.halfW * 0.48f, seatTop, frontSeatZ),             // passenger seat
                new Vector3(c.halfW * 0.45f, c.floorY + 0.05f, frontSeatZ + 0.45f),  // passenger footwell
            };
            if (frontSeatZ - c.rearZ > 0.9f)
            {
                float rz = Mathf.Max(c.rearZ + 0.4f, frontSeatZ - 0.95f);
                pts.Add(new Vector3(-c.halfW * 0.4f, seatTop, rz));
                pts.Add(new Vector3(c.halfW * 0.4f, seatTop, rz));
                pts.Add(new Vector3(0f, c.floorY + 0.05f, rz + 0.45f));
            }
            return pts.Select((p, i) =>
            {
                var t = new GameObject("TrashSpot" + i).transform; t.SetParent(g, false); t.localPosition = p; return t;
            }).ToArray();
        }
    }
}
