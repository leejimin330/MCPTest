using System.Collections.Generic;
using FarmCreature.Core;
using FarmCreature.Data;
using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>시작 시 세이브를 불러오거나 새 게임을 만들고, 씬의 조각들을 연결한다.</summary>
    public class GameRoot : MonoBehaviour
    {
        [Header("데이터")]
        public SpeciesDatabase speciesDatabase;
        public ItemDatabase itemDatabase;
        public CreatureSpeciesData startingSpecies;

        [Tooltip("새 게임에서 헛간에 넣어줄 케어 수. 부화소가 생기기 전까지의 임시 조치다 — "
                 + "1마리로는 파티/전투를 시험할 수 없다.")]
        public int startingCreatureCount = 3;

        [Header("새 게임 시작 소지품 (두 목록의 길이를 맞출 것)")]
        public List<ItemData> startingItems = new List<ItemData>();
        public List<int> startingItemCounts = new List<int>();

        [Header("씬 참조")]
        public CareManager careManager;
        public GameHud hud;
        public PartyField partyField;
        public List<FarmBuilding> farmBuildings = new List<FarmBuilding>();

        private void Start()
        {
            GameState state;
            string error;
            string message;

            if (SaveSystem.TryLoad(speciesDatabase, itemDatabase, out state, out error))
            {
                message = "이어서 돌보는 중.";
            }
            else
            {
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning("[FarmCreature] 세이브를 불러오지 못해 새로 시작합니다: " + error);
                }

                state = BuildNewGame();
                message = "새로운 케어들을 맞이했다.";
            }

            if (hud != null)
            {
                hud.Bind(state);
            }

            if (careManager != null)
            {
                careManager.Bind(state, hud);
                careManager.SetLog(message);
            }

            for (int i = 0; i < farmBuildings.Count; i++)
            {
                if (farmBuildings[i] != null)
                {
                    farmBuildings[i].Bind(hud);
                }
            }
        }

        private GameState BuildNewGame()
        {
            var roster = new Roster();

            int count = Mathf.Clamp(startingCreatureCount, 1, Roster.MaxMembers);
            for (int i = 0; i < count; i++)
            {
                roster.Add(CreatureInstance.CreateNew(startingSpecies));
            }

            // 처음부터 한 마리는 밖에 나와 있어야 화면이 비어 보이지 않는다.
            if (roster.Count > 0)
            {
                roster.JoinParty(roster.GetMember(0));
            }

            return new GameState(roster, BuildStartingInventory(), 0);
        }

        private Inventory BuildStartingInventory()
        {
            var inventory = new Inventory();

            for (int i = 0; i < startingItems.Count; i++)
            {
                var item = startingItems[i];
                if (item == null)
                {
                    continue;
                }

                int count = i < startingItemCounts.Count ? startingItemCounts[i] : 1;
                inventory.Add(item.itemId, count);
            }

            return inventory;
        }
    }
}
