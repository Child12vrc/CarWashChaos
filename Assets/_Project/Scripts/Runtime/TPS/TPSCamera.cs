using UnityEngine;

namespace MyGame.TPS
{
    /// <summary>
    /// TPS 숄더 카메라. 캐릭터의 왼쪽-뒤에 위치하며 캐릭터를 부드럽게 따라간다.
    /// 마우스로 회전, 우클릭(시선집중) 시 살짝 줌인. 벽에 막히면 카메라를 당긴다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(100)]
    public class TPSCamera : MonoBehaviour
    {
        [Header("대상")]
        public Transform target;
        public TPSInput input;
        public TPSCharacterController character;

        [Header("마우스")]
        public float sensitivity = 0.1f;
        public bool invertY;
        public float minPitch = -35f;
        public float maxPitch = 65f;
        public float startPitch = 12f;

        [Header("배치 (음수 shoulder = 캐릭터의 왼쪽)")]
        public float pivotHeight = 1.55f;
        public float crouchPivotHeight = 1.15f;
        public float distance = 3.2f;
        public float shoulderOffset = -0.65f;
        public float fov = 60f;

        [Header("시선집중 (우클릭) 줌")]
        public float aimDistance = 2.1f;
        public float aimShoulderOffset = -0.75f;
        public float aimPivotHeightOffset = 0.05f;
        public float aimFov = 48f;
        public float aimBlendSpeed = 7f;

        [Header("추적 스무딩")]
        public float followSmoothXZ = 0.04f;
        public float followSmoothY = 0.12f;

        [Header("충돌")]
        public float collisionRadius = 0.2f;
        public LayerMask collisionMask = ~(1 << 6);
        public float returnSpeed = 6f;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public Vector3 AimPoint { get; private set; }

        Camera cam;
        Vector3 pivot, pivotVelXZ;
        float pivotVelY, aimT, crouchT, curDist;
        bool initialized;

        void Awake()
        {
            cam = GetComponent<Camera>();
        }

        void Start()
        {
            if (!character && target) character = target.GetComponent<TPSCharacterController>();
            if (!input && target) input = target.GetComponent<TPSInput>();
            Init();
        }

        void Init()
        {
            if (!target) return;
            Yaw = target.eulerAngles.y;
            Pitch = startPitch;
            pivot = target.position + Vector3.up * pivotHeight;
            curDist = distance;
            initialized = true;
            AimPoint = target.position + Vector3.up * 1.6f + target.forward * 20f;
        }

        public void SetAngles(float yaw, float pitch)
        {
            Yaw = yaw; Pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        void LateUpdate()
        {
            if (!target) return;
            if (!initialized) Init();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // ---- 마우스 회전 ----
            Vector2 look = input ? input.Look : Vector2.zero;
            Yaw += look.x * sensitivity;
            Pitch += (invertY ? look.y : -look.y) * sensitivity;
            Pitch = Mathf.Clamp(Pitch, minPitch, maxPitch);
            Yaw = Mathf.Repeat(Yaw + 180f, 360f) - 180f;

            // ---- 줌/앉기 보간 ----
            bool aiming = character && character.IsAiming;
            bool crouching = character && character.IsCrouching;
            aimT = Mathf.Lerp(aimT, aiming ? 1f : 0f, 1f - Mathf.Exp(-aimBlendSpeed * dt));
            crouchT = Mathf.Lerp(crouchT, crouching ? 1f : 0f, 1f - Mathf.Exp(-6f * dt));
            float e = aimT * aimT * (3f - 2f * aimT);

            // ---- 피벗 추적 (수직은 더 부드럽게: 계단에서 출렁임 방지) ----
            float h = Mathf.Lerp(pivotHeight, crouchPivotHeight, crouchT) + aimPivotHeightOffset * e;
            Vector3 desired = target.position + Vector3.up * h;
            Vector3 pxz = Vector3.SmoothDamp(new Vector3(pivot.x, 0f, pivot.z), new Vector3(desired.x, 0f, desired.z),
                                             ref pivotVelXZ, followSmoothXZ, Mathf.Infinity, dt);
            float py = Mathf.SmoothDamp(pivot.y, desired.y, ref pivotVelY, followSmoothY, Mathf.Infinity, dt);
            pivot = new Vector3(pxz.x, py, pxz.z);

            Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
            float dist = Mathf.Lerp(distance, aimDistance, e);
            float shoulder = Mathf.Lerp(shoulderOffset, aimShoulderOffset, e);

            // ---- 어깨 위치 (피벗 → 어깨 방향 충돌 체크) ----
            Vector3 shoulderDir = rot * Vector3.right * Mathf.Sign(shoulder);
            float shoulderLen = Mathf.Abs(shoulder);
            if (Physics.SphereCast(pivot, collisionRadius, shoulderDir, out RaycastHit sh, shoulderLen, collisionMask, QueryTriggerInteraction.Ignore))
                shoulderLen = Mathf.Max(0f, sh.distance);
            Vector3 shoulderPos = pivot + shoulderDir * shoulderLen;

            // ---- 거리 (어깨 → 뒤쪽 충돌 체크) ----
            Vector3 back = rot * Vector3.back;
            float allowed = dist;
            if (Physics.SphereCast(shoulderPos, collisionRadius, back, out RaycastHit hit, dist, collisionMask, QueryTriggerInteraction.Ignore))
                allowed = Mathf.Max(0.3f, hit.distance);
            curDist = allowed < curDist ? allowed : Mathf.Lerp(curDist, allowed, 1f - Mathf.Exp(-returnSpeed * dt));

            transform.SetPositionAndRotation(shoulderPos + back * curDist, rot);
            cam.fieldOfView = Mathf.Lerp(fov, aimFov, e);

            // ---- 조준점 (머리가 바라볼 지점) ----
            Vector3 rayOrigin = transform.position + transform.forward * (curDist + 0.3f);
            AimPoint = Physics.Raycast(rayOrigin, transform.forward, out RaycastHit aimHit, 200f, collisionMask, QueryTriggerInteraction.Ignore)
                ? aimHit.point
                : rayOrigin + transform.forward * 200f;
        }
    }
}
