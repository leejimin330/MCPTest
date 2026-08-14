using FarmCreature.Data;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    public class PipelineSmokeTests
    {
        [Test]
        public void StatType_HasThreeCombatStats()
        {
            Assert.AreEqual(3, System.Enum.GetValues(typeof(StatType)).Length);
        }

        [Test]
        public void StatType_AttackIsFirst()
        {
            Assert.AreEqual(0, (int)StatType.Attack);
        }
    }
}
