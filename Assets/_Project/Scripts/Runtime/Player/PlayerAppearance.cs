using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace CarWash.Player
{
    /// <summary>
    /// Player look (docs/ART_IMPORT.md "Player characters", Quaternius CC0, Humanoid). The prefab carries every look under
    /// "Body" (each with its own Animator on the shared Mixamo controller via humanoid retargeting + its own 11-bone ragdoll);
    /// only the chosen one is active. Look is a server-owned NetworkVariable: defaults P1 Male_Worker, P2 Female_Worker,
    /// P3 Male_Casual, P4 Female_Casual (by join order, first free); in the lobby Z / X pick the previous / next of the 12.
    /// Player colour is no longer a body tint (the outfits keep their colours) but a colour outline (inverted-hull copies of
    /// the body meshes) + a colour name tag above the head that only the others see.
    /// </summary>
    public class PlayerAppearance : NetworkBehaviour
    {
        [Serializable]
        public class LookEntry
        {
            public string id;
            public string nameKo;
            public GameObject root;
            public Animator animator;
            public Rigidbody[] bones;
            public Renderer[] renderers;   // body + outline renderers (PlayerNet colours the outline ones)
        }

        public static readonly string[] Defaults = { "Male_Worker", "Female_Worker", "Male_Casual", "Female_Casual" };

        [SerializeField] private LookEntry[] _looks = Array.Empty<LookEntry>();
        [SerializeField] private float _tagHeight = 2.15f;

        public readonly NetworkVariable<int> Look = new NetworkVariable<int>(-1);

        public int Count => _looks.Length;
        public LookEntry Current => Look.Value >= 0 && Look.Value < _looks.Length ? _looks[Look.Value] : null;
        public string CurrentName => Current != null ? Current.nameKo : "-";
        public static string NameOf(PlayerAppearance a, int i) => a != null && i >= 0 && i < a._looks.Length ? a._looks[i].nameKo : "-";

        PlayerNet _net;
        int _applied = -2;
        Transform _tag;
        Text _tagText;

        void Awake()
        {
            _net = GetComponent<PlayerNet>();
            // M4.6 D8: hands on the held tool
            foreach (var l in _looks) if (l.animator != null && !l.animator.TryGetComponent<HandIK>(out _)) l.animator.gameObject.AddComponent<HandIK>();
        }

        public override void OnNetworkSpawn()
        {
            Look.OnValueChanged += (_, v) => Apply(v);
            if (IsServer && Look.Value < 0) Look.Value = PickDefault();
            Apply(Look.Value);
            if (!IsOwner) BuildTag();
            // M4.9 G3: my saved look (if any) once the server picked a default
            if (IsOwner) { int saved = IndexOf(PlayerPrefs.GetString(SavedLookKey, "")); if (saved >= 0) SetRpc(saved); }
        }

        // ---------------- server ----------------
        int PickDefault()
        {
            var taken = FindObjectsByType<PlayerAppearance>(FindObjectsSortMode.None).Where(a => a != this && a.Look.Value >= 0).Select(a => a.Look.Value).ToList();
            foreach (var id in Defaults)
            {
                int i = IndexOf(id);
                if (i >= 0 && !taken.Contains(i)) return i;
            }
            return _looks.Length > 0 ? (int)(OwnerClientId % (ulong)_looks.Length) : -1;
        }

        public int IndexOf(string id) => Array.FindIndex(_looks, l => l.id == id);

        /// <summary>Owner, lobby: step through the 12 looks (server validates the lobby state).</summary>
        public void RequestStep(int dir) => StepRpc(dir);

        [Rpc(SendTo.Server)]
        void StepRpc(int dir, RpcParams p = default)
        {
            if (p.Receive.SenderClientId != OwnerClientId || _looks.Length == 0) return;
            var dm = Day.DayManager.Instance;
            if (dm != null && dm.State.Value != Core.GameState.Lobby) return;
            var rag = GetComponent<PlayerRagdoll>();
            if (rag != null && rag.ControlsBody) return;
            Look.Value = ((Look.Value + (dir >= 0 ? 1 : -1)) % _looks.Length + _looks.Length) % _looks.Length;
        }

        /// <summary>M4.9 G3 owner (wardrobe): pick a look directly; remembered locally (PlayerPrefs "cw.look" = look id).</summary>
        public void RequestSet(int index)
        {
            if (index < 0 || index >= _looks.Length) return;
            PlayerPrefs.SetString(SavedLookKey, _looks[index].id); PlayerPrefs.Save();
            SetRpc(index);
        }
        public const string SavedLookKey = "cw.look";
        public LookEntry EntryAt(int i) => i >= 0 && i < _looks.Length ? _looks[i] : null;

        [Rpc(SendTo.Server)]
        void SetRpc(int index, RpcParams p = default)
        {
            if (p.Receive.SenderClientId != OwnerClientId || index < 0 || index >= _looks.Length) return;
            var rag = GetComponent<PlayerRagdoll>();
            if (rag != null && rag.ControlsBody) return; // not while knocked down / carried
            Look.Value = index;
        }

        /// <summary>Server, dev checks: set a look directly.</summary>
        public void ServerSet(int index) { if (IsServer && index >= 0 && index < _looks.Length) Look.Value = index; }

        // ---------------- every peer ----------------
        void Apply(int index)
        {
            if (index < 0 || index >= _looks.Length || index == _applied) return;
            _applied = index;
            for (int i = 0; i < _looks.Length; i++) if (_looks[i].root != null) _looks[i].root.SetActive(i == index);
            var l = _looks[index];
            GetComponent<PlayerBody>()?.SetAnimator(l.animator);
            GetComponent<PlayerRagdoll>()?.Bind(l.animator, l.bones);
            _net?.SetBodyRenderers(l.renderers);
        }

        void BuildTag()
        {
            var go = new GameObject("NameTag", typeof(Canvas));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * _tagHeight;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(320, 70);
            rt.localScale = Vector3.one * 0.0045f;
            var bg = new GameObject("Bg", typeof(Image)).GetComponent<Image>();
            bg.transform.SetParent(go.transform, false);
            bg.rectTransform.sizeDelta = new Vector2(240, 54);
            bg.color = new Color(0.05f, 0.07f, 0.15f, 0.65f);
            _tagText = new GameObject("Text", typeof(Text)).GetComponent<Text>();
            _tagText.transform.SetParent(go.transform, false);
            _tagText.rectTransform.sizeDelta = new Vector2(320, 70);
            _tagText.font = UI.UiFactory.Font;
            _tagText.fontSize = 40;
            _tagText.fontStyle = FontStyle.Bold;
            _tagText.alignment = TextAnchor.MiddleCenter;
            // M4.10 W1 rule: name tags are world text → depth-tested like every other world text (hidden behind walls; the
            // colour outline still shows where a teammate is), and hidden beyond 30 m
            var ws = Shader.Find("CarWash/WorldText");
            if (ws != null) _tagText.material = new Material(ws) { name = "NameTag (WorldText)" };
            _tag = go.transform;
        }

        void LateUpdate()
        {
            if (_tag == null) return;
            int ci = _net != null ? Mathf.Max(0, _net.ColorIndex.Value) % PlayerNet.Colors.Length : 0;
            _tagText.text = _net != null ? _net.DisplayName : PlayerNet.ColorNames[ci];
            _tagText.color = PlayerNet.Colors[ci];
            // follow the hips while ragdolled / carried so the tag stays over the body
            var rag = GetComponent<PlayerRagdoll>();
            var anchor = rag != null && rag.ControlsBody ? rag.Hips.position + Vector3.up * 0.9f : transform.position + Vector3.up * _tagHeight;
            _tag.position = anchor;
            var cam = Camera.main != null ? Camera.main : (PlayerLook.Local != null ? PlayerLook.Local.Camera : null);
            if (cam != null) _tag.rotation = Quaternion.LookRotation(_tag.position - cam.transform.position);
            if (cam != null) _tagText.enabled = (cam.transform.position - _tag.position).sqrMagnitude < 30f * 30f;
        }

        /// <summary>Dev / tests: is the name tag shown, and with which text.</summary>
        public string TagText => _tagText != null ? _tagText.text : null;
    }
}
