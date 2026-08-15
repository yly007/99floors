using System;
using System.Collections.Generic;
using UnityEngine;

namespace NinetyNine
{
    public sealed class EvacuationAudio : MonoBehaviour
    {
        private readonly Dictionary<GameObject, AudioSource> _monsterSources =
            new Dictionary<GameObject, AudioSource>();
        private readonly Dictionary<GameObject, float> _monsterBasePitch =
            new Dictionary<GameObject, float>();
        private readonly Dictionary<GameObject, AudioLowPassFilter> _monsterFilters =
            new Dictionary<GameObject, AudioLowPassFilter>();
        private readonly List<AudioSource> _spatialOneShots = new List<AudioSource>();
        private readonly List<AudioLowPassFilter> _outsideFilters = new List<AudioLowPassFilter>();
        private FirstPersonController _player;
        private AudioSource _ambience;
        private AudioSource _machinery;
        private AudioSource _breathing;
        private AudioSource _oneShot;
        private AudioSource _doorSource;
        private AudioSource _heartbeat;
        private AudioSource _lowPowerAlarm;
        private AudioClip _ambientClip;
        private AudioClip _machineClip;
        private AudioClip _breathClip;
        private AudioClip _monsterClip;
        private AudioClip _stepClip;
        private AudioClip _metalStepClip;
        private AudioClip _wetStepClip;
        private AudioClip _doorClip;
        private AudioClip _brakeClip;
        private AudioClip _railClip;
        private AudioClip _buttonClip;
        private AudioClip _pickupClip;
        private AudioClip _hitClip;
        private AudioClip _deathClip;
        private AudioClip _flashlightClip;
        private AudioClip _threatClip;
        private AudioClip _phoneClip;
        private AudioClip _distantKnockClip;
        private AudioClip[] _stepVariants;
        private AudioClip[] _metalStepVariants;
        private AudioClip[] _wetStepVariants;
        private AudioClip[] _doorVariants;
        private AudioClip[] _railVariants;
        private float _nextStepTime;
        private float _targetMachineVolume;
        private float _mood;
        private float _nextRailTime;
        private bool _travelling;
        private float _nextDistantKnock;
        private float _doorSeal;
        private int _spatialSourceIndex;
        private int _surfaceIndex;
        private float _power01 = 1f;
        private float _tension;
        private bool _criticalPower;
        private System.Random _random = new System.Random(9941);
        private AudioLowPassFilter _ambienceFilter;

        public void Initialize(FirstPersonController player)
        {
            _player = player;
            _ambientClip = CreateAmbient();
            _machineClip = CreateMachine();
            _breathClip = CreateBreathing();
            _monsterClip = CreateMonsterBreath();
            _stepClip = CreateNoiseImpact("Heavy Footstep", 0.19f, 96f, 0.72f, 1001);
            _metalStepClip = CreateNoiseImpact("Metal Footstep", 0.14f, 162f, 0.6f, 1811);
            _wetStepClip = CreateNoiseImpact("Wet Footstep", 0.22f, 58f, 0.56f, 2711);
            _doorClip = CreateMetalSweep();
            _brakeClip = CreateToneSweep("Mechanical Brake", 175f, 48f, 0.75f, 0.62f);
            _railClip = CreateNoiseImpact("Rail Joint", 0.16f, 86f, 0.38f, 1207);
            _buttonClip = CreateToneSweep("Mechanical Button", 540f, 210f, 0.11f, 0.26f);
            _pickupClip = CreateToneSweep("Object Pickup", 260f, 620f, 0.16f, 0.22f);
            _hitClip = CreateNoiseImpact("Body Hit", 0.42f, 58f, 0.9f, 7701);
            _deathClip = CreateToneSweep("Fatal Drop", 105f, 21f, 2.2f, 0.7f);
            _flashlightClip = CreateNoiseImpact("Flashlight Click", 0.07f, 1100f, 0.23f, 990);
            _threatClip = CreateToneSweep("Threat Sting", 190f, 41f, 0.75f, 0.48f);
            _phoneClip = CreatePhoneRing();
            _distantKnockClip = CreateNoiseImpact("Distant Pipe Knock", 0.34f, 74f, 0.52f, 8127);
            _stepVariants = new[]
            {
                _stepClip,
                CreateNoiseImpact("Heavy Footstep B", 0.18f, 88f, 0.68f, 1069),
                CreateNoiseImpact("Heavy Footstep C", 0.2f, 104f, 0.64f, 1123)
            };
            _metalStepVariants = new[]
            {
                _metalStepClip,
                CreateNoiseImpact("Metal Footstep B", 0.13f, 176f, 0.56f, 1877),
                CreateNoiseImpact("Metal Footstep C", 0.16f, 148f, 0.62f, 1931)
            };
            _wetStepVariants = new[]
            {
                _wetStepClip,
                CreateNoiseImpact("Wet Footstep B", 0.24f, 51f, 0.58f, 2789),
                CreateNoiseImpact("Wet Footstep C", 0.2f, 67f, 0.52f, 2851)
            };
            _doorVariants = new[]
            {
                _doorClip,
                CreateNoiseImpact("Door Gear Strain", 0.72f, 43f, 0.5f, 4421),
                CreateMetalSweep()
            };
            _railVariants = new[]
            {
                _railClip,
                CreateNoiseImpact("Rail Joint B", 0.18f, 72f, 0.4f, 1277),
                CreateNoiseImpact("Rail Joint C", 0.14f, 101f, 0.34f, 1321)
            };

            _ambience = CreateSource("Floor Ambience", false, true, 0.34f);
            _ambienceFilter = _ambience.gameObject.AddComponent<AudioLowPassFilter>();
            _ambience.clip = _ambientClip;
            _ambience.Play();
            _machinery = CreateSource("Elevator Machinery", false, true, 0f);
            _machinery.clip = _machineClip;
            _machinery.Play();
            _breathing = CreateSource("Player Breathing", false, true, 0f);
            _breathing.clip = _breathClip;
            _breathing.Play();
            _heartbeat = CreateSource("Player Heartbeat", false, true, 0f);
            _heartbeat.clip = CreateHeartbeat();
            _heartbeat.Play();
            _lowPowerAlarm = CreateSource("Low Power Relay", false, true, 0f);
            _lowPowerAlarm.clip = CreateLowPowerRelay();
            _lowPowerAlarm.Play();
            _oneShot = CreateSource("Player Feedback", false, false, 0.82f);
            _doorSource = CreateSource("Elevator Door Mechanism", true, false, 0.9f);
            _doorSource.transform.position = new Vector3(0f, 1.45f, 2.15f);
            _doorSource.minDistance = 1f;
            _doorSource.maxDistance = 12f;
            for (int i = 0; i < 4; i++)
            {
                AudioSource spatialSource = CreateSource("Spatial One Shot " + (i + 1), true, false, 1f);
                spatialSource.minDistance = 1f;
                spatialSource.maxDistance = 24f;
                AudioLowPassFilter filter = spatialSource.gameObject.AddComponent<AudioLowPassFilter>();
                filter.cutoffFrequency = 15000f;
                _outsideFilters.Add(filter);
                _spatialOneShots.Add(spatialSource);
            }
            _nextDistantKnock = Time.time + 8f;
        }

        public void SetRunSeed(int runSeed)
        {
            _random = new System.Random(unchecked(runSeed * 486187739) ^ 9941);
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
            _surfaceIndex = mood == (int)EvacuationTheme.Maintenance ? 1 :
                mood == (int)EvacuationTheme.Flooded ? 2 : 0;
        }

        public void SetDoorSeal(float seal)
        {
            _doorSeal = Mathf.Clamp01(seal);
            if (_ambienceFilter != null)
            {
                _ambienceFilter.cutoffFrequency = Mathf.Lerp(9000f, 1450f, _doorSeal);
            }
            foreach (KeyValuePair<GameObject, AudioLowPassFilter> pair in _monsterFilters)
            {
                if (pair.Value != null)
                {
                    pair.Value.cutoffFrequency = Mathf.Lerp(15000f, 780f, _doorSeal);
                }
            }
            for (int i = _outsideFilters.Count - 1; i >= 0; i--)
            {
                AudioLowPassFilter filter = _outsideFilters[i];
                if (filter == null)
                {
                    _outsideFilters.RemoveAt(i);
                    continue;
                }
                filter.cutoffFrequency = Mathf.Lerp(15000f, 680f, _doorSeal);
            }
        }

        public void SetPowerState(float power01, bool critical, float tension)
        {
            _power01 = Mathf.Clamp01(power01);
            _criticalPower = critical;
            _tension = Mathf.Clamp01(tension);
        }

        public void PlayDoor()
        {
            _doorSource.pitch = RandomRange(0.96f, 1.03f);
            _doorSource.PlayOneShot(RandomClip(_doorVariants), 0.88f);
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
            PlaySpatial(_threatClip, position, 0.95f, RandomRange(0.96f, 1.02f));
        }

        public void AttachMonsterSource(GameObject monster, MonsterArchetype archetype)
        {
            AudioSource source = monster.AddComponent<AudioSource>();
            source.clip = _monsterClip;
            source.loop = true;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1.2f;
            source.maxDistance = 24f;
            source.volume = 0.15f;
            float basePitch = archetype == MonsterArchetype.CeilingChild ? 1.18f :
                archetype == MonsterArchetype.Watcher ? 0.58f :
                archetype == MonsterArchetype.Janitor ? 0.76f : 0.68f;
            source.pitch = basePitch;
            source.Play();
            _monsterSources[monster] = source;
            _monsterBasePitch[monster] = basePitch;
            AudioLowPassFilter filter = monster.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = Mathf.Lerp(15000f, 780f, _doorSeal);
            _monsterFilters[monster] = filter;
        }

        public void DetachObject(GameObject target)
        {
            AudioSource source;
            if (_monsterSources.TryGetValue(target, out source) && source != null) source.Stop();
            _monsterSources.Remove(target);
            _monsterBasePitch.Remove(target);
            _monsterFilters.Remove(target);
        }

        public void AttachPhoneSource(GameObject phone)
        {
            AudioSource source = phone.AddComponent<AudioSource>();
            source.clip = _phoneClip;
            source.loop = true;
            source.spatialBlend = 1f;
            source.minDistance = 1f;
            source.maxDistance = 18f;
            source.volume = 0.72f;
            AudioLowPassFilter filter = phone.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = Mathf.Lerp(15000f, 680f, _doorSeal);
            _outsideFilters.Add(filter);
            source.Play();
        }

        public void SetMonsterUrgency(GameObject monster, float urgency)
        {
            AudioSource source;
            if (_monsterSources.TryGetValue(monster, out source) && source != null)
            {
                source.volume = Mathf.Lerp(0.12f, 0.92f, urgency);
                float basePitch;
                if (!_monsterBasePitch.TryGetValue(monster, out basePitch)) basePitch = 0.7f;
                source.pitch = Mathf.Lerp(basePitch, basePitch + 0.38f, urgency);
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
            _heartbeat.volume = Mathf.MoveTowards(_heartbeat.volume,
                Mathf.Lerp(0f, 0.58f, Mathf.Max(_tension, exhaustion * 0.8f)),
                Time.deltaTime * 0.8f);
            _heartbeat.pitch = Mathf.Lerp(0.82f, 1.28f, Mathf.Max(_tension, exhaustion));
            float relayTarget = _criticalPower ? 0.42f : Mathf.InverseLerp(0.35f, 0.08f, _power01) * 0.2f;
            _lowPowerAlarm.volume = Mathf.MoveTowards(_lowPowerAlarm.volume, relayTarget,
                Time.deltaTime * 0.5f);

            if (_travelling && Time.time >= _nextRailTime)
            {
                _nextRailTime = Time.time + RandomRange(0.82f, 1.28f);
                _oneShot.pitch = RandomRange(0.86f, 1.06f);
                _oneShot.PlayOneShot(RandomClip(_railVariants), 0.42f);
            }

            if (_player.MovementAmount > 0.2f && Time.time >= _nextStepTime)
            {
                float pace = _player.IsSprinting ? 0.29f : 0.48f;
                _nextStepTime = Time.time + pace;
                _oneShot.pitch = RandomRange(0.88f, 1.08f);
                AudioClip step = _surfaceIndex == 1 ? RandomClip(_metalStepVariants) :
                    _surfaceIndex == 2 ? RandomClip(_wetStepVariants) : RandomClip(_stepVariants);
                _oneShot.PlayOneShot(step, _player.IsSprinting ? 0.7f : 0.4f);
            }
            if (!_travelling && Time.time >= _nextDistantKnock)
            {
                _nextDistantKnock = Time.time + RandomRange(9f, 19f);
                float angle = RandomRange(0f, Mathf.PI * 2f);
                float radius = RandomRange(7f, 13f);
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                PlaySpatial(_distantKnockClip,
                    _player.transform.position + new Vector3(offset.x, 1f, offset.y), 0.42f,
                    RandomRange(0.9f, 1.06f));
            }
        }

        private void PlaySpatial(AudioClip clip, Vector3 position, float volume, float pitch)
        {
            if (clip == null || _spatialOneShots.Count == 0) return;
            AudioSource source = _spatialOneShots[_spatialSourceIndex++ % _spatialOneShots.Count];
            source.Stop();
            source.transform.position = position;
            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }

        private float RandomRange(float min, float max)
        {
            return Mathf.Lerp(min, max, (float)_random.NextDouble());
        }

        private AudioClip RandomClip(AudioClip[] values)
        {
            if (values == null || values.Length == 0) return null;
            return values[_random.Next(0, values.Length)];
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

        private static AudioClip CreateHeartbeat()
        {
            const int rate = 22050;
            const float duration = 1.2f;
            int count = Mathf.CeilToInt(rate * duration);
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)rate;
                float first = Mathf.Exp(-Mathf.Pow((time - 0.08f) * 24f, 2f));
                float second = Mathf.Exp(-Mathf.Pow((time - 0.24f) * 30f, 2f));
                samples[i] = (first * 0.78f + second * 0.48f) * Mathf.Sin(time * 54f);
            }
            AudioClip clip = AudioClip.Create("Heartbeat", count, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateLowPowerRelay()
        {
            const int rate = 22050;
            const float duration = 1.6f;
            int count = Mathf.CeilToInt(rate * duration);
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)rate;
                float pulse = Mathf.Exp(-Mathf.Pow((time - 0.08f) * 30f, 2f));
                float click = Mathf.Sin(time * 780f) * pulse * 0.34f;
                float hum = Mathf.Sin(time * 96f) * 0.045f;
                samples[i] = click + hum;
            }
            AudioClip clip = AudioClip.Create("Low Power Relay", count, 1, rate, false);
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

        private static AudioClip CreatePhoneRing()
        {
            const int rate = 22050;
            const float duration = 2.6f;
            int length = Mathf.RoundToInt(rate * duration);
            float[] samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                float time = i / (float)rate;
                float pulse = (time < 0.72f || (time > 1.02f && time < 1.74f)) ? 1f : 0f;
                float tremolo = 0.62f + Mathf.Sin(time * Mathf.PI * 2f * 7f) * 0.18f;
                samples[i] = (Mathf.Sin(time * Mathf.PI * 2f * 430f) * 0.32f +
                    Mathf.Sin(time * Mathf.PI * 2f * 510f) * 0.18f) * pulse * tremolo;
            }
            AudioClip clip = AudioClip.Create("Unanswered Hallway Phone", length, 1, rate, false);
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
