using UnityEngine;

namespace MyGame.TPS
{
    /// <summary>
    /// 발 IK + 머리(시선) IK.
    /// [발] 애니메이션의 발 높이(들림)는 그대로 유지하고, 그 기준 바닥만 실제 지형(계단 단)으로 바꾼다.
    ///      → 디딤발은 계단 단 위에 정확히 놓이고, 스윙 발은 원래 궤적대로 움직임. 바닥 관통 없음.
    ///      골반은 더 낮은 쪽 발이 닿도록 내려주며, 계단에서 캡슐이 툭툭 올라가는 것도 부드럽게 흡수한다.
    /// [머리] 마우스(카메라) 조준점을 바라보되 좌우/상하 가동범위를 제한, 완전히 뒤를 볼 때는 고개를 덜 돌린다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class TPSIK : MonoBehaviour
    {
        public TPSCharacterController owner;

        [Header("발 IK")]
        public bool footIK = true;
        public LayerMask groundMask = ~(1 << 6);
        [Tooltip("발 위 어느 높이에서 레이를 쏠지 (계단 한 단보다 커야 함)")] public float rayStartHeight = 0.6f;
        [Tooltip("발 아래로 얼마나 더 내려가서 지면을 찾을지")] public float maxStepDown = 0.6f;
        public float maxGroundAngle = 55f;
        [Tooltip("발목에서 발끝까지 거리 (발끝도 지면 검사)")] public float toeLength = 0.16f;
        [Tooltip("스윙 중인 발이 이동 방향 앞쪽 지면을 미리 보는 시간")] public float lookAheadTime = 0.12f;
        [Tooltip("지면이 높아질 때 (계단 위로) 반응 속도")] public float footGroundUpSmooth = 40f;
        [Tooltip("지면이 낮아질 때 반응 속도")] public float footGroundSmooth = 16f;
        [Tooltip("골반이 올라갈 때 속도 (계단 오를 때)")] public float pelvisUpSpeed = 7f;
        [Tooltip("골반이 내려갈 때 속도 (계단 내려갈 때)")] public float pelvisDownSpeed = 16f;
        public float maxPelvisDrop = 0.5f;
        [Tooltip("이 높이 이상 들린 발은 지면 경사에 맞춰 회전시키지 않음")] public float footAlignFadeHeight = 0.12f;

        [Header("시선(머리) IK")]
        public bool lookIK = true;
        [Tooltip("좌우 최대 회전각")] public float maxYaw = 70f;
        public float maxPitchUp = 35f;
        public float maxPitchDown = 40f;
        [Tooltip("이 각도보다 뒤쪽을 보면 고개 회전량을 줄임")] public float behindFadeStart = 110f;
        [Range(0f, 1f)] public float behindMinWeight = 0.4f;
        public float lookSmooth = 7f;
        [Range(0f, 1f)] public float bodyWeight = 0.12f;
        [Range(0f, 1f)] public float aimBodyWeight = 0.35f;
        [Range(0f, 1f)] public float headWeight = 0.85f;
        [Range(0f, 1f)] public float eyesWeight = 1f;
        [Range(0f, 1f)] public float clampWeight = 0.5f;

        Animator anim;
        Transform head;
        float ikWeight;
        float lGround, rGround, baseY;
        Vector3 lNormal = Vector3.up, rNormal = Vector3.up;
        bool groundInit;
        Vector3 lPrev, rPrev;
        float curYaw, curPitch, curLookWeight, curBodyWeight;

        void Awake()
        {
            anim = GetComponent<Animator>();
            head = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (owner == null || layerIndex != 0) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            FootIK(dt);
            LookIK(dt);
        }

        // ------------------------------------------------------------------ 발
        void FootIK(float dt)
        {
            float targetW = (footIK && owner.IsGrounded && !owner.IsJumping) ? 1f : 0f;
            ikWeight = Mathf.MoveTowards(ikWeight, targetW, dt * (targetW > ikWeight ? 4f : 8f));

            float rootY = transform.position.y;
            Vector3 lp = anim.GetIKPosition(AvatarIKGoal.LeftFoot);
            Vector3 rp = anim.GetIKPosition(AvatarIKGoal.RightFoot);
            Quaternion lr = anim.GetIKRotation(AvatarIKGoal.LeftFoot);
            Quaternion rr = anim.GetIKRotation(AvatarIKGoal.RightFoot);

            Vector3 lVel = groundInit ? (lp - lPrev) / dt : Vector3.zero;
            Vector3 rVel = groundInit ? (rp - rPrev) / dt : Vector3.zero;
            lPrev = lp; rPrev = rp;

            float lg = ProbeFoot(lp, lr, lVel, rootY, out Vector3 ln);
            float rg = ProbeFoot(rp, rr, rVel, rootY, out Vector3 rn);

            float k = 1f - Mathf.Exp(-footGroundSmooth * dt);
            float kUp = 1f - Mathf.Exp(-footGroundUpSmooth * dt);
            if (!groundInit) { lGround = lg; rGround = rg; baseY = rootY; groundInit = true; }
            lGround = Mathf.Lerp(lGround, lg, lg > lGround ? kUp : k);
            rGround = Mathf.Lerp(rGround, rg, rg > rGround ? kUp : k);
            lNormal = Vector3.Slerp(lNormal, ln, k);
            rNormal = Vector3.Slerp(rNormal, rn, k);

            // 골반: 더 낮은 발이 닿을 수 있게 내림 (월드 공간에서 스무딩 → 캡슐이 계단을 툭 오를 때도 부드럽게)
            float desiredBase = Mathf.Min(rootY, Mathf.Min(lGround, rGround));
            desiredBase = Mathf.Max(desiredBase, rootY - maxPelvisDrop);
            float spd = desiredBase < baseY ? pelvisDownSpeed : pelvisUpSpeed;
            baseY = Mathf.Lerp(baseY, desiredBase, 1f - Mathf.Exp(-spd * dt));
            baseY = Mathf.Clamp(baseY, rootY - maxPelvisDrop, rootY + 0.3f);

            if (ikWeight <= 0.001f) { baseY = rootY; return; }

            anim.bodyPosition += Vector3.up * ((baseY - rootY) * ikWeight);

            SetFoot(AvatarIKGoal.LeftFoot, lp, lr, lGround, lNormal, rootY, anim.leftFeetBottomHeight);
            SetFoot(AvatarIKGoal.RightFoot, rp, rr, rGround, rNormal, rootY, anim.rightFeetBottomHeight);
        }

        /// <summary>
        /// 발목/발끝 + (스윙 중이면) 이동 방향 앞쪽까지 검사해 가장 높은 지면을 사용.
        /// → 계단 모서리를 넘는 발이 단 안으로 파고들지 않는다.
        /// </summary>
        float ProbeFoot(Vector3 footPos, Quaternion footRot, Vector3 footVel, float rootY, out Vector3 normal)
        {
            Vector3 fwd = footRot * Vector3.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = transform.forward;
            fwd.Normalize();

            float best = Probe(footPos, rootY, out normal);
            float g = Probe(footPos + fwd * toeLength, rootY, out Vector3 n2);
            if (g > best + 0.005f) { best = g; normal = n2; }

            Vector3 hv = footVel; hv.y = 0f;
            float lift = footPos.y - rootY;
            if (hv.sqrMagnitude > 0.04f && lift > 0.02f)
            {
                Vector3 ahead = footPos + hv * lookAheadTime;
                g = Probe(ahead, rootY, out n2);
                if (g > best + 0.005f) { best = g; normal = n2; }
                g = Probe(ahead + fwd * toeLength, rootY, out n2);
                if (g > best + 0.005f) { best = g; normal = n2; }
            }
            return best;
        }

        float Probe(Vector3 footPos, float rootY, out Vector3 normal)
        {
            float lift = Mathf.Max(0f, footPos.y - rootY);
            Vector3 origin = new Vector3(footPos.x, rootY + lift + rayStartHeight, footPos.z);
            float dist = lift + rayStartHeight + maxStepDown;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, dist, groundMask, QueryTriggerInteraction.Ignore)
                && Vector3.Angle(hit.normal, Vector3.up) <= maxGroundAngle)
            {
                normal = hit.normal;
                return hit.point.y;
            }
            normal = Vector3.up;
            return rootY;
        }

        void SetFoot(AvatarIKGoal goal, Vector3 animPos, Quaternion animRot, float groundY, Vector3 normal, float rootY, float bottomHeight)
        {
            // 애니메이션상 발이 평지 기준으로 얼마나 들려 있는지 (발바닥 두께 포함, 최소값 = 발바닥 두께 → 바닥 관통 방지)
            float animHeight = Mathf.Max(animPos.y - rootY, bottomHeight);
            Vector3 target = new Vector3(animPos.x, groundY + animHeight, animPos.z);

            float liftAboveSole = animHeight - bottomHeight;
            float alignW = 1f - Mathf.Clamp01(liftAboveSole / Mathf.Max(0.001f, footAlignFadeHeight));
            Quaternion aligned = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, normal, alignW)) * animRot;

            anim.SetIKPositionWeight(goal, ikWeight);
            anim.SetIKRotationWeight(goal, ikWeight);
            anim.SetIKPosition(goal, target);
            anim.SetIKRotation(goal, aligned);
        }

        // ------------------------------------------------------------------ 시선
        void LookIK(float dt)
        {
            if (!lookIK) { anim.SetLookAtWeight(0f); return; }

            Vector3 headPos = head ? head.position : transform.position + Vector3.up * 1.6f;
            Vector3 dir = owner.LookTarget - headPos;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            Vector3 local = transform.InverseTransformDirection(dir.normalized);

            float yawA = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitchA = -Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg; // + = 아래
            float absYaw = Mathf.Abs(yawA);

            // 거의 정반대일 때 좌/우가 번갈아 튀지 않도록 기존 방향 유지
            if (absYaw > 150f && Mathf.Abs(curYaw) > 1f && Mathf.Sign(yawA) != Mathf.Sign(curYaw)) yawA = -yawA;

            // 가동범위 제한
            float yawC = Mathf.Clamp(yawA, -maxYaw, maxYaw);
            float pitchC = Mathf.Clamp(pitchA, -maxPitchUp, maxPitchDown);
            // 뒤쪽을 볼수록 고개를 덜 돌림 (목이 꺾이는 느낌 방지)
            float fade = absYaw <= behindFadeStart ? 1f
                : Mathf.Lerp(1f, behindMinWeight, (absYaw - behindFadeStart) / (180f - behindFadeStart));

            float k = 1f - Mathf.Exp(-lookSmooth * dt);
            curYaw = Mathf.Lerp(curYaw, yawC * fade, k);
            curPitch = Mathf.Lerp(curPitch, pitchC, k);
            float targetWeight = owner.IsJumping ? 0.5f : 1f;
            curLookWeight = Mathf.Lerp(curLookWeight, targetWeight, k);
            curBodyWeight = Mathf.Lerp(curBodyWeight, owner.IsAiming ? aimBodyWeight : bodyWeight, k);

            Vector3 lookLocal = Quaternion.Euler(curPitch, curYaw, 0f) * Vector3.forward;
            Vector3 lookPos = headPos + transform.TransformDirection(lookLocal) * 5f;

            anim.SetLookAtWeight(curLookWeight, curBodyWeight, headWeight, eyesWeight, clampWeight);
            anim.SetLookAtPosition(lookPos);
        }
    }
}
