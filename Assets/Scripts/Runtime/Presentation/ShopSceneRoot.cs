namespace FarmCreature.Presentation
{
    /// <summary>
    /// 상점 씬의 루트. 화면은 기존 <see cref="ShopPanel"/> 이 그대로 옮겨온 것이다.
    /// 구매 규칙은 여전히 <see cref="Core.ShopService"/> 한 곳에 있다.
    /// </summary>
    public class ShopSceneRoot : BuildingSceneRoot
    {
        public const string SceneName = "ShopScene";

        /// <summary>씬의 본체. 팝업이 아니라 열린 채로 저장된다.</summary>
        public ShopPanel roomPanel;

        protected override string DisplayName
        {
            get { return "상점"; }
        }
    }
}
