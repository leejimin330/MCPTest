using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 땅 위를 <b>좌우로만</b> 걷는다 (A/D, ←/→). 레거시 Input Manager 사용.
    /// Y 는 매 프레임 땅 윗면에 맞춰 고정한다.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 5f;
        public float margin = 0.5f;

        private void Start()
        {
            SnapToGround(transform.position.x);
        }

        private void Update()
        {
            float x = Input.GetAxisRaw("Horizontal");

            if (x == 0f)
            {
                return;
            }

            SnapToGround(transform.position.x + x * moveSpeed * Time.deltaTime);
        }

        private void SnapToGround(float x)
        {
            transform.position = FarmArea.OnGround(x, transform.localScale.y, margin);
        }
    }
}
