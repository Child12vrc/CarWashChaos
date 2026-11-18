using CarWash.Tools;
using UnityEngine;

namespace CarWash.Player
{
    /// <summary>
    /// M4.6 D8: two-hand IK on a player look's Animator (needs the IK pass on the controller's base layer — PlayerLooks sets it).
    /// While the player holds an active (not stowed) tool and is standing, the right hand goes to the tool's grip and the left
    /// hand to its off-hand socket, so on every peer the tool sits in the character's hands (pose / spray stance / swing arc come
    /// from ToolBase.LateUpdate, which every peer runs from the same synced state).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class HandIK : MonoBehaviour
    {
        Animator _anim;
        PlayerTools _tools;
        PlayerRagdoll _rag;
        float _w;

        /// <summary>Current IK weight (0 = free hands, 1 = on the tool).</summary>
        public float Weight => _w;
        public ToolBase Tool { get; private set; }

        void Awake()
        {
            _anim = GetComponent<Animator>();
            _tools = GetComponentInParent<PlayerTools>();
            _rag = GetComponentInParent<PlayerRagdoll>();
        }

        /// <summary>Where the left hand is sent: the off-hand socket, or — if the arm can't reach it — the nearest reachable point down the
        /// shaft toward the grip (hands closer together on a long pole).</summary>
        public Vector3 LeftTarget { get; private set; }

        Vector3 ReachableOffHand(Vector3 grip)
        {
            var off = Tool.OffHandPosition;
            var up = _anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var lo = _anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var hand = _anim.GetBoneTransform(HumanBodyBones.LeftHand);
            if (up == null || lo == null || hand == null) return off;
            float reach = (Vector3.Distance(up.position, lo.position) + Vector3.Distance(lo.position, hand.position)) * 0.97f;
            var s = up.position;
            for (int i = 0; i <= 10; i++)
            {
                var p = Vector3.Lerp(off, grip, i * 0.08f); // at most 80 % of the way: keep the hands apart
                if ((p - s).sqrMagnitude <= reach * reach) return p;
            }
            return Vector3.Lerp(off, grip, 0.8f);
        }

        void OnAnimatorIK(int layer)
        {
            if (layer != 0 || _tools == null) return;
            var tool = _tools.Equipped;
            bool on = tool != null && !tool.Stowed && (_rag == null || !_rag.ControlsBody);
            _w = Mathf.MoveTowards(_w, on ? 1f : 0f, Time.deltaTime * 8f);
            if (on) Tool = tool;
            if (_w <= 0f || Tool == null) { Tool = on ? tool : null; return; }
            var g = Tool.Grip;
            // the hand bone sits a few cm behind the palm: pull the target back along the tool so the fingers wrap the handle
            _anim.SetIKPositionWeight(AvatarIKGoal.RightHand, _w);
            _anim.SetIKRotationWeight(AvatarIKGoal.RightHand, _w * 0.6f);
            _anim.SetIKPosition(AvatarIKGoal.RightHand, g.position - g.forward * 0.04f);
            _anim.SetIKRotation(AvatarIKGoal.RightHand, g.rotation * Quaternion.Euler(0f, 0f, -90f));
            _anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, _w);
            _anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, _w * 0.5f);
            LeftTarget = ReachableOffHand(g.position);
            _anim.SetIKPosition(AvatarIKGoal.LeftHand, LeftTarget);
            _anim.SetIKRotation(AvatarIKGoal.LeftHand, Tool.OffHandRotation * Quaternion.Euler(0f, 0f, 90f));
            // elbows out and down a little
            var root = transform;
            _anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, _w * 0.5f);
            _anim.SetIKHintPosition(AvatarIKHint.RightElbow, root.TransformPoint(new Vector3(0.45f, 0.95f, -0.1f)));
            _anim.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, _w * 0.5f);
            _anim.SetIKHintPosition(AvatarIKHint.LeftElbow, root.TransformPoint(new Vector3(-0.45f, 0.95f, -0.1f)));
        }
    }
}
