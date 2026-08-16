using FarmCreature.Core;
using FarmCreature.Data;
using UnityEngine;
using UnityEngine.UI;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 부화소 — 알을 깨서 새 케어를 얻는다. 행은 <see cref="EggDatabase"/> 인덱스와 1:1 이고
    /// 개수가 씬 생성 시점에 고정된다 (함정 3.1). [부화] 버튼은 <b>int 인자 persistent listener</b>
    /// 로 그 인덱스를 넘긴다 (함정 3.6).
    ///
    /// 규칙은 <see cref="HatchService"/> 한 곳에만 있다 — 이 패널은 표시와 배선만 한다.
    /// </summary>
    public class HatchPanel : GamePanel
    {
        public Text titleText;

        [Tooltip("EggDatabase 인덱스 순서와 같아야 한다. GameBootstrap 이 채운다.")]
        public Text[] rowTexts;

        [Tooltip("알 목록. 행 개수가 여기서 나온다. GameBootstrap 이 채운다.")]
        public EggDatabase eggDatabase;

        [Tooltip("로그와 전체 갱신을 맡길 조율자. GameBootstrap 이 채운다.")]
        public CareManager careManager;

        /// <summary>uGUI 버튼이 알 인덱스를 넘긴다.</summary>
        public void Hatch(int eggIndex)
        {
            var egg = EggAt(eggIndex);
            if (egg == null)
            {
                Log("그런 알은 없다.");
                return;
            }

            var result = HatchService.TryHatch(state, egg);

            // 성공이든 실패든 다시 그린다 — 헛간 보유 줄과 제목의 n/8 이 함께 바뀐다.
            if (careManager != null)
            {
                careManager.RefreshAll();
            }
            else
            {
                Refresh();
            }

            Log(result.Message);
        }

        public override void Refresh()
        {
            if (titleText != null)
            {
                titleText.text = PanelLabels.HatchTitle(state);
            }

            if (rowTexts == null)
            {
                return;
            }

            var inventory = Inventory;

            for (int i = 0; i < rowTexts.Length; i++)
            {
                if (rowTexts[i] == null)
                {
                    continue;
                }

                var egg = EggAt(i);
                if (egg == null)
                {
                    rowTexts[i].text = string.Empty;
                    continue;
                }

                int owned = inventory != null ? inventory.GetCount(egg.eggId) : 0;
                rowTexts[i].text = PanelLabels.HatchRow(egg, owned);
            }
        }

        private EggData EggAt(int index)
        {
            if (eggDatabase == null || eggDatabase.eggs == null
                || index < 0 || index >= eggDatabase.eggs.Count)
            {
                return null;
            }

            return eggDatabase.eggs[index];
        }

        private void Log(string message)
        {
            if (careManager != null)
            {
                careManager.SetLog(message);
            }
        }
    }
}
