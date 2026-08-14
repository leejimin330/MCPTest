using System.Collections.Generic;
using UnityEngine;

namespace FarmCreature.Core
{
    /// <summary>
    /// 전투 시작 시 유닛을 세울 좌표를 고른다. <b>표시 전용</b>이라 전투 규칙과 무관하다 (설계 1.1).
    ///
    /// <see cref="UnityEngine.Random"/> 대신 <see cref="System.Random"/> 을 쓰는 이유:
    /// 전역 난수 상태를 건드리지 않아야 같은 seed 로 항상 같은 배치가 나오고, 그래야 테스트가 선다.
    /// </summary>
    public static class SpawnPointPicker
    {
        /// <summary>한 좌표를 고르는 데 허용하는 시도 횟수. 넘으면 격자로 물러난다.</summary>
        private const int MaxAttemptsPerPoint = 24;

        /// <summary>
        /// <paramref name="area"/> 안에서 서로 <paramref name="minSpacing"/> 이상 떨어진 좌표를
        /// <paramref name="count"/> 개 고른다.
        ///
        /// 자리를 못 찾으면 <b>전량 격자로 물러난다.</b> 랜덤 좌표와 격자 좌표를 섞으면
        /// 간격 보장이 깨지므로, 한 번이라도 실패하면 처음부터 격자로 다시 깐다.
        /// 어느 쪽이든 좌표는 항상 영역 안이고 개수는 항상 <paramref name="count"/> 다.
        /// </summary>
        public static List<Vector2> PickSpawnPoints(Rect area, int count, float minSpacing, int seed)
        {
            var points = new List<Vector2>();
            if (count <= 0)
            {
                return points;
            }

            var random = new System.Random(seed);
            float sqrSpacing = minSpacing * minSpacing;

            for (int i = 0; i < count; i++)
            {
                Vector2 picked;
                if (!TryPick(area, points, sqrSpacing, random, out picked))
                {
                    return GridPoints(area, count);
                }

                points.Add(picked);
            }

            return points;
        }

        private static bool TryPick(
            Rect area, List<Vector2> taken, float sqrSpacing, System.Random random, out Vector2 picked)
        {
            for (int attempt = 0; attempt < MaxAttemptsPerPoint; attempt++)
            {
                var candidate = new Vector2(
                    area.xMin + (float)random.NextDouble() * area.width,
                    area.yMin + (float)random.NextDouble() * area.height);

                if (IsFarEnough(taken, candidate, sqrSpacing))
                {
                    picked = candidate;
                    return true;
                }
            }

            picked = Vector2.zero;
            return false;
        }

        private static bool IsFarEnough(List<Vector2> taken, Vector2 candidate, float sqrSpacing)
        {
            for (int i = 0; i < taken.Count; i++)
            {
                if ((taken[i] - candidate).sqrMagnitude < sqrSpacing)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>영역을 격자로 나눠 칸 가운데에 세운다. 좁아서 간격을 못 지킬 때의 최후 수단이다.</summary>
        private static List<Vector2> GridPoints(Rect area, int count)
        {
            var points = new List<Vector2>(count);

            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            if (columns < 1)
            {
                columns = 1;
            }

            int rows = Mathf.CeilToInt((float)count / columns);

            for (int i = 0; i < count; i++)
            {
                int column = i % columns;
                int row = i / columns;

                points.Add(new Vector2(
                    area.xMin + area.width * (column + 0.5f) / columns,
                    area.yMin + area.height * (row + 0.5f) / rows));
            }

            return points;
        }
    }
}
