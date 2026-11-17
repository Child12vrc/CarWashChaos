using CarWash.Core;
using CarWash.Dirt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>DirtTest.unity: placeholder car + mouse spray + % label (TECH §7 manual scene).</summary>
    public static class DirtTestBuilder
    {
        public const string MaterialPath = "Assets/_Project/Art/Materials/VehicleDirt.mat";

        public static Material VehicleMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (m == null)
            {
                m = new Material(Shader.Find("CarWash/VehicleDirt"));
                AssetDatabase.CreateAsset(m, MaterialPath);
            }
            return m;
        }

        /// <summary>Creates a car GameObject (body with DirtPainter + wheels). Used by DirtTest and the vehicle prefabs.</summary>
        public static GameObject CreateCarVisual(string id, Transform parent, out DirtPainter painter, out MeshCollider bodyCollider)
        {
            var mesh = VehicleMeshBuilder.BuildBody(id);
            var body = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer));
            body.transform.SetParent(parent, false);
            body.GetComponent<MeshFilter>().sharedMesh = mesh;
            body.GetComponent<MeshRenderer>().sharedMaterial = VehicleMaterial();
            bodyCollider = body.AddComponent<MeshCollider>();
            bodyCollider.sharedMesh = mesh;
            painter = body.AddComponent<DirtPainter>();
            SceneBuilder.Set(painter, "_paintShader", Shader.Find("Hidden/CarWash/DirtUnwrapPaint"));
            SceneBuilder.Set(painter, "_evalShader", Shader.Find("Hidden/CarWash/DirtEval"));
            SceneBuilder.Set(painter, "_body", body.GetComponent<MeshFilter>());
            SceneBuilder.Set(painter, "_renderer", body.GetComponent<MeshRenderer>());

            var wheelMat = GrayboxMaterials.Get("Wheel", new Color(0.08f, 0.08f, 0.08f));
            foreach (var w in VehicleMeshBuilder.Wheels(id, out float r))
            {
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = "Wheel";
                Object.DestroyImmediate(wheel.GetComponent<Collider>());
                wheel.transform.SetParent(parent, false);
                wheel.transform.localPosition = w;
                wheel.transform.localRotation = Quaternion.Euler(0, 0, 90);
                wheel.transform.localScale = new Vector3(r * 2f, 0.15f, r * 2f);
                wheel.GetComponent<Renderer>().sharedMaterial = wheelMat;
            }
            return body;
        }

        [MenuItem("CarWash/Build/Scene DirtTest")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GrayboxMaterials.Box(null, "Ground", new Vector3(0, -0.5f, 0), new Vector3(30, 1, 30), GrayboxMaterials.Get("Concrete", new Color(0.62f, 0.62f, 0.6f)));
            var sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" }.GetComponent<Camera>();

            var car = new GameObject("TestCar").transform;
            CreateCarVisual("V02", car, out var painter, out var col);

            var ctrl = new GameObject("DirtTest").AddComponent<DirtTestController>();
            SceneBuilder.Set(ctrl, "_painter", painter);
            SceneBuilder.Set(ctrl, "_collider", col);
            SceneBuilder.Set(ctrl, "_camera", cam);
            SceneBuilder.Save(scene, SceneNames.DirtTest);
        }
    }
}
