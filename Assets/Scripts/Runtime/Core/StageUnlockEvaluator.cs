using FarmCreature.Data;

namespace FarmCreature.Core
{
    /// <summary>
    /// 스테이지가 열렸는가를 판정한다. 조건은 <b>전부 AND</b> 이고,
    /// 목록이 비어 있으면 항상 열린다 (설계 1.2).
    ///
    /// <see cref="StageMap"/> 과 인덱스를 함께 받는 이유: <c>PreviousStageCleared</c> 가
    /// 바로 앞 스테이지를 알아야 하는데, 그것은 스테이지 자신이 아니라
    /// <b>맵의 목록 순서</b>가 정한다 (설계 3.2).
    /// </summary>
    public static class StageUnlockEvaluator
    {
        public static bool IsUnlocked(
            StageMap map, int stageIndex, StageProgress progress, Inventory inventory)
        {
            return LockReason(map, stageIndex, progress, inventory, null) == null;
        }

        /// <summary>잠긴 이유를 사람이 읽을 문장으로. 열려 있으면 <c>null</c>.</summary>
        public static string LockReason(
            StageMap map, int stageIndex, StageProgress progress, Inventory inventory,
            ItemDatabase itemDatabase)
        {
            var stage = map != null ? map.Get(stageIndex) : null;
            if (stage == null)
            {
                return "갈 수 없는 곳이다.";
            }

            if (stage.unlock == null)
            {
                return null;
            }

            for (int i = 0; i < stage.unlock.Count; i++)
            {
                string reason = ReasonFor(
                    stage.unlock[i], map, stageIndex, progress, inventory, itemDatabase);
                if (reason != null)
                {
                    return reason;
                }
            }

            return null;
        }

        private static string ReasonFor(
            StageUnlock condition, StageMap map, int stageIndex,
            StageProgress progress, Inventory inventory, ItemDatabase itemDatabase)
        {
            if (condition == null)
            {
                return null;
            }

            switch (condition.kind)
            {
                case StageUnlockKind.PreviousStageCleared:
                {
                    // 첫 스테이지에는 앞이 없다. 데이터 실수로 맵 전체가 막히는 것이 더 나쁘다.
                    if (stageIndex <= 0)
                    {
                        return null;
                    }

                    var previous = map.Get(stageIndex - 1);
                    if (previous == null || IsClear(progress, previous.stageId))
                    {
                        return null;
                    }

                    return previous.displayName + "을(를) 깨야 한다.";
                }

                case StageUnlockKind.StageCleared:
                {
                    if (IsClear(progress, condition.stageId))
                    {
                        return null;
                    }

                    return NameOfStage(map, condition.stageId) + "을(를) 깨야 한다.";
                }

                case StageUnlockKind.RequiresItem:
                {
                    int need = condition.count < 1 ? 1 : condition.count;
                    int have = inventory != null ? inventory.GetCount(condition.itemId) : 0;
                    if (have >= need)
                    {
                        return null;
                    }

                    return ItemName(itemDatabase, condition.itemId)
                           + (need > 1 ? " " + need + "개가" : "이(가)") + " 필요하다.";
                }
            }

            return null;
        }

        private static bool IsClear(StageProgress progress, string stageId)
        {
            return progress != null && progress.IsCleared(stageId);
        }

        /// <summary>같은 맵 안에 있으면 표시 이름을, 없으면 id 를 쓴다.</summary>
        private static string NameOfStage(StageMap map, string stageId)
        {
            for (int i = 0; i < map.Count; i++)
            {
                var stage = map.Get(i);
                if (stage != null && stage.stageId == stageId)
                {
                    return stage.displayName;
                }
            }

            return stageId;
        }

        private static string ItemName(ItemDatabase itemDatabase, string itemId)
        {
            if (itemDatabase != null)
            {
                var item = itemDatabase.Find(itemId);
                if (item != null && !string.IsNullOrEmpty(item.displayName))
                {
                    return item.displayName;
                }
            }

            return itemId;
        }
    }
}
