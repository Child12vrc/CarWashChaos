using System.Collections.Generic;
using System.Linq;
using CarWash.Core;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>
    /// Blender models (Batch F #3 / #6, docs/ART_IMPORT.md "Lot, critter and scenery models") swapped in for the lot's graybox
    /// pieces after the lot is built. The graybox object keeps its colliders / triggers / components (cage capture, locker
    /// wardrobe trigger, office collider) — only its renderers go — and the model is placed at its bottom centre.
    /// All models face −Z; the office / lockers / day board / terminal / cage face +Z in LotBuilder → turned 180°.
    /// </summary>
    public static class LotModels
    {
        public static readonly List<string> Swapped = new List<string>();

        /// <summary>Replace the look of <paramref name="graybox"/> with Generated/<paramref name="category"/>/<paramref name="file"/>.glb.</summary>
        public static GameObject Swap(GameObject graybox, string category, string file, float yaw, bool centreOrigin = false, int layer = -1)
        {
            if (graybox == null) return null;
            string glb = GlbKit.Find(category, file);
            if (glb == null) return null;
            var rs = graybox.GetComponentsInChildren<MeshRenderer>(true);
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(graybox.transform.position, Vector3.zero);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            foreach (var r in rs)
            {
                var mf = r.GetComponent<MeshFilter>();
                Object.DestroyImmediate(r);
                if (mf != null) Object.DestroyImmediate(mf);
            }
            var holder = new GameObject("Model_" + file) { layer = graybox.layer }.transform;
            holder.SetParent(graybox.transform.parent, true);
            holder.position = centreOrigin ? b.center : new Vector3(b.center.x, b.min.y, b.center.z);
            holder.rotation = Quaternion.Euler(0f, graybox.transform.eulerAngles.y + yaw, 0f);
            var model = GlbKit.Model(glb, holder, layer >= 0 ? layer : graybox.layer, Vector3.zero);
            Swapped.Add(file);
            return model;
        }

        static GameObject Named(Transform env, string n) => env.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n)?.gameObject;

        public static void Apply(Transform env)
        {
            Swapped.Clear();
            var office = Swap(Named(env, "Office"), "Lot", "Office", 180f);
            if (office != null) SignText(office.transform, "사무실");
            Swap(Named(env, "ShopTerminal"), "Lot", "Terminal", 180f);
            Swap(Named(env, "DayBoard"), "Lot", "DayBoard", 180f, centreOrigin: true);
            for (int i = 0; i < 4; i++)
            {
                var box = Named(env, "Locker" + i);
                var locker = box != null ? box.GetComponentInChildren<Player.WardrobeLocker>(true) : null;
                var m = Swap(box, "Lot", "Locker", 180f);
                var door = m != null ? VehicleInteriorBuilder.Deep(m.transform, "Door") : null;
                if (locker != null && door != null) SceneBuilder.Set(locker, "_door", door); // the wardrobe swings it open
            }
            Swap(Named(env, "Dumpster"), "Lot", "Dumpster", 0f);
            Swap(Named(env, "Vent"), "Lot", "Vent", 0f);
            var cage = Named(env, "AnimalCage");
            if (cage != null)
            {
                // the cage is a group: hide every bar / post / roof look, keep the colliders + the AnimalCage capture trigger
                var model = Swap(cage, "Lot", "Cage", 180f);
                if (model != null) SignText(model.transform, "동물 보호소");
            }
            Debug.Log($"[LotModels] swapped: {string.Join(", ", Swapped)}");
        }

        /// <summary>A text decal on the model's blank "Sign" board (front = +Z after the 180° turn → projector looks −Z).</summary>
        static void SignText(Transform model, string text)
        {
            var sign = VehicleInteriorBuilder.Deep(model, "Sign");
            var r = sign != null ? sign.GetComponentInChildren<Renderer>() : null;
            if (r == null) return;
            var b = r.bounds;
            var frame = model.parent != null ? model.parent : model; // the holder carries the 180° turn (GlbKit may rotate the model root on import)
            var fwd = frame.forward * -1f; // the model's front (−Z) points this way in the world
            var go = new GameObject("SignText"); go.transform.SetParent(model, true);
            go.transform.position = b.center + fwd * (Vector3.Dot(b.extents, new Vector3(Mathf.Abs(fwd.x), Mathf.Abs(fwd.y), Mathf.Abs(fwd.z))) + 0.03f);
            go.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up);
            float h = Mathf.Min(b.size.y * 0.55f, 0.5f);
            TextDecals.Board(go.transform, text, Vector3.zero, Vector3.zero, h, Color.white, 0.25f);
        }
    }
}
