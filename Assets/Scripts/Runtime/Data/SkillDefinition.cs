using System;
using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>스킬이 하는 일. 지금은 두 가지뿐이다.</summary>
    public enum SkillEffect
    {
        /// <summary>대상 하나에게 평타의 배수만큼 피해.</summary>
        Damage,

        /// <summary>체력이 가장 적은 아군을 회복.</summary>
        Heal
    }

    /// <summary>
    /// 종마다 하나씩 가지는 고유 스킬. 별도 ScriptableObject 계통을 만들지 않고
    /// <see cref="CreatureSpeciesData"/> 안에 직렬화된다 — 스킬 교체/습득이 없으므로
    /// 종 데이터와 수명이 완전히 같기 때문이다.
    ///
    /// 진화하면 종이 바뀌므로 스킬도 함께 바뀐다.
    /// </summary>
    [Serializable]
    public class SkillDefinition
    {
        public string displayName = "스킬";

        public SkillEffect effect = SkillEffect.Damage;

        [Tooltip("전투 시작 후 이 시간이 지날 때마다 자동으로 쓴다.")]
        public float cooldownSeconds = 6f;

        [Tooltip("Damage 는 평타 피해의 배수, Heal 은 공격력의 배수만큼 회복한다.")]
        public float multiplier = 2f;
    }
}
