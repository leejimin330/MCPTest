using UnityEngine;
using UnityEngine.UI;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 육성 — 먹이주기 / 훈련 / 저장. 헛간 목록의 [육성] 버튼으로 열리며,
    /// 그때 고른 케어가 곧 <see cref="GamePanel.Target"/> 이 된다.
    ///
    /// 훈련 가능 여부로 버튼을 막지 않는다. 포만도 하한 판정은 <see cref="Core.CareActions.Train"/>
    /// 한 곳에만 두고, 거부 사유는 실패 메시지로 로그에 띄운다 — 같은 규칙을 UI 에 복제하면
    /// 둘이 어긋났을 때 어느 쪽이 맞는지 알 수 없게 된다.
    /// </summary>
    public class RaisingPanel : GamePanel
    {
        public Text titleText;
        public Text statusText;

        public override void Refresh()
        {
            if (titleText != null)
            {
                titleText.text = PanelLabels.RaisingTitle(Target);
            }

            if (statusText != null)
            {
                statusText.text = PanelLabels.RaisingStatus(Target);
            }
        }
    }
}
