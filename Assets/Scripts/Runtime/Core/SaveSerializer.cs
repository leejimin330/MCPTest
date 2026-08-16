using System.Collections.Generic;
using FarmCreature.Data;
using UnityEngine;

namespace FarmCreature.Core
{
    /// <summary>
    /// 상태 ↔ SaveData ↔ JSON 변환. <b>파일 IO 를 하지 않는다</b> —
    /// 그래야 파일 시스템 없이 라운드트립을 테스트할 수 있다.
    ///
    /// 스탯은 <b>기본 스탯만</b> 저장한다. 장비 보너스는 저장하지 않고,
    /// 복원 시 장착 아이템을 다시 끼워 자동으로 붙는다.
    /// </summary>
    public static class SaveSerializer
    {
        public static SaveData Capture(GameState state, string savedAtUtc)
        {
            if (state == null)
            {
                return new SaveData { savedAtUtc = savedAtUtc };
            }

            var members = state.Roster.Members;
            var creatures = new CreatureSaveData[members.Count];

            for (int i = 0; i < members.Count; i++)
            {
                creatures[i] = CaptureCreature(members[i]);
            }

            var party = state.Roster.Party;
            var partyIndices = new List<int>(party.Count);

            for (int i = 0; i < party.Count; i++)
            {
                int index = IndexOf(members, party[i]);
                if (index >= 0)
                {
                    partyIndices.Add(index);
                }
            }

            var inventory = state.Inventory;
            var ids = inventory != null ? inventory.ItemIds : null;
            int itemCount = ids != null ? ids.Count : 0;

            var data = new SaveData
            {
                version = SaveData.CurrentVersion,
                creatures = creatures,
                partyIndices = partyIndices.ToArray(),
                targetIndex = Mathf.Max(0, IndexOf(members, state.Target)),
                gold = state.Gold,
                itemIds = new string[itemCount],
                itemCounts = new int[itemCount],
                clearedStageIds = ClearedIdsOf(state),
                savedAtUtc = savedAtUtc
            };

            for (int i = 0; i < itemCount; i++)
            {
                data.itemIds[i] = ids[i];
                data.itemCounts[i] = inventory.GetCount(ids[i]);
            }

            return data;
        }

        private static string[] ClearedIdsOf(GameState state)
        {
            if (state == null || state.Progress == null)
            {
                return new string[0];
            }

            var ids = state.Progress.ClearedStageIds;
            var copy = new string[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                copy[i] = ids[i];
            }

            return copy;
        }

        private static CreatureSaveData CaptureCreature(CreatureInstance creature)
        {
            if (creature == null)
            {
                return new CreatureSaveData { equippedItemId = string.Empty };
            }

            return new CreatureSaveData
            {
                speciesId = creature.CurrentSpecies != null ? creature.CurrentSpecies.speciesId : null,
                equippedItemId = creature.EquippedItem != null ? creature.EquippedItem.itemId : string.Empty,
                exp = creature.Exp,
                attack = creature.GetBaseStat(StatType.Attack),
                defense = creature.GetBaseStat(StatType.Defense),
                health = creature.GetBaseStat(StatType.Health),
                fullness = creature.Fullness,
                bond = creature.Bond
            };
        }

        public static string ToJson(SaveData data)
        {
            return JsonUtility.ToJson(data, true);
        }

        /// <summary>
        /// 어떤 실패든 false 를 반환하고 <paramref name="error"/> 에 사유를 담는다.
        /// 호출자는 실패 시 새 게임으로 폴백한다.
        /// </summary>
        public static bool TryRestore(
            string json,
            SpeciesDatabase speciesDatabase,
            ItemDatabase itemDatabase,
            out GameState state,
            out string error)
        {
            state = null;

            if (string.IsNullOrEmpty(json))
            {
                error = "세이브 내용이 비어 있습니다.";
                return false;
            }

            if (speciesDatabase == null)
            {
                error = "종 데이터베이스가 없습니다.";
                return false;
            }

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (System.Exception e)
            {
                error = "세이브를 읽을 수 없습니다: " + e.Message;
                return false;
            }

            if (data == null)
            {
                error = "세이브를 읽을 수 없습니다.";
                return false;
            }

            // v3 은 필드가 하나 느는 것뿐이라 옮길 것이 없다 — 진행도를 빈 채로 승격한다.
            // v2 이하는 보유 케어가 단일 → 배열로 바뀌어 옮길 방법이 없다 (설계 1.4).
            const int OldestPromotableVersion = 3;

            if (data.version > SaveData.CurrentVersion || data.version < OldestPromotableVersion)
            {
                error = "지원하지 않는 세이브 버전입니다: " + data.version;
                return false;
            }

            var entries = data.creatures ?? new CreatureSaveData[0];
            if (entries.Length == 0)
            {
                error = "세이브에 케어가 한 마리도 없습니다.";
                return false;
            }

            var ids = data.itemIds ?? new string[0];
            var counts = data.itemCounts ?? new int[0];
            if (ids.Length != counts.Length)
            {
                error = "인벤토리 데이터가 손상됐습니다.";
                return false;
            }

            var roster = new Roster();
            var restored = new List<CreatureInstance>(entries.Length);

            for (int i = 0; i < entries.Length; i++)
            {
                CreatureInstance creature;
                if (!TryRestoreCreature(entries[i], speciesDatabase, itemDatabase, out creature, out error))
                {
                    return false;
                }

                var added = roster.Add(creature);
                if (!added.Success)
                {
                    error = "케어를 헛간에 넣지 못했습니다: " + added.Message;
                    return false;
                }

                restored.Add(creature);
            }

            var partyIndices = data.partyIndices ?? new int[0];
            for (int i = 0; i < partyIndices.Length; i++)
            {
                int index = partyIndices[i];
                if (index < 0 || index >= restored.Count)
                {
                    error = "파티 데이터가 손상됐습니다: 인덱스 " + index;
                    return false;
                }

                var joined = roster.JoinParty(restored[index]);
                if (!joined.Success)
                {
                    error = "파티를 복원하지 못했습니다: " + joined.Message;
                    return false;
                }
            }

            var inventory = new Inventory();
            for (int i = 0; i < ids.Length; i++)
            {
                inventory.Add(ids[i], counts[i]);
            }

            state = new GameState(roster, inventory, data.gold);

            // 대상 인덱스가 망가져 있어도 전체를 버리지 않는다 — 첫 번째로 되돌리면 충분하다.
            if (data.targetIndex >= 0 && data.targetIndex < restored.Count)
            {
                state.SetTarget(restored[data.targetIndex]);
            }

            state.EnsureTarget();

            // v3 에는 이 배열이 없다 — null 이면 진행도 없이 시작한다 (승격).
            // StageProgress 가 빈 id 와 중복을 걸러내므로 손상된 세이브도 안전하다.
            var clearedIds = data.clearedStageIds ?? new string[0];
            for (int i = 0; i < clearedIds.Length; i++)
            {
                state.Progress.MarkCleared(clearedIds[i]);
            }

            error = null;
            return true;
        }

        private static bool TryRestoreCreature(
            CreatureSaveData entry,
            SpeciesDatabase speciesDatabase,
            ItemDatabase itemDatabase,
            out CreatureInstance creature,
            out string error)
        {
            creature = null;

            if (entry == null)
            {
                error = "케어 데이터가 비어 있습니다.";
                return false;
            }

            var species = speciesDatabase.Find(entry.speciesId);
            if (species == null)
            {
                error = "알 수 없는 종입니다: " + entry.speciesId;
                return false;
            }

            // 장착 아이템은 인벤토리 밖에 있으므로 별도로 되찾는다.
            ItemData equipped = null;
            if (!string.IsNullOrEmpty(entry.equippedItemId))
            {
                if (itemDatabase == null)
                {
                    error = "아이템 데이터베이스가 없습니다.";
                    return false;
                }

                equipped = itemDatabase.Find(entry.equippedItemId);
                if (equipped == null)
                {
                    error = "알 수 없는 장착 아이템입니다: " + entry.equippedItemId;
                    return false;
                }
            }

            creature = CreatureInstance.Restore(
                species, entry.exp, entry.attack, entry.defense, entry.health, entry.fullness, entry.bond);
            creature.SetEquipped(equipped);

            error = null;
            return true;
        }

        private static int IndexOf(IReadOnlyList<CreatureInstance> list, CreatureInstance value)
        {
            if (value == null)
            {
                return -1;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], value))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
