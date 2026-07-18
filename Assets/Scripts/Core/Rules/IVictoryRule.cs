namespace NotANap.Core
{
    public interface IVictoryRule { VictoryResult Evaluate(NightState night); }

    public sealed class ConfiguredVictoryRule : IVictoryRule
    {
        private readonly VictoryRuleDefinition _definition;
        public ConfiguredVictoryRule(VictoryRuleDefinition definition) =>
            _definition = definition ?? throw new System.ArgumentNullException(nameof(definition));
        public VictoryResult Evaluate(NightState night) => VictoryResolver.Evaluate(night, _definition);
    }
}
