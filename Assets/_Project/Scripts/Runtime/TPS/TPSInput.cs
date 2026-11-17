using UnityEngine;
using UnityEngine.InputSystem;

namespace MyGame.TPS
{
    /// <summary>
    /// 키보드/마우스 입력을 한 프레임 단위로 모아두는 컴포넌트.
    /// WASD 이동 / Shift 달리기 / C 또는 Ctrl 앉기 토글 / Space 점프 / 마우스 우클릭 시선집중 / Esc 커서 해제
    /// useScripted 를 켜면 자동 테스트용 입력을 사용한다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class TPSInput : MonoBehaviour
    {
        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool Run { get; private set; }
        public bool Aim { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool CrouchPressed { get; private set; }

        [Header("커서")]
        public bool lockCursorOnStart = true;

        [Header("스크립트 입력 (자동 테스트용)")]
        public bool useScripted;
        public Vector2 scriptedMove;
        public Vector2 scriptedLook;
        public bool scriptedRun;
        public bool scriptedAim;
        public bool scriptedJump;
        public bool scriptedCrouchToggle;

        void Start()
        {
            if (lockCursorOnStart && !useScripted) LockCursor(true);
        }

        void Update()
        {
            if (useScripted)
            {
                Move = Vector2.ClampMagnitude(scriptedMove, 1f);
                Look = scriptedLook;
                Run = scriptedRun;
                Aim = scriptedAim;
                JumpPressed = scriptedJump; scriptedJump = false;
                CrouchPressed = scriptedCrouchToggle; scriptedCrouchToggle = false;
                return;
            }

            var k = Keyboard.current;
            var m = Mouse.current;

            Vector2 mv = Vector2.zero;
            if (k != null)
            {
                if (k.wKey.isPressed) mv.y += 1f;
                if (k.sKey.isPressed) mv.y -= 1f;
                if (k.dKey.isPressed) mv.x += 1f;
                if (k.aKey.isPressed) mv.x -= 1f;
                Run = k.leftShiftKey.isPressed;
                JumpPressed = k.spaceKey.wasPressedThisFrame;
                CrouchPressed = k.cKey.wasPressedThisFrame || k.leftCtrlKey.wasPressedThisFrame;
                if (k.escapeKey.wasPressedThisFrame) LockCursor(false);
            }
            else
            {
                Run = false; JumpPressed = false; CrouchPressed = false;
            }
            // 대각선 입력도 길이 1 로 정규화
            Move = Vector2.ClampMagnitude(mv, 1f);

            if (m != null)
            {
                bool locked = Cursor.lockState == CursorLockMode.Locked;
                Look = locked ? m.delta.ReadValue() : Vector2.zero;
                Aim = m.rightButton.isPressed;
                if (!locked && m.leftButton.wasPressedThisFrame) LockCursor(true);
            }
            else
            {
                Look = Vector2.zero; Aim = false;
            }
        }

        static void LockCursor(bool on)
        {
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }
    }
}
