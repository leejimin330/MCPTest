namespace FarmCreature.Presentation
{
    /// <summary>
    /// 헛간 씬의 루트. 왕복(상태를 받고 돌려주기)은 전부 <see cref="BuildingSceneRoot"/> 에 있고,
    /// 여기에는 헛간만의 것 — 이름과 방 화면 참조 — 만 남는다.
    ///
    /// 나머지 건물(부화소·창고·상점·전투터)도 같은 자리에 같은 모양으로 들어온다.
    /// </summary>
    public class BarnSceneRoot : BuildingSceneRoot
    {
        public const string SceneName = "BarnScene";

        /// <summary>씬의 본체. 팝업이 아니라 <b>열린 채로</b> 저장된다.</summary>
        public BarnRoomPanel roomPanel;

        protected override string DisplayName
        {
            get { return "헛간"; }
        }

        /// <summary>
        /// <see cref="CareManager.Bind"/> 가 이미 열려 있는 패널을 다시 그리므로 보통은 할 일이 없다.
        /// 조율자 배선이 빠진 채 저장된 씬에서도 방이 비어 보이지 않도록 한 번 더 그린다 —
        /// 배선 누락은 무결성 검사가 잡지만, 잡히기 전까지 화면이 통째로 비면 원인을 짚기 어렵다.
        /// </summary>
        protected override void OnEnter()
        {
            if (roomPanel != null && careManager == null)
            {
                roomPanel.Bind(state, itemDatabase);
            }
        }
    }
}
