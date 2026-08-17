namespace FarmCreature.Presentation
{
    /// <summary>
    /// 창고 씬의 루트. 화면은 기존 <see cref="StoragePanel"/> 이 그대로 옮겨온 것이다.
    ///
    /// 장착 대상은 <c>GameState.Target</c> 이고 그것을 고르는 곳은 헛간이다 —
    /// 창고에 들어와서 대상을 바꿀 수단은 없다. 씬이 갈리면서 더 눈에 띄게 됐다(8절 확인 목록).
    /// </summary>
    public class StorageSceneRoot : BuildingSceneRoot
    {
        public const string SceneName = "StorageScene";

        /// <summary>씬의 본체. 팝업이 아니라 열린 채로 저장된다.</summary>
        public StoragePanel roomPanel;

        protected override string DisplayName
        {
            get { return "창고"; }
        }
    }
}
