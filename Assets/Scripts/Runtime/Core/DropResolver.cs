using System.Collections.Generic;
using FarmCreature.Data;

namespace FarmCreature.Core
{
    /// <summary>드랍 한 종류와 개수. 인벤토리에 넣을 것이라 id 와 수량만 있으면 된다.</summary>
    public struct DropAward
    {
        public string ItemId;
        public int Count;
    }

    /// <summary>
    /// 쓰러뜨린 적들에게서 무엇이 나오는가. <b>확률을 굴리는 유일한 곳이다.</b>
    ///
    /// <b>왜 전투 밖에 있는가</b>: <see cref="BattleSimulator"/> 는 난수를 부르지 않는다는 성질이
    /// 이 프로젝트의 규율이다(설계 2.5) — 그래야 좌표만 고정하면 전투가 완전히 결정적이고
    /// 밸런스 하네스로 수천 번 돌릴 수 있다. 드랍에 확률이 필요하지만 그것을 시뮬레이터 안에서
    /// 굴리면 그 성질이 깨진다. 그래서 <b>스폰 좌표와 같은 방식</b>을 따랐다 —
    /// <b>seed 를 주입받아</b> 굴리고, 게임은 랜덤 seed 를, 테스트는 고정 seed 를 넘긴다.
    ///
    /// <b>승리했을 때만 부른다.</b> 재화와 같은 규칙이다(<see cref="BattleResult.GoldReward"/>) —
    /// 이기면 적이 전멸했으므로 인카운터의 적 전부가 드랍을 굴린다. 그래서 시뮬레이터가
    /// "누가 죽었는지" 를 따로 알려줄 필요가 없고, <b>시뮬레이터를 한 줄도 고치지 않았다.</b>
    /// </summary>
    public static class DropResolver
    {
        /// <summary>
        /// <paramref name="defeated"/> 는 <see cref="BattleEncounter.BuildEnemyList"/> 처럼
        /// <b>한 마리씩 펼친 목록</b>이다 — 3마리면 세 번 굴린다.
        /// 같은 id 가 여러 번 나오면 하나로 합쳐 돌려준다.
        /// </summary>
        public static List<DropAward> Resolve(List<EnemyData> defeated, int seed)
        {
            var awards = new List<DropAward>();

            if (defeated == null)
            {
                return awards;
            }

            var random = new System.Random(seed);

            for (int i = 0; i < defeated.Count; i++)
            {
                var enemy = defeated[i];
                if (enemy == null || enemy.drops == null)
                {
                    continue;
                }

                for (int d = 0; d < enemy.drops.Count; d++)
                {
                    var drop = enemy.drops[d];

                    // 쓸모없는 항목은 굴리지도 않는다 — 데이터 실수가 난수 순서를 바꾸면
                    // 같은 seed 로도 결과가 달라져 재현이 깨진다.
                    if (drop == null || string.IsNullOrEmpty(drop.itemId)
                        || drop.count <= 0 || drop.chancePercent <= 0)
                    {
                        continue;
                    }

                    // 0..99 를 뽑아 확률과 비교한다. 100 이면 항상 참이다.
                    if (random.Next(100) < drop.chancePercent)
                    {
                        Add(awards, drop.itemId, drop.count);
                    }
                }
            }

            return awards;
        }

        private static void Add(List<DropAward> awards, string itemId, int count)
        {
            for (int i = 0; i < awards.Count; i++)
            {
                if (awards[i].ItemId == itemId)
                {
                    var merged = awards[i];
                    merged.Count += count;
                    awards[i] = merged;
                    return;
                }
            }

            awards.Add(new DropAward { ItemId = itemId, Count = count });
        }
    }
}
