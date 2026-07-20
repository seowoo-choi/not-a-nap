namespace NotANap.Core
{
    /// <summary>
    /// 백일째 밤 승리 판정 — 아침 6시 기준 3개 중 2개 이상 (docs/final-night-spec.md).
    /// 1. 아기가 깊은 잠(sleep ≥ 85, 울지 않음)으로 아침을 맞음
    /// 2. 보호자 체력 ≥ 30
    /// 3. 맨손 눕히기 성공 1회 이상
    /// </summary>
    public static class VictoryResolver
    {
        public static VictoryResult Evaluate(NightState night)
            => Evaluate(night, VictoryRuleDefinition.Default());

        public static VictoryResult Evaluate(NightState night, VictoryRuleDefinition definition)
        {
            if (definition == null) throw new System.ArgumentNullException(nameof(definition));
            var result = new VictoryResult { RequiredCount = definition.RequiredCount };
            var b = night.Baby;
            if (b.Sleep >= definition.DeepSleepThreshold && !b.Crying)
                result.Met.Add(VictoryCondition.DeepSleepMorning);
            if (night.Parent.Stamina >= definition.ParentStaminaThreshold)
                result.Met.Add(VictoryCondition.ParentStamina);
            if (night.Stats.BareHandsLaydownSucceeded)
                result.Met.Add(VictoryCondition.BareHandsLaydown);
            return result;
        }
    }
}
