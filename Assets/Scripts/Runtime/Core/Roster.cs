using System.Collections.Generic;

namespace FarmCreature.Core
{
    /// <summary>
    /// 보유한 케어 전부와, 그중 <b>파티에 나와 있는</b> 것들.
    /// 파티원은 필드를 돌아다니고 전투에 그대로 출전한다.
    ///
    /// 보유 목록(<see cref="Members"/>)의 순서는 <b>바뀌지 않는다</b> —
    /// 헛간 패널의 버튼이 이 인덱스를 persistent listener 인자로 들고 있기 때문이다.
    /// 파티 합류/복귀는 보유 목록을 건드리지 않고 파티 목록만 바꾼다.
    /// </summary>
    public class Roster
    {
        /// <summary>동시에 필드에 나올 수 있는 최대 인원. 필드 슬롯 개수와 같아야 한다.</summary>
        public const int MaxParty = 4;

        /// <summary>헛간 패널이 보여줄 수 있는 최대 줄 수. 이보다 많이 담지 않는다.</summary>
        public const int MaxMembers = 8;

        private readonly List<CreatureInstance> members = new List<CreatureInstance>();
        private readonly List<CreatureInstance> party = new List<CreatureInstance>();

        public IReadOnlyList<CreatureInstance> Members
        {
            get { return members; }
        }

        /// <summary>합류한 순서대로. 필드 슬롯 배정과 전투 진형이 이 순서를 따른다.</summary>
        public IReadOnlyList<CreatureInstance> Party
        {
            get { return party; }
        }

        public int Count
        {
            get { return members.Count; }
        }

        public bool IsFull
        {
            get { return members.Count >= MaxMembers; }
        }

        public CreatureInstance GetMember(int index)
        {
            return index >= 0 && index < members.Count ? members[index] : null;
        }

        public bool IsInParty(CreatureInstance creature)
        {
            return creature != null && party.Contains(creature);
        }

        public bool Has(CreatureInstance creature)
        {
            return creature != null && members.Contains(creature);
        }

        /// <summary>보유 목록에 추가한다. 이미 있으면 아무것도 하지 않는다.</summary>
        public ActionResult Add(CreatureInstance creature)
        {
            if (creature == null)
            {
                return ActionResult.Fail("케어가 없습니다.");
            }

            if (members.Contains(creature))
            {
                return ActionResult.Fail("이미 보유한 케어다.");
            }

            if (IsFull)
            {
                return ActionResult.Fail("헛간이 가득 찼다. (최대 " + MaxMembers + "마리)");
            }

            members.Add(creature);
            return ActionResult.Ok(Name(creature) + "이(가) 헛간에 들어왔다.");
        }

        public ActionResult JoinParty(CreatureInstance creature)
        {
            if (creature == null)
            {
                return ActionResult.Fail("케어가 없습니다.");
            }

            if (!members.Contains(creature))
            {
                return ActionResult.Fail("헛간에 없는 케어다.");
            }

            if (party.Contains(creature))
            {
                return ActionResult.Fail(Name(creature) + "은(는) 이미 나가 있다.");
            }

            if (party.Count >= MaxParty)
            {
                return ActionResult.Fail("파티가 가득 찼다. (최대 " + MaxParty + "마리)");
            }

            party.Add(creature);
            return ActionResult.Ok(Name(creature) + "이(가) 밖으로 나왔다.");
        }

        public ActionResult LeaveParty(CreatureInstance creature)
        {
            if (creature == null)
            {
                return ActionResult.Fail("케어가 없습니다.");
            }

            if (!party.Remove(creature))
            {
                return ActionResult.Fail(Name(creature) + "은(는) 밖에 나와 있지 않다.");
            }

            return ActionResult.Ok(Name(creature) + "이(가) 헛간으로 돌아갔다.");
        }

        private static string Name(CreatureInstance creature)
        {
            return creature != null && creature.CurrentSpecies != null
                ? creature.CurrentSpecies.displayName
                : "케어";
        }
    }
}
