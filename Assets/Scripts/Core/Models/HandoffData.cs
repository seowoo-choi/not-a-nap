using System.Collections.Generic;

namespace NotANap.Core
{
    public enum HandoffCueId { Hunger, Tiredness, Sensitivity, PreferredSoothing }

    public sealed class HandoffData
    {
        public NightId NightId;
        public double InitialHunger;
        public double InitialCalm;
        public readonly List<HandoffCueId> Cues = new List<HandoffCueId>();
    }
}
