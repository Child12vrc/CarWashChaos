using UnityEngine;

namespace CarWash.Visual
{
    /// <summary>
    /// M4.9 G1: two headlight spots + glowing lamp quads at the front of the body (+Z), created locally at start from the
    /// painted body's mesh bounds (works for every car / boss shape). Off in daylight (NightLight), no shadows.
    /// </summary>
    public class VehicleHeadlights : MonoBehaviour
    {
        static Material _lensMat;

        void Start()
        {
            var painter = GetComponentInChildren<Dirt.DirtPainter>(true);
            var mf = painter != null ? painter.GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null) { enabled = false; return; }
            // body bounds in this (car root) space
            var b = mf.sharedMesh.bounds;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                var p = transform.InverseTransformPoint(mf.transform.TransformPoint(c));
                min = Vector3.Min(min, p); max = Vector3.Max(max, p);
            }
            if (_lensMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh != null) { _lensMat = new Material(sh) { name = "HeadlightLens" }; _lensMat.SetColor("_BaseColor", new Color(3f, 2.85f, 2.5f)); }
            }
            float y = Mathf.Lerp(min.y, max.y, 0.32f), halfW = (max.x - min.x) * 0.5f;
            var lights = new Light[2];
            for (int s = 0; s < 2; s++)
            {
                float x = (min.x + max.x) * 0.5f + (s == 0 ? -1f : 1f) * halfW * 0.68f;
                var go = new GameObject(s == 0 ? "Headlight_L" : "Headlight_R");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(x, y, max.z + 0.02f);
                go.transform.localRotation = Quaternion.Euler(9f, 0f, 0f);
                var l = go.AddComponent<Light>();
                l.type = LightType.Spot; l.range = 13f; l.spotAngle = 62f; l.innerSpotAngle = 30f; l.intensity = 5f;
                l.color = new Color(1f, 0.95f, 0.85f); l.shadows = LightShadows.None; l.enabled = false;
                lights[s] = l;
                if (_lensMat != null)
                {
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Destroy(q.GetComponent<Collider>());
                    q.name = "Lens"; q.layer = gameObject.layer;
                    q.transform.SetParent(go.transform, false);
                    q.transform.localPosition = Vector3.forward * 0.005f;
                    q.transform.localRotation = Quaternion.Euler(-9f, 180f, 0f); // quad faces -Z → turn it to face forward
                    q.transform.localScale = new Vector3(Mathf.Clamp(halfW * 0.35f, 0.14f, 0.3f), 0.1f, 1f);
                    var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = _lensMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    q.SetActive(false);
                }
            }
            var nl = gameObject.AddComponent<NightLight>();
            nl.Init(lights, 0.3f);
            nl.OnLevel = k => { foreach (var l in lights) if (l != null && l.transform.childCount > 0) l.transform.GetChild(0).gameObject.SetActive(k > 0.01f); };
        }
    }
}
