using System.Globalization;
using System.Text;
using NotANap.Core;

namespace NotANap.Core.Tests
{
    internal static class TestHelpers
    {
        /// <summary>백일째 밤까지 진행된 런 생성.</summary>
        public static RunState FinalRun(Temperament temperament = null)
        {
            var run = RunState.Create(temperament ?? Temperament.Soft);
            run.AdvanceNight();
            run.AdvanceNight();
            return run;
        }

        /// <summary>턴 소비 행동 1회 + 시간 경과.</summary>
        public static ActionOutcome Step(RunState run, NightState night, GameAction action, IRandomSource rng)
        {
            var outcome = ActionResolver.Apply(run, night, action, rng);
            if (outcome.ConsumedTurn) TurnResolver.EndTurn(run, night, rng);
            return outcome;
        }

        /// <summary>밤이 끝날 때까지 filler 행동 반복.</summary>
        public static void PlayUntilMorning(RunState run, NightState night, IRandomSource rng,
            GameAction filler = GameAction.Pat)
        {
            int guard = 0;
            while (!night.Over)
            {
                if (++guard > 30)
                    throw new System.InvalidOperationException("밤이 30턴 안에 끝나지 않았다.");
                Step(run, night, filler, rng);
            }
        }

        private static string D(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>결정론 검증용 전체 상태 덤프.</summary>
        public static string Snapshot(RunState run, NightState n)
        {
            var sb = new StringBuilder();
            var b = n.Baby;
            var s = n.Stats;
            sb.Append("night=").Append(n.NightId)
              .Append(";hour=").Append(n.Hour)
              .Append(";over=").Append(n.Over)
              .Append(";result=").Append(n.Result)
              .Append(";turns=").Append(n.ConsumedTurns).AppendLine();
            sb.Append("baby calm=").Append(D(b.Calm))
              .Append(" sleep=").Append(D(b.Sleep))
              .Append(" hunger=").Append(D(b.Hunger))
              .Append(" held=").Append(b.Held)
              .Append(" crying=").Append(b.Crying).AppendLine();
            sb.Append("parent stamina=").Append(D(n.Parent.Stamina)).AppendLine();
            sb.Append("wearing c=").Append(n.Wearing.Carrier)
              .Append(" n=").Append(n.Wearing.Noise)
              .Append(" b=").Append(n.Wearing.Bouncer)
              .Append(";pacifierLeft=").Append(n.PacifierLeft)
              .Append(";pacifierInUse=").Append(n.PacifierInUse).AppendLine();
            sb.Append("stats wakes=").Append(s.Wakes)
              .Append(" ldFail=").Append(s.LaydownFail)
              .Append(" ldOk=").Append(s.LaydownOk)
              .Append(" carrierT=").Append(s.CarrierTurns)
              .Append(" heldSleepT=").Append(s.HeldSleepTurns)
              .Append(" noiseT=").Append(s.NoiseTurns)
              .Append(" watchOk=").Append(s.WatchOk)
              .Append(" feeds=").Append(s.Feeds)
              .Append(" refusals=").Append(s.Refusals)
              .Append(" holds=").Append(s.Holds)
              .Append(" grandma=").Append(s.Grandma)
              .Append(" staminaLeft=").Append(D(s.StaminaLeft))
              .Append(" bareHands=").Append(s.BareHandsLaydownSucceeded).AppendLine();
            var m = run.Memory;
            sb.Append("memory carrier=").Append(D(m.Carrier))
              .Append(" heldDep=").Append(D(m.HeldDep))
              .Append(" noiseHab=").Append(D(m.NoiseHab))
              .Append(" selfSoothe=").Append(D(m.SelfSoothe)).AppendLine();
            sb.Append("nightResults=").Append(run.NightResults.Count).AppendLine();
            foreach (var e in n.Log) sb.AppendLine(e.ToString());
            return sb.ToString();
        }

        public static bool LogContains(NightState night, string fragment)
        {
            foreach (var e in night.Log)
                if (e.Text.Contains(fragment)) return true;
            return false;
        }
    }
}
