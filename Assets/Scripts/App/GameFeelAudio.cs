using System;
using NotANap.Core;
using NotANap.Presentation;
using UnityEngine;

namespace NotANap.App
{
    /// <summary>
    /// 외부 음원 없이 짧은 절차 음향을 만드는 Presentation 전용 오디오.
    /// 판정과 상태는 읽기만 하며 Core에는 영향을 주지 않는다.
    /// </summary>
    public sealed class GameFeelAudio : MonoBehaviour
    {
        private const int SampleRate = 22050;

        private AudioSource _ambient;
        private AudioSource _sfx;
        private AudioClip _roomTone;
        private AudioClip _softCry;
        private AudioClip _hardCry;
        private AudioClip _breath;
        private AudioClip _tap;
        private AudioClip _success;
        private AudioClip _failure;
        private AudioClip _door;
        private AudioClip _bottle;
        private AudioClip _dawn;
        private float _cryIntensity;
        private bool _sleeping;
        private float _nextBabySoundAt;
        private float _babyVoicePitch = 1f;

        public static GameFeelAudio Attach(GameObject target)
            => target.GetComponent<GameFeelAudio>() ?? target.AddComponent<GameFeelAudio>();

        private void Awake()
        {
            _ambient = gameObject.AddComponent<AudioSource>();
            _ambient.loop = true;
            _ambient.volume = 0.075f;
            _ambient.ignoreListenerPause = true;

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.volume = 0.42f;
            _sfx.ignoreListenerPause = true;

            _roomTone = Make("room tone", 2.4f, (t, r) =>
                Mathf.Sin(t * Mathf.PI * 2f * 55f) * 0.025f +
                (r.Next(-1000, 1001) / 1000f) * 0.012f);
            _softCry = Make("soft fuss", 0.34f, (t, r) =>
                Envelope(t, 0.34f) * Mathf.Sin(t * Mathf.PI * 2f * (310f + 35f * Mathf.Sin(t * 18f))) * 0.48f);
            _hardCry = Make("hard cry", 0.58f, (t, r) =>
                Envelope(t, 0.58f) * (Mathf.Sin(t * Mathf.PI * 2f * (430f + 120f * Mathf.Sin(t * 15f))) * 0.5f +
                Mathf.Sin(t * Mathf.PI * 2f * 860f) * 0.12f));
            _breath = Make("sleep breath", 0.7f, (t, r) =>
                Mathf.Sin(Mathf.Clamp01(t / 0.7f) * Mathf.PI) *
                (r.Next(-1000, 1001) / 1000f) * 0.1f);
            _tap = Tone("soft tap", 0.09f, 520f, 0.24f);
            _success = Chime("laydown success", 0.42f, 440f, 660f, 0.34f);
            _failure = Chime("laydown wake", 0.35f, 360f, 190f, 0.3f);
            _door = Chime("room move", 0.2f, 180f, 120f, 0.22f);
            _bottle = Chime("bottle prep", 0.16f, 720f, 930f, 0.18f);
            _dawn = Chime("dawn", 0.85f, 330f, 660f, 0.3f);

            _ambient.clip = _roomTone;
            _ambient.Play();
            _nextBabySoundAt = Time.unscaledTime + 2f;
        }

        public void SetBabyState(V2PlayViewModel vm)
        {
            _cryIntensity = (float)vm.CryIntensity;
            _sleeping = vm.SleepStage == V2SleepStage.RemActiveSleep ||
                        vm.SleepStage == V2SleepStage.NremDeepSleep;
        }

        public void SetBabyVoiceVariant(int variant)
            => _babyVoicePitch = variant == 0 ? 1.12f : variant == 2 ? 0.9f : 1f;

        public void PlayAction(V2ActionOutcome outcome)
        {
            if (outcome == null) return;
            if (!outcome.Accepted) { Play(_failure, 0.42f); return; }
            if (outcome.EventIds.Contains(GameEventId.LaydownSucceeded)) { Play(_success, 0.7f); return; }
            if (outcome.EventIds.Contains(GameEventId.LaydownFailed) ||
                outcome.EventIds.Contains(GameEventId.BabyFullyWoke)) { Play(_failure, 0.72f); return; }
            if (outcome.Action == V2ActionId.PrepareWater ||
                outcome.Action == V2ActionId.CoolBottle ||
                outcome.Action == V2ActionId.FeedPreparedBottle) { Play(_bottle, 0.55f); return; }
            if (outcome.Action == V2ActionId.CatchBreath) { Play(_breath, 0.55f); return; }
            Play(_tap, 0.48f);
        }

        public void PlayMove() => Play(_door, 0.55f);
        public void PlayDawn() => Play(_dawn, 0.72f);
        public void PlayUi() => Play(_tap, 0.35f);

        private void Update()
        {
            if (Time.unscaledTime < _nextBabySoundAt || _sfx.isPlaying) return;
            if (_sleeping)
            {
                PlayBaby(_breath, 0.18f);
                _nextBabySoundAt = Time.unscaledTime + 4.5f;
            }
            else if (_cryIntensity >= 45f)
            {
                PlayBaby(_hardCry, Mathf.Lerp(0.3f, 0.62f, _cryIntensity / 100f));
                _nextBabySoundAt = Time.unscaledTime + 1.4f;
            }
            else if (_cryIntensity > 8f)
            {
                PlayBaby(_softCry, 0.28f);
                _nextBabySoundAt = Time.unscaledTime + 2.8f;
            }
            else _nextBabySoundAt = Time.unscaledTime + 2f;
        }

        private void Play(AudioClip clip, float volume)
        {
            _sfx.pitch = 1f;
            if (clip != null) _sfx.PlayOneShot(clip, volume);
        }

        private void PlayBaby(AudioClip clip, float volume)
        {
            _sfx.pitch = _babyVoicePitch;
            if (clip != null) _sfx.PlayOneShot(clip, volume);
        }

        private static AudioClip Tone(string name, float duration, float frequency, float gain)
            => Make(name, duration, (t, r) =>
                Envelope(t, duration) * Mathf.Sin(t * Mathf.PI * 2f * frequency) * gain);

        private static AudioClip Chime(string name, float duration, float from, float to, float gain)
            => Make(name, duration, (t, r) =>
            {
                float p = t / duration;
                float frequency = Mathf.Lerp(from, to, p);
                return Envelope(t, duration) * Mathf.Sin(t * Mathf.PI * 2f * frequency) * gain;
            });

        private static float Envelope(float time, float duration)
        {
            float attack = Mathf.Clamp01(time / Mathf.Min(0.035f, duration * 0.25f));
            float release = Mathf.Clamp01((duration - time) / Mathf.Min(0.12f, duration * 0.45f));
            return attack * release;
        }

        private static AudioClip Make(string name, float duration, Func<float, System.Random, float> sample)
        {
            int count = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[count];
            var random = new System.Random(name.GetHashCode());
            for (int i = 0; i < count; i++)
                data[i] = Mathf.Clamp(sample(i / (float)SampleRate, random), -1f, 1f);
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
