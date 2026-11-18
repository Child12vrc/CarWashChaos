using UnityEngine;

namespace CarWash.Player
{
    /// <summary>
    /// M4.6 D8: the owner's first-person hands (the own body is hidden in first person). Two simple arms (sleeve + glove) on the
    /// FPArms layer (no collisions, no shadows): the right glove at the held tool's grip, the left one at its off-hand socket,
    /// each forearm reaching back to a shoulder point low at the camera's side. Only while an active tool is held.
    /// </summary>
    [DefaultExecutionOrder(100)] // after ToolBase.LateUpdate posed the tool
    public class FPHands : MonoBehaviour
    {
        static readonly Vector3 ShoulderR = new Vector3(0.26f, -0.42f, -0.08f), ShoulderL = new Vector3(-0.26f, -0.42f, -0.08f);

        PlayerTools _tools;
        Transform _sleeveR, _sleeveL, _gloveR, _gloveL;
        Transform _root;

        public bool Visible => _root != null && _root.gameObject.activeSelf;
        public Vector3 RightHand => _gloveR.position;
        public Vector3 LeftHand => _gloveL.position;

        public void Init(PlayerTools tools)
        {
            _tools = tools;
            _root = new GameObject("FPHands") { layer = Core.Layers.FPArms }.transform;
            _root.SetParent(transform, false);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var sleeve = Mat(lit, new Color(0.16f, 0.22f, 0.38f), 0.2f);
            var glove = Mat(lit, new Color(0.95f, 0.75f, 0.15f), 0.45f);
            _sleeveR = Part(PrimitiveType.Capsule, sleeve); _sleeveL = Part(PrimitiveType.Capsule, sleeve);
            _gloveR = Part(PrimitiveType.Sphere, glove); _gloveL = Part(PrimitiveType.Sphere, glove);
            _root.gameObject.SetActive(false);
        }

        static Material Mat(Shader s, Color c, float smooth)
        {
            var m = new Material(s != null ? s : Shader.Find("Standard")) { name = "FPHands" };
            m.SetColor("_BaseColor", c); m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            return m;
        }

        Transform Part(PrimitiveType type, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.layer = Core.Layers.FPArms;
            go.transform.SetParent(_root, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        void LateUpdate()
        {
            if (_root == null) return;
            var tool = _tools != null ? _tools.Equipped : null;
            var rag = _tools != null ? _tools.GetComponent<PlayerRagdoll>() : null;
            bool on = tool != null && !tool.Stowed && (rag == null || !rag.ControlsBody);
            if (_root.gameObject.activeSelf != on) _root.gameObject.SetActive(on);
            if (!on) return;
            var g = tool.Grip;
            Arm(_sleeveR, _gloveR, transform.TransformPoint(ShoulderR), g.position - g.forward * 0.03f, g.rotation);
            Arm(_sleeveL, _gloveL, transform.TransformPoint(ShoulderL), tool.OffHandPosition, tool.OffHandRotation);
        }

        static void Arm(Transform sleeve, Transform glove, Vector3 shoulder, Vector3 hand, Quaternion handRot)
        {
            glove.SetPositionAndRotation(hand, handRot);
            glove.localScale = new Vector3(0.085f, 0.07f, 0.11f);
            var d = hand - shoulder;
            float len = d.magnitude;
            // sleeve stops just behind the glove; capsule's long axis is Y (height 2 at scale 1)
            var end = hand - d.normalized * 0.05f;
            sleeve.SetPositionAndRotation((shoulder + end) * 0.5f, Quaternion.FromToRotation(Vector3.up, d.normalized));
            sleeve.localScale = new Vector3(0.075f, Mathf.Max(0.05f, (len - 0.05f) * 0.5f), 0.075f);
        }
    }
}
