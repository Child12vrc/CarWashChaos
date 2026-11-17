using UnityEngine;

namespace MyGame.TPS
{
    /// <summary>
    /// 루트모션 기반 TPS 캐릭터 컨트롤러.
    /// - 이동 거리는 애니메이션의 루트모션에서 가져오므로 발 미끄러짐이 생기지 않는다.
    /// - 블렌드트리 파라미터(MoveX/MoveZ)는 캐릭터 기준 로컬 방향으로 들어가며,
    ///   방향 차이가 클수록(반대 방향일수록) 더 천천히, 가까운 방향일수록 빠르게 전환된다.
    /// - 일반 모드: 몸이 이동 방향으로 부드럽게 회전 / 시선집중(우클릭) 모드: 카메라 방향을 보고 8방향 이동.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-50)]
    public class TPSCharacterController : MonoBehaviour
    {
        static readonly int HashMoveX = Animator.StringToHash("MoveX");
        static readonly int HashMoveZ = Animator.StringToHash("MoveZ");
        static readonly int HashCrouch = Animator.StringToHash("Crouch");
        static readonly int HashGrounded = Animator.StringToHash("Grounded");
        static readonly int HashAim = Animator.StringToHash("Aim");
        static readonly int HashJump = Animator.StringToHash("Jump");
        static readonly int HashSpeed = Animator.StringToHash("Speed");

        [Header("참조")]
        public Animator animator;
        public TPSInput input;
        public TPSCamera cameraRig;

        [Header("속도 단계 (블렌드트리 반경)")]
        public float walkLevel = 1f;
        public float runLevel = 2f;

        [Header("모션 전환 시간 (블렌드트리 파라미터 스무딩)")]
        [Tooltip("가까운 방향으로 바뀔 때")] public float nearDirBlendTime = 0.08f;
        [Tooltip("정반대 방향으로 바뀔 때")] public float oppositeDirBlendTime = 0.6f;
        [Tooltip("정지 상태에서 출발할 때")] public float startBlendTime = 0.2f;
        [Tooltip("이동하다 멈출 때 (클수록 천천히 멈춤)")] public float stopBlendTime = 0.4f;
        [Tooltip("걷기<->달리기 등 같은 방향 속도만 바뀔 때")] public float speedChangeBlendTime = 0.3f;

        [Header("몸 회전")]
        [Tooltip("가까운 방향으로 돌 때")] public float nearTurnTime = 0.08f;
        [Tooltip("정반대 방향으로 돌 때")] public float oppositeTurnTime = 0.45f;
        [Tooltip("시선집중 시 카메라 방향으로 도는 시간")] public float aimTurnTime = 0.12f;
        [Tooltip("시선집중 중 제자리에 있을 때 이 각도 이상 벌어지면 몸을 돌림 (그 전엔 상체/머리만 돌아감)")]
        public float idleAimTurnThreshold = 65f;

        [Header("앉기")]
        public float standHeight = 1.8f;
        public float crouchHeight = 1.2f;
        public float capsuleLerpSpeed = 10f;

        [Header("중력 / 지면")]
        public float gravity = -25f;
        public float groundStickSpeed = -2f;
        [Tooltip("계단을 내려갈 때 지면에 붙여주는 최대 거리")] public float groundSnapDistance = 0.5f;
        public LayerMask groundMask = ~(1 << 6);

        [Header("점프 (높이는 점프 애니메이션의 루트모션을 그대로 사용)")]
        public float airControl = 3f;
        [Range(0f, 1f)] public float jumpMomentumKeep = 1f;

        public bool IsGrounded { get; private set; } = true;
        public bool IsCrouching { get; private set; }
        public bool IsJumping { get; private set; }
        public bool IsAiming { get; private set; }
        public Vector2 BlendValue => blend;
        public Vector3 LookTarget { get; private set; }
        public float Yaw => yaw;

        CharacterController cc;
        Vector2 blend, blendVel, lastDir = Vector2.up;
        float yaw, yawVel;
        float verticalVel;
        float jumpTime;
        bool aimTurning;
        Vector3 horizVel, jumpMomentum, desiredWorldDir;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (!input) input = GetComponent<TPSInput>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            if (!cameraRig) cameraRig = FindFirstObjectByType<TPSCamera>();
            yaw = transform.eulerAngles.y;
            SetCapsuleHeight(standHeight);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            IsAiming = input.Aim;

            // ---- 앉기 토글 ----
            if (input.CrouchPressed && IsGrounded && !IsJumping)
            {
                if (IsCrouching) { if (CanStand()) IsCrouching = false; }
                else IsCrouching = true;
            }
            UpdateCapsule(dt);

            // ---- 카메라 기준 입력 방향 ----
            Vector2 mv = input.Move;
            float camYaw = cameraRig ? cameraRig.Yaw : yaw;
            desiredWorldDir = Quaternion.Euler(0f, camYaw, 0f) * new Vector3(mv.x, 0f, mv.y);
            float mag = Mathf.Clamp01(desiredWorldDir.magnitude);

            float level = (IsCrouching || IsAiming) ? walkLevel : (input.Run ? runLevel : walkLevel);

            UpdateRotation(mag, camYaw, dt);

            // ---- 블렌드트리 파라미터 (캐릭터 로컬 기준) ----
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f)) * desiredWorldDir;
            Vector2 target = new Vector2(local.x, local.z) * level;
            UpdateBlend(target, dt);

            // ---- 점프 ----
            if (input.JumpPressed && IsGrounded && !IsJumping && !IsCrouching)
            {
                IsJumping = true;
                jumpTime = 0f;
                jumpMomentum = horizVel * jumpMomentumKeep;
                animator.ResetTrigger(HashJump);
                animator.SetTrigger(HashJump);
            }
            if (IsJumping)
            {
                jumpTime += dt;
                // 공중 제어: 입력 방향으로 관성을 조금씩 틀어준다
                Vector3 wish = desiredWorldDir * Mathf.Max(jumpMomentum.magnitude, 1.2f * mag);
                jumpMomentum = Vector3.MoveTowards(jumpMomentum, wish, airControl * dt);
                if (jumpTime > 0.35f && !InJumpState())
                {
                    IsJumping = false;
                    verticalVel = 0f;
                }
            }

            // Speed = 크기(대기 0 / 걷기 1 / 달리기 2), MoveX/Z = 방향 (정규화)
            float speed = blend.magnitude;
            if (speed > 0.001f) lastDir = blend / speed;
            animator.SetFloat(HashSpeed, speed);
            animator.SetFloat(HashMoveX, lastDir.x);
            animator.SetFloat(HashMoveZ, lastDir.y);
            animator.SetBool(HashCrouch, IsCrouching);
            animator.SetBool(HashGrounded, IsGrounded);
            animator.SetBool(HashAim, IsAiming);

            LookTarget = cameraRig ? cameraRig.AimPoint
                                   : transform.position + Vector3.up * 1.6f + transform.forward * 10f;
        }

        void UpdateRotation(float mag, float camYaw, float dt)
        {
            if (IsAiming)
            {
                float diff = Mathf.Abs(Mathf.DeltaAngle(yaw, camYaw));
                bool moving = mag > 0.1f || blend.magnitude > 0.25f;
                if (moving || diff > idleAimTurnThreshold) aimTurning = true;
                if (aimTurning)
                {
                    yaw = Mathf.SmoothDampAngle(yaw, camYaw, ref yawVel, aimTurnTime, Mathf.Infinity, dt);
                    if (!moving && diff < 3f) aimTurning = false;
                }
                else yawVel = 0f;
            }
            else
            {
                aimTurning = false;
                if (mag > 0.1f && !IsJumping)
                {
                    float targetYaw = Mathf.Atan2(desiredWorldDir.x, desiredWorldDir.z) * Mathf.Rad2Deg;
                    float ang = Mathf.Abs(Mathf.DeltaAngle(yaw, targetYaw));
                    // 반대 방향일수록 천천히, 가까운 방향일수록 빠르게
                    float t = Mathf.Lerp(nearTurnTime, oppositeTurnTime, Smooth01(ang / 180f));
                    yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVel, t, Mathf.Infinity, dt);
                }
                else yawVel = 0f;
            }
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        void UpdateBlend(Vector2 target, float dt)
        {
            bool curZero = blend.sqrMagnitude < 0.0025f;
            bool tgtZero = target.sqrMagnitude < 0.0025f;
            float t;
            if (tgtZero) t = stopBlendTime;
            else if (curZero) t = startBlendTime;
            else
            {
                float ang = Vector2.Angle(blend, target);
                if (ang < 10f && Mathf.Abs(blend.magnitude - target.magnitude) > 0.2f) t = speedChangeBlendTime;
                else t = Mathf.Lerp(nearDirBlendTime, oppositeDirBlendTime, Smooth01(ang / 180f));
            }
            blend = Vector2.SmoothDamp(blend, target, ref blendVel, t, Mathf.Infinity, dt);
        }

        /// <summary>모델(Animator)의 OnAnimatorMove 에서 호출. 루트모션을 CharacterController 로 적용.</summary>
        public void ApplyRootMotion()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || cc == null || !cc.enabled) return;

            Vector3 d = animator.deltaPosition;
            Vector3 move;
            if (IsJumping)
            {
                // 수평은 점프 직전 관성, 수직은 점프 애니메이션(발 기준) 루트모션
                move = jumpMomentum * dt;
                move.y = d.y;
            }
            else
            {
                move = new Vector3(d.x, 0f, d.z);
                if (IsGrounded && verticalVel <= 0f) verticalVel = groundStickSpeed;
                else verticalVel += gravity * dt;
                move.y = verticalVel * dt;
                Vector3 h = new Vector3(d.x, 0f, d.z) / dt;
                horizVel = Vector3.Lerp(horizVel, h, 1f - Mathf.Exp(-15f * dt));
            }

            bool wasGrounded = IsGrounded;
            cc.Move(move);
            GroundCheck(wasGrounded);
        }

        void GroundCheck(bool wasGrounded)
        {
            float r = cc.radius * 0.9f;
            float up = cc.radius + 0.1f;
            Vector3 origin = transform.position + Vector3.up * up;
            float baseGap = up - r; // 캐스트 구의 바닥과 발바닥(루트) 사이 거리
            bool hitGround = Physics.SphereCast(origin, r, Vector3.down, out RaycastHit hit,
                baseGap + groundSnapDistance + 0.05f, groundMask, QueryTriggerInteraction.Ignore);

            if (!hitGround) { IsGrounded = cc.isGrounded && !IsJumping; return; }

            float gap = hit.distance - baseGap;
            bool walkable = Vector3.Angle(hit.normal, Vector3.up) <= cc.slopeLimit + 5f;

            if (IsJumping)
            {
                IsGrounded = gap < 0.06f && walkable;
                return;
            }

            if (wasGrounded && walkable && verticalVel <= 0f && gap < groundSnapDistance)
            {
                // 계단/경사 내려갈 때 공중에 뜨지 않도록 지면에 붙인다
                if (gap > 0.01f) cc.Move(Vector3.down * gap);
                IsGrounded = true;
            }
            else
            {
                IsGrounded = (gap < 0.06f && walkable) || cc.isGrounded;
            }
        }

        bool InJumpState()
        {
            if (animator.GetCurrentAnimatorStateInfo(0).IsTag("Jump")) return true;
            return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag("Jump");
        }

        void UpdateCapsule(float dt)
        {
            float target = IsCrouching ? crouchHeight : standHeight;
            float h = Mathf.Lerp(cc.height, target, 1f - Mathf.Exp(-capsuleLerpSpeed * dt));
            if (Mathf.Abs(h - target) < 0.001f) h = target;
            SetCapsuleHeight(h);
        }

        void SetCapsuleHeight(float h)
        {
            cc.height = h;
            cc.center = new Vector3(0f, h * 0.5f + cc.skinWidth, 0f);
        }

        bool CanStand()
        {
            float r = cc.radius * 0.95f;
            // 앉은 캡슐 위쪽 공간만 검사 (계단 바닥과 겹쳐서 못 일어나는 일 방지)
            Vector3 bottom = transform.position + Vector3.up * (crouchHeight - cc.radius + cc.skinWidth);
            Vector3 top = transform.position + Vector3.up * (standHeight - cc.radius + cc.skinWidth);
            return !Physics.CheckCapsule(bottom, top, r, groundMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>순간이동 (테스트/리스폰용)</summary>
        public void Teleport(Vector3 position, float newYaw)
        {
            cc.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, newYaw, 0f));
            cc.enabled = true;
            yaw = newYaw; yawVel = 0f;
            blend = Vector2.zero; blendVel = Vector2.zero;
            verticalVel = 0f; horizVel = Vector3.zero;
            IsGrounded = true;
        }

        static float Smooth01(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    }
}
