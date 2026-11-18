using System.Linq;
using CarWash.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarWash.Editor
{
    /// <summary>Layers, tags, collision matrix, physics + graphics settings (TECH §5). Idempotent.</summary>
    public static class ProjectConfigurator
    {
        public static void Apply()
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            for (int i = 0; i < Layers.Names.Length; i++)
                if (!string.IsNullOrEmpty(Layers.Names[i]))
                    layers.GetArrayElementAtIndex(i).stringValue = Layers.Names[i];

            var tags = tm.FindProperty("tags");
            foreach (var t in new[] { Layers.TagTrash, Layers.TagVacSmall, Layers.TagDirtable })
            {
                bool found = false;
                for (int i = 0; i < tags.arraySize; i++) if (tags.GetArrayElementAtIndex(i).stringValue == t) found = true;
                if (!found)
                {
                    tags.InsertArrayElementAtIndex(tags.arraySize);
                    tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = t;
                }
            }
            tm.ApplyModifiedPropertiesWithoutUndo();

            // Collision matrix: start from "all collide", then cut.
            for (int a = 0; a < 32; a++)
                for (int b = 0; b < 32; b++)
                    Physics.IgnoreLayerCollision(a, b, false);
            for (int l = 0; l < 32; l++) Physics.IgnoreLayerCollision(Layers.FPArms, l, true);
            // FoamArea volumes only need to detect players/props/critters.
            for (int l = 0; l < 32; l++)
            {
                bool keep = l == Layers.Player || l == Layers.Prop || l == Layers.Critter;
                Physics.IgnoreLayerCollision(Layers.FoamArea, l, !keep);
            }
            // DESIGN-GAP: held tools must not shove their holder → Tool vs Player ignored.
            Physics.IgnoreLayerCollision(Layers.Tool, Layers.Player, true);
            Physics.IgnoreLayerCollision(Layers.Trigger, Layers.Trigger, true);
            Physics.IgnoreLayerCollision(Layers.Trigger, Layers.Default, true);
            // P3 ragdolls: no pushing players' capsules (own body!), held tools, triggers or interactables
            foreach (var l in new[] { Layers.Player, Layers.Tool, Layers.Trigger, Layers.Interactable, Layers.FPArms, Layers.FoamArea })
                Physics.IgnoreLayerCollision(Layers.Ragdoll, l, true);
            // GLB vehicle wash surfaces: raycast-only (spray / towel), never physical
            for (int l = 0; l < 32; l++) Physics.IgnoreLayerCollision(Layers.WashSurface, l, true);

            Physics.defaultSolverIterations = 8;
            Physics.defaultSolverVelocityIterations = 2;
            Physics.bounceThreshold = 1f;
            Physics.autoSyncTransforms = false;
            Physics.reuseCollisionCallbacks = true;
            Time.fixedDeltaTime = 1f / 50f;

            // Graphics APIs: DX12 > DX11 > Vulkan (T1.3 must work on D3D11 & Vulkan)
            var win = BuildTarget.StandaloneWindows64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(win, false);
            PlayerSettings.SetGraphicsAPIs(win, new[] { GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Direct3D11, GraphicsDeviceType.Vulkan });
            PlayerSettings.runInBackground = true; // MPPM / multi-instance testing

            AssetDatabase.SaveAssets();
            Debug.Log("[CarWash] ProjectConfigurator applied: layers " + string.Join(",", Layers.Names.Where(n => n != null)));
        }
    }
}
