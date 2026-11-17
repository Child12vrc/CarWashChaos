using UnityEngine;

namespace MyGame.TPS
{
    /// <summary>테스트 맵용 간단한 조작 안내 + 상태 표시 + 시선집중 시 조준점.</summary>
    public class TPSHud : MonoBehaviour
    {
        public TPSCharacterController character;
        public bool showHelp = true;

        GUIStyle box, dot;

        void OnGUI()
        {
            if (!character) return;
            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, richText = true };
                box.normal.textColor = Color.white;
                box.padding = new RectOffset(10, 10, 8, 8);
            }

            if (showHelp)
            {
                string s =
                    "<b>TPS 테스트</b>\n" +
                    "WASD : 이동 (대각선 포함)\n" +
                    "Shift : 달리기\n" +
                    "C / Ctrl : 앉기 토글\n" +
                    "Space : 점프\n" +
                    "마우스 : 카메라 / 시선\n" +
                    "우클릭 : 시선집중 (줌)\n" +
                    "Esc : 커서 해제, 클릭 : 다시 잠금\n\n" +
                    $"Grounded {character.IsGrounded}  Crouch {character.IsCrouching}\n" +
                    $"Jump {character.IsJumping}  Aim {character.IsAiming}\n" +
                    $"Blend ({character.BlendValue.x:0.00}, {character.BlendValue.y:0.00})";
                GUI.Box(new Rect(12, 12, 270, 230), s, box);
            }

            if (character.IsAiming)
            {
                float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
                var c = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.85f);
                GUI.DrawTexture(new Rect(cx - 2, cy - 2, 4, 4), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 12, cy - 1, 6, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 6, cy - 1, 6, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 1, cy - 12, 2, 6), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 1, cy + 6, 2, 6), Texture2D.whiteTexture);
                GUI.color = c;
            }
        }
    }
}
