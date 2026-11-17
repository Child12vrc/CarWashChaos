using UnityEngine;

namespace MyGame.TPS
{
    /// <summary>
    /// Animator 가 붙은 모델(자식)에서 루트모션 콜백을 받아 부모의 컨트롤러로 전달한다.
    /// (이 스크립트가 있으면 Unity 가 자식 Transform 에 루트모션을 직접 적용하지 않음)
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class TPSAnimatorBridge : MonoBehaviour
    {
        public TPSCharacterController owner;

        void OnAnimatorMove()
        {
            if (owner) owner.ApplyRootMotion();
        }
    }
}
