using FarmCreature.Core;
using FarmCreature.Data;
using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 건물/케어를 클릭하면 열리는 패널의 공통 뼈대.
    /// 씬에는 <b>비활성</b> 상태로 저장된다 — 그래서 폰트 주입과 무결성 검사가
    /// 비활성 오브젝트를 포함해야 한다 (설계 3절).
    ///
    /// <see cref="Open"/> / <see cref="Close"/> 는 무인자다. uGUI persistent listener 가
    /// 안정적으로 직렬화하는 형태가 무인자와 int 인자뿐이기 때문이다 (HANDOFF 함정 3.6).
    /// </summary>
    public abstract class GamePanel : MonoBehaviour
    {
        protected GameState state;
        protected ItemDatabase itemDatabase;

        /// <summary>육성·장착이 적용될 케어. 헛간에서 고른 대상이다.</summary>
        protected CreatureInstance Target
        {
            get { return state != null ? state.Target : null; }
        }

        protected Inventory Inventory
        {
            get { return state != null ? state.Inventory : null; }
        }

        public bool IsOpen
        {
            get { return gameObject.activeSelf; }
        }

        public void Bind(GameState gameState, ItemDatabase database)
        {
            state = gameState;
            itemDatabase = database;

            if (IsOpen)
            {
                Refresh();
            }
        }

        /// <summary>
        /// 이 패널을 연다. 형제 패널은 닫는다 — 두 패널이 겹쳐 떠서 어느 쪽 버튼을
        /// 누르는지 알 수 없게 되는 것을 막는다.
        /// </summary>
        public void Open()
        {
            if (transform.parent != null)
            {
                var siblings = transform.parent.GetComponentsInChildren<GamePanel>(true);
                for (int i = 0; i < siblings.Length; i++)
                {
                    if (siblings[i] != this)
                    {
                        siblings[i].gameObject.SetActive(false);
                    }
                }
            }

            gameObject.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        /// <summary>현재 상태를 다시 그린다. 액션이 성공하든 실패하든 호출된다.</summary>
        public abstract void Refresh();
    }
}
