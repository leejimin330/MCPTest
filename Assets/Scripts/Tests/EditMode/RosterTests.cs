using FarmCreature.Core;
using FarmCreature.Data;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    public class RosterTests
    {
        private CreatureSpeciesData species;

        [SetUp]
        public void SetUp()
        {
            species = TestFixtures.MakeSpecies("sprigling", "스프리글링");
        }

        private CreatureInstance New()
        {
            return CreatureInstance.CreateNew(species);
        }

        [Test]
        public void Add_KeepsInsertionOrder()
        {
            var roster = new Roster();
            var a = New();
            var b = New();

            roster.Add(a);
            roster.Add(b);

            Assert.AreSame(a, roster.GetMember(0));
            Assert.AreSame(b, roster.GetMember(1));
            Assert.AreEqual(2, roster.Count);
        }

        [Test]
        public void Add_RejectsDuplicate()
        {
            var roster = new Roster();
            var a = New();

            Assert.IsTrue(roster.Add(a).Success);
            Assert.IsFalse(roster.Add(a).Success, "같은 개체를 두 번 넣으면 인덱스가 어긋난다");
            Assert.AreEqual(1, roster.Count);
        }

        [Test]
        public void Add_RejectsBeyondMaxMembers()
        {
            var roster = new Roster();

            for (int i = 0; i < Roster.MaxMembers; i++)
            {
                Assert.IsTrue(roster.Add(New()).Success, "한도까지는 들어가야 한다");
            }

            var overflow = roster.Add(New());
            Assert.IsFalse(overflow.Success);
            Assert.AreEqual(Roster.MaxMembers, roster.Count);
        }

        [Test]
        public void JoinParty_RequiresMembership()
        {
            var roster = new Roster();
            var stranger = New();

            Assert.IsFalse(roster.JoinParty(stranger).Success, "헛간에 없는 케어는 못 내보낸다");
        }

        [Test]
        public void JoinParty_RejectsDuplicate()
        {
            var roster = new Roster();
            var a = New();
            roster.Add(a);

            Assert.IsTrue(roster.JoinParty(a).Success);
            Assert.IsFalse(roster.JoinParty(a).Success);
            Assert.AreEqual(1, roster.Party.Count);
        }

        [Test]
        public void JoinParty_RejectsBeyondMaxParty()
        {
            var roster = new Roster();

            for (int i = 0; i < Roster.MaxParty + 1; i++)
            {
                roster.Add(New());
            }

            for (int i = 0; i < Roster.MaxParty; i++)
            {
                Assert.IsTrue(roster.JoinParty(roster.GetMember(i)).Success);
            }

            Assert.IsFalse(roster.JoinParty(roster.GetMember(Roster.MaxParty)).Success,
                "파티 인원은 필드 슬롯 수와 같아야 한다");
            Assert.AreEqual(Roster.MaxParty, roster.Party.Count);
        }

        [Test]
        public void LeaveParty_DoesNotRemoveFromRoster()
        {
            var roster = new Roster();
            var a = New();
            roster.Add(a);
            roster.JoinParty(a);

            Assert.IsTrue(roster.LeaveParty(a).Success);
            Assert.AreEqual(0, roster.Party.Count);
            Assert.AreEqual(1, roster.Count, "복귀는 보유 목록을 건드리지 않는다");
            Assert.AreSame(a, roster.GetMember(0), "보유 인덱스가 그대로여야 버튼 배선이 유효하다");
        }

        [Test]
        public void LeaveParty_RejectsWhenNotOut()
        {
            var roster = new Roster();
            var a = New();
            roster.Add(a);

            Assert.IsFalse(roster.LeaveParty(a).Success);
        }

        [Test]
        public void PartyOrder_FollowsJoinOrderNotRosterOrder()
        {
            var roster = new Roster();
            var a = New();
            var b = New();
            var c = New();
            roster.Add(a);
            roster.Add(b);
            roster.Add(c);

            roster.JoinParty(c);
            roster.JoinParty(a);

            Assert.AreSame(c, roster.Party[0]);
            Assert.AreSame(a, roster.Party[1]);
        }

        [Test]
        public void GetMember_OutOfRangeReturnsNull()
        {
            var roster = new Roster();
            roster.Add(New());

            Assert.IsNull(roster.GetMember(-1));
            Assert.IsNull(roster.GetMember(5));
        }
    }
}
