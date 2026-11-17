using System.Collections;
using System.Text;
using UnityEngine;

namespace MyGame.TPS
{
    /// <summary>
    /// 자동 검증용 드라이버. 플레이어에 붙이고 플레이하면 정해진 입력 시퀀스를 돌리며
    /// 발 관통 / 디딤발 미끄러짐 / 계단 높이 / 머리 회전각 등을 콘솔에 [TPSTest] 로 출력한다.
    /// (평소에는 붙이지 않아도 됨)
    /// </summary>
    public class TPSAutoTest : MonoBehaviour
    {
        public float timeScale = 1f;
        public LayerMask groundMask = ~(1 << 6);

        TPSCharacterController ctrl;
        TPSInput input;
        TPSCamera cam;
        Animator anim;
        Transform lf, rf, head;
        Quaternion headRel0;

        // metrics
        string phaseName;
        float minClearance, slideSum, maxYawHead, minY, maxY;
        int slideCount;
        Vector3 phaseStart, lastL, lastR;
        bool lWasPlanted, rWasPlanted;
        float phaseTime;
        bool measuring;
        readonly StringBuilder report = new StringBuilder();

        IEnumerator Start()
        {
            ctrl = GetComponent<TPSCharacterController>();
            input = GetComponent<TPSInput>();
            cam = FindFirstObjectByType<TPSCamera>();
            anim = ctrl.animator;
            lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            head = anim.GetBoneTransform(HumanBodyBones.Head);
            input.useScripted = true;
            Time.timeScale = timeScale;

            cam.SetAngles(0f, 10f);
            yield return Wait(1.5f);
            headRel0 = Quaternion.Inverse(transform.rotation) * head.rotation;

            yield return Phase("Idle", 1.5f, () => { });
            // 장애물 없는 직선 구간 (x=6 → -x 방향)
            ctrl.Teleport(new Vector3(6f, 0f, 0f), -90f); cam.SetAngles(-90f, 10f);
            yield return Wait(0.8f);
            yield return Phase("Walk forward", 3f, () => Move(0, 1));
            yield return Phase("Run forward", 3f, () => { Move(0, 1); input.scriptedRun = true; });
            yield return Phase("Run -> reverse (S)", 2.5f, () => { Move(0, -1); input.scriptedRun = true; });
            yield return Phase("Stop (decel)", 1.5f, () => { Move(0, 0); input.scriptedRun = false; });
            ctrl.Teleport(new Vector3(0f, 0f, -2f), 0f); cam.SetAngles(0f, 10f);
            yield return Wait(0.8f);
            yield return Phase("Walk fwd", 1.5f, () => Move(0, 1));
            yield return Phase("Walk -> right 90deg", 2f, () => Move(1, 0));
            yield return Phase("Walk diagonal", 2.5f, () => Move(1, 1));
            yield return Phase("Stop", 1.2f, () => Move(0, 0));
            ctrl.Teleport(new Vector3(0f, 0f, 0f), 0f); cam.SetAngles(0f, 10f);
            yield return Wait(0.8f);
            yield return Phase("Aim diagonal FL (rest)", 2f, () => { input.scriptedAim = true; Move(-1, 1); });
            yield return Phase("Aim strafe left", 2f, () => { input.scriptedAim = true; Move(-1, 0); });
            yield return Phase("Aim walk back", 2f, () => { input.scriptedAim = true; Move(0, -1); });
            yield return Phase("Aim diagonal FR", 2f, () => { input.scriptedAim = true; Move(1, 1); });
            yield return Phase("Aim stop", 1f, () => { input.scriptedAim = false; Move(0, 0); });
            input.scriptedCrouchToggle = true;
            yield return Phase("Crouch idle", 1.2f, () => Move(0, 0));
            yield return Phase("Crouch walk", 2.5f, () => Move(0, 1));
            yield return Phase("Crouch strafe", 2f, () => Move(1, 0));
            Move(0, 0);
            yield return Wait(0.6f);
            input.scriptedCrouchToggle = true;
            yield return Phase("Stand up", 1f, () => { });

            // 머리 가동범위: 카메라를 뒤로 돌려본다
            cam.SetAngles(ctrl.Yaw + 60f, 10f);
            yield return Phase("Look 60deg right", 1.5f, () => { });
            cam.SetAngles(ctrl.Yaw + 120f, 10f);
            yield return Phase("Look 120deg", 1.5f, () => { });
            cam.SetAngles(ctrl.Yaw + 179f, 10f);
            yield return Phase("Look behind 179deg", 1.5f, () => { });
            cam.SetAngles(ctrl.Yaw, 10f);
            yield return Wait(1f);

            // 점프
            input.scriptedJump = true;
            yield return Phase("Jump in place", 2f, () => { });
            yield return Phase("Run before jump", 1.5f, () => { Move(0, 1); input.scriptedRun = true; });
            input.scriptedJump = true;
            yield return Phase("Running jump", 2f, () => { Move(0, 1); input.scriptedRun = true; });
            Move(0, 0); input.scriptedRun = false;
            yield return Wait(1.5f);

            // 계단 A 오르기 (단 20cm) → 플랫폼 2m
            ctrl.Teleport(new Vector3(0f, 0f, 4f), 0f); cam.SetAngles(0f, 10f);
            yield return Wait(1f);
            yield return Phase("Stairs A up (20cm)", 5.5f, () => Move(0, 1));
            // 계단 B 내려가기 (12.5cm)
            yield return Phase("Platform -> Stairs B down", 6f, () => Move(0, 1));
            Move(0, 0);
            yield return Wait(1f);
            // 계단 B 뛰어 오르기
            ctrl.Teleport(new Vector3(0f, 0f, 22f), 180f); cam.SetAngles(180f, 10f);
            yield return Wait(1f);
            yield return Phase("Stairs B run up", 3f, () => { Move(0, 1); input.scriptedRun = true; });
            Move(0, 0); input.scriptedRun = false;
            yield return Wait(1f);
            // 계단 C 내려가기 (25cm)
            ctrl.Teleport(new Vector3(1.5f, 2f, 12.5f), 90f); cam.SetAngles(90f, 10f);
            yield return Wait(1f);
            yield return Phase("Stairs C down (25cm)", 4f, () => Move(0, 1));
            Move(0, 0);
            yield return Wait(1f);

            Debug.Log("[TPSTest] ===== REPORT =====\n" + report);
            Time.timeScale = 1f;
            input.useScripted = false;
        }

        void Move(float x, float z) { input.scriptedMove = new Vector2(x, z); }

        IEnumerator Wait(float t) { float e = 0; while (e < t) { e += Time.deltaTime; yield return null; } }

        IEnumerator Phase(string name, float duration, System.Action setup)
        {
            phaseName = name;
            minClearance = 999f; slideSum = 0f; slideCount = 0; maxYawHead = 0f;
            minY = 999f; maxY = -999f;
            phaseStart = transform.position;
            lastL = lf.position; lastR = rf.position;
            lWasPlanted = rWasPlanted = false;
            phaseTime = 0f;
            measuring = true;
            float e = 0f;
            while (e < duration)
            {
                setup();
                e += Time.deltaTime;
                yield return null;
            }
            measuring = false;
            Vector3 d = transform.position - phaseStart; d.y = 0;
            string line = $"{name,-26} dist {d.magnitude,5:0.00}m  avg {d.magnitude / duration,4:0.00}m/s  y[{minY:0.00}..{maxY:0.00}]  " +
                          $"footClear(min) {minClearance * 100f,6:0.0}cm  plantedSlide {(slideCount > 0 ? slideSum / slideCount : 0f) * 100f,5:0.0}cm/s  headYaw(max) {maxYawHead,5:0.0}°  " +
                          $"blend({ctrl.BlendValue.x:0.00},{ctrl.BlendValue.y:0.00}) grounded {ctrl.IsGrounded}";
            report.AppendLine(line);
            Debug.Log("[TPSTest] " + line);
        }

        void LateUpdate()
        {
            if (!measuring) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            phaseTime += dt;
            minY = Mathf.Min(minY, transform.position.y);
            maxY = Mathf.Max(maxY, transform.position.y);

            float cl = Clearance(lf, anim.leftFeetBottomHeight);
            float cr = Clearance(rf, anim.rightFeetBottomHeight);
            bool valid = ctrl.IsGrounded && !ctrl.IsJumping;
            if (valid) minClearance = Mathf.Min(minClearance, Mathf.Min(cl, cr));
            // 지지발(더 낮은 발)만 미끄러짐 측정
            bool lSupport = valid && cl <= cr && cl < 0.02f;
            bool rSupport = valid && cr < cl && cr < 0.02f;
            Slide(lf, lSupport, ref lastL, ref lWasPlanted, dt);
            Slide(rf, rSupport, ref lastR, ref rWasPlanted, dt);

            Quaternion rel = Quaternion.Inverse(transform.rotation) * head.rotation * Quaternion.Inverse(headRel0);
            Vector3 f = rel * Vector3.forward;
            float yaw = Mathf.Abs(Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg);
            maxYawHead = Mathf.Max(maxYawHead, yaw);
        }

        float Clearance(Transform foot, float bottom)
        {
            Vector3 p = foot.position;
            // 발 위쪽(0.25m)에서 쏴서 발이 실제로 놓인 면을 찾는다
            if (Physics.Raycast(p + Vector3.up * 0.25f, Vector3.down, out RaycastHit hit, 1.5f, groundMask, QueryTriggerInteraction.Ignore))
                return p.y - bottom - hit.point.y;
            return 1f;
        }

        void Slide(Transform foot, bool planted, ref Vector3 last, ref bool wasPlanted, float dt)
        {
            Vector3 p = foot.position;
            if (planted && wasPlanted && phaseTime > 0.3f)
            {
                Vector3 v = p - last; v.y = 0f;
                slideSum += v.magnitude / dt;
                slideCount++;
            }
            wasPlanted = planted;
            last = p;
        }
    }
}
