using System.Collections.Generic;
using NotANap.Core;
using NotANap.Presentation;
using UnityEngine;

namespace NotANap.App
{
    /// <summary>
    /// Core/ViewModel 상태를 캐릭터 그림으로만 번역한다.
    /// 게임 상태와 판정은 변경하지 않는다.
    /// </summary>
    public sealed class BabyVisualPresenter
    {
        public enum VisualState
        {
            AwakeCalm,
            FussSoft,
            CryHard,
            HungerEarly,
            HungerLate,
            Drowsy,
            RemActive,
            NremDeep,
            Relaxed,
            MoroStartle,
            PacifierAccept,
            PacifierReject
        }

        private readonly Dictionary<VisualState, Texture2D> _textures = new Dictionary<VisualState, Texture2D>();

        public BabyVisualPresenter()
        {
            Load(VisualState.AwakeCalm, "awake_calm");
            Load(VisualState.FussSoft, "fuss_soft");
            Load(VisualState.CryHard, "cry_hard");
            Load(VisualState.HungerEarly, "hunger_early");
            Load(VisualState.HungerLate, "hunger_late");
            Load(VisualState.Drowsy, "drowsy");
            Load(VisualState.RemActive, "rem_active");
            Load(VisualState.NremDeep, "nrem_deep");
            Load(VisualState.Relaxed, "relaxed");
            Load(VisualState.MoroStartle, "moro_startle");
            Load(VisualState.PacifierAccept, "pacifier_accept");
            Load(VisualState.PacifierReject, "pacifier_reject");
        }

        public Texture2D TextureFor(V2PlayViewModel vm, V2ActionOutcome latestOutcome)
            => _textures.TryGetValue(Resolve(vm, latestOutcome), out var texture) ? texture : null;

        public VisualState Resolve(V2PlayViewModel vm, V2ActionOutcome latestOutcome)
        {
            if (latestOutcome != null)
            {
                if (latestOutcome.EventIds.Contains(GameEventId.LaydownFailed))
                    return VisualState.MoroStartle;
                if (latestOutcome.Action == V2ActionId.Pacifier)
                    return latestOutcome.Accepted ? VisualState.PacifierAccept : VisualState.PacifierReject;
                if (latestOutcome.HungerSignalStage == HungerSignalStage.Late)
                    return VisualState.HungerLate;
                if (latestOutcome.HungerSignalStage == HungerSignalStage.Early ||
                    latestOutcome.HungerSignalStage == HungerSignalStage.Active)
                    return VisualState.HungerEarly;
            }

            switch (vm.SleepStage)
            {
                case V2SleepStage.Drowsy:
                    return VisualState.Drowsy;
                case V2SleepStage.RemActiveSleep:
                    return VisualState.RemActive;
                case V2SleepStage.NremDeepSleep:
                    return vm.DeepSleepObserved ? VisualState.Relaxed : VisualState.NremDeep;
                default:
                    if (vm.CryIntensity > 35) return VisualState.CryHard;
                    if (vm.CryIntensity > 0) return VisualState.FussSoft;
                    return VisualState.AwakeCalm;
            }
        }

        private void Load(VisualState state, string fileName)
            => _textures[state] = Resources.Load<Texture2D>($"Art/Baby/{fileName}");
    }
}
