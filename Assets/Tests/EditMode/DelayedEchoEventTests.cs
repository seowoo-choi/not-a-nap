using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NotANap.Core;

namespace NotANap.Core.Tests
{
    public class DelayedEchoEventTests
    {
        private static readonly TraceId TraceA = new TraceId("fixture.trace.a");
        private static readonly FutureEventId EventA = new FutureEventId("fixture.event.a");
        private static readonly FutureEventId EventB = new FutureEventId("fixture.event.b");

        private sealed class ContextRule : IContextualEventRule
        {
            public EventResolution Resolve(EventSeed seed, EventContext context)
            {
                var result = new EventResolution
                {
                    EventId = seed.EventId,
                    OutcomeType = context.ActiveConditionTags.Contains("supportive")
                        ? EventOutcomeType.Positive : EventOutcomeType.Negative
                };
                result.SourceTraceIds.AddRange(seed.SourceTraceIds);
                return result;
            }
        }

        private static TraceState Traces()
        {
            var traces = new TraceState();
            TraceRecorder.FromAction(traces, TraceA, ActionId.Hold, NightId.FirstNight, 2, .6, new[] { "routine" });
            return traces;
        }

        private static FutureEventDefinition Definition(FutureEventId id, int max = 1)
        {
            var definition = new FutureEventDefinition
            {
                EventId = id, EarliestTurn = 2, LatestTurn = 7,
                BaseWeight = 1, MaxTriggersPerRun = max,
                FeedforwardCue = new FeedforwardCueId("fixture.cue")
            };
            definition.EligibleNights.Add(NightId.SecondNight);
            definition.RequiredTraceIds.Add(TraceA);
            return definition;
        }

        [Test]
        public void ActionCanCreateValueNeutralTrace()
        {
            var traces = Traces();
            var trace = traces.Records.Single();
            Assert.AreEqual(ActionId.Hold, trace.SourceActionId);
            Assert.AreEqual(.6, trace.Strength);
        }

        [Test]
        public void TraceHasNoPolarityAndOnlyResolutionClassifiesOutcome()
        {
            Assert.IsNull(typeof(TraceRecord).GetField("OutcomeType"));
            Assert.IsNotNull(typeof(EventResolution).GetField("OutcomeType"));
        }

        [Test]
        public void SameSeedTraceAndContextScheduleSameFutureEvent()
        {
            var definitions = new[] { Definition(EventA), Definition(EventB) };
            var a = FutureEventScheduler.ScheduleOne(definitions, NightId.SecondNight, Traces(), new SystemRandomSource(42));
            var b = FutureEventScheduler.ScheduleOne(definitions, NightId.SecondNight, Traces(), new SystemRandomSource(42));
            Assert.AreEqual(a.EventId, b.EventId);
            Assert.AreEqual(a.TriggerTurn, b.TriggerTurn);
        }

        [Test]
        public void DifferentSeedsCanChangeCandidateOrTurn()
        {
            var definitions = new[] { Definition(EventA), Definition(EventB) };
            var choices = Enumerable.Range(0, 20).Select(seed =>
            {
                var picked = FutureEventScheduler.ScheduleOne(definitions, NightId.SecondNight, Traces(), new SystemRandomSource(seed));
                return picked.EventId.Value + ":" + picked.TriggerTurn;
            }).Distinct();
            Assert.Greater(choices.Count(), 1);
        }

        [Test]
        public void MissingRequiredTraceMakesEventIneligible()
        {
            Assert.IsNull(FutureEventScheduler.ScheduleOne(new[] { Definition(EventA) },
                NightId.SecondNight, new TraceState(), new SystemRandomSource(1)));
        }

        [Test]
        public void SameTraceCanResolveDifferentlyByContext()
        {
            var seed = new EventSeed { EventId = EventA, NightId = NightId.SecondNight, TriggerTurn = 3 };
            seed.SourceTraceIds.Add(TraceA);
            var positive = Context("supportive");
            var negative = Context("demanding");
            var rule = new ContextRule();
            Assert.AreEqual(EventOutcomeType.Positive, rule.Resolve(seed, positive).OutcomeType);
            Assert.AreEqual(EventOutcomeType.Negative, rule.Resolve(seed, negative).OutcomeType);
        }

        [Test]
        public void FeedforwardCueDoesNotResolveOrMutateState()
        {
            var traces = Traces();
            var seed = FutureEventScheduler.ScheduleOne(new[] { Definition(EventA) },
                NightId.SecondNight, traces, new SystemRandomSource(1));
            int before = traces.Records.Count;
            Assert.IsTrue(DelayedEchoEngine.GetFeedforwardCue(seed).HasValue);
            Assert.AreEqual(before, traces.Records.Count);
            Assert.AreEqual(0, traces.TriggerCount(EventA.Value));
        }

        [Test]
        public void TriggerDoesNotDependOnAiNarrative()
        {
            var withoutAi = FutureEventScheduler.ScheduleOne(new[] { Definition(EventA) },
                NightId.SecondNight, Traces(), new SystemRandomSource(7));
            NarrativeBoundary.Validate("arbitrary narrative");
            var withAi = FutureEventScheduler.ScheduleOne(new[] { Definition(EventA) },
                NightId.SecondNight, Traces(), new SystemRandomSource(7));
            Assert.AreEqual(withoutAi.TriggerTurn, withAi.TriggerTurn);
            Assert.AreEqual(withoutAi.EventId, withAi.EventId);
        }

        [Test]
        public void NarrativeBoundaryCannotChangeTraceState()
        {
            var traces = Traces();
            var before = traces.Records.Single().Strength;
            NarrativeBoundary.Validate("trace should become good");
            Assert.AreEqual(before, traces.Records.Single().Strength);
            Assert.AreEqual(1, traces.Records.Count);
        }

        [Test]
        public void ScheduledTurnAlwaysStaysInsideEligibleWindow()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var result = FutureEventScheduler.ScheduleOne(new[] { Definition(EventA) },
                    NightId.SecondNight, Traces(), new SystemRandomSource(seed));
                Assert.That(result.TriggerTurn, Is.InRange(2, 7));
            }
        }

        [Test]
        public void MaxTriggersPerRunIsEnforced()
        {
            var traces = Traces();
            var definition = Definition(EventA, 1);
            var seed = FutureEventScheduler.ScheduleOne(new[] { definition }, NightId.SecondNight, traces,
                new SequenceRandomSource(0));
            var context = Context("supportive", traces, seed.TriggerTurn);
            Assert.IsNotNull(DelayedEchoEngine.TryResolve(seed, context, traces, 1, new ContextRule()));
            Assert.IsNull(FutureEventScheduler.ScheduleOne(new[] { definition }, NightId.SecondNight, traces,
                new SequenceRandomSource(0)));
            Assert.IsNull(DelayedEchoEngine.TryResolve(seed, context, traces, 1, new ContextRule()));
        }

        [Test]
        public void ExistingMemoryConsolidatorStillFormsMemory()
        {
            var run = RunState.Create(Temperament.Soft);
            var night = NightFactory.CreateNight(run, new[] { ItemId.Carrier, ItemId.Noise, ItemId.Monitor });
            night.Stats.CarrierTurns = 3;
            MemoryConsolidator.Consolidate(run, night);
            Assert.AreEqual(.35, run.Memory.Carrier, 1e-12);
            Assert.AreEqual(0, run.Traces.Records.Count, "기존 Memory 변환을 암묵적으로 Trace로 바꾸지 않는다.");
        }

        private static EventContext Context(string tag, TraceState traces = null, int turn = 3)
        {
            var run = RunState.Create(Temperament.Soft);
            run.AdvanceNight();
            run.Traces = traces ?? Traces();
            var night = NightFactory.CreateNight(run, new[] { ItemId.Pacifier, ItemId.Noise, ItemId.Monitor });
            night.ConsumedTurns = turn;
            return EventContext.Capture(run, night, new[] { tag });
        }
    }
}
