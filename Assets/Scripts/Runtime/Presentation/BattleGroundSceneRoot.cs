using FarmCreature.Core;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 전투터 씬의 루트. 다른 건물과 달리 <b>여기서 또 다른 씬(전투)으로 나갔다가 돌아온다.</b>
    ///
    /// <b>전투에서 돌아오는 곳이 농장이 아니라 여기다.</b> 매 전투마다 농장을 거쳐
    /// 다시 들어오는 것은 번거롭고, 스테이지를 이어서 고르는 것이 자연스럽다.
    /// 그래서 <see cref="BattlePanel"/> 이 요청에 <see cref="BattleRequest.ReturnScene"/> 을 싣는다.
    ///
    /// <b>그 대가로 보상을 새기는 곳이 둘이 됐다</b> — 농장(<see cref="GameRoot"/>)과 여기.
    /// 규칙 자체는 <see cref="BattleReward"/> 한 곳에 있어 갈라지지 않는다.
    /// </summary>
    public class BattleGroundSceneRoot : BuildingSceneRoot
    {
        public const string SceneName = "BattleGroundScene";

        /// <summary>씬의 본체. 팝업이 아니라 열린 채로 저장된다.</summary>
        public BattlePanel roomPanel;

        protected override string DisplayName
        {
            get { return "전투터"; }
        }

        /// <summary>
        /// 전투에서 돌아왔다면 보상과 진행도를 새긴다.
        /// 처음 들어온 것이라면 결과가 없어 아무 일도 일어나지 않는다.
        /// </summary>
        protected override void OnEnter()
        {
            var result = BattleHandoff.TakeResult();
            if (result == null)
            {
                return;
            }

            string message = BattleReward.Apply(state, result);

            if (careManager != null)
            {
                // 진행도가 늘었으면 잠겨 있던 줄이 열려야 한다. 로그보다 이쪽이 중요하다.
                careManager.RefreshAll();
                careManager.SetLog(message);
            }
        }
    }
}
