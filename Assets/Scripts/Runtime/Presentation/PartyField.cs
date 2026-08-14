using System.Collections.Generic;
using FarmCreature.Core;
using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 파티원을 필드에 세운다. 슬롯은 씬에 <see cref="Roster.MaxParty"/> 개가 미리 만들어져 있고,
    /// 파티 인원에 맞춰 켜고 끈다 — 런타임 생성은 스프라이트 참조가 씬에 남지 않아 쓰지 않는다.
    /// </summary>
    public class PartyField : MonoBehaviour
    {
        [Tooltip("Roster.MaxParty 개. GameBootstrap 이 채운다.")]
        public List<CreatureController> slots = new List<CreatureController>();

        private GameState state;
        private CareManager careManager;

        public void Bind(GameState gameState, CareManager manager)
        {
            state = gameState;
            careManager = manager;
            Rebuild();
        }

        /// <summary>파티 목록을 슬롯에 다시 배정한다. 합류/복귀가 일어날 때마다 부른다.</summary>
        public void Rebuild()
        {
            if (slots == null)
            {
                return;
            }

            var party = state != null ? state.Roster.Party : null;
            int count = party != null ? party.Count : 0;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                if (i >= count)
                {
                    slot.gameObject.SetActive(false);
                    continue;
                }

                slot.gameObject.SetActive(true);
                slot.Bind(party[i], careManager);
                slot.PlaceAt(SpawnX(i, count));
            }
        }

        /// <summary>진화 등으로 겉모습만 바뀌었을 때. 슬롯 배정은 건드리지 않는다.</summary>
        public void RefreshVisuals()
        {
            if (slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].gameObject.activeSelf)
                {
                    slots[i].RefreshVisual();
                }
            }
        }

        /// <summary>겹쳐 서지 않도록 필드 폭을 인원수로 나눠 배치한다.</summary>
        private static float SpawnX(int index, int count)
        {
            if (count <= 1)
            {
                return 0f;
            }

            float usable = FarmArea.HalfWidth * 1.4f;
            float step = usable * 2f / (count - 1);
            return -usable + step * index;
        }
    }
}
