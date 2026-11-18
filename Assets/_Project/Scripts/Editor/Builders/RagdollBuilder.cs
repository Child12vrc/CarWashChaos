using System.Collections.Generic;
using CarWash.Core;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>
    /// P3: physics ragdoll on the YBot humanoid (11 bones like Unity's ragdoll wizard): rigidbodies (kinematic until
    /// knocked down), capsules along each limb / boxes for hips+chest / sphere head on the Ragdoll layer, CharacterJoints
    /// with per-part limits. Total mass 68 kg. Returns the bodies in PlayerRagdoll order (hips first).
    /// </summary>
    public static class RagdollBuilder
    {
        struct Part { public HumanBodyBones bone, child; public HumanBodyBones parent; public float mass, radius; public string kind; public float lowTwist, highTwist, swing; }

        static readonly Part[] Parts =
        {
            new Part { bone = HumanBodyBones.Hips, child = HumanBodyBones.Spine, parent = HumanBodyBones.LastBone, mass = 15f, kind = "box" },
            new Part { bone = HumanBodyBones.Chest, child = HumanBodyBones.Neck, parent = HumanBodyBones.Hips, mass = 15f, kind = "box", lowTwist = -25, highTwist = 25, swing = 30 },
            new Part { bone = HumanBodyBones.Head, child = HumanBodyBones.LastBone, parent = HumanBodyBones.Chest, mass = 5f, radius = 0.11f, kind = "sphere", lowTwist = -50, highTwist = 40, swing = 40 },
            new Part { bone = HumanBodyBones.LeftUpperArm, child = HumanBodyBones.LeftLowerArm, parent = HumanBodyBones.Chest, mass = 2.5f, radius = 0.05f, kind = "capsule", lowTwist = -90, highTwist = 40, swing = 85 },
            new Part { bone = HumanBodyBones.LeftLowerArm, child = HumanBodyBones.LeftHand, parent = HumanBodyBones.LeftUpperArm, mass = 2f, radius = 0.045f, kind = "capsule", lowTwist = -115, highTwist = 0, swing = 25 },
            new Part { bone = HumanBodyBones.RightUpperArm, child = HumanBodyBones.RightLowerArm, parent = HumanBodyBones.Chest, mass = 2.5f, radius = 0.05f, kind = "capsule", lowTwist = -90, highTwist = 40, swing = 85 },
            new Part { bone = HumanBodyBones.RightLowerArm, child = HumanBodyBones.RightHand, parent = HumanBodyBones.RightUpperArm, mass = 2f, radius = 0.045f, kind = "capsule", lowTwist = -115, highTwist = 0, swing = 25 },
            new Part { bone = HumanBodyBones.LeftUpperLeg, child = HumanBodyBones.LeftLowerLeg, parent = HumanBodyBones.Hips, mass = 7f, radius = 0.075f, kind = "capsule", lowTwist = -30, highTwist = 85, swing = 45 },
            new Part { bone = HumanBodyBones.LeftLowerLeg, child = HumanBodyBones.LeftFoot, parent = HumanBodyBones.LeftUpperLeg, mass = 5f, radius = 0.06f, kind = "capsule", lowTwist = -105, highTwist = 0, swing = 20 },
            new Part { bone = HumanBodyBones.RightUpperLeg, child = HumanBodyBones.RightLowerLeg, parent = HumanBodyBones.Hips, mass = 7f, radius = 0.075f, kind = "capsule", lowTwist = -30, highTwist = 85, swing = 45 },
            new Part { bone = HumanBodyBones.RightLowerLeg, child = HumanBodyBones.RightFoot, parent = HumanBodyBones.RightUpperLeg, mass = 5f, radius = 0.06f, kind = "capsule", lowTwist = -105, highTwist = 0, swing = 20 },
        };

        public static Rigidbody[] Build(Animator anim)
        {
            var bodies = new List<Rigidbody>();
            var byBone = new Dictionary<HumanBodyBones, Rigidbody>();
            foreach (var p in Parts)
            {
                var t = anim.GetBoneTransform(p.bone);
                if (t == null && p.bone == HumanBodyBones.Chest) t = anim.GetBoneTransform(HumanBodyBones.Spine);
                if (t == null) { Debug.LogWarning("[Ragdoll] missing bone " + p.bone); continue; }
                t.gameObject.layer = Layers.Ragdoll;
                if (!t.TryGetComponent<Rigidbody>(out var rb)) rb = t.gameObject.AddComponent<Rigidbody>();
                rb.mass = p.mass; rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // valid for kinematic too
                rb.linearDamping = 0.6f; rb.angularDamping = 0.3f;   // P3: no 13 m glides (linear); M4.8 F6: limbs swing freely (angular 1.0 → 0.3)
                rb.solverIterations = 10; rb.solverVelocityIterations = 2;  // looser joint limits stay stable
                AddCollider(t, anim, p);
                if (p.parent != HumanBodyBones.LastBone && byBone.TryGetValue(p.parent, out var parentRb))
                {
                    if (!t.TryGetComponent<CharacterJoint>(out var j)) j = t.gameObject.AddComponent<CharacterJoint>();
                    j.connectedBody = parentRb;
                    j.enableProjection = true;
                    j.lowTwistLimit = new SoftJointLimit { limit = p.lowTwist };
                    j.highTwistLimit = new SoftJointLimit { limit = p.highTwist };
                    j.swing1Limit = new SoftJointLimit { limit = p.swing };
                    j.swing2Limit = new SoftJointLimit { limit = p.swing };
                }
                byBone[p.bone] = rb;
                bodies.Add(rb);
            }
            return bodies.ToArray();
        }

        public const string MaterialPath = "Assets/_Project/Art/Physics/Ragdoll.physicMaterial";

        /// <summary>Grippy, nearly dead body material (friction 0.9/1.0, bounce 0.05).</summary>
        public static PhysicsMaterial RagdollMaterial()
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(MaterialPath));
            var m = UnityEditor.AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(MaterialPath);
            if (m == null) { m = new PhysicsMaterial("Ragdoll"); UnityEditor.AssetDatabase.CreateAsset(m, MaterialPath); }
            m.dynamicFriction = 0.9f; m.staticFriction = 1.0f; m.bounciness = 0.05f;
            m.frictionCombine = PhysicsMaterialCombine.Maximum; m.bounceCombine = PhysicsMaterialCombine.Minimum;
            UnityEditor.EditorUtility.SetDirty(m);
            return m;
        }

        static void AddCollider(Transform t, Animator anim, Part p)
        {
            foreach (var old in t.GetComponents<Collider>()) Object.DestroyImmediate(old);
            var mat = RagdollMaterial();
            var childT = p.child != HumanBodyBones.LastBone ? anim.GetBoneTransform(p.child) : null;
            switch (p.kind)
            {
                case "sphere":
                {
                    var s = t.gameObject.AddComponent<SphereCollider>();
                    s.radius = p.radius / t.lossyScale.x; s.sharedMaterial = mat;
                    s.center = t.InverseTransformPoint(t.position + Vector3.up * 0.08f);
                    break;
                }
                case "capsule":
                {
                    var c = t.gameObject.AddComponent<CapsuleCollider>();
                    var end = childT != null ? childT.position : t.position + t.up * 0.3f;
                    var local = t.InverseTransformPoint(end);
                    int axis = Mathf.Abs(local.x) > Mathf.Abs(local.y) ? (Mathf.Abs(local.x) > Mathf.Abs(local.z) ? 0 : 2) : (Mathf.Abs(local.y) > Mathf.Abs(local.z) ? 1 : 2);
                    c.direction = axis; c.sharedMaterial = mat;
                    c.center = local * 0.5f;
                    float scale = Mathf.Max(1e-4f, t.lossyScale.x);
                    c.radius = p.radius / scale;
                    c.height = local.magnitude + c.radius * 2f;
                    break;
                }
                default:
                {
                    var b = t.gameObject.AddComponent<BoxCollider>(); b.sharedMaterial = mat;
                    var end = childT != null ? childT.position : t.position + Vector3.up * 0.25f;
                    float scale = Mathf.Max(1e-4f, t.lossyScale.x);
                    var local = t.InverseTransformPoint(end);
                    float len = Mathf.Max(0.15f / scale, local.magnitude);
                    b.center = local * 0.5f;
                    b.size = new Vector3(0.32f / scale, len, 0.2f / scale);
                    break;
                }
            }
        }
    }
}
