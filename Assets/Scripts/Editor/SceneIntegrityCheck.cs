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
        private const string FarmScenePath = "Assets/Scenes/FarmScene.unity";
        private const string BattleScenePath = "Assets/Scenes/BattleScene.unity";
        private const string BarnScenePath = "Assets/Scenes/BarnScene.unity";

        public static void Run()
        {
            var report = new StringBuilder();
            int failures = 0;

            failures += CheckBuildSettings(report);
            failures += CheckFarmScene(report);
            failures += CheckBattleScene(report);
            failures += CheckBarnScene(report);

            if (failures == 0)
            {
                report.AppendLine("결과: 통과 — 세 씬의 모든 참조가 유효합니다.");
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

        /// <summary>
        /// 두 씬이 빌드 설정에 등록됐는가. 빠지면 <c>SceneManager.LoadScene</c> 이
        /// <b>런타임에</b> 실패한다 — 에디터에서는 멀쩡해 보인다 (설계 5.1).
        /// </summary>
        private static int CheckBuildSettings(StringBuilder report)
        {
            int failures = 0;
            var scenes = EditorBuildSettings.scenes;

            failures += RequireSceneRegistered(report, scenes, FarmScenePath);
            failures += RequireSceneRegistered(report, scenes, BattleScenePath);
            failures += RequireSceneRegistered(report, scenes, BarnScenePath);

            report.AppendLine("빌드 설정 씬 " + scenes.Length + "개 검사");
            return failures;
        }

        private static int RequireSceneRegistered(
            StringBuilder report, EditorBuildSettingsScene[] scenes, string path)
        {
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == path && scenes[i].enabled)
                {
                    return 0;
                }
            }

            report.AppendLine("  [실패] 빌드 설정에 없거나 꺼져 있음: " + path
                              + " — LoadScene 이 런타임에 실패한다");
            return 1;
        }

        private static int CheckFarmScene(StringBuilder report)
        {
            int failures = 0;

            var scene = EditorSceneManager.OpenScene(FarmScenePath, OpenSceneMode.Single);
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

            // 3. 건물 5동과 콜라이더
            var buildings = Object.FindObjectsByType<FarmBuilding>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (buildings.Length != 5)
            {
                report.AppendLine("  [실패] 건물이 5동이 아님: " + buildings.Length);
                failures++;
            }

            int buildingsWithEntry = 0;
            for (int i = 0; i < buildings.Length; i++)
            {
                if (buildings[i].GetComponent<BoxCollider2D>() == null)
                {
                    report.AppendLine("  [실패] 콜라이더 없음: " + buildings[i].displayName);
                    failures++;
                }

                // 진입점은 패널이거나 씬이다. 둘 다 없으면 눌러도 "준비 중" 만 뜬다.
                bool entersScene = !string.IsNullOrEmpty(buildings[i].sceneName);

                if (entersScene || buildings[i].panel != null)
                {
                    buildingsWithEntry++;
                }

                // 씬으로 들어가는 건물은 조율자가 있어야 상태를 들려 보낼 수 있다.
                // 빠지면 클릭해도 아무 일이 없고, 그것이 유일한 증상이다.
                if (entersScene && buildings[i].careManager == null)
                {
                    report.AppendLine("  [실패] 씬으로 들어가는 건물에 careManager 가 없음: "
                                      + buildings[i].displayName);
                    failures++;
                }

                // 건물 x 좌표가 맵 밖으로 나갔는지. FarmLayoutTests 는 GameBootstrap 이
                // Editor 어셈블리라 좌표를 테스트 안에 복제해서 검사한다 — 좌표 자체가
                // 바뀌어도 복제본은 못 잡는다. 실제 배치는 여기서만 볼 수 있다.
                float edge = Mathf.Abs(buildings[i].transform.position.x) + 1.7f;
                if (edge > FarmArea.HalfWidth)
                {
                    report.AppendLine("  [실패] 건물이 맵 밖으로 나감: " + buildings[i].displayName
                                      + " x=" + buildings[i].transform.position.x);
                    failures++;
                }
            }

            // 다섯 동이 전부 진입점을 갖는다 — 넷은 패널, 헛간은 씬이다.
            if (buildingsWithEntry != 5)
            {
                report.AppendLine("  [실패] 진입점이 연결된 건물이 5동이 아님: " + buildingsWithEntry);
                failures++;
            }

            // 4. 패널 4종 — 저장 시점에는 전부 꺼져 있어야 한다.
            // 헛간·육성은 여기 없다. 헛간 씬으로 옮겨갔다.
            failures += RequirePanelClosed<StoragePanel>(report, "창고 패널");
            failures += RequirePanelClosed<BattlePanel>(report, "전투 패널");
            failures += RequirePanelClosed<ShopPanel>(report, "상점 패널");
            failures += RequirePanelClosed<HatchPanel>(report, "부화소 패널");

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

                if (root.farmBuildings == null || root.farmBuildings.Count != 5)
                {
                    report.AppendLine("  [실패] GameRoot.farmBuildings 가 5개가 아님");
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
                failures += RequireRef(report, "careManager.partyField", care.partyField);

                // raisingPanel 은 농장에 없다 — 헛간 씬 안에 있다.
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

            // 8. 전투터 패널 배선
            var battle = Object.FindFirstObjectByType<BattlePanel>(FindObjectsInactive.Include);
            if (battle != null)
            {
                failures += RequireRef(report, "battlePanel.stageDatabase", battle.stageDatabase);
                failures += RequireRef(report, "battlePanel.itemDatabase", battle.itemDatabase);
                failures += RequireRef(report, "battlePanel.careManager", battle.careManager);
                failures += RequireRef(report, "battlePanel.titleText", battle.titleText);
                failures += RequireRef(report, "battlePanel.infoText", battle.infoText);
            }

            // 8-1. 상점 패널 배선 ★
            var shop = Object.FindFirstObjectByType<ShopPanel>(FindObjectsInactive.Include);
            if (shop == null)
            {
                report.AppendLine("  [실패] ShopPanel 없음");
                failures++;
            }
            else
            {
                failures += RequireRef(report, "shopPanel.stockDatabase", shop.stockDatabase);
                failures += RequireRef(report, "shopPanel.eggDatabase", shop.eggDatabase);
                failures += RequireRef(report, "shopPanel.careManager", shop.careManager);
                failures += RequireRef(report, "shopPanel.titleText", shop.titleText);
            }

            var hatch = Object.FindFirstObjectByType<HatchPanel>(FindObjectsInactive.Include);
            if (hatch == null)
            {
                report.AppendLine("  [실패] HatchPanel 없음");
                failures++;
            }
            else
            {
                failures += RequireRef(report, "hatchPanel.eggDatabase", hatch.eggDatabase);
                failures += RequireRef(report, "hatchPanel.careManager", hatch.careManager);
                failures += RequireRef(report, "hatchPanel.titleText", hatch.titleText);
            }

            // 8-2. 건물 종류가 다섯 가지 전부 한 번씩 나오는가.
            // 개수만 세면 같은 종류가 두 번 서 있어도 통과한다.
            var kinds = new System.Collections.Generic.HashSet<BuildingKind>();
            for (int i = 0; i < buildings.Length; i++)
            {
                kinds.Add(buildings[i].kind);
            }

            var requiredKinds = new[]
            {
                BuildingKind.Barn, BuildingKind.Hatchery, BuildingKind.Storage,
                BuildingKind.Shop, BuildingKind.BattleGround
            };

            for (int i = 0; i < requiredKinds.Length; i++)
            {
                if (!kinds.Contains(requiredKinds[i]))
                {
                    report.AppendLine("  [실패] 건물 종류가 빠졌음: " + requiredKinds[i]);
                    failures++;
                }
            }

            // 8-3. 전투 진입점이 전투터로 옮겨졌는가 (설계 4.1 — 상점 건물이 열던 것을 뗐다)
            for (int i = 0; i < buildings.Length; i++)
            {
                if (buildings[i].kind == BuildingKind.Shop
                    && buildings[i].panel != null && !(buildings[i].panel is ShopPanel))
                {
                    report.AppendLine("  [실패] 상점 건물이 상점 패널을 열지 않는다: "
                                      + buildings[i].panel.GetType().Name);
                    failures++;
                }

                if (buildings[i].kind == BuildingKind.BattleGround
                    && buildings[i].panel != null && !(buildings[i].panel is BattlePanel))
                {
                    report.AppendLine("  [실패] 전투터 건물이 전투 패널을 열지 않는다: "
                                      + buildings[i].panel.GetType().Name);
                    failures++;
                }

                if (buildings[i].kind == BuildingKind.Hatchery
                    && buildings[i].panel != null && !(buildings[i].panel is HatchPanel))
                {
                    report.AppendLine("  [실패] 부화소 건물이 부화 패널을 열지 않는다: "
                                      + buildings[i].panel.GetType().Name);
                    failures++;
                }

                // 헛간은 패널이 아니라 씬으로 들어간다. 이름을 오타로 적어도 클릭이 조용히
                // 아무것도 안 하므로, 문자열을 코드의 상수와 직접 대조한다.
                if (buildings[i].kind == BuildingKind.Barn
                    && buildings[i].sceneName != BarnSceneRoot.SceneName)
                {
                    report.AppendLine("  [실패] 헛간 건물의 sceneName 이 "
                                      + BarnSceneRoot.SceneName + " 이 아님: '"
                                      + buildings[i].sceneName + "'");
                    failures++;
                }
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

            // 전투터는 데이터 개수가 아니라 **고정 6칸**이다 — 맵·스테이지가 늘어도 씬은 그대로다.
            // 여기서 데이터 개수를 세면 스테이지를 늘릴 때마다 BuildAll 이 필요해진다 (설계 5절).
            if (battle != null)
            {
                failures += RequireNamedChildren(report, battle.transform, "Row",
                    BattlePanel.RowsPerPage, "전투터 줄");
                failures += RequireNamedChildren(report, battle.transform, "Select",
                    BattlePanel.RowsPerPage, "전투터 [선택] 버튼");

                int battleRowTexts = battle.rowTexts == null ? 0 : battle.rowTexts.Length;
                if (battleRowTexts != BattlePanel.RowsPerPage)
                {
                    report.AppendLine("  [실패] battlePanel.rowTexts 가 "
                                      + BattlePanel.RowsPerPage + "개가 아님: " + battleRowTexts);
                    failures++;
                }

                if (battle.stageDatabase != null && battle.stageDatabase.Count == 0)
                {
                    report.AppendLine("  [실패] StageMapDatabase 가 비어 있음 — 갈 곳이 없다");
                    failures++;
                }

                string[] navButtons = { "PrevPage", "NextPage", "Back" };
                for (int i = 0; i < navButtons.Length; i++)
                {
                    if (battle.transform.Find(navButtons[i]) == null)
                    {
                        report.AppendLine("  [실패] 전투터에 " + navButtons[i] + " 버튼이 없음");
                        failures++;
                    }
                }
            }

            // 상점 — 행 개수가 재고 목록에서 나온다. 0 이면 참조가 죽은 것이다 (함정 3.9/3.10).
            if (shop != null && shop.stockDatabase != null)
            {
                int expected = shop.stockDatabase.Count;

                if (expected == 0)
                {
                    report.AppendLine("  [실패] ShopStockDatabase 가 비어 있음 — 살 수 있는 것이 없다");
                    failures++;
                }

                failures += RequireNamedChildren(report, shop.transform, "Buy", expected, "상점 [구매] 버튼");
                failures += RequireNamedChildren(report, shop.transform, "Row", expected, "상점 판매 행");

                int rowTexts = shop.rowTexts == null ? 0 : shop.rowTexts.Length;
                if (rowTexts != expected)
                {
                    report.AppendLine("  [실패] shopPanel.rowTexts 가 " + expected + "개가 아님: " + rowTexts);
                    failures++;
                }
            }

            // 부화 행·버튼 개수 = 알 종류 수. 0 이면 참조가 죽었다는 뜻이다 (함정 3.9/3.10).
            if (hatch != null && hatch.eggDatabase != null && hatch.eggDatabase.eggs != null)
            {
                int expectedEggs = hatch.eggDatabase.eggs.Count;

                if (expectedEggs == 0)
                {
                    report.AppendLine("  [실패] EggDatabase 가 비어 있음 — 깰 알이 없다");
                    failures++;
                }

                failures += RequireNamedChildren(report, hatch.transform, "Hatch", expectedEggs, "부화소 [부화] 버튼");
                failures += RequireNamedChildren(report, hatch.transform, "Row", expectedEggs, "부화소 알 행");

                int hatchRowTexts = hatch.rowTexts == null ? 0 : hatch.rowTexts.Length;
                if (hatchRowTexts != expectedEggs)
                {
                    report.AppendLine("  [실패] hatchPanel.rowTexts 가 " + expectedEggs + "개가 아님: " + hatchRowTexts);
                    failures++;
                }
            }

            return failures;
        }

        /// <summary>
        /// 헛간 씬. 농장·전투와 다른 것을 단언한다 — <b>방 화면은 켜진 채로, 육성 팝업은 꺼진 채로</b>
        /// 저장돼야 하고(다른 씬은 전부 꺼진 채가 정답이다), 칸은 데이터가 아니라
        /// <see cref="Roster.MaxMembers"/> 로 고정된다.
        /// </summary>
        private static int CheckBarnScene(StringBuilder report)
        {
            int failures = 0;

            var scene = EditorSceneManager.OpenScene(BarnScenePath, OpenSceneMode.Single);
            report.AppendLine("씬 열기: " + scene.path + " (루트 " + scene.rootCount + "개)");

            // 1. Image 스프라이트가 전부 에셋인가 (함정 3.1)
            var images = Object.FindObjectsByType<UnityEngine.UI.Image>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].sprite == null
                    || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(images[i].sprite)))
                {
                    report.AppendLine("  [실패] 헛간 씬: 에셋이 아닌 Image 스프라이트: "
                                      + Path(images[i].transform));
                    failures++;
                }
            }

            report.AppendLine("헛간 씬 UI Image " + images.Length + "개 검사");

            // 2. BarnSceneRoot 배선. 단독 Play 폴백이 세이브를 읽으려면 DB 두 개가 필요하다.
            var root = Object.FindFirstObjectByType<BarnSceneRoot>(FindObjectsInactive.Include);
            if (root == null)
            {
                report.AppendLine("  [실패] BarnSceneRoot 없음");
                return failures + 1;
            }

            failures += RequireRef(report, "barnRoot.speciesDatabase", root.speciesDatabase);
            failures += RequireRef(report, "barnRoot.itemDatabase", root.itemDatabase);
            failures += RequireRef(report, "barnRoot.careManager", root.careManager);
            failures += RequireRef(report, "barnRoot.hud", root.hud);
            failures += RequireRef(report, "barnRoot.roomPanel", root.roomPanel);

            // 3. 방 화면 배선과 칸 개수
            var room = Object.FindFirstObjectByType<BarnRoomPanel>(FindObjectsInactive.Include);
            if (room == null)
            {
                report.AppendLine("  [실패] BarnRoomPanel 없음");
                return failures + 1;
            }

            failures += RequireRef(report, "roomPanel.careManager", room.careManager);
            failures += RequireRef(report, "roomPanel.boxTitleText", room.boxTitleText);
            failures += RequireRef(report, "roomPanel.keeperText", room.keeperText);
            failures += RequireRef(report, "roomPanel.keeperPortrait", room.keeperPortrait);
            failures += RequireRef(report, "roomPanel.detailText", room.detailText);
            failures += RequireRef(report, "roomPanel.partyButtonLabel", room.partyButtonLabel);

            int cellTexts = room.cellTexts == null ? 0 : room.cellTexts.Length;
            if (cellTexts != Roster.MaxMembers)
            {
                report.AppendLine("  [실패] roomPanel.cellTexts 가 " + Roster.MaxMembers
                                  + "개가 아님: " + cellTexts);
                failures++;
            }

            int cellBackgrounds = room.cellBackgrounds == null ? 0 : room.cellBackgrounds.Length;
            if (cellBackgrounds != Roster.MaxMembers)
            {
                report.AppendLine("  [실패] roomPanel.cellBackgrounds 가 " + Roster.MaxMembers
                                  + "개가 아님: " + cellBackgrounds);
                failures++;
            }

            int partySlots = room.partySlotTexts == null ? 0 : room.partySlotTexts.Length;
            if (partySlots != Roster.MaxParty)
            {
                report.AppendLine("  [실패] roomPanel.partySlotTexts 가 " + Roster.MaxParty
                                  + "개가 아님: " + partySlots);
                failures++;
            }

            // 칸 버튼이 실제로 만들어졌는가. 배열만 채우고 버튼을 안 만들면 눌 곳이 없다.
            failures += RequireNamedChildren(report, room.transform, "Cell",
                Roster.MaxMembers, "보관함 칸 버튼");
            failures += RequireNamedChildren(report, room.transform, "PartySlot",
                Roster.MaxParty, "파티 줄");

            // 4. ★ 방은 켜진 채로, 육성 팝업은 꺼진 채로 저장돼야 한다.
            // 방을 끄면 빈 방이 뜨고, 팝업을 켜면 들어가자마자 육성창이 덮는다.
            if (!room.gameObject.activeSelf)
            {
                report.AppendLine("  [실패] 방 화면이 꺼진 채로 저장됨 — 들어가면 빈 방이 뜬다");
                failures++;
            }

            failures += RequirePanelClosed<RaisingPanel>(report, "육성 패널(헛간 씬)");

            // 5. ★ 방과 팝업의 부모가 달라야 한다.
            // GamePanel.Open 은 형제 패널을 닫는다 — 한 부모에 있으면 [육성]을 누르는 순간
            // 방 화면이 통째로 꺼진다. 화면이 검게 변하는 것이 유일한 증상이다.
            var raising = Object.FindFirstObjectByType<RaisingPanel>(FindObjectsInactive.Include);
            if (raising != null && raising.transform.parent == room.transform.parent)
            {
                report.AppendLine("  [실패] 방과 육성 팝업의 부모가 같다 — "
                                  + "[육성]을 누르면 방 화면이 함께 꺼진다");
                failures++;
            }

            // 6. CareManager 배선 — 방과 팝업 둘 다 들고 있어야 액션 뒤에 다시 그려진다
            var care = Object.FindFirstObjectByType<CareManager>(FindObjectsInactive.Include);
            if (care == null)
            {
                report.AppendLine("  [실패] 헛간 씬에 CareManager 없음");
                failures++;
            }
            else
            {
                failures += RequireRef(report, "헛간 careManager.itemDatabase", care.itemDatabase);
                failures += RequireRef(report, "헛간 careManager.raisingPanel", care.raisingPanel);

                if (care.panels == null || care.panels.Count != 2)
                {
                    report.AppendLine("  [실패] 헛간 CareManager.panels 가 2개가 아님: "
                                      + (care.panels == null ? 0 : care.panels.Count));
                    failures++;
                }
            }

            // 7. 나가는 길이 있는가. 없으면 헛간에 갇힌다.
            var exit = Object.FindObjectsByType<UnityEngine.UI.Button>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool hasExit = false;
            int wired = 0;

            for (int i = 0; i < exit.Length; i++)
            {
                if (exit[i].onClick.GetPersistentEventCount() > 0
                    && !string.IsNullOrEmpty(exit[i].onClick.GetPersistentMethodName(0))
                    && exit[i].onClick.GetPersistentTarget(0) != null)
                {
                    wired++;

                    if (exit[i].onClick.GetPersistentMethodName(0) == "ExitToFarm")
                    {
                        hasExit = true;
                    }
                }
                else
                {
                    report.AppendLine("  [실패] 헛간 씬: 리스너 없는 버튼: " + Path(exit[i].transform));
                    failures++;
                }
            }

            if (!hasExit)
            {
                report.AppendLine("  [실패] 농장으로 나가는 버튼이 없음 — 헛간에 갇힌다");
                failures++;
            }

            report.AppendLine("헛간 씬 버튼 " + exit.Length + "개 중 연결됨 " + wired + "개");

            // 8. HUD·카메라
            var hud = Object.FindFirstObjectByType<GameHud>(FindObjectsInactive.Include);
            if (hud == null)
            {
                report.AppendLine("  [실패] 헛간 씬에 GameHud 없음");
                failures++;
            }
            else
            {
                failures += RequireRef(report, "헛간 hud.logText", hud.logText);
            }

            if (Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include) == null)
            {
                report.AppendLine("  [실패] 헛간 씬에 카메라가 없음");
                failures++;
            }

            return failures;
        }

        /// <summary>
        /// 전투 씬은 농장과 다른 것을 단언한다 — 슬롯이 <b>존재하고 꺼져 있는가</b>,
        /// 체력바 채움이 슬롯마다 있는가, 배속·나가기 버튼이 배선됐는가 (설계 5.2).
        ///
        /// 슬롯은 씬에 꺼진 채로 저장되므로 <b>FindObjectsInactive.Include 가 필수</b>다 —
        /// 빼면 슬롯이 한 개도 검사되지 않는다.
        /// </summary>
        private static int CheckBattleScene(StringBuilder report)
        {
            int failures = 0;

            var scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
            report.AppendLine("씬 열기: " + scene.path + " (루트 " + scene.rootCount + "개)");

            // 1. 스프라이트 — 몸통·체력바·바닥 전부 에셋이어야 한다
            var renderers = Object.FindObjectsByType<SpriteRenderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < renderers.Length; i++)
            {
                var sprite = renderers[i].sprite;

                if (sprite == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sprite)))
                {
                    report.AppendLine("  [실패] 전투 씬: 에셋이 아닌 스프라이트: " + Path(renderers[i].transform));
                    failures++;
                }
            }

            report.AppendLine("전투 씬 SpriteRenderer " + renderers.Length + "개 검사");

            // 2. BattleSceneRoot 배선
            var root = Object.FindFirstObjectByType<BattleSceneRoot>(FindObjectsInactive.Include);
            if (root == null)
            {
                report.AppendLine("  [실패] BattleSceneRoot 없음");
                return failures + 1;
            }

            failures += RequireRef(report, "battleRoot.ground", root.ground);
            failures += RequireRef(report, "battleRoot.defaultMap", root.defaultMap);
            failures += RequireRef(report, "battleRoot.titleText", root.titleText);
            failures += RequireRef(report, "battleRoot.logText", root.logText);
            failures += RequireRef(report, "battleRoot.speedText", root.speedText);
            failures += RequireRef(report, "battleRoot.practiceEncounter", root.practiceEncounter);
            failures += RequireRef(report, "battleRoot.practiceSpecies", root.practiceSpecies);

            // 3. 슬롯 개수 — 데이터가 아니라 상한으로 고정된다
            failures += RequireSlots(report, root.allySlots, Roster.MaxParty, "아군 슬롯");
            failures += RequireSlots(report, root.enemySlots, BattleField.MaxEnemies, "적 슬롯");

            // 4. 버튼 3개(1x · 2x · 나가기)가 리스너를 갖는가
            var buttons = Object.FindObjectsByType<UnityEngine.UI.Button>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (buttons.Length != 3)
            {
                report.AppendLine("  [실패] 전투 씬 버튼이 3개가 아님: " + buttons.Length
                                  + " (1x · 2x · 나가기)");
                failures++;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].onClick.GetPersistentEventCount() == 0
                    || string.IsNullOrEmpty(buttons[i].onClick.GetPersistentMethodName(0))
                    || buttons[i].onClick.GetPersistentTarget(0) == null)
                {
                    report.AppendLine("  [실패] 전투 씬: 리스너 없는 버튼: " + Path(buttons[i].transform));
                    failures++;
                }
            }

            report.AppendLine("전투 씬 버튼 " + buttons.Length + "개 검사");

            // 5. 카메라
            var camera = Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
            if (camera == null)
            {
                report.AppendLine("  [실패] 전투 씬에 카메라가 없음");
                failures++;
            }

            return failures;
        }

        /// <summary>
        /// 슬롯이 <paramref name="expected"/> 개 있고, 참조가 살아 있고, 조각이 다 붙어 있고,
        /// <b>저장 시점에 꺼져 있는지</b> 본다. 켜진 채로 저장되면 전투 시작 전부터 유령이 서 있다.
        /// </summary>
        private static int RequireSlots(
            StringBuilder report, System.Collections.Generic.List<BattleUnitView> slots,
            int expected, string label)
        {
            if (slots == null || slots.Count != expected)
            {
                report.AppendLine("  [실패] " + label + " 이 " + expected + "개가 아님: "
                                  + (slots == null ? 0 : slots.Count));
                return 1;
            }

            int failures = 0;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];

                if (slot == null)
                {
                    report.AppendLine("  [실패] " + label + " " + i + " 참조 끊김");
                    failures++;
                    continue;
                }

                if (slot.body == null || slot.healthBarFill == null
                    || slot.healthBarBack == null || slot.nameLabel == null)
                {
                    report.AppendLine("  [실패] " + label + " " + i + " 의 조각이 빠졌다 "
                                      + "(body/healthBarBack/healthBarFill/nameLabel)");
                    failures++;
                }

                if (slot.gameObject.activeSelf)
                {
                    report.AppendLine("  [실패] " + label + " " + i + " 이(가) 켜진 채로 저장됨");
                    failures++;
                }
            }

            report.AppendLine(label + " " + slots.Count + "개 확인");
            return failures;
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
