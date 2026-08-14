using FarmCreature.Core;
using NUnit.Framework;
using UnityEngine;

namespace FarmCreature.Tests
{
    /// <summary>
    /// 스폰 좌표는 랜덤이지만 <b>전투 결과는 여전히 결정적이다</b> — 규칙이 좌표를 읽지 않기 때문이다.
    /// 여기서 못박는 것은 "맵 밖에 서지 않는다 / 겹쳐 서지 않는다 / 무한 루프에 빠지지 않는다" 셋이다.
    /// </summary>
    public class SpawnPointPickerTests
    {
        private static readonly Rect Roomy = new Rect(-5f, -3f, 10f, 6f);
        private static readonly Rect Cramped = new Rect(0f, 0f, 4f, 4f);

        [Test]
        public void PickSpawnPoints_ReturnsTheRequestedCount()
        {
            var points = SpawnPointPicker.PickSpawnPoints(Roomy, 8, minSpacing: 1f, seed: 1);

            Assert.AreEqual(8, points.Count);
        }

        [Test]
        public void PickSpawnPoints_StaysInsideTheArea()
        {
            var points = SpawnPointPicker.PickSpawnPoints(Roomy, 8, minSpacing: 1f, seed: 7);

            for (int i = 0; i < points.Count; i++)
            {
                Assert.GreaterOrEqual(points[i].x, Roomy.xMin, "좌표 " + i + " 가 왼쪽으로 벗어났다");
                Assert.LessOrEqual(points[i].x, Roomy.xMax, "좌표 " + i + " 가 오른쪽으로 벗어났다");
                Assert.GreaterOrEqual(points[i].y, Roomy.yMin, "좌표 " + i + " 가 아래로 벗어났다");
                Assert.LessOrEqual(points[i].y, Roomy.yMax, "좌표 " + i + " 가 위로 벗어났다");
            }
        }

        [Test]
        public void PickSpawnPoints_KeepsUnitsApart()
        {
            const float spacing = 1.2f;
            var points = SpawnPointPicker.PickSpawnPoints(Roomy, 6, spacing, seed: 3);

            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    float distance = Vector2.Distance(points[i], points[j]);
                    Assert.GreaterOrEqual(distance, spacing - 0.001f,
                        "유닛 " + i + " 와 " + j + " 가 겹쳐 선다 (거리 " + distance + ")");
                }
            }
        }

        [Test]
        public void PickSpawnPoints_IsReproducibleForTheSameSeed()
        {
            var first = SpawnPointPicker.PickSpawnPoints(Roomy, 5, 1f, seed: 42);
            var second = SpawnPointPicker.PickSpawnPoints(Roomy, 5, 1f, seed: 42);

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void PickSpawnPoints_DiffersForADifferentSeed()
        {
            var first = SpawnPointPicker.PickSpawnPoints(Roomy, 5, 1f, seed: 1);
            var second = SpawnPointPicker.PickSpawnPoints(Roomy, 5, 1f, seed: 2);

            CollectionAssert.AreNotEqual(first, second,
                "seed 가 달라도 같은 배치가 나오면 매 전투가 똑같아 보인다");
        }

        [Test]
        public void PickSpawnPoints_TerminatesWhenTheAreaIsTooCrowded()
        {
            // 간격 5 를 12마리에게 주려면 이 영역으로는 절대 불가능하다.
            // 물러날 곳이 없다고 무한 루프에 빠지거나 개수를 줄이면 안 된다.
            var points = SpawnPointPicker.PickSpawnPoints(Cramped, 12, 5f, seed: 9);

            Assert.AreEqual(12, points.Count);

            for (int i = 0; i < points.Count; i++)
            {
                // Rect.Contains 는 오른쪽·위쪽 경계를 포함하지 않아 직접 비교한다.
                Assert.GreaterOrEqual(points[i].x, Cramped.xMin, "격자로 물러났더라도 영역 안이어야 한다");
                Assert.LessOrEqual(points[i].x, Cramped.xMax, "격자로 물러났더라도 영역 안이어야 한다");
                Assert.GreaterOrEqual(points[i].y, Cramped.yMin, "격자로 물러났더라도 영역 안이어야 한다");
                Assert.LessOrEqual(points[i].y, Cramped.yMax, "격자로 물러났더라도 영역 안이어야 한다");
            }
        }

        [Test]
        public void PickSpawnPoints_ReturnsEmptyForZeroCount()
        {
            Assert.AreEqual(0, SpawnPointPicker.PickSpawnPoints(Roomy, 0, 1f, seed: 1).Count);
            Assert.AreEqual(0, SpawnPointPicker.PickSpawnPoints(Roomy, -3, 1f, seed: 1).Count);
        }
    }
}
