using UnityEngine;

namespace CarWash.Visual
{
    /// <summary>
    /// M4.6 B4: puts a world TextMesh on the depth-tested CarWash/WorldText shader (the font's default GUI material draws on top
    /// of everything) and keeps its atlas in sync when the dynamic font texture is rebuilt.
    /// </summary>
    [RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
    public class WorldTextDepth : MonoBehaviour
    {
        [SerializeField] private Shader _shader;
        Material _mat;
        Font _font;

        void Awake()
        {
            var tm = GetComponent<TextMesh>();
            _font = tm.font;
            if (_shader == null || _font == null) return;
            _mat = new Material(_shader) { name = "WorldText (runtime)", mainTexture = _font.material.mainTexture };
            GetComponent<MeshRenderer>().sharedMaterial = _mat;
            Font.textureRebuilt += OnRebuilt;
        }

        void OnDestroy() { Font.textureRebuilt -= OnRebuilt; if (_mat != null) Destroy(_mat); }

        void OnRebuilt(Font f) { if (f == _font && _mat != null) _mat.mainTexture = f.material.mainTexture; }

        /// <summary>For tests: the material actually used.</summary>
        public Material Material => _mat;
    }
}
