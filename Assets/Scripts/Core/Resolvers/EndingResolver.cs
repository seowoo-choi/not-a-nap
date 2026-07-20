using System.Collections.Generic;

namespace NotANap.Core
{
    /// <summary>
    /// 엔딩 6종 판정 — docs/final-night-spec.md의 우선순위를 위에서부터 적용.
    /// 앞선 조건에 해당하면 뒤 조건은 검사하지 않는다.
    /// memory 값은 백일째 밤 종료·기억 형성까지 반영된 저장값(1.5배 미적용) 기준.
    /// </summary>
    public static class EndingResolver
    {
        public static EndingResult Decide(RunState run, VictoryResult victory)
        {
            // 1. 아침이 이겼다 (실패): 승리 조건 0~1개
            if (!victory.IsVictory)
                return Make(EndingId.MorningWon, false, victory);

            var m = run.Memory;

            // 2. 우리 집의 루틴: 모든 의존치 < 0.4 — 이상 엔딩
            if (m.Carrier < 0.4 && m.HeldDep < 0.4 && m.NoiseHab < 0.4)
                return Make(EndingId.FamilyRoutine, true, victory);

            // 3. 품 안의 우주: carrier 또는 heldDep ≥ 0.5
            if (m.Carrier >= 0.5 || m.HeldDep >= 0.5)
                return Make(EndingId.UniverseInArms, true, victory);

            // 4. 할머니가 최고야: 1~2일차에 할머니 찬스 사용
            if (run.GrandmaUsed)
                return Make(EndingId.GrandmaBest, true, victory);

            // 5. 장비의 지배자: 3일간 서로 다른 아이템 4종 이상 사용
            if (run.UsedItemKinds.Count >= 4)
                return Make(EndingId.GearMaster, true, victory);

            // 6. 새벽의 생존자: 그 외 승리
            return Make(EndingId.DawnSurvivor, true, victory);
        }

        private static EndingResult Make(EndingId id, bool success, VictoryResult victory)
            => new EndingResult
            {
                Id = id,
                IsSuccess = success,
                MetConditions = new List<VictoryCondition>(victory.Met),
            };
    }
}
