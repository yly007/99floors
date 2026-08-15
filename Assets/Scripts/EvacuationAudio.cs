using System;
using System.Collections.Generic;
using UnityEngine;

namespace NinetyNine
{
    public sealed class EvacuationAudio : MonoBehaviour
    {
        private readonly Dictionary<GameObject, AudioSource> _monsterSources =
            new Dictionary<GameObject, AudioSource>();
        private FirstPersonController _player;
        private AudioSource _ambience;
        private AudioSource _machinery;
        private AudioSource _breathing;
        private AudioSource _oneShot;
        private AudioClip _ambientClip;
        private AudioClip _machineClip;
        private AudioClip _breathClip;
        private AudioClip _monsterClip;
        private AudioClip _stepClip;
        private AudioClip _doorClip;
        private AudioClip _brakeClip;
        private AudioClip _railClip;
        private AudioClip _buttonClip;
        private AudioClip _pickupClip;
        private AudioClip _hitClip;
        private AudioClip _deathClip;
        private AudioClip _flashlightClip;
        private AudioClip _threatClip;
        private float _nextStepTime;
        private float _targetMachineVolume;
        private float _mood;
        private float _nextRailTime;
        private bool _travelling;

        public void Initialize(FirstPersonController player)
        {
            _player = player;
            _ambientClip = CreateAmbient();
            _machineClip = CreateMachine();
            _breathClip = CreateBreathing();
            _monsterClip = CreateMonsterBreath();
            _stepClip = CreateNoiseImpact("Heavy Footstep", 0.19f, 96f, 0.72f, 1001);
            _doorClip = CreateMetalSweep();
            _brakeClip = CreateToneSweep("Mechanical Brake", 175f, 48f, 0.75f, 0.62f);
            _railClip = CreateNoiseImpact("Rail Joint", 0.16f, 86f, 0.38f, 1207);
            _buttonClip = CreateToneSweep("Mechanical Button", 540f, 210f, 0.11f, 0.26f);
            _pickupClip = CreateToneSweep("Object Pickup", 260f, 620f, 0.16f, 0.22f);
            _hitClip = CreateNoiseImpact("Body Hit", 0.42f, 58f, 0.9f, 7701);
            _deathClip = CreateToneSweep("Fatal Drop", 105f, 21f, 2.2f, 0.7f);
            _flashlightClip = CreateNoiseImpact("Flashlight Click", 0.07f, 1100f, 0.23f, 990);
            _threatClip = CreateToneSweep("Threat Sting", 190f, 41f, 0.75f, 0.48f);

            _ambience = CreateSource("Floor Ambience", false, true, 0.34f);
            _ambience.clip = _ambientClip;
            _ambience.Play();
            _machinery = CreateSource("Elevator Machinery", false, true, 0f);
            _machinery.clip = _machineClip;
            _machinery.Play();
            _breathing = CreateSource("Player Breathing", false, true, 0f);
            _breathing.clip = _breathClip;
            _breathing.Play();
            _oneShot = CreateSource("Player Feedback", false, false, 0.82f);
        }

        public void SetTravelling(bool travelling)
        {
            _travelling = travelling;
            _targetMachineVolume = travelling ? 0.82f : 0.04f;
            if (travelling)
            {
                _nextRailTime = Time.time + 0.65f;
                PlayButton();
            }
        }

        public void SetFloorMood(int mood)
        {
            _mood = Mathf.Clamp01(mood / 5f);
            _ambience.pitch = Mathf.Lerp(0.78f, 1.16f, Mathf.Repeat(mood * 0.37f, 1f));
        }

        public void PlayDoor()
        {
            _oneShot.pitch = 1f;
            _oneShot.PlayOneShot(_doorClip, 0.88f);
        }

        public void PlayBrake()
        {
            _oneShot.pitch = 1f;
            _oneShot.PlayOneShot(_brakeClip, 0.92f);
        }

        public void PlayButton()
        {
            _oneShot.pitch = 1f;
            _oneShot.PlayOneShot(_buttonClip, 0.62f);
        }

        public void PlayPickup()
        {
            _oneShot.PlayOneShot(_pickupClip, 0.68f);
        }

        public void PlayHit()
        {
            _oneShot.PlayOneShot(_hitClip, 1f);
        }

        public void PlayDeath()
        {
            _oneShot.PlayOneShot(_deathClip, 1f);
        }

        public void PlayFlashlight()
        {
            _oneShot.PlayOneShot(_flashlightClip, 0.78f);
        }

        public void PlayThreatCue(Vector3 position)
        {
            AudioSource.PlayClipAtPoint(_threatClip, position, 0.95f);
        }

        public void AttachMonsterSource(GameObject monster)
        {
            AudioSource source = monster.AddComponent<AudioSource>();
            source.clip = _monsterClip;
            source.loop = true;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1.2f;
            source.maxDistance = 24f;
            source.volume = 0.15f;
            source.pitch = 0.72f;
            source.Play();
            _monsterSources[monster] = source;
        }

        public void SetMonsterUrgency(GameObject monster, float urgency)
        {
            AudioSource source;
            if (_monsterSources.TryGetValue(monster, out source) && source != null)
            {
                source.volume = Mathf.Lerp(0.12f, 0.92f, urgency);
                source.pitch = Mathf.Lerp(0.7f, 1.12f, urgency);
            }
        }

        private void Update()
        {
            if (_player == null)
            {
                return;
            }
            _machinery.volume = Mathf.MoveTowards(_machinery.volume, _targetMachineVolume,
                Time.deltaTime * 0.7f);
            _ambience.volume = (_travelling ? 0.11f : 0.25f) + _mood * (_travelling ? 0.06f : 0.16f);
            float exhaustion = 1f - _player.Stamina01;
            _breathing.volume = Mathf.Lerp(0f, 0.78f, Mathf.InverseLerp(0.38f, 0.94f, exhaustion));
            _breathing.pitch = Mathf.Lerp(0.82f, 1.35f, exhaustion);

            if (_travelling && Time.time >= _nextRailTime)
            {
                _nextRailTime = Time.time + UnityEngine.Random.Range(0.82f, 1.28f);
                _oneShot.pitch = UnityEngine.Random.Range(0.86f, 1.06f);
                _oneShot.PlayOneShot(_railClip, 0.42f);
            }

            if (_player.MovementAmount > 0.2f && Time.time >= _nextStepTime)
            {
                float pace = _player.IsSprinting ? 0.29f : 0.48f;
                _nextStepTime = Time.time + pace;
                _oneShot.pitch = UnityEngine.Random.Range(0.88f, 1.08f);
                _oneShot.PlayOneShot(_stepClip, _player.IsSprinting ? 0.7f : 0.4f);
            }
        }

        private AudioSource CreateSource(string sourceName, bool spatial, bool loop, float volume)
        {
            GameObject sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.spatialBlend = spatial ? 1f : 0f;
            source.loop = loop;
            source.playOnAwake = false;
            source.volume = volume;
            return source;
        }

        private static AudioClip CreateAmbient()
        {
            const int rate = 22050;
            const int seconds = 7;
            float[] samples = new float[rate * seconds];
            System.Random random = new System.Random(9942);
            float filtered = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)rate;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                filtered = Mathf.Lerp(filtered, noise, 0.006f);
                float hum = Mathf.Sin(time * Mathf.PI * 2f * 49f) * 0.12f;
                hum += Mathf.Sin(time * Mathf.PI * 2f * 17.5f) * 0.07f;
                float pipe = Mathf.Sin(time * Mathf.PI * 2f * 2.1f) *
                    Mathf.Sin(time * Mathf.PI * 2f * 91f) * 0.025f;
                samples[i] = (hum + pipe + filtered * 0.28f) * 0.72f;
            }
            AudioClip clip = AudioClip.Create("Abandoned Building Ambience", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateMachine()
        {
            const int rate = 22050;
            const int seconds = 4;
            float[] samples = new float[rate * seconds];
            System.Random random = new System.Random(9904);
            float filtered = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)rate;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                filtered = Mathf.Lerp(filtered, noise, 0.022f);
                float motor = Mathf.Sin(time * Mathf.PI * 2f * 42f) * 0.28f;
                motor += Mathf.Sin(time * Mathf.PI * 2f * 96f) * 0.13f;
                motor += Mathf.Sin(time * Mathf.PI * 2f * 147f) * 0.055f;
                motor *= 0.78f + Mathf.Sin(time * Mathf.PI * 2f * 2.7f) * 0.1f;
                samples[i] = Mathf.Clamp(motor + filtered * 0.16f, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("Elevator Cable Motor", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateBreathing()
        {
            const int rate = 22050;
            const int seconds = 3;
            float[] samples = new float[rate * seconds];
            System.Random random = new System.Random(554);
            float filtered = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)rate;
                float phase = Mathf.Repeat(time / seconds, 1f);
                float envelope = Mathf.Pow(Mathf.Sin(phase * Mathf.PI), 2f);
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                filtered = Mathf.Lerp(filtered, noise, 0.045f);
                samples[i] = filtered * envelope * 0.62f;
            }
            AudioClip clip = AudioClip.Create("Exhausted Breathing", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateMonsterBreath()
        {
            const int rate = 22050;
            const int seconds = 3;
            float[] samples = new float[rate * seconds];
            System.Random random = new System.Random(6669);
            float filtered = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)rate;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                filtered = Mathf.Lerp(filtered, noise, 0.012f);
                float growl = Mathf.Sin(time * Mathf.PI * 2f * 32f +
                    Mathf.Sin(time * 7f) * 2.4f) * 0.26f;
                float gasp = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * Mathf.PI * 1.35f)), 5f);
                samples[i] = growl + filtered * gasp * 0.72f;
            }
            AudioClip clip = AudioClip.Create("Pursuer Breathing", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateNoiseImpact(string clipName, float duration, float tone,
            float volume, int seed)
        {
            const int rate = 22050;
            int length = Mathf.RoundToInt(rate * duration);
            float[] samples = new float[length];
            System.Random random = new System.Random(seed);
            for (int i = 0; i < length; i++)
            {
                float time = i / (float)rate;
                float progress = time / duration;
                float envelope = Mathf.Exp(-progress * 6f) * (1f - progress);
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                float wave = Mathf.Sin(time * Mathf.PI * 2f * tone) * 0.45f;
                samples[i] = (noise * 0.55f + wave) * envelope * volume;
            }
            AudioClip clip = AudioClip.Create(clipName, length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateMetalSweep()
        {
            const int rate = 22050;
            const float duration = 3.4f;
            int length = Mathf.RoundToInt(rate * duration);
            float[] samples = new float[length];
            System.Random random = new System.Random(882);
            for (int i = 0; i < length; i++)
            {
                float time = i / (float)rate;
                float progress = time / duration;
                float scrape = (float)(random.NextDouble() * 2.0 - 1.0) * (0.45f - progress * 0.22f);
                float rail = Mathf.Sin(time * Mathf.PI * 2f * Mathf.Lerp(145f, 62f, progress)) * 0.24f;
                float slam = progress > 0.88f ? Mathf.Sin(time * Mathf.PI * 2f * 48f) * (1f - progress) * 3f : 0f;
                samples[i] = (scrape + rail + slam) * Mathf.Sin(progress * Mathf.PI);
            }
            AudioClip clip = AudioClip.Create("Elevator Door Rail", length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateToneSweep(string clipName, float from, float to,
            float duration, float volume)
        {
            const int rate = 22050;
            int length = Mathf.RoundToInt(rate * duration);
            float[] samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                float time = i / (float)rate;
                float progress = time / duration;
                float frequency = Mathf.Lerp(from, to, progress);
                float envelope = Mathf.Sin(progress * Mathf.PI) * (1f - progress * 0.35f);
                samples[i] = Mathf.Sin(time * Mathf.PI * 2f * frequency) * envelope * volume;
            }
            AudioClip clip = AudioClip.Create(clipName, length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
