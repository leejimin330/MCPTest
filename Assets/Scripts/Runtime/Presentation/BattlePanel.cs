using FarmCreature.Core;
using FarmCreature.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 전투터 — 맵을 고르고 그 안의 스테이지를 고른다.
    ///
    /// <b>고정 6칸을 두 모드가 번갈아 쓴다</b> (설계 5절). 행을 런타임에 만들지 않으므로
    /// 맵이 늘든 스테이지가 늘든 <c>BuildAll</c> 이 필요 없다 — 함정 3.1 을 정면으로 푸는 방법이다.
    ///
    /// 규칙은 <see cref="StageUnlockEvaluator"/> 에만 있다. 여기는 표시와 배선만 한다.
    /// </summary>
    public class BattlePanel : GamePanel
    {
        public const string BattleSceneName = "BattleScene";

        /// <summary>한 페이지에 그리는 줄 수. 씬에 만들어 둔 행 개수와 같아야 한다.</summary>
        public const int RowsPerPage = 6;

        public Text titleText;
        public Text infoText;

        [Tooltip("항상 6개다. 데이터 개수와 무관하다. GameBootstrap 이 채운다.")]
        public Text[] rowTexts;

        [Tooltip("맵 목록. GameBootstrap 이 채운다.")]
        public StageMapDatabase stageDatabase;

        [Tooltip("잠금 사유에 아이템 이름을 쓴다. GameBootstrap 이 채운다.")]
        public ItemDatabase itemDatabase;

        [Tooltip("로그를 맡길 조율자. GameBootstrap 이 채운다.")]
        public CareManager careManager;

        /// <summary>−1 이면 맵 목록을 보고 있다. 0 이상이면 그 맵의 스테이지 목록이다.</summary>
        private int openMapIndex = -1;

        private int page;

        /// <summary>uGUI 버튼 6개가 각자 줄 번호를 넘긴다 (함정 3.6).</summary>
        public void SelectRow(int rowIndex)
        {
            int index = page * RowsPerPage + rowIndex;

            if (openMapIndex < 0)
            {
                OpenMapAt(index);
                return;
            }

            StartStage(index);
        }

        public void NextPage()
        {
            // 그릴 것이 있을 때만 넘어간다. 빈 페이지로 가면 무엇을 보는지 알 수 없다.
            if ((page + 1) * RowsPerPage < ItemCount)
            {
                page++;
                Refresh();
            }
        }

        public void PreviousPage()
        {
            if (page > 0)
            {
                page--;
                Refresh();
            }
        }

        /// <summary>스테이지 목록에서 맵 목록으로 돌아간다.</summary>
        public void GoBack()
        {
            openMapIndex = -1;
            page = 0;
            Refresh();
        }

        public override void Refresh()
        {
            var map = OpenMap;

            if (titleText != null)
            {
                titleText.text = map != null
                    ? PanelLabels.StageMapTitle(map, Progress)
                    : "전투터";
            }

            if (infoText != null)
            {
                string pageLabel = PanelLabels.PageIndicator(page, PageCount);
                string ready = PanelLabels.BattleReadyLine(state);
                infoText.text = string.IsNullOrEmpty(pageLabel) ? ready : ready + "   " + pageLabel;
            }

            if (rowTexts == null)
            {
                return;
            }

            for (int i = 0; i < rowTexts.Length; i++)
            {
                if (rowTexts[i] == null)
                {
                    continue;
                }

                rowTexts[i].text = RowTextAt(map, page * RowsPerPage + i);
            }
        }

        // ---------- 안쪽 ----------

        private StageProgress Progress
        {
            get { return state != null ? state.Progress : null; }
        }

        private StageMap OpenMap
        {
            get
            {
                return openMapIndex >= 0 && stageDatabase != null
                    ? stageDatabase.Get(openMapIndex)
                    : null;
            }
        }

        /// <summary>지금 모드에서 그릴 것의 총 개수.</summary>
        private int ItemCount
        {
            get
            {
                var map = OpenMap;
                if (map != null)
                {
                    return map.Count;
                }

                return stageDatabase != null ? stageDatabase.Count : 0;
            }
        }

        private int PageCount
        {
            get
            {
                int count = ItemCount;
                if (count <= 0)
                {
                    return 1;
                }

                return (count + RowsPerPage - 1) / RowsPerPage;
            }
        }

        private string RowTextAt(StageMap map, int index)
        {
            if (map == null)
            {
                var candidate = stageDatabase != null ? stageDatabase.Get(index) : null;

                // 남는 칸은 빈 문자열이다. "(빈 칸)" 은 고를 수 있는 것처럼 보인다.
                return candidate == null ? string.Empty : PanelLabels.MapRow(candidate, Progress);
            }

            if (map.Get(index) == null)
            {
                return string.Empty;
            }

            return PanelLabels.StageRow(map, index, Progress, Inventory, itemDatabase);
        }

        private void OpenMapAt(int index)
        {
            if (stageDatabase == null || stageDatabase.Get(index) == null)
            {
                return;   // 빈 칸을 눌렀다. 아무 일도 일어나지 않는 것이 맞다.
            }

            openMapIndex = index;
            page = 0;
            Refresh();
        }

        private void StartStage(int index)
        {
            var map = OpenMap;
            if (map == null || map.Get(index) == null)
            {
                return;   // 빈 칸
            }

            string reason = StageUnlockEvaluator.LockReason(
                map, index, Progress, Inventory, itemDatabase);
            if (reason != null)
            {
                Log(reason);
                return;
            }

            if (state == null)
            {
                Log("전투터를 열 수 없다.");
                return;
            }

            // 파티가 비었으면 씬을 바꾸기 전에 막는다 — 대가가 씬 전환 왕복이다.
            if (state.Roster.Party.Count == 0)
            {
                Log("밖에 나와 있는 케어가 없다. 헛간에서 먼저 내보내자.");
                return;
            }

            BattleHandoff.SetRequest(state, map.Get(index), map.field);
            SceneManager.LoadScene(BattleSceneName);
        }

        private void Log(string message)
        {
            if (careManager != null)
            {
                careManager.SetLog(message);
            }
        }

        /// <summary>
        /// 패널을 열 때는 <b>항상 맵 목록부터</b> 시작한다. 지난번에 보던 화면이 남아 있으면
        /// 무엇을 보고 있는지 헷갈린다 (설계 5절).
        /// </summary>
        private void OnEnable()
        {
            openMapIndex = -1;
            page = 0;
        }
    }
}
