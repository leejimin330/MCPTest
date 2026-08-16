using System.Collections.Generic;
using FarmCreature.Data;
using UnityEngine;

namespace FarmCreature.Core
{
    public enum BattleOutcome
    {
        InProgress,
        Victory,
        Defeat
    }

    /// <summary>
    /// 자동전투. 아무도 조작하지 않고, 각자 자기 타이머에 따라 평타와 고유 스킬을 쓴다.
    ///
    /// <b>난수를 쓰지 않는다</b> — 같은 입력이면 같은 결과가 나오므로 EditMode 에서
    /// 시간을 원하는 만큼 흘려보내며 검증할 수 있다. UnityEngine API 도 부르지 않는다.
    ///
    /// <see cref="Tick"/> 에 흐른 시간을 넘기는 방식이라 배속·일시정지가 공짜로 따라온다.
    /// </summary>
    public class BattleSimulator
    {
        /// <summary>전투가 길어져도 로그가 무한히 쌓이지 않도록 오래된 줄부터 버린다.</summary>
        public const int MaxLogLines = 40;

        /// <summary>한 틱에 같은 유닛이 무한히 행동하지 않게 막는 상한 (긴 dt 방어).</summary>
        private const int MaxActionsPerTickPerUnit = 8;

        /// <summary>
        /// 유닛끼리 이 거리보다 가까우면 밀어낸다 (설계 4절).
        /// <b><see cref="BattleUnit.DefaultAttackRange"/> 보다 작아야 한다</b> —
        /// 크면 사거리 안에 들기 전에 서로 밀어내 영원히 못 때린다.
        /// </summary>
        public const float DefaultMinSpacing = 0.9f;

        private readonly List<BattleUnit> allies = new List<BattleUnit>();
        private readonly List<BattleUnit> enemies = new List<BattleUnit>();
        private readonly List<string> log = new List<string>();

        /// <summary>겹침을 막는 최소 간격. 맵마다 다를 수 있어 생성자가 값으로 받는다.</summary>
        private readonly float minSpacing;

        public IReadOnlyList<BattleUnit> Allies
        {
            get { return allies; }
        }

        public IReadOnlyList<BattleUnit> Enemies
        {
            get { return enemies; }
        }

        public IReadOnlyList<string> Log
        {
            get { return log; }
        }

        public BattleOutcome Outcome { get; private set; }

        /// <summary>승리했을 때 받을 재화. 전투 중에는 0 이다.</summary>
        public int GoldReward { get; private set; }

        public float ElapsedSeconds { get; private set; }

        /// <summary>
        /// <paramref name="allyPositions"/> · <paramref name="enemyPositions"/> 는 스폰 좌표다.
        /// <b>시뮬레이터 안에서는 난수를 부르지 않는다</b> — 배치를 고르는 일은 호출부가 하고,
        /// 여기서는 받은 좌표를 그대로 쓴다. 그래서 게임은 매번 다르고 테스트는 완전히
        /// 결정적이다 (설계 3.1).
        /// </summary>
        public BattleSimulator(
            IList<CreatureInstance> party,
            IList<EnemyData> foes,
            IList<Vector2> allyPositions,
            IList<Vector2> enemyPositions,
            float minSpacing = DefaultMinSpacing)
        {
            this.minSpacing = minSpacing;

            if (party != null)
            {
                for (int i = 0; i < party.Count; i++)
                {
                    var unit = BattleUnit.FromCreature(party[i]);
                    if (unit != null)
                    {
                        allies.Add(unit);
                    }
                }
            }

            if (foes != null)
            {
                for (int i = 0; i < foes.Count; i++)
                {
                    var unit = BattleUnit.FromEnemy(foes[i]);
                    if (unit != null)
                    {
                        enemies.Add(unit);
                    }
                }
            }

            // 좌표는 Resolve 보다 먼저 선다 — 규칙이 좌표를 읽기 때문이다.
            Place(allies, allyPositions);
            Place(enemies, enemyPositions);

            // 한쪽이 비어 있으면 시작하자마자 끝난다. 파티가 비었을 때가 이 경우다.
            Outcome = Resolve();

            if (Outcome == BattleOutcome.InProgress)
            {
                Append("전투 시작!");
            }
            else if (allies.Count == 0)
            {
                Append("내보낼 케어가 없다.");
            }
        }

        /// <summary>
        /// 좌표가 유닛 수보다 적으면 남는 유닛은 원점에 선다. 짧은 목록은 호출부의 실수지만
        /// 예외로 죽이지 않는다 — 전투가 안 열리는 것보다 겹쳐 서는 편이 낫다 (설계 3.1).
        /// </summary>
        private static void Place(List<BattleUnit> units, IList<Vector2> positions)
        {
            for (int i = 0; i < units.Count; i++)
            {
                units[i].Position = positions != null && i < positions.Count
                    ? positions[i]
                    : Vector2.zero;
            }
        }

        public void Tick(float deltaTime)
        {
            if (Outcome != BattleOutcome.InProgress || deltaTime <= 0f)
            {
                return;
            }

            ElapsedSeconds += deltaTime;

            // 아군 먼저, 그다음 적. 순서를 고정해야 결과가 재현된다.
            Advance(allies, enemies, deltaTime);
            Advance(enemies, allies, deltaTime);

            Outcome = Resolve();

            if (Outcome == BattleOutcome.Victory)
            {
                GoldReward = TotalGold();
                Append("승리! 재화 " + GoldReward + " 획득.");
            }
            else if (Outcome == BattleOutcome.Defeat)
            {
                Append("파티가 전멸했다...");
            }
        }

        private void Advance(List<BattleUnit> side, List<BattleUnit> opponents, float deltaTime)
        {
            for (int i = 0; i < side.Count; i++)
            {
                var unit = side[i];
                if (!unit.IsAlive)
                {
                    continue;
                }

                var target = NearestAlive(opponents, unit.Position);
                if (target == null)
                {
                    continue;
                }

                // 타이머는 사거리 밖에서도 찬다 — 붙는 순간 바로 나가야 접근 시간이 공백으로
                // 느껴지지 않는다 (설계 3.2).
                if (unit.Skill != null && unit.Skill.cooldownSeconds > 0f)
                {
                    unit.SkillTimer += deltaTime;
                }

                unit.AttackTimer += deltaTime;

                if (!InRange(unit, target))
                {
                    continue;
                }

                if (unit.Skill != null && unit.Skill.cooldownSeconds > 0f)
                {
                    int used = 0;
                    while (unit.SkillTimer >= unit.Skill.cooldownSeconds
                           && used < MaxActionsPerTickPerUnit
                           && unit.IsAlive
                           && AnyAlive(opponents))
                    {
                        unit.SkillTimer -= unit.Skill.cooldownSeconds;
                        UseSkill(unit, side, opponents);
                        used++;
                    }
                }

                int swings = 0;
                while (unit.AttackTimer >= unit.AttackInterval
                       && swings < MaxActionsPerTickPerUnit
                       && unit.IsAlive
                       && AnyAlive(opponents))
                {
                    unit.AttackTimer -= unit.AttackInterval;
                    BasicAttack(unit, target, opponents);
                    swings++;
                }
            }
        }

        /// <summary>
        /// <paramref name="from"/> 에서 가장 가까운 생존자. <b>거리가 같으면 목록에서 앞선 쪽</b>이다 —
        /// 동률에 순서를 못 박아야 결과가 재현된다 (설계 3.3).
        /// </summary>
        private static BattleUnit NearestAlive(List<BattleUnit> units, Vector2 from)
        {
            BattleUnit best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < units.Count; i++)
            {
                if (!units[i].IsAlive)
                {
                    continue;
                }

                float sqr = (units[i].Position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = units[i];
                }
            }

            return best;
        }

        private static bool InRange(BattleUnit unit, BattleUnit target)
        {
            return (target.Position - unit.Position).sqrMagnitude
                   <= unit.AttackRange * unit.AttackRange;
        }

        private void BasicAttack(BattleUnit attacker, BattleUnit target, List<BattleUnit> opponents)
        {
            // 앞선 타격으로 타겟이 죽었으면 다시 고른다.
            if (target == null || !target.IsAlive)
            {
                target = NearestAlive(opponents, attacker.Position);
            }

            if (target == null)
            {
                return;
            }

            int damage = DamageAgainst(attacker, target);
            target.TakeDamage(damage);

            Append(attacker.Name + " → " + target.Name + " 평타 " + damage);
            NoteIfDown(target);
        }

        private void UseSkill(BattleUnit user, List<BattleUnit> side, List<BattleUnit> opponents)
        {
            var skill = user.Skill;

            if (skill.effect == SkillEffect.Heal)
            {
                var patient = LowestHealth(side);
                if (patient == null)
                {
                    return;
                }

                int amount = Scale(user.Attack, skill.multiplier);
                patient.Heal(amount);
                Append(user.Name + "의 " + skill.displayName + " — " + patient.Name + " 체력 " + amount + " 회복");
                return;
            }

            var target = NearestAlive(opponents, user.Position);
            if (target == null)
            {
                return;
            }

            int damage = Scale(DamageAgainst(user, target), skill.multiplier);
            target.TakeDamage(damage);

            Append(user.Name + "의 " + skill.displayName + " — " + target.Name + "에게 " + damage);
            NoteIfDown(target);
        }

        /// <summary>
        /// 피해 공식(초안): <c>공격 − 방어/2</c>, 최소 1.
        /// 방어를 절반만 반영하는 이유는 방어 특화가 완전 무적이 되지 않게 하기 위함이다.
        /// </summary>
        public static int DamageAgainst(BattleUnit attacker, BattleUnit target)
        {
            if (attacker == null || target == null)
            {
                return 0;
            }

            int raw = attacker.Attack - target.Defense / 2;
            return raw < 1 ? 1 : raw;
        }

        private static int Scale(int baseValue, float multiplier)
        {
            int scaled = (int)(baseValue * multiplier);
            return scaled < 1 ? 1 : scaled;
        }

        private void NoteIfDown(BattleUnit unit)
        {
            if (!unit.IsAlive)
            {
                Append(unit.Name + " 쓰러짐.");
            }
        }

        private BattleOutcome Resolve()
        {
            if (!AnyAlive(allies))
            {
                return BattleOutcome.Defeat;
            }

            if (!AnyAlive(enemies))
            {
                return BattleOutcome.Victory;
            }

            return BattleOutcome.InProgress;
        }

        private int TotalGold()
        {
            int total = 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                total += enemies[i].GoldReward;
            }

            return total;
        }

        private static bool AnyAlive(List<BattleUnit> units)
        {
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].IsAlive)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>비율이 가장 낮은 생존자. 회복 스킬의 대상이다.</summary>
        private static BattleUnit LowestHealth(List<BattleUnit> units)
        {
            BattleUnit best = null;
            float bestRatio = 2f;

            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (!unit.IsAlive || unit.MaxHp <= 0)
                {
                    continue;
                }

                float ratio = (float)unit.Hp / unit.MaxHp;
                if (ratio < bestRatio)
                {
                    bestRatio = ratio;
                    best = unit;
                }
            }

            return best;
        }

        private void Append(string line)
        {
            log.Add(line);

            if (log.Count > MaxLogLines)
            {
                log.RemoveAt(0);
            }
        }
    }
}
