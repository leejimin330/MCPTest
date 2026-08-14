using FarmCreature.Core;
using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 전투 화면의 유닛 하나 — 몸통 · 이름표 · 체력바.
    ///
    /// 슬롯은 씬에 <see cref="Roster.MaxParty"/> + <see cref="BattleField.MaxEnemies"/> 개가
    /// 미리 만들어져 있고 인원수에 맞춰 켜고 끈다. 런타임 생성을 쓰지 않는 이유는
    /// 코드로 만든 오브젝트의 스프라이트 참조가 씬에 남지 않기 때문이다 (HANDOFF 함정 3.1).
    ///
    /// 체력바에 uGUI 를 쓰지 않고 <see cref="SpriteRenderer"/> 를 쓰는 이유:
    /// 월드 좌표에 uGUI 를 붙이려면 World Space Canvas 가 필요한데, 농장 씬은 이미 건물 이름표를
    /// <see cref="TextMesh"/> 로 처리하고 있어 같은 방식을 따른다.
    /// 덤으로 무결성 검사 1번(모든 SpriteRenderer 가 에셋을 참조하는가)이 체력바까지 함께 본다.
    /// </summary>
    public class BattleUnitView : MonoBehaviour
    {
        [Tooltip("몸통. 농장과 같은 Circle.png 에 종/적의 색을 입힌다. GameBootstrap 이 채운다.")]
        public SpriteRenderer body;

        public SpriteRenderer healthBarBack;
        public SpriteRenderer healthBarFill;
        public TextMesh nameLabel;

        [Tooltip("체력이 가득 찼을 때 체력바의 가로 길이 (월드 유닛).")]
        public float healthBarWidth = 1.3f;

        [Tooltip("쓰러진 유닛을 남겨둘 때의 투명도. 지우지 않는 이유는 무슨 일이 났는지 보이게 하려는 것.")]
        public float downedAlpha = 0.35f;

        private BattleUnit unit;
        private Color baseColor = Color.white;

        /// <summary>
        /// 이 슬롯을 켜고 한 유닛을 담는다.
        /// <paramref name="facingRight"/> 로 서로 마주 보게 만든다 — 아군은 오른쪽, 적은 왼쪽.
        /// </summary>
        public void Show(BattleUnit battleUnit, Color color, float scale, bool facingRight, Vector2 position)
        {
            unit = battleUnit;
            baseColor = color;

            gameObject.SetActive(true);
            transform.position = BattleField.ToWorld(position);

            if (body != null)
            {
                body.flipX = !facingRight;
                body.transform.localScale = new Vector3(scale, scale, 1f);
                body.sortingOrder = BattleField.SortingOrderFor(position.y);
            }

            // 체력바와 이름표는 항상 몸통보다 앞에 그린다.
            ApplySortingOffset(healthBarBack, 1);
            ApplySortingOffset(healthBarFill, 2);

            if (nameLabel != null)
            {
                nameLabel.text = battleUnit != null ? battleUnit.Name : string.Empty;

                var labelRenderer = nameLabel.GetComponent<MeshRenderer>();
                if (labelRenderer != null && body != null)
                {
                    labelRenderer.sortingOrder = body.sortingOrder + 3;
                }
            }

            Refresh();
        }

        public void Hide()
        {
            unit = null;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 체력바를 다시 그린다. 쓰러진 유닛은 <b>지우지 않고</b> 반투명으로 남긴다 —
        /// 갑자기 사라지면 무슨 일이 났는지 보이지 않는다 (설계 4.1).
        /// </summary>
        public void Refresh()
        {
            if (unit == null)
            {
                return;
            }

            float ratio = unit.MaxHp > 0 ? Mathf.Clamp01((float)unit.Hp / unit.MaxHp) : 0f;

            if (healthBarFill != null)
            {
                var scale = healthBarFill.transform.localScale;
                healthBarFill.transform.localScale = new Vector3(healthBarWidth * ratio, scale.y, 1f);

                // 가운데 피벗이라, 왼쪽 끝을 고정하려면 줄어든 만큼 오른쪽으로 밀어준다.
                var local = healthBarFill.transform.localPosition;
                healthBarFill.transform.localPosition = new Vector3(
                    -healthBarWidth * 0.5f + healthBarWidth * ratio * 0.5f, local.y, local.z);
            }

            if (body != null)
            {
                body.color = unit.IsAlive
                    ? baseColor
                    : new Color(baseColor.r, baseColor.g, baseColor.b, downedAlpha);
            }
        }

        private void ApplySortingOffset(SpriteRenderer renderer, int offset)
        {
            if (renderer != null && body != null)
            {
                renderer.sortingOrder = body.sortingOrder + offset;
            }
        }
    }
}
