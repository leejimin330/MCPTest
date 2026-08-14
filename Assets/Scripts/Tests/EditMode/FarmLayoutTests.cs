using FarmCreature.Core;
using FarmCreature.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace FarmCreature.Tests
{
    /// <summary>
    /// 농장 배치의 산술. 건물 5동이 맵 안에 들어오는지, 파티원이 맵 밖을 목표로 잡지 않는지를 본다
    /// (HANDOFF 4.6).
    /// </summary>
    public class FarmLayoutTests
    {
        private const float BuildingHalfWidth = 1.7f;   // GameBootstrap 의 bodyWidth 3.4 의 절반
        private static readonly float[] BuildingX = { -12f, -6f, 0f, 6f, 12f };

        [Test]
        public void AllFiveBuildings_FitInsideMap()
        {
            for (int i = 0; i < BuildingX.Length; i++)
            {
                float edge = Mathf.Abs(BuildingX[i]) + BuildingHalfWidth;
                Assert.LessOrEqual(edge, FarmArea.HalfWidth,
                    "건물 " + i + " 이 맵 밖으로 나간다 — 카메라가 그 자리를 비추지 못한다");
            }
        }

        [Test]
        public void SpawnX_StaysInsideMap_ForEveryPartySize()
        {
            for (int count = 1; count <= Roster.MaxParty; count++)
            {
                for (int i = 0; i < count; i++)
                {
                    float x = PartyField.SpawnX(i, count);
                    Assert.LessOrEqual(Mathf.Abs(x), FarmArea.HalfWidth,
                        "파티 " + count + "명 중 " + i + "번이 맵 밖을 목표로 잡는다");
                }
            }
        }

        [Test]
        public void SpawnX_SpreadsMembersApart()
        {
            float first = PartyField.SpawnX(0, 4);
            float last = PartyField.SpawnX(3, 4);

            Assert.Less(first, last, "왼쪽부터 오른쪽으로 펼친다");
            Assert.Greater(last - first, 10f, "4명이 겹쳐 서지 않을 만큼은 벌어져야 한다");
        }

        [Test]
        public void SpawnX_SingleMember_IsCentered()
        {
            Assert.AreEqual(0f, PartyField.SpawnX(0, 1));
        }

        [Test]
        public void BuildingKind_BattleGround_IsLastValue()
        {
            Assert.AreEqual(4, (int)BuildingKind.BattleGround,
                "씬에 정수로 직렬화되므로 새 값은 반드시 마지막에 붙인다");
        }
    }
}
