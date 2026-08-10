using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NotANap.Core.Tests
{
    public sealed class BabyVariantArtTests
    {
        private static readonly string[] Variants =
        {
            "double_straight", "monolid_curly", "monolid_straight"
        };

        private static readonly string[] StateNames =
        {
            "awake_calm", "fuss_soft", "cry_hard", "hunger_early", "hunger_late",
            "drowsy", "rem_active", "nrem_deep", "relaxed", "moro_startle",
            "pacifier_accept", "pacifier_reject"
        };

        private static readonly string[] AnimatedNames =
        {
            "awake_0", "awake_1", "awake_2", "awake_3",
            "fuss_0", "fuss_1", "fuss_2", "fuss_3",
            "sleep_0", "sleep_1", "sleep_2", "sleep_3"
        };

        private static readonly string[] InteractionNames =
        {
            "pat_0", "pat_1", "pat_2", "pat_3",
            "hold_0", "hold_1", "hold_2", "hold_3",
            "feed_0", "feed_1", "feed_2", "feed_3",
            "carrier_0", "carrier_1", "carrier_2", "carrier_3",
            "pacifier_0", "pacifier_1", "pacifier_2", "pacifier_3",
            "diaper_check", "diaper_change", "limb_check", "temperature_check", "lying_sleep"
        };

        [Test]
        public void EveryEyeAndHairVariantHasAllGameplayFrames()
        {
            var missing = new List<string>();

            foreach (string variant in Variants)
            {
                CheckGroup(variant, string.Empty, StateNames, missing);
                CheckGroup(variant, "Animated/", AnimatedNames, missing);
                CheckGroup(variant, "Interaction/", InteractionNames, missing);
            }

            Assert.That(missing, Is.Empty,
                "Missing or oversized baby variant resources:\n" + string.Join("\n", missing));
        }

        private static void CheckGroup(string variant, string folder, IEnumerable<string> names,
            ICollection<string> failures)
        {
            foreach (string name in names)
            {
                string path = $"Art/Baby/Variants/{variant}/{folder}{name}";
                Texture2D texture = Resources.Load<Texture2D>(path);
                if (texture == null)
                {
                    failures.Add($"missing: {path}");
                    continue;
                }

                if (texture.width > 512 || texture.height > 512)
                    failures.Add($"oversized: {path} ({texture.width}x{texture.height})");
            }
        }
    }
}
