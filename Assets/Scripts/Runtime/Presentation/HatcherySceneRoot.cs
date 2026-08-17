namespace FarmCreature.Presentation
{
    /// <summary>
    /// 부화소 씬의 루트. 왕복은 <see cref="BuildingSceneRoot"/> 에 있고 여기에는 이름만 남는다.
    /// 화면은 기존 <see cref="HatchPanel"/> 이 그대로 옮겨온 것이다 — 그릇만 바뀌었다.
    /// </summary>
    public class HatcherySceneRoot : BuildingSceneRoot
    {
        public const string SceneName = "HatcheryScene";

        /// <summary>씬의 본체. 팝업이 아니라 열린 채로 저장된다.</summary>
        public HatchPanel roomPanel;

        protected override string DisplayName
        {
            get { return "부화소"; }
        }
    }
}
