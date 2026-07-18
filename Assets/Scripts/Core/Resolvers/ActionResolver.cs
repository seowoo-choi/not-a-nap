namespace NotANap.Core
{
    /// <summary>
    /// 행동 처리. 수치·공식 원본: Reference/prototype.html applyAction().
    /// 턴을 소비한 행동(ConsumedTurn=true)은 호출측이 이어서 TurnResolver.EndTurn()을 호출한다.
    /// </summary>
    public static class ActionResolver
    {
        public static ActionOutcome Apply(RunState run, NightState night, GameAction action, IRandomSource rng)
        {
            var outcome = new ActionOutcome { Action = action, Accepted = true, ConsumedTurn = true };
            if (night.Over)
            {
                outcome.Accepted = false;
                outcome.ConsumedTurn = false;
                return outcome;
            }

            var b = night.Baby;
            var p = night.Parent;
            var t = run.Temperament;
            var m = run.GetEffectiveMemory();
            double weak = p.Stamina < 20 ? 0.6 : 1; // 체력 고갈 시 효과 감소

            void Log(string text, LogClass cls = LogClass.Sys)
            {
                var entry = new GameLogEntry(night.Hour, text, cls);
                night.Log.Add(entry);
                outcome.Log.Add(entry);
            }

            void Reject(string text, LogClass cls = LogClass.Sys)
            {
                outcome.Accepted = false;
                outcome.ConsumedTurn = false;
                Log(text, cls);
            }

            switch (action)
            {
                case GameAction.Hold:
                {
                    if (night.Wearing.Carrier) { Reject("이미 아기띠로 안고 있다."); break; }
                    b.Held = true;
                    night.Stats.Holds++;
                    b.Calm = CoreMath.Clamp(b.Calm + (16 + t.HoldNeed * 12) * weak, 0, 100);
                    p.Stamina = CoreMath.Clamp(p.Stamina - 10, 0, 100);
                    Log(b.Crying ? "품에 안자 울음이 조금씩 잦아든다." : "품에 안았다. 아기가 파고든다.");
                    if (b.Crying && b.Calm > 45) b.Crying = false;
                    break;
                }
                case GameAction.Pat:
                {
                    b.Calm = CoreMath.Clamp(b.Calm + 10 * weak, 0, 100);
                    if (b.Sleep > 0 && !b.Crying) b.Sleep = CoreMath.Clamp(b.Sleep + 8, 0, 100);
                    p.Stamina = CoreMath.Clamp(p.Stamina - 5, 0, 100);
                    if (b.Crying && b.Calm > 50)
                    {
                        b.Crying = false;
                        Log("토닥토닥… 울음이 멎었다.", LogClass.Good);
                    }
                    else Log("등을 토닥여 주었다.");
                    break;
                }
                case GameAction.Feed:
                {
                    p.Stamina = CoreMath.Clamp(p.Stamina - 8, 0, 100);
                    if (b.Hunger >= 45)
                    {
                        b.Hunger = 5;
                        b.Calm = CoreMath.Clamp(b.Calm + (20 + t.FeedBonus) * weak, 0, 100);
                        b.Crying = false;
                        night.Stats.Feeds++;
                        Log("🍼 꿀꺽꿀꺽. 세상 행복한 표정이다.", LogClass.Good);
                    }
                    else
                    {
                        b.Calm = CoreMath.Clamp(b.Calm - 6, 0, 100);
                        night.Stats.Refusals++;
                        Log("고개를 홱 돌린다. 지금은 배가 고픈 게 아닌가 보다.", LogClass.Warn);
                    }
                    break;
                }
                case GameAction.Laydown:
                {
                    if (!b.Held && !night.Wearing.Carrier) { Reject("아기는 이미 침대에 있다."); break; }
                    outcome.LaydownAttempted = true;
                    // 맨손 눕히기 판정은 행동 시작 시점 기준 (docs/final-night-spec.md 구현 확정 사항)
                    bool bareHands = b.Held && !night.Wearing.Carrier && !night.Wearing.Bouncer;
                    double pSuccess = CalculateLaydownSuccessProbability(run, night);
                    outcome.HabitPenaltyApplied =
                        m.HeldDep > 0 || m.Carrier > 0 || night.LaydownExtraPenalty > 0;
                    if (night.Wearing.Carrier) night.Wearing.Carrier = false;
                    p.Stamina = CoreMath.Clamp(p.Stamina - 4, 0, 100);
                    if (rng.NextDouble() < pSuccess)
                    {
                        b.Held = false;
                        night.Stats.LaydownOk++;
                        outcome.LaydownSucceeded = true;
                        night.AddEvent(GameEventId.LaydownSucceeded);
                        Log("🛏️ 숨을 죽이고… 성공. 아기가 침대에서 계속 잔다.", LogClass.Good);
                        if (bareHands)
                        {
                            night.Stats.BareHandsLaydownSucceeded = true;
                            outcome.BareHandsLaydown = true;
                        }
                    }
                    else
                    {
                        night.AddEvent(GameEventId.LaydownFailed);
                        b.Held = false;
                        night.Stats.LaydownFail++;
                        TurnResolver.WakeBaby(run, night, "등이 침대에 닿는 순간 센서 발동", rng);
                        if (run.Memory.Carrier > 0.3 || run.Memory.HeldDep > 0.3)
                            Log("※ 안겨 자는 습관 때문에 눕히기가 더 어려워졌다.", LogClass.Warn);
                    }
                    break;
                }
                case GameAction.Watch:
                {
                    p.Stamina = CoreMath.Clamp(p.Stamina + 9, 0, 100);
                    double soothe = t.SelfSoothe + m.SelfSoothe;
                    if (!b.Crying && b.Calm >= 45 && rng.NextDouble() < soothe + 0.15)
                    {
                        b.Calm = CoreMath.Clamp(b.Calm + 7, 0, 100);
                        night.Stats.WatchOk++;
                        Log("아기가 혼자 꼼지락거리다 스스로 진정했다.", LogClass.Good);
                    }
                    else if (b.Crying)
                    {
                        b.Calm = CoreMath.Clamp(b.Calm - 9, 0, 100);
                        Log("우는 아기를 지켜보는 건 고문이다… 울음이 더 커진다.", LogClass.Warn);
                    }
                    else
                    {
                        b.Calm = CoreMath.Clamp(b.Calm - 5, 0, 100);
                        Log("아기가 심심한지 칭얼거리기 시작한다.");
                    }
                    break;
                }
                case GameAction.Grandma:
                {
                    if (run.IsFinalNight)
                    {
                        Reject("오늘 밤은 가족 없이 버텨야 한다. 할머니 찬스를 쓸 수 없다.", LogClass.Warn);
                        break;
                    }
                    if (run.GrandmaUsed) { Reject("할머니 찬스는 이미 사용했다."); break; }
                    run.GrandmaUsed = true;
                    night.Stats.Grandma = true;
                    b.Calm = 95;
                    b.Sleep = System.Math.Max(b.Sleep, 60);
                    b.Crying = false;
                    b.Held = true;
                    p.Stamina = CoreMath.Clamp(p.Stamina + 35, 0, 100);
                    Log("👵 \"애는 원래 안아서 재워야 해.\" 할머니의 품에서 아기가 순식간에 잠든다.", LogClass.Good);
                    Log("…하지만 아기는 이 품도 기억할 것이다.", LogClass.Warn);
                    break;
                }
                case GameAction.Pacifier: // 시간 소모 없음
                {
                    outcome.ConsumedTurn = false;
                    if (!night.HasItem(ItemId.Pacifier)) { Reject("쪽쪽이를 가져오지 않았다."); break; }
                    if (night.PacifierLeft <= 0) { Reject("쪽쪽이를 다 썼다."); break; }
                    night.PacifierLeft--;
                    if (rng.NextDouble() < 0.15)
                    {
                        b.Calm = CoreMath.Clamp(b.Calm - 8, 0, 100);
                        Log("퉤. 쪽쪽이를 뱉어버렸다.", LogClass.Warn);
                    }
                    else
                    {
                        b.Calm = CoreMath.Clamp(b.Calm + 18, 0, 100);
                        night.PacifierInUse = true;
                        if (b.Crying && b.Calm > 45) b.Crying = false;
                        Log("🍭 쪽쪽이를 물자 순식간에 조용해졌다.", LogClass.Good);
                    }
                    break;
                }
                case GameAction.ToggleCarrier:
                {
                    outcome.ConsumedTurn = false;
                    if (!night.HasItem(ItemId.Carrier)) { Reject("아기띠를 가져오지 않았다."); break; }
                    if (!night.Wearing.Carrier && night.CarrierDisabledTurns > 0)
                    {
                        Reject("버클이 고장 나 아기띠를 쓸 수 없다.", LogClass.Warn);
                        break;
                    }
                    night.Wearing.Carrier = !night.Wearing.Carrier;
                    if (night.Wearing.Carrier)
                    {
                        b.Held = true;
                        Log("🎒 아기띠를 착용했다. 두 손이 자유롭다.");
                    }
                    else Log("아기띠를 벗었다. (아기는 아직 품 안)");
                    break;
                }
                case GameAction.ToggleNoise:
                {
                    outcome.ConsumedTurn = false;
                    if (!night.HasItem(ItemId.Noise)) { Reject("백색소음기를 가져오지 않았다."); break; }
                    if (!night.Wearing.Noise && night.NoiseDisabled)
                    {
                        Reject("백색소음기 배터리가 방전됐다. 오늘 밤은 켤 수 없다.", LogClass.Warn);
                        break;
                    }
                    night.Wearing.Noise = !night.Wearing.Noise;
                    Log(night.Wearing.Noise ? "🔊 백색소음기 작동. 쏴아아—" : "백색소음기를 껐다.");
                    break;
                }
                case GameAction.ToggleBouncer:
                {
                    outcome.ConsumedTurn = false;
                    if (!night.HasItem(ItemId.Bouncer)) { Reject("바운서를 가져오지 않았다."); break; }
                    if (b.Held || night.Wearing.Carrier)
                    {
                        night.Wearing.Bouncer = false;
                        Reject("안고 있는 상태에선 바운서를 쓸 수 없다. 먼저 눕히세요.");
                        break;
                    }
                    night.Wearing.Bouncer = !night.Wearing.Bouncer;
                    Log(night.Wearing.Bouncer ? "🪑 아기를 바운서에 태웠다." : "바운서에서 내렸다.");
                    break;
                }
            }
            return outcome;
        }

        /// <summary>
        /// 눕히기 성공 확률 (순수 함수). 원본: prototype laydown 공식
        /// + 백일째 밤 memory 1.5배·새벽 각성 추가 페널티(docs/final-night-spec.md).
        /// </summary>
        public static double CalculateLaydownSuccessProbability(RunState run, NightState night)
            => CalculateLaydownSuccessProbability(run, night, null);

        public static double CalculateLaydownSuccessProbability(
            RunState run, NightState night, GameBalanceConfig config)
        {
            var b = night.Baby;
            var m = run.GetEffectiveMemory();
            double p = b.Sleep >= 85 ? 0.9 : b.Sleep >= 50 ? 0.6 : 0.15;
            double cribSensitivity = run.Temperament.CribSens;
            if (config != null && config.TemperamentModifiers.TryGetValue(
                    run.Temperament.Id, out var modifier))
                cribSensitivity = modifier.CribSensitivity;
            p -= cribSensitivity + m.HeldDep * 0.45 + m.Carrier * 0.20;
            p -= night.LaydownExtraPenalty;
            return CoreMath.Clamp(p, 0.05, 0.95);
        }
    }
}
