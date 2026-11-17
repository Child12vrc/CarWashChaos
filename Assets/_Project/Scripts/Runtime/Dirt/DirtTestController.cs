using System.Collections;
using System.Collections.Generic;
using CarWash.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CarWash.Dirt
{
    /// <summary>
    /// DirtTest.unity (TECH §7): mouse-ray spray on a placeholder car + % label every 0.5 s.
    /// LMB wash (mud+grease), F foam, RMB towel (spots), A/D orbit, R reset. -autotest = automatic full clean + FPS log.
    /// </summary>
    public class DirtTestController : MonoBehaviour
    {
        [SerializeField] private DirtPainter _painter;
        [SerializeField] private MeshCollider _collider;
        [SerializeField] private Camera _camera;
        [SerializeField] private float _radius = 0.35f, _rate = 1.5f;

        Text _label;
        float _orbit = 30f, _labelT;
        readonly List<float> _frameTimes = new List<float>();

        void Start()
        {
            UiFactory.EnsureEventSystem();
            var root = UiFactory.Canvas("DirtTestCanvas", 0, transform).transform;
            _label = UiFactory.Label("Pct", root, "", 34, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(1600, 120));
            UiFactory.Label("Help", root, "좌클릭: 세척  F: 거품  우클릭: 수건  A/D: 회전  R: 초기화", 24, new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(1600, 40));
            ResetCar();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autotest") >= 0) StartCoroutine(AutoTest());
        }

        void ResetCar() => _painter.Init(512, 0.9f, 0.6f, Random.Range(0f, 100f));

        void Update()
        {
            var kb = Keyboard.current; var m = Mouse.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed) _orbit -= 60f * Time.deltaTime;
                if (kb.dKey.isPressed) _orbit += 60f * Time.deltaTime;
                if (kb.rKey.wasPressedThisFrame) ResetCar();
            }
            _camera.transform.position = Quaternion.Euler(25f, _orbit, 0) * new Vector3(0, 0, -7.5f) + new Vector3(0, 0.8f, 0);
            _camera.transform.LookAt(new Vector3(0, 0.8f, 0));

            if (m != null)
            {
                var ray = _camera.ScreenPointToRay(m.position.ReadValue());
                if (_collider.Raycast(ray, out var hit, 50f))
                {
                    float s = _rate * Time.deltaTime;
                    if (m.leftButton.isPressed) _painter.AddWorldStroke(hit.point, _radius, new Vector4(s, s, 0, 0), StrokeType.Clean);
                    if (m.rightButton.isPressed) _painter.AddWorldStroke(hit.point, _radius, new Vector4(0, 0, 0, s), StrokeType.Clean);
                    if (kb != null && kb.fKey.isPressed) _painter.AddWorldStroke(hit.point, _radius, new Vector4(1, 0, 0, 0), StrokeType.Foam);
                }
            }

            _labelT += Time.deltaTime;
            if (_labelT >= 0.5f)
            {
                _labelT = 0f;
                _label.text = $"청결도 {_painter.CleanPct * 100f:0.0}%   (진흙 {_painter.MudPct * 100f:0.0}%  기름 {_painter.GreasePct * 100f:0.0}%)   {SystemInfo.graphicsDeviceType}";
            }
        }

        /// <summary>Strokes over every surface sample: foam → wash → towel; logs % and frame time.</summary>
        IEnumerator AutoTest()
        {
            yield return new WaitForSeconds(1f);
            // orientation check: clean only a spot at the cabin roof centre, then capture from a fixed camera
            _orbit = 35f;
            for (int i = 0; i < 40; i++) _painter.AddStroke(new DirtStroke(new Vector3(0f, 1.5f, -0.2f), 0.45f, new Vector4(1, 1, 0, 0), StrokeType.Clean));
            yield return new WaitForSeconds(0.8f);
            ScreenCapture.CaptureScreenshot($"dirttest_partial_{SystemInfo.graphicsDeviceType}.png");
            yield return new WaitForSeconds(0.5f);
            ResetCar();
            yield return new WaitForSeconds(0.8f);
            var samples = SurfaceSamples(_painter.Mesh, 3000);
            Log($"device={SystemInfo.graphicsDeviceType} start clean={_painter.CleanPct * 100f:0.0}%");
            yield return Pass(samples, new Vector4(1, 0, 0, 0), StrokeType.Foam, 1);
            yield return Pass(samples, new Vector4(0.6f, 0.6f, 0, 0), StrokeType.Clean, 3);
            yield return new WaitForSeconds(1.2f);
            Log($"after wash clean={_painter.CleanPct * 100f:0.0}% mud={_painter.MudPct * 100f:0.0}% grease={_painter.GreasePct * 100f:0.0}% (spots remain)");
            yield return Pass(samples, new Vector4(0, 0, 0, 0.6f), StrokeType.Clean, 3);
            yield return new WaitForSeconds(1.2f);
            _frameTimes.Sort();
            float median = _frameTimes.Count > 0 ? _frameTimes[_frameTimes.Count / 2] : 0f;
            float p95 = _frameTimes.Count > 0 ? _frameTimes[(int)(_frameTimes.Count * 0.95f)] : 0f;
            Log($"after towel clean={_painter.CleanPct * 100f:0.00}%  frame median {median * 1000f:0.0} ms, p95 {p95 * 1000f:0.0} ms while painting 32 strokes/frame");
            ScreenCapture.CaptureScreenshot($"dirttest_{SystemInfo.graphicsDeviceType}.png");
            yield return new WaitForSeconds(1f);
            Log("DONE");
            if (!Application.isEditor) Application.Quit();
        }

        IEnumerator Pass(List<(Vector3 p, Vector3 n)> samples, Vector4 str, StrokeType type, int repeats)
        {
            for (int r = 0; r < repeats; r++)
                for (int i = 0; i < samples.Count; i++)
                {
                    _painter.AddStroke(new DirtStroke(samples[i].p, _radius, str, type));
                    if (i % 32 == 31) { _frameTimes.Add(Time.unscaledDeltaTime); yield return null; }
                }
        }

        static List<(Vector3, Vector3)> SurfaceSamples(Mesh mesh, int count)
        {
            var v = mesh.vertices; var n = mesh.normals; var t = mesh.triangles;
            var list = new List<(Vector3, Vector3)>();
            var areas = new float[t.Length / 3]; float total = 0;
            for (int i = 0; i < areas.Length; i++) { areas[i] = Vector3.Cross(v[t[i * 3 + 1]] - v[t[i * 3]], v[t[i * 3 + 2]] - v[t[i * 3]]).magnitude * 0.5f; total += areas[i]; }
            var rng = new System.Random(7);
            for (int i = 0; i < areas.Length; i++)
            {
                int k = Mathf.Max(2, Mathf.CeilToInt(count * areas[i] / total));
                for (int j = 0; j < k; j++)
                {
                    float a = (float)rng.NextDouble(), b = (float)rng.NextDouble();
                    if (a + b > 1) { a = 1 - a; b = 1 - b; }
                    var p = v[t[i * 3]] + a * (v[t[i * 3 + 1]] - v[t[i * 3]]) + b * (v[t[i * 3 + 2]] - v[t[i * 3]]);
                    list.Add((p, n[t[i * 3]]));
                }
                list.Add((v[t[i * 3]], n[t[i * 3]]));
            }
            return list;
        }

        static void Log(string m) => Debug.Log("[DIRTTEST] " + m);
    }
}
