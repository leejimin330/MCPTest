using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 농장의 측면 뷰 지형. 화면 아래쪽에 땅 사각형이 깔리고, 캐릭터는 그 <b>윗면 위에 서서</b>
    /// 좌우로만 이동한다 (위아래 이동 없음).
    /// 맵은 화면보다 넓어서 카메라가 좌우로 슬라이드한다 (<see cref="CameraPanController"/>).
    /// </summary>
    public static class FarmArea
    {
        /// <summary>땅 윗면의 Y 좌표. 캐릭터의 발이 닿는 선.</summary>
        public const float GroundSurfaceY = -2f;

        /// <summary>땅 사각형이 윗면 아래로 뻗는 두께. 화면 밖까지 덮도록 넉넉히 잡는다.</summary>
        public const float GroundThickness = 4f;

        /// <summary>
        /// 맵의 좌우 반너비. 16:9 화면 반너비는 약 8.9 이므로 슬라이드가 필요하다.
        /// 건물 4동이 −9 / −3 / 3 / 9 에 서 있어 한 화면에 2~3동이 보이고,
        /// 밀면 나머지가 드러난다.
        /// </summary>
        public const float HalfWidth = 13f;

        public static float ClampX(float x, float margin)
        {
            return Mathf.Clamp(x, -HalfWidth + margin, HalfWidth - margin);
        }

        public static float RandomX(float margin)
        {
            return Random.Range(-HalfWidth + margin, HalfWidth - margin);
        }

        /// <summary>
        /// 발이 땅 윗면에 닿도록 오브젝트의 <b>중심</b> Y 를 구한다.
        /// 스프라이트는 가운데 피벗이고 스케일 1 에서 1 유닛 높이라, 높이의 절반만큼 띄우면 된다.
        /// </summary>
        public static float StandingY(float visualHeight)
        {
            return GroundSurfaceY + visualHeight * 0.5f;
        }

        /// <summary>X 만 유지한 채 땅 위에 올려놓은 위치.</summary>
        public static Vector3 OnGround(float x, float visualHeight, float margin)
        {
            return new Vector3(ClampX(x, margin), StandingY(visualHeight), 0f);
        }
    }
}
