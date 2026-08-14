using FarmCreature.Core;
using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 필드에 나와 있는 파티원 하나. 외형 갱신과 배회를 맡고, 땅 윗면 위를 <b>좌우로만</b> 오간다.
    /// 게임 규칙은 전혀 모른다 — 클릭을 받으면 <see cref="CareManager"/> 에 넘길 뿐이다.
    ///
    /// 슬롯은 씬에 <see cref="Roster.MaxParty"/> 개가 미리 만들어져 있고,
    /// <see cref="PartyField"/> 가 파티 인원에 맞춰 켜고 끈다. 런타임에 생성하지 않는 이유는
    /// 코드로 만든 오브젝트의 스프라이트가 씬에 저장되지 않기 때문이다 (HANDOFF 함정 3.1).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class CreatureController : MonoBehaviour
    {
        public float moveSpeed = 1.2f;

        [Tooltip("목표 지점에 닿거나 이 시간이 지나면 새 목표를 고른다.")]
        public float repathInterval = 3f;

        [Tooltip("목표 지점에 도착한 뒤 잠시 멈춰 서는 시간.")]
        public float restDuration = 0.8f;

        public float margin = 0.6f;

        private SpriteRenderer spriteRenderer;
        private CreatureInstance creature;
        private CareManager careManager;
        private float targetX;
        private float repathTimer;
        private float restTimer;

        public CreatureInstance Creature
        {
            get { return creature; }
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();

            // 씬에 에셋 스프라이트가 이미 꽂혀 있으면 그대로 쓴다.
            // 코드 생성 스프라이트는 에셋이 없을 때의 최후 수단이다.
            if (spriteRenderer.sprite == null)
            {
                spriteRenderer.sprite = PlaceholderSprites.Circle();
            }

            targetX = transform.position.x;

            // 원 스프라이트가 PPU = 텍스처 크기라 스케일 1 에서 지름 1 유닛이다.
            // 콜라이더는 같은 트랜스폼에 있어 스케일을 따라가므로 진화로 커져도 다시 맞출 필요가 없다.
            var clickArea = GetComponent<CircleCollider2D>();
            if (clickArea != null)
            {
                clickArea.radius = 0.5f;
            }
        }

        private void Start()
        {
            SnapToGround(transform.position.x);
            PickNewTarget();
        }

        public void Bind(CreatureInstance instance, CareManager manager)
        {
            creature = instance;
            careManager = manager;
            RefreshVisual();
        }

        /// <summary>
        /// 필드의 케어를 클릭하면 헛간으로 돌아간다.
        /// <see cref="FarmBuilding"/> 과 같은 방식으로 좌우 슬라이드 제스처를 걸러낸다 —
        /// 걸러내지 않으면 화면을 넘길 때마다 파티원이 사라진다.
        /// </summary>
        private void OnMouseUpAsButton()
        {
            if (CameraPanController.DraggedThisGesture || careManager == null || creature == null)
            {
                return;
            }

            careManager.ReturnToBarn(creature);
        }

        /// <summary>종이 바뀌었을 때 색·크기를 다시 읽고, 커진 만큼 땅 위로 다시 올려놓는다.</summary>
        public void RefreshVisual()
        {
            if (creature == null || creature.CurrentSpecies == null)
            {
                return;
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            spriteRenderer.color = creature.CurrentSpecies.placeholderColor;

            float scale = creature.CurrentSpecies.placeholderScale;
            transform.localScale = new Vector3(scale, scale, 1f);

            // 스케일이 커지면 중심도 올라가야 발이 땅에 붙는다.
            SnapToGround(transform.position.x);
        }

        /// <summary>
        /// 슬롯을 다시 쓸 때 겹쳐 서지 않도록 시작 위치를 지정한다.
        /// 목표는 <b>클램프된 뒤의 좌표</b>로 잡는다 — 맵 밖을 목표로 두면 도착 판정이 서지 않아
        /// 재탐색 타이머가 돌 때까지 그 자리에 멈춰 있는다 (HANDOFF 4.6).
        /// </summary>
        public void PlaceAt(float x)
        {
            SnapToGround(x);
            targetX = transform.position.x;
            repathTimer = repathInterval;
            restTimer = 0f;
        }

        private void Update()
        {
            if (restTimer > 0f)
            {
                restTimer -= Time.deltaTime;
                return;
            }

            repathTimer -= Time.deltaTime;

            float nextX = Mathf.MoveTowards(transform.position.x, targetX, moveSpeed * Time.deltaTime);
            SnapToGround(nextX);

            bool arrived = Mathf.Abs(transform.position.x - targetX) < 0.01f;
            if (arrived || repathTimer <= 0f)
            {
                restTimer = arrived ? restDuration : 0f;
                PickNewTarget();
            }
        }

        private void PickNewTarget()
        {
            targetX = FarmArea.RandomX(margin);
            repathTimer = repathInterval;

            // 바라보는 방향으로 스프라이트를 뒤집는다.
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = targetX < transform.position.x;
            }
        }

        private void SnapToGround(float x)
        {
            transform.position = FarmArea.OnGround(x, transform.localScale.y, margin);
        }
    }
}
