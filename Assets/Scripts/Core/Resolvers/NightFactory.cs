using System;
using System.Collections.Generic;

namespace NotANap.Core
{
    /// <summary>
    /// 밤 생성 + 아이템 선택 검증.
    /// 1~2일차는 슬롯 3개, 백일째 밤은 슬롯 2개 (docs/final-night-spec.md).
    /// </summary>
    public static class NightFactory
    {
        public static int ItemSlots(NightId nightId)
            => nightId == NightId.HundredthNight
                ? GameConfig.FinalNightItemSlots
                : GameConfig.NormalNightItemSlots;

        public static NightState CreateNight(RunState run, IReadOnlyList<ItemId> items)
            => CreateNight(run, items, GameBalanceConfig.Default());

        public static NightState CreateNight(RunState run, IReadOnlyList<ItemId> items, GameBalanceConfig config)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (config == null) throw new ArgumentNullException(nameof(config));
            int slots = run.CurrentNightId == NightId.HundredthNight
                ? config.FinalNightItemSlots : config.NormalNightItemSlots;
            var distinct = new HashSet<ItemId>(items);
            if (items.Count != slots || distinct.Count != items.Count)
                throw new ArgumentException(
                    $"{run.CurrentNightId}에는 서로 다른 아이템 {slots}개를 선택해야 한다. (선택: {items.Count}개)",
                    nameof(items));

            foreach (var item in items) run.UsedItemKinds.Add(item);

            var night = new NightState
            {
                NightId = run.CurrentNightId,
                Hour = config.StartHour,
                Baby = new BabyState
                {
                    Calm = config.InitialCalm,
                    Sleep = config.InitialSleep,
                    Hunger = config.InitialHunger
                },
                Parent = new ParentState { Stamina = config.InitialStamina }
            };
            night.Items.AddRange(items);
            if (run.CurrentNightId == NightId.HundredthNight)
                night.ActiveTargetedEvents = FinalNightResolver.SelectTargetedEvents(run);
            return night;
        }
    }
}
