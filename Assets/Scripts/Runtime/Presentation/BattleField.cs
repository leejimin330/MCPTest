using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 전투 씬의 탑뷰 평면. 농장의 <see cref="FarmArea"/> 에 대응한다.
    ///
    /// <b>여기 있는 좌표는 전부 표시 전용이다</b> — <see cref="FarmCreature.Core.BattleSimulator"/> 는
    /// 이 값을 한 번도 읽지 않는다 (설계 1.1). 이동·사거리를 넣고 싶어지면 그때는 이 결정을
    /// 뒤집는 별도 설계가 필요하다.
    /// </summary>
    public static class BattleField
    {
        /// <summary>
        /// 씬에 미리 만들어 두는 적 슬롯 수. 인카운터 총원이 이보다 많으면
        /// <c>BuildAll</c> 이 <c>LogError</c> 로 실패시킨다 (설계 4.3).
        /// <b>이 값을 바꾸면 슬롯이 씬에 고정돼 있으므로 BuildAll 재실행이 필요하다.</b>
        /// </summary>
        public const int MaxEnemies = 8;

        /// <summary>탑뷰라서 아래(작은 y)에 선 유닛이 앞에 그려져야 겹침이 자연스럽다.</summary>
        public static int SortingOrderFor(float y)
        {
            return Mathf.RoundToInt(-y * 10f);
        }

        /// <summary>z 는 0 으로 고정한다. 앞뒤는 <see cref="SortingOrderFor"/> 로만 가른다.</summary>
        public static Vector3 ToWorld(Vector2 point)
        {
            return new Vector3(point.x, point.y, 0f);
        }
    }
}
