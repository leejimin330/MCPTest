using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>농장에 세워지는 건물 종류.</summary>
    public enum BuildingKind
    {
        /// <summary>헛간 — 몬스터를 넣고 꺼낸다.</summary>
        Barn,

        /// <summary>부화소 — 알을 부화시켜 몬스터를 얻는다.</summary>
        Hatchery,

        /// <summary>창고 — 몬스터에게 아이템을 장착시킨다.</summary>
        Storage,

        /// <summary>상점 — 여러 가지를 산다.</summary>
        Shop
    }

    /// <summary>
    /// 배경의 농장 건물. 마우스 클릭 / 터치에 반응한다.
    /// <see cref="BoxCollider2D"/> 가 있어야 <c>OnMouseDown</c> 이 들어온다.
    /// 내부 기능(보관/장착/구매)은 아직 없고, 지금은 진입점만 만들어 둔 상태다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class FarmBuilding : MonoBehaviour
    {
        public BuildingKind kind = BuildingKind.Barn;
        public string displayName = "헛간";

        [Tooltip("이 건물이 여는 패널. 비워두면 '준비 중' 안내만 띄운다. GameBootstrap 이 채운다.")]
        public GamePanel panel;

        [Tooltip("마우스를 올렸을 때 밝아지는 정도.")]
        public float hoverBrightness = 1.18f;

        private SpriteRenderer[] renderers;
        private Color[] baseColors;
        private GameHud hud;

        private void Awake()
        {
            // 본체 + 지붕 + 문이 함께 밝아지도록 자식 렌더러까지 전부 잡아둔다.
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            baseColors = new Color[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                baseColors[i] = renderers[i].color;
            }
        }

        public void Bind(GameHud gameHud)
        {
            hud = gameHud;
        }

        private void OnMouseEnter()
        {
            SetHighlighted(true);
        }

        private void OnMouseExit()
        {
            SetHighlighted(false);
        }

        /// <summary>
        /// 누른 채 놓았을 때만 반응한다. 화면을 좌우로 슬라이드한 제스처는 클릭으로 치지 않는다.
        /// </summary>
        private void OnMouseUpAsButton()
        {
            if (CameraPanController.DraggedThisGesture)
            {
                return;
            }

            if (panel != null)
            {
                panel.Open();
                return;
            }

            if (hud != null)
            {
                hud.SetLog(displayName + " — " + PendingDescription());
            }
        }

        /// <summary>패널이 아직 없는 건물이 클릭됐을 때의 안내. 전투 명세가 오면 차례로 사라진다.</summary>
        private string PendingDescription()
        {
            switch (kind)
            {
                case BuildingKind.Barn:
                    return "몬스터를 넣고 꺼내는 곳. (기능 준비 중)";
                case BuildingKind.Hatchery:
                    return "알을 부화시키는 곳. (알 시스템 준비 중)";
                case BuildingKind.Storage:
                    return "몬스터에게 아이템을 장착시키는 곳. (기능 준비 중)";
                case BuildingKind.Shop:
                    return "여러 가지를 살 수 있는 곳. (기능 준비 중)";
                default:
                    return "(기능 준비 중)";
            }
        }

        private void SetHighlighted(bool on)
        {
            if (renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                Color c = baseColors[i];
                renderers[i].color = on
                    ? new Color(
                        Mathf.Clamp01(c.r * hoverBrightness),
                        Mathf.Clamp01(c.g * hoverBrightness),
                        Mathf.Clamp01(c.b * hoverBrightness),
                        c.a)
                    : c;
            }
        }
    }
}
