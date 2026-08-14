using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 드래그 / 터치 스와이프로 카메라를 좌우로만 민다. 맵 밖은 보이지 않도록 클램프한다.
    ///
    /// 건물 클릭과 충돌하지 않도록 <see cref="DraggedThisGesture"/> 를 노출한다 —
    /// 손가락이 <see cref="dragThreshold"/> 이상 움직였으면 그 제스처는 '슬라이드'로 보고,
    /// <see cref="FarmBuilding"/> 은 클릭으로 처리하지 않는다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraPanController : MonoBehaviour
    {
        [Tooltip("이 픽셀 이상 끌면 클릭이 아니라 슬라이드로 본다.")]
        public float dragThreshold = 12f;

        [Tooltip("손을 뗐을 때 남는 관성. 0 이면 즉시 멈춘다.")]
        public float inertiaDamping = 6f;

        /// <summary>이번 누름-뗌 제스처가 슬라이드였는지. 건물이 클릭 여부를 판단할 때 본다.</summary>
        public static bool DraggedThisGesture { get; private set; }

        private Camera cam;
        private bool pressing;
        private float pressStartScreenX;
        private float lastWorldX;
        private float velocityX;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                BeginPress(Input.mousePosition.x);
            }
            else if (Input.GetMouseButton(0) && pressing)
            {
                ContinuePress(Input.mousePosition.x);
            }
            else if (Input.GetMouseButtonUp(0) && pressing)
            {
                pressing = false;
            }
            else if (!pressing)
            {
                ApplyInertia();
            }

            ClampPosition();
        }

        private void BeginPress(float screenX)
        {
            pressing = true;
            DraggedThisGesture = false;
            velocityX = 0f;
            pressStartScreenX = screenX;
            lastWorldX = ScreenToWorldX(screenX);
        }

        private void ContinuePress(float screenX)
        {
            if (!DraggedThisGesture && Mathf.Abs(screenX - pressStartScreenX) >= dragThreshold)
            {
                DraggedThisGesture = true;
            }

            if (!DraggedThisGesture)
            {
                return;
            }

            float worldX = ScreenToWorldX(screenX);
            float delta = lastWorldX - worldX;

            transform.position += new Vector3(delta, 0f, 0f);
            velocityX = Time.deltaTime > 0f ? delta / Time.deltaTime : 0f;

            // 카메라를 옮겼으니 같은 화면 좌표의 월드 X 도 따라 옮겨진다.
            lastWorldX = ScreenToWorldX(screenX);
        }

        private void ApplyInertia()
        {
            if (Mathf.Abs(velocityX) < 0.01f)
            {
                velocityX = 0f;
                return;
            }

            transform.position += new Vector3(velocityX * Time.deltaTime, 0f, 0f);
            velocityX = Mathf.Lerp(velocityX, 0f, inertiaDamping * Time.deltaTime);
        }

        /// <summary>카메라가 맵 밖을 비추지 않도록 X 를 제한한다.</summary>
        private void ClampPosition()
        {
            float halfViewWidth = cam.orthographicSize * cam.aspect;
            float limit = Mathf.Max(0f, FarmArea.HalfWidth - halfViewWidth);

            Vector3 p = transform.position;
            float clamped = Mathf.Clamp(p.x, -limit, limit);

            if (!Mathf.Approximately(clamped, p.x))
            {
                velocityX = 0f;
            }

            transform.position = new Vector3(clamped, p.y, p.z);
        }

        private float ScreenToWorldX(float screenX)
        {
            return cam.ScreenToWorldPoint(new Vector3(screenX, 0f, -transform.position.z)).x;
        }
    }
}
