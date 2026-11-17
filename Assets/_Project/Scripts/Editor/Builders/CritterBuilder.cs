using System;
using System.Collections.Generic;
using CarWash.Core;
using CarWash.Critters;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace CarWash.Editor
{
    /// <summary>
    /// Graybox critter prefabs at CritterData.prefab (T3.1-T3.5): kinematic body on the Critter layer, server-only
    /// NavMeshAgent, server-auth NetworkTransform, stun stars (fx_stun_star). All start Hidden (revealed by surprises /
    /// the dev reveal in automated checks).
    /// </summary>
    public static class CritterBuilder
    {
        public const string Dir = "Assets/_Project/Prefabs/Critters";
        public static string PathOf(string file) => $"{Dir}/{file}.prefab";

        public static List<GameObject> BuildAll()
        {
            var list = new List<GameObject>
            {
                Build<Raccoon>("C01", "C01_Raccoon", (v, r) => Animal(v, new Color(0.45f, 0.42f, 0.4f), new Vector3(0.45f, 0.4f, 0.7f), true, r)),
                Build<Possum>("C05", "C05_Possum", (v, r) => Animal(v, new Color(0.75f, 0.73f, 0.7f), new Vector3(0.35f, 0.3f, 0.6f), false, r)),
                Build<MudSlime>("C03", "C03_MudSlime", Slime),
                Build<BeeSwarm>("C02", "C02_BeeSwarm", Bees),
            };
            // the slime spawns copies of itself when it splits
            var slime = AssetDatabase.LoadAssetAtPath<GameObject>(PathOf("C03_MudSlime"));
            var so = new SerializedObject(slime.GetComponent<MudSlime>());
            so.FindProperty("_selfPrefab").objectReferenceValue = slime.GetComponent<NetworkObject>();
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(slime);
            AssetDatabase.SaveAssets();
            return list;
        }

        static GameObject Prim(PrimitiveType t, Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.layer = Layers.Critter;
            return go;
        }

        static Material Dark => GrayboxMaterials.Get("CritterDark", new Color(0.12f, 0.12f, 0.13f));

        /// <summary>Four-legged graybox animal; returns the collider height.</summary>
        static float Animal(Transform visual, Color color, Vector3 body, bool raccoon, GameObject root)
        {
            var mat = GrayboxMaterials.Get("Critter_" + ColorUtility.ToHtmlStringRGB(color), color);
            Prim(PrimitiveType.Capsule, visual, "Body", new Vector3(0, body.y * 0.6f, 0), new Vector3(body.x, body.y * 0.6f, body.z), mat).transform.localRotation = Quaternion.Euler(90, 0, 0);
            Prim(PrimitiveType.Sphere, visual, "Head", new Vector3(0, body.y * 0.85f, body.z * 0.55f), Vector3.one * body.x * 0.75f, mat);
            Prim(PrimitiveType.Sphere, visual, "Nose", new Vector3(0, body.y * 0.8f, body.z * 0.55f + body.x * 0.38f), Vector3.one * 0.07f, Dark);
            if (raccoon)
            {
                Prim(PrimitiveType.Cube, visual, "EarL", new Vector3(-0.1f, body.y * 1.1f, body.z * 0.5f), new Vector3(0.08f, 0.1f, 0.04f), Dark);
                Prim(PrimitiveType.Cube, visual, "EarR", new Vector3(0.1f, body.y * 1.1f, body.z * 0.5f), new Vector3(0.08f, 0.1f, 0.04f), Dark);
                Prim(PrimitiveType.Cube, visual, "Mask", new Vector3(0, body.y * 0.9f, body.z * 0.55f + body.x * 0.3f), new Vector3(body.x * 0.7f, 0.07f, 0.05f), Dark);
            }
            Prim(PrimitiveType.Capsule, visual, "Tail", new Vector3(0, body.y * 0.5f, -body.z * 0.65f), new Vector3(0.1f, 0.2f, 0.1f), raccoon ? Dark : mat).transform.localRotation = Quaternion.Euler(60, 0, 0);
            var col = root.AddComponent<CapsuleCollider>();
            col.direction = 2; col.radius = body.x * 0.55f; col.height = body.z + 0.1f; col.center = new Vector3(0, body.y * 0.6f, 0);
            return body.y * 1.4f;
        }

        static float Slime(Transform visual, GameObject root)
        {
            var mud = GrayboxMaterials.Get("Critter_Mud", ArtBuilder.Hex("#7A4E2D"), 0.7f);
            Prim(PrimitiveType.Sphere, visual, "Blob", new Vector3(0, 0.25f, 0), new Vector3(0.8f, 0.5f, 0.8f), mud);
            Prim(PrimitiveType.Sphere, visual, "Top", new Vector3(0, 0.48f, -0.05f), new Vector3(0.45f, 0.3f, 0.45f), mud);
            var eye = GrayboxMaterials.Get("CritterEye", Color.white);
            Prim(PrimitiveType.Sphere, visual, "EyeL", new Vector3(-0.13f, 0.45f, 0.3f), Vector3.one * 0.12f, eye);
            Prim(PrimitiveType.Sphere, visual, "EyeR", new Vector3(0.13f, 0.45f, 0.3f), Vector3.one * 0.12f, eye);
            var col = root.AddComponent<SphereCollider>();
            col.radius = 0.4f; col.center = new Vector3(0, 0.3f, 0);
            return 0.8f;
        }

        static float Bees(Transform visual, GameObject root)
        {
            var go = new GameObject("BeeParticles") { layer = Layers.Critter };
            go.transform.SetParent(visual, false);
            go.transform.localPosition = new Vector3(0, 1.2f, 0);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = true; main.duration = 1f;
            main.startLifetime = 1.6f; main.startSpeed = 0.5f; main.startSize = 0.08f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; main.maxParticles = 60;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 0.75f, 0.1f), new Color(0.1f, 0.08f, 0.05f));
            var em = ps.emission; em.rateOverTime = 30f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.55f;
            var noise = ps.noise; noise.enabled = true; noise.strength = 1.4f; noise.frequency = 2f; noise.scrollSpeed = 1f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = ArtBuilder.Particle("fx_water_droplet");
            var col = root.AddComponent<SphereCollider>();
            col.radius = 0.7f; col.center = new Vector3(0, 1.2f, 0);
            return 2.0f;
        }

        public static GameObject Build<T>(string id, string file, Func<Transform, GameObject, float> shape) where T : CritterBase
        {
            var root = new GameObject(file) { layer = Layers.Critter };
            var visual = new GameObject("Visual") { layer = Layers.Critter }.transform;
            visual.SetParent(root.transform, false);
            float height = shape(visual, root);
            var rb = root.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f; agent.height = Mathf.Max(0.5f, height); agent.acceleration = 20f; agent.angularSpeed = 540f; agent.stoppingDistance = 0.3f;
            agent.enabled = false;
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>().Interpolate = true;

            var stars = StunStars(root.transform, height);
            var c = root.AddComponent<T>();
            var so = new SerializedObject(c);
            so.FindProperty("_data").objectReferenceValue = DataBuilder.LoadRegistry();
            so.FindProperty("_critterId").stringValue = id;
            so.FindProperty("_agentType").stringValue = "Small";
            so.FindProperty("_startHidden").boolValue = true;
            so.FindProperty("_stunStars").objectReferenceValue = stars;
            so.FindProperty("_visual").objectReferenceValue = visual;
            var beesProp = so.FindProperty("_bees");
            if (beesProp != null) beesProp.objectReferenceValue = visual.GetComponentInChildren<ParticleSystem>();
            so.ApplyModifiedPropertiesWithoutUndo();
            System.IO.Directory.CreateDirectory(Dir);
            return PrefabBuilder.Save(root, PathOf(file));
        }

        static ParticleSystem StunStars(Transform parent, float height)
        {
            var go = new GameObject("StunStars");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, height, 0);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false; main.duration = 1f;
            main.startLifetime = 0.9f; main.startSpeed = 0f; main.startSize = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; main.maxParticles = 10;
            main.startColor = new Color(1f, 0.9f, 0.3f);
            var em = ps.emission; em.rateOverTime = 6f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.3f; sh.radiusThickness = 0f;
            sh.rotation = new Vector3(90f, 0f, 0f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalY = new ParticleSystem.MinMaxCurve(5f);
            vel.x = vel.y = vel.z = new ParticleSystem.MinMaxCurve(0f);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = ArtBuilder.Particle("fx_stun_star");
            return ps;
        }
    }
}
