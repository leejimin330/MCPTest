using System.Text;
using FarmCreature.Core;
using FarmCreature.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FarmCreature.EditorTools
{
    /// <summary>
    /// 저장된 씬을 <b>새 프로세스에서 열어</b> 참조가 살아있는지 검사한다.
    /// 같은 세션에서 만든 직후에 확인하면 런타임 생성 객체가 메모리에 남아 있어
    /// 문제가 드러나지 않는다 — 실제로 그 함정에 빠져 스프라이트가 전부 유실된 적이 있다.
    ///
    /// <b>비활성 오브젝트를 반드시 포함해서 찾는다.</b> 패널은 씬에 꺼진 채로 저장되므로
    /// 기본값(FindObjectsInactive.Exclude)으로 찾으면 패널 안의 버튼이 한 개도 검사되지 않는다.
    ///
    /// 배치모드: -executeMethod FarmCreature.EditorTools.SceneIntegrityCheck.Run
    /// 실패하면 종료 코드 1 로 끝난다.
    /// </summary>
    public static class SceneIntegrityCheck
    {
        private const string ScenePath = "Assets/Scenes/FarmScene.unity";

        public static void Run()
        {
            var report = new StringBuilder();
            int failures = 0;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            report.AppendLine("씬 열기: " + scene.path + " (루트 " + scene.rootCount + "개)");

            // 1. 모든 SpriteRenderer 가 에셋 스프라이트를 참조하는가
            var renderers = Object.FindObjectsByType<SpriteRenderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int assetBacked = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                var sprite = renderers[i].sprite;

                if (sprite == null)
                {
                    report.AppendLine("  [실패] 스프라이트 비어 있음: " + renderers[i].gameObject.name);
                    failures++;
                    continue;
                }

                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sprite)))
                {
                    report.AppendLine("  [실패] 에셋이 아닌 스프라이트: " + renderers[i].gameObject.name);
                    failures++;
                    continue;
                }

                assetBacked++;
            }

            report.AppendLine("SpriteRenderer " + renderers.Length + "개 중 에셋 참조 " + assetBacked + "개");

            // 2. UI Image 도 같은 함정에 빠진다 (패널 배경 / 버튼 바탕)
            var images = Object.FindObjectsByType<UnityEngine.UI.Image>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].sprite == null
                    || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(images[i].sprite)))
                {
                    report.AppendLine("  [실패] 에셋이 아닌 Image 스프라이트: " + images[i].gameObject.name);
                    failures++;
                }
            }

            report.AppendLine("UI Image " + images.Length + "개 검사");

            // 3. 건물 4동과 콜라이더
            var buildings = Object.FindObjectsByType<FarmBuilding>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (buildings.Length != 4)
            {
                report.AppendLine("  [실패] 건물이 4동이 아님: " + buildings.Length);
                failures++;
            }

            int buildingsWithPanel = 0;
            for (int i = 0; i < buildings.Length; i++)
            {
                if (buildings[i].GetComponent<BoxCollider2D>() == null)
                {
                    report.AppendLine("  [실패] 콜라이더 없음: " + buildings[i].displayName);
                    failures++;
                }

                if (buildings[i].panel != null)
                {
                    buildingsWithPanel++;
                }
            }

            // 헛간 / 창고 / 상점 셋만 패널이 있고, 부화소는 아직 없다.
            if (buildingsWithPanel != 3)
            {
                report.AppendLine("  [실패] 패널이 연결된 건물이 3동이 아님: " + buildingsWithPanel);
                failures++;
            }

            // 4. 패널 4종 — 저장 시점에는 전부 꺼져 있어야 한다
            failures += RequirePanelClosed<BarnPanel>(report, "헛간 패널");
            failures += RequirePanelClosed<StoragePanel>(report, "창고 패널");
            failures += RequirePanelClosed<RaisingPanel>(report, "육성 패널");
            failures += RequirePanelClosed<BattlePanel>(report, "전투 패널");

            // 5. GameRoot 배선
            var root = Object.FindFirstObjectByType<GameRoot>(FindObjectsInactive.Include);
            if (root == null)
            {
                report.AppendLine("  [실패] GameRoot 없음");
                failures++;
            }
            else
            {
                failures += RequireRef(report, "speciesDatabase", root.speciesDatabase);
                failures += RequireRef(report, "itemDatabase", root.itemDatabase);
                failures += RequireRef(report, "startingSpecies", root.startingSpecies);
                failures += RequireRef(report, "careManager", root.careManager);
                failures += RequireRef(report, "hud", root.hud);
                failures += RequireRef(report, "partyField", root.partyField);

                if (root.farmBuildings == null || root.farmBuildings.Count != 4)
                {
                    report.AppendLine("  [실패] GameRoot.farmBuildings 가 4개가 아님");
                    failures++;
                }
            }

            // 6. CareManager 배선
            var care = Object.FindFirstObjectByType<CareManager>(FindObjectsInactive.Include);
            if (care == null)
            {
                report.AppendLine("  [실패] CareManager 없음");
                failures++;
            }
            else
            {
                failures += RequireRef(report, "careManager.itemDatabase", care.itemDatabase);
                failures += RequireRef(report, "careManager.raisingPanel", care.raisingPanel);
                failures += RequireRef(report, "careManager.partyField", care.partyField);

                if (care.panels == null || care.panels.Count != 4)
                {
                    report.AppendLine("  [실패] CareManager.panels 가 4개가 아님: "
                                      + (care.panels == null ? 0 : care.panels.Count));
                    failures++;
                }
                else
                {
                    for (int i = 0; i < care.panels.Count; i++)
                    {
                        failures += RequireRef(report, "careManager.panels[" + i + "]", care.panels[i]);
                    }
                }
            }

            // 7. 파티 슬롯 — Roster.MaxParty 개, 전부 클릭 가능해야 한다
            var partyField = Object.FindFirstObjectByType<PartyField>(FindObjectsInactive.Include);
            if (partyField == null)
            {
                report.AppendLine("  [실패] PartyField 없음");
                failures++;
            }
            else if (partyField.slots == null || partyField.slots.Count != Roster.MaxParty)
            {
                report.AppendLine("  [실패] 파티 슬롯이 " + Roster.MaxParty + "개가 아님: "
                                  + (partyField.slots == null ? 0 : partyField.slots.Count));
                failures++;
            }
            else
            {
                for (int i = 0; i < partyField.slots.Count; i++)
                {
                    var slot = partyField.slots[i];
                    if (slot == null)
                    {
                        report.AppendLine("  [실패] 파티 슬롯 " + i + " 참조 끊김");
                        failures++;
                        continue;
                    }

                    if (slot.GetComponent<CircleCollider2D>() == null)
                    {
                        report.AppendLine("  [실패] 파티 슬롯 " + i + " 에 콜라이더 없음 (클릭 불가)");
                        failures++;
                    }
                }
            }

            // 8. 전투 패널의 적 데이터베이스
            var battle = Object.FindFirstObjectByType<BattlePanel>(FindObjectsInactive.Include);
            if (battle != null)
            {
                failures += RequireRef(report, "battlePanel.enemyDatabase", battle.enemyDatabase);
                failures += RequireRef(report, "battlePanel.careManager", battle.careManager);
            }

            // 9. HUD 텍스트 배선
            var hud = Object.FindFirstObjectByType<GameHud>(FindObjectsInactive.Include);
            if (hud != null)
            {
                failures += RequireRef(report, "hud.statusText", hud.statusText);
                failures += RequireRef(report, "hud.logText", hud.logText);
            }

            // 10. 카메라 팬
            var cam = Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
            if (cam == null || cam.GetComponent<CameraPanController>() == null)
            {
                report.AppendLine("  [실패] 카메라에 CameraPanController 가 없음");
                failures++;
            }

            // 11. 버튼 리스너 — 비활성 패널 안의 버튼까지 전부
            var buttons = Object.FindObjectsByType<UnityEngine.UI.Button>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int wired = 0;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].onClick.GetPersistentEventCount() > 0
                    && !string.IsNullOrEmpty(buttons[i].onClick.GetPersistentMethodName(0))
                    && buttons[i].onClick.GetPersistentTarget(0) != null)
                {
                    wired++;
                }
                else
                {
                    report.AppendLine("  [실패] 리스너 없는 버튼: " + Path(buttons[i].transform));
                    failures++;
                }
            }

            report.AppendLine("버튼 " + buttons.Length + "개 중 연결됨 " + wired + "개");

            if (buttons.Length == 0)
            {
                report.AppendLine("  [실패] 버튼이 하나도 없음 — 패널이 생성되지 않았다");
                failures++;
            }

            // 12. 데이터로 개수가 정해지는 버튼들이 실제로 만들어졌는가 ★
            //
            // 11 번은 '있는 버튼'의 리스너만 본다. 그래서 버튼이 통째로 안 만들어진 경우를 놓친다.
            // 실제로 그 일이 있었다: NewScene 이 ScriptableObject 참조를 죽여
            // itemDb != null 이 false 가 되는 바람에 [장착]·상대 버튼이 0개인 씬이 저장됐고,
            // 이 검사는 "버튼 26개 중 26개 연결됨" 으로 통과시켰다.
            var storage = Object.FindFirstObjectByType<StoragePanel>(FindObjectsInactive.Include);

            if (storage != null && care != null && care.itemDatabase != null)
            {
                int expected = care.itemDatabase.items != null ? care.itemDatabase.items.Count : 0;

                if (expected == 0)
                {
                    report.AppendLine("  [실패] ItemDatabase 가 비어 있음 — 장착 버튼을 만들 수 없다");
                    failures++;
                }

                failures += RequireNamedChildren(report, storage.transform, "Equip", expected, "창고 [장착] 버튼");
                failures += RequireNamedChildren(report, storage.transform, "Row", expected, "창고 아이템 행");

                int rowTexts = storage.rowTexts == null ? 0 : storage.rowTexts.Length;
                if (rowTexts != expected)
                {
                    report.AppendLine("  [실패] storagePanel.rowTexts 가 " + expected + "개가 아님: " + rowTexts);
                    failures++;
                }
            }

            if (battle != null && battle.enemyDatabase != null)
            {
                int enemies = battle.enemyDatabase.enemies != null ? battle.enemyDatabase.enemies.Count : 0;

                if (enemies == 0)
                {
                    report.AppendLine("  [실패] EnemyDatabase 가 비어 있음 — 전투를 시작할 수단이 없다");
                    failures++;
                }

                // 자리는 3칸뿐이라 상대가 더 많아도 버튼은 3개까지만 만든다.
                int expected = enemies < 3 ? enemies : 3;
                failures += RequireNamedChildren(report, battle.transform, "Enemy", expected, "전투터 상대 버튼");
            }

            // 헛간은 데이터가 아니라 상한으로 줄 수가 고정된다.
            var barn = Object.FindFirstObjectByType<BarnPanel>(FindObjectsInactive.Include);
            if (barn != null)
            {
                failures += RequireNamedChildren(report, barn.transform, "RowButtons",
                    Roster.MaxMembers, "헛간 줄 버튼 묶음");
            }

            if (failures == 0)
            {
                report.AppendLine("결과: 통과 — 저장된 씬의 모든 참조가 유효합니다.");
                Debug.Log("[SceneIntegrityCheck]\n" + report);
                EditorApplication.Exit(0);
            }
            else
            {
                report.AppendLine("결과: 실패 " + failures + "건");
                Debug.LogError("[SceneIntegrityCheck]\n" + report);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>패널은 존재하되 <b>꺼진 채로</b> 저장돼야 한다. 켜져 있으면 시작하자마자 떠 있다.</summary>
        private static int RequirePanelClosed<T>(StringBuilder report, string label) where T : GamePanel
        {
            var panel = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

            if (panel == null)
            {
                report.AppendLine("  [실패] " + label + " 없음");
                return 1;
            }

            if (panel.gameObject.activeSelf)
            {
                report.AppendLine("  [실패] " + label + " 이(가) 켜진 채로 저장됨");
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// <paramref name="prefix"/>0, 1, 2 … 로 이름 붙은 직계 자식이 정확히
        /// <paramref name="expected"/> 개 있는지 본다. 개수가 데이터에서 나오는 UI 는
        /// 루프가 0회 돌아도 예외가 나지 않으므로, 존재 자체를 단언해야 한다.
        /// </summary>
        private static int RequireNamedChildren(
            StringBuilder report, Transform parent, string prefix, int expected, string label)
        {
            int found = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                string name = parent.GetChild(i).name;
                if (name.Length > prefix.Length && name.StartsWith(prefix))
                {
                    // "Row" 가 "RowButtons" 를 잡지 않도록 접두사 뒤는 숫자만 허용한다.
                    bool allDigits = true;
                    for (int c = prefix.Length; c < name.Length; c++)
                    {
                        if (!char.IsDigit(name[c]))
                        {
                            allDigits = false;
                            break;
                        }
                    }

                    if (allDigits)
                    {
                        found++;
                    }
                }
            }

            if (found != expected)
            {
                report.AppendLine("  [실패] " + label + " 가 " + expected + "개가 아님: " + found
                                  + " (" + prefix + "0.. 를 " + parent.name + " 아래에서 셈)");
                return 1;
            }

            report.AppendLine(label + " " + found + "개 확인");
            return 0;
        }

        private static int RequireRef(StringBuilder report, string label, Object value)
        {
            if (value == null)
            {
                report.AppendLine("  [실패] 참조 끊김: " + label);
                return 1;
            }

            return 0;
        }

        /// <summary>같은 이름의 버튼이 패널마다 있어서(닫기 등) 경로로 찍어야 어느 것인지 안다.</summary>
        private static string Path(Transform transform)
        {
            var builder = new StringBuilder(transform.name);

            var cursor = transform.parent;
            while (cursor != null)
            {
                builder.Insert(0, cursor.name + "/");
                cursor = cursor.parent;
            }

            return builder.ToString();
        }
    }
}
