using System.Collections.Generic;
using System.Text;
using FarmCreature.Core;
using FarmCreature.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 전투 씬을 구동한다. 규칙은 전부 <see cref="BattleSimulator"/> 안에 있고 여기서는
    /// <b>시간만 흘려보낸다</b> — 그래서 배속은 <c>Time.deltaTime * speed</c> 한 줄로 끝난다 (설계 4.2).
    ///
    /// 플레이어는 <b>관전만</b> 한다. 조작은 배속과 나가기뿐이다.
    ///
    /// <b>재화를 여기서 더하지 않는다.</b> 승패와 보상액만 <see cref="BattleHandoff"/> 에 담고,
    /// 농장으로 돌아간 뒤 <see cref="GameRoot"/> 가 지급한다. 지급 지점이 하나뿐이라
    /// 씬이 중간에 바뀌어도 재화가 이중으로 들어가지 않는다 (설계 3.3).
    /// </summary>
    public class BattleSceneRoot : MonoBehaviour
    {
        public const string FarmSceneName = "FarmScene";

        [Header("맵")]
        [Tooltip("탑뷰 바닥. GameBootstrap 이 채운다.")]
        public SpriteRenderer ground;

        [Tooltip("인카운터에 맵이 지정되지 않았을 때 쓸 기본 맵.")]
        public BattleMapData defaultMap;

        [Header("슬롯 — 씬에 미리 만들어 둔다 (런타임 생성 금지)")]
        public List<BattleUnitView> allySlots = new List<BattleUnitView>();
        public List<BattleUnitView> enemySlots = new List<BattleUnitView>();

        [Header("UI — 전부 Canvas 하위여야 한글 폰트가 붙는다")]
        public Text titleText;
        public Text logText;
        public Text speedText;

        [Tooltip("전투 로그에서 화면에 남길 줄 수.")]
        public int visibleLogLines = 6;

        [Header("연습 전투 — 전투 씬을 단독으로 Play 했을 때만 쓴다")]
        public BattleEncounter practiceEncounter;
        public CreatureSpeciesData practiceSpecies;

        private BattleSimulator battle;
        private GameState state;
        private float speed = 1f;
        private bool concluded;

        private readonly List<BattleUnitView> shownAllies = new List<BattleUnitView>();
        private readonly List<BattleUnitView> shownEnemies = new List<BattleUnitView>();
        private readonly StringBuilder builder = new StringBuilder();

        private void Start()
        {
            // 배경에서도 전투가 흘러야 한다. 켜지 않으면 에디터 창이 비활성일 때
            // 플레이어 루프가 멈춰 전투가 멎은 것처럼 보인다 (HANDOFF 8절).
            Application.runInBackground = true;

            HideAllSlots();

            var request = BattleHandoff.TakeRequest();
            BattleEncounter encounter;

            if (request != null)
            {
                state = request.State;
                encounter = request.Encounter;
            }
            else
            {
                // 개발 중 전투 씬만 열어보는 일이 잦다. 오류로 죽는 대신 연습 전투로 시작한다 (설계 3.2).
                Debug.Log("[FarmCreature] 전투 요청이 없어 연습 전투로 시작합니다. (전투 씬 단독 Play)");
                state = BuildPracticeState();
                encounter = practiceEncounter;
            }

            StartBattle(encounter);
        }

        private void StartBattle(BattleEncounter encounter)
        {
            var party = LivePartyOf(state);
            var foes = FoesOf(encounter);

            battle = new BattleSimulator(party, foes);

            var map = encounter != null && encounter.map != null ? encounter.map : defaultMap;
            ApplyMap(map);
            PlaceUnits(map, party, foes);

            RefreshTexts();
        }

        // ---------- 준비 ----------

        /// <summary>
        /// <see cref="BattleSimulator"/> 는 <c>null</c> 을 건너뛴다. 그래서 넘기기 전에 걸러야
        /// <c>battle.Allies[i]</c> 와 <c>party[i]</c> 가 1:1 로 맞는다 — 어긋나면 유닛이 남의 색을 입는다.
        /// </summary>
        private static List<CreatureInstance> LivePartyOf(GameState gameState)
        {
            var party = new List<CreatureInstance>();
            if (gameState == null)
            {
                return party;
            }

            var members = gameState.Roster.Party;
            for (int i = 0; i < members.Count && party.Count < Roster.MaxParty; i++)
            {
                if (members[i] != null)
                {
                    party.Add(members[i]);
                }
            }

            return party;
        }

        /// <summary>
        /// 인카운터를 적 목록으로 펼친다. 슬롯보다 많으면 <c>LogError</c> 로 알리고 잘라낸다 —
        /// 여기까지 왔다는 것은 <c>BuildAll</c> 의 검사를 지나쳤다는 뜻이라 조용히 넘기면 안 된다.
        /// </summary>
        private static List<EnemyData> FoesOf(BattleEncounter encounter)
        {
            if (encounter == null)
            {
                Debug.LogError("[FarmCreature] 인카운터가 없어 적 없는 전투가 됐다.");
                return new List<EnemyData>();
            }

            var foes = encounter.BuildEnemyList();

            if (foes.Count > BattleField.MaxEnemies)
            {
                Debug.LogError("[FarmCreature] 인카운터 '" + encounter.displayName + "' 의 적이 "
                    + foes.Count + "마리다. 슬롯은 " + BattleField.MaxEnemies
                    + "개뿐이라 잘라낸다. BuildAll 의 검사를 확인할 것.");
                foes.RemoveRange(BattleField.MaxEnemies, foes.Count - BattleField.MaxEnemies);
            }

            return foes;
        }

        private void ApplyMap(BattleMapData map)
        {
            if (map == null || ground == null)
            {
                return;
            }

            ground.transform.localScale = new Vector3(map.groundSize.x, map.groundSize.y, 1f);
            ground.color = map.groundColor;
        }

        /// <summary>
        /// 양측을 각자의 영역 안 랜덤 좌표에 세운다. <b>좌표는 표시 전용이다</b> —
        /// 스폰이 랜덤이어도 전투 결과는 결정적이다 (설계 1.1).
        /// </summary>
        private void PlaceUnits(BattleMapData map, List<CreatureInstance> party, List<EnemyData> foes)
        {
            shownAllies.Clear();
            shownEnemies.Clear();

            if (map == null)
            {
                Debug.LogError("[FarmCreature] 맵이 없어 유닛을 세울 수 없다. defaultMap 배선을 확인할 것.");
                return;
            }

            // 게임에서는 매번 다른 배치가 나와야 한다. seed 를 인자로 받는 것은 테스트를 위한 것이다.
            int seed = Random.Range(int.MinValue, int.MaxValue);

            var allyPoints = SpawnPointPicker.PickSpawnPoints(
                map.AllySpawnArea, party.Count, map.minSpacing, seed);
            var enemyPoints = SpawnPointPicker.PickSpawnPoints(
                map.EnemySpawnArea, foes.Count, map.minSpacing, seed + 1);

            for (int i = 0; i < party.Count && i < allySlots.Count; i++)
            {
                var species = party[i].CurrentSpecies;
                var view = allySlots[i];
                if (view == null)
                {
                    continue;
                }

                view.Show(battle.Allies[i],
                    species != null ? species.placeholderColor : Color.white,
                    species != null ? species.placeholderScale : 1f,
                    facingRight: true,
                    position: allyPoints[i]);

                shownAllies.Add(view);
            }

            for (int i = 0; i < foes.Count && i < enemySlots.Count; i++)
            {
                var view = enemySlots[i];
                if (view == null)
                {
                    continue;
                }

                view.Show(battle.Enemies[i],
                    foes[i].placeholderColor,
                    scale: 1f,
                    facingRight: false,
                    position: enemyPoints[i]);

                shownEnemies.Add(view);
            }
        }

        private void HideAllSlots()
        {
            for (int i = 0; i < allySlots.Count; i++)
            {
                if (allySlots[i] != null)
                {
                    allySlots[i].Hide();
                }
            }

            for (int i = 0; i < enemySlots.Count; i++)
            {
                if (enemySlots[i] != null)
                {
                    enemySlots[i].Hide();
                }
            }
        }

        private GameState BuildPracticeState()
        {
            var roster = new Roster();

            if (practiceSpecies != null)
            {
                roster.Add(CreatureInstance.CreateNew(practiceSpecies));
                roster.JoinParty(roster.GetMember(0));
            }

            return new GameState(roster, new Inventory(), 0);
        }

        // ---------- 진행 ----------

        private void Update()
        {
            if (battle == null)
            {
                return;
            }

            if (battle.Outcome == BattleOutcome.InProgress)
            {
                battle.Tick(Time.deltaTime * speed);
                RefreshUnits();
            }

            if (battle.Outcome != BattleOutcome.InProgress && !concluded)
            {
                concluded = true;
                RefreshUnits();

                BattleHandoff.SetResult(
                    battle.Outcome == BattleOutcome.Victory
                        ? BattleConclusion.Victory
                        : BattleConclusion.Defeat,
                    battle.Outcome == BattleOutcome.Victory ? battle.GoldReward : 0,
                    state);
            }

            RefreshTexts();
        }

        private void RefreshUnits()
        {
            for (int i = 0; i < shownAllies.Count; i++)
            {
                shownAllies[i].Refresh();
            }

            for (int i = 0; i < shownEnemies.Count; i++)
            {
                shownEnemies[i].Refresh();
            }
        }

        private void RefreshTexts()
        {
            if (titleText != null)
            {
                titleText.text = concluded
                    ? PanelLabels.BattleTitle(battle) + "   [나가기]로 농장에 돌아간다"
                    : PanelLabels.BattleTitle(battle);
            }

            if (speedText != null)
            {
                speedText.text = PanelLabels.SpeedLabel(speed);
            }

            if (logText != null)
            {
                logText.text = TailOfLog();
            }
        }

        private string TailOfLog()
        {
            if (battle == null)
            {
                return string.Empty;
            }

            var lines = battle.Log;
            int start = lines.Count - visibleLogLines;
            if (start < 0)
            {
                start = 0;
            }

            builder.Length = 0;
            for (int i = start; i < lines.Count; i++)
            {
                builder.AppendLine(lines[i]);
            }

            return builder.ToString();
        }

        // ---------- 버튼 ----------

        /// <summary>
        /// uGUI 버튼이 배속 인덱스를 넘긴다 (0 = 1x, 1 = 2x).
        /// int 인자만 안정적으로 직렬화되기 때문이다 (HANDOFF 함정 3.6).
        /// </summary>
        public void SetSpeed(int speedIndex)
        {
            speed = speedIndex <= 0 ? 1f : 2f;
            RefreshTexts();
        }

        /// <summary>
        /// 농장으로 돌아간다. 전투 중에 눌러도 되고, 그때는 <b>보상 없음</b>이다.
        /// 도망 페널티는 두지 않는다 — 져도 잃는 것이 없는 게임이다 (설계 4.2).
        /// </summary>
        public void ExitBattle()
        {
            if (!concluded)
            {
                BattleHandoff.SetResult(BattleConclusion.Aborted, 0, state);
            }

            SceneManager.LoadScene(FarmSceneName);
        }
    }
}
