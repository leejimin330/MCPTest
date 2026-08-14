using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>
    /// 전투가 벌어지는 탑뷰 맵. 여기 있는 좌표는 <b>표시 전용</b>이다 —
    /// <see cref="FarmCreature.Core.BattleSimulator"/> 는 이 값을 한 번도 읽지 않는다 (설계 1.1).
    /// 그래서 스폰 좌표가 랜덤이어도 전투 결과는 여전히 결정적이다.
    ///
    /// 영역을 <see cref="Rect"/> 로 직접 두지 않고 중심+크기로 나눈 이유:
    /// 인스펙터에서 <c>Rect</c> 는 좌하단 모서리를 x·y 로 보여줘서, 맵 가운데를 기준으로
    /// 영역을 잡을 때 매번 암산을 해야 한다.
    /// </summary>
    [CreateAssetMenu(fileName = "BattleMap", menuName = "FarmCreature/Battle Map")]
    public class BattleMapData : ScriptableObject
    {
        public string displayName = "들판";

        [Header("바닥")]
        [Tooltip("탑뷰 바닥의 가로·세로 (월드 유닛).")]
        public Vector2 groundSize = new Vector2(17f, 9f);

        public Color groundColor = new Color(0.45f, 0.62f, 0.35f);

        [Header("아군 스폰 영역 (중심 + 크기)")]
        public Vector2 allySpawnCenter = new Vector2(-4.5f, -0.5f);
        public Vector2 allySpawnSize = new Vector2(5f, 5.5f);

        [Header("적 스폰 영역 (중심 + 크기)")]
        public Vector2 enemySpawnCenter = new Vector2(4.5f, -0.5f);
        public Vector2 enemySpawnSize = new Vector2(5f, 5.5f);

        [Tooltip("유닛끼리 최소 간격. 겹쳐 서는 것을 막는다.")]
        public float minSpacing = 1.4f;

        public Rect AllySpawnArea
        {
            get { return MakeRect(allySpawnCenter, allySpawnSize); }
        }

        public Rect EnemySpawnArea
        {
            get { return MakeRect(enemySpawnCenter, enemySpawnSize); }
        }

        private static Rect MakeRect(Vector2 center, Vector2 size)
        {
            return new Rect(
                center.x - size.x * 0.5f,
                center.y - size.y * 0.5f,
                size.x,
                size.y);
        }
    }
}
