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
        private AudioSource _titleMusic;
        private AudioSource _themeAmbienceA;
        private AudioSource _themeAmbienceB;
        private AudioSource _machinery;
        private AudioSource _breathing;
        private AudioSource _oneShot;
        private AudioSource _doorSource;
        private AudioSource _heartbeat;
        private AudioSource _lowPowerAlarm;
        private AudioClip _ambientClip;
        private AudioClip _titleClip;
        private AudioClip[] _themeClips;
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
        private AudioClip _victoryClip;
        private AudioClip _flashlightClip;
        private AudioClip _threatClip;
        private AudioClip _phoneClip;
        private AudioClip _distantKnockClip;
        private AudioClip _narrativeCueClip;
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
        private bool _titleMode;
        private bool _themeOnA;
        private int _themeIndex = -1;
        private System.Random _random = new System.Random(9941);
        private AudioLowPassFilter _ambienceFilter;

        public void Initialize(FirstPersonController player)
        {
            _player = player;
            _ambientClip = CreateAmbient();
            _titleClip = CreateTitleAmbience();
            _themeClips = new AudioClip[System.Enum.GetValues(typeof(EvacuationTheme)).Length];
            for (int i = 0; i < _themeClips.Length; i++)
            {
                _themeClips[i] = CreateThemeAmbience((EvacuationTheme)i);
            }
            _machineClip = CreateMachine();
            _breathClip = CreateBreathing();
            _monsterClip = CreateMonsterBreath();
            _stepClip = CreateSoftFootstep("Soft Floor Footstep", 0.2f, 78f, 0.045f, 0.38f, 1001);
            _metalStepClip = CreateSoftFootstep("Soft Metal Footstep", 0.18f, 132f, 0.08f, 0.32f, 1811);
            _wetStepClip = CreateSoftFootstep("Soft Wet Footstep", 0.24f, 48f, 0.025f, 0.34f, 2711);
            _doorClip = CreateMetalSweep();
            _brakeClip = CreateToneSweep("Mechanical Brake", 175f, 48f, 0.75f, 0.62f);
            _railClip = CreateCableCreak("Cabin Cable Creak", 0.9f, 37f, 51f, 1207);
            _buttonClip = CreateToneSweep("Mechanical Button", 540f, 210f, 0.11f, 0.26f);
            _pickupClip = CreateToneSweep("Object Pickup", 260f, 620f, 0.16f, 0.22f);
            _hitClip = CreateNoiseImpact("Body Hit", 0.42f, 58f, 0.9f, 7701);
            _deathClip = CreateToneSweep("Fatal Drop", 105f, 21f, 2.2f, 0.7f);
            _victoryClip = CreateToneSweep("Distant Arrival", 310f, 680f, 1.8f, 0.34f);
            _flashlightClip = CreateNoiseImpact("Flashlight Click", 0.07f, 1100f, 0.23f, 990);
            _threatClip = CreateToneSweep("Threat Sting", 190f, 41f, 0.75f, 0.48f);
            _phoneClip = CreatePhoneRing();
            _distantKnockClip = CreateNoiseImpact("Distant Pipe Knock", 0.34f, 74f, 0.52f, 8127);
            _narrativeCueClip = CreateToneSweep("Lockdown Intercom Cue", 420f, 92f, 0.65f, 0.2f);
            _stepVariants = new[]
            {
                _stepClip,
                CreateSoftFootstep("Soft Floor Footstep B", 0.19f, 72f, 0.04f, 0.36f, 1069),
                CreateSoftFootstep("Soft Floor Footstep C", 0.21f, 84f, 0.05f, 0.34f, 1123)
            };
            _metalStepVariants = new[]
            {
                _metalStepClip,
                CreateSoftFootstep("Soft Metal Footstep B", 0.17f, 143f, 0.075f, 0.3f, 1877),
                CreateSoftFootstep("Soft Metal Footstep C", 0.19f, 121f, 0.085f, 0.31f, 1931)
            };
            _wetStepVariants = new[]
            {
                _wetStepClip,
                CreateSoftFootstep("Soft Wet Footstep B", 0.26f, 43f, 0.022f, 0.33f, 2789),
                CreateSoftFootstep("Soft Wet Footstep C", 0.22f, 55f, 0.028f, 0.31f, 2851)
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
                CreateCableCreak("Cabin Cable Creak B", 1.15f, 31f, 46f, 1277),
                CreateCableCreak("Cabin Cable Creak C", 0.72f, 44f, 34f, 1321)
            };

            _ambience = CreateSource("Floor Ambience", false, true, 0.34f);
            _ambienceFilter = _ambience.gameObject.AddComponent<AudioLowPassFilter>();
            _ambience.clip = _ambientClip;
            _ambience.Play();
            _titleMusic = CreateSource("Midnight Title Ambience", false, true, 0f);
            _titleMusic.clip = _titleClip;
            _titleMusic.Play();
            _themeAmbienceA = CreateSource("Floor Theme Ambience A", false, true, 0f);
            _themeAmbienceB = CreateSource("Floor Theme Ambience B", false, true, 0f);
            AudioLowPassFilter themeFilterA = _themeAmbienceA.gameObject.AddComponent<AudioLowPassFilter>();
            AudioLowPassFilter themeFilterB = _themeAmbienceB.gameObject.AddComponent<AudioLowPassFilter>();
            themeFilterA.cutoffFrequency = 15000f;
            themeFilterB.cutoffFrequency = 15000f;
            _outsideFilters.Add(themeFilterA);
            _outsideFilters.Add(themeFilterB);
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

        public void SetTitleMode(bool active)
        {
            _titleMode = active;
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

        public void SetFloorMood(EvacuationTheme theme)
        {
            int mood = (int)theme;
            _mood = Mathf.Clamp01(mood / 5f);
            _ambience.pitch = Mathf.Lerp(0.92f, 1.06f, Mathf.Repeat(mood * 0.37f, 1f));
            _surfaceIndex = mood == (int)EvacuationTheme.Maintenance ? 1 :
                mood == (int)EvacuationTheme.Flooded ? 2 : 0;
            if (_themeIndex == mood || _themeClips == null || mood >= _themeClips.Length)
            {
                return;
            }
            _themeIndex = mood;
            AudioSource next = _themeOnA ? _themeAmbienceB : _themeAmbienceA;
            next.clip = _themeClips[mood];
            next.Play();
            _themeOnA = !_themeOnA;
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

        public void PlayPowerTick(bool gained)
        {
            _oneShot.pitch = 1f;
            _oneShot.PlayOneShot(gained ? _pickupClip : _buttonClip, gained ? 0.14f : 0.1f);
        }

        public void PlayNarrativeCue()
        {
            _oneShot.pitch = 1f;
            _oneShot.PlayOneShot(_narrativeCueClip, 0.72f);
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

        public void PlayVictory()
        {
            _oneShot.pitch = 1f;
            _oneShot.PlayOneShot(_victoryClip, 0.72f);
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
            float audioDelta = Mathf.Max(Time.unscaledDeltaTime, Time.deltaTime);
            _titleMusic.volume = Mathf.MoveTowards(_titleMusic.volume, _titleMode ? 0.46f : 0f,
                audioDelta * 0.2f);
            float doorReveal = Mathf.Lerp(0.08f, 1f, 1f - _doorSeal);
            float themeVolume = _titleMode ? 0f : (_travelling ? 0.025f : 0.28f + _mood * 0.12f) * doorReveal;
            _themeAmbienceA.volume = Mathf.MoveTowards(_themeAmbienceA.volume,
                _themeOnA ? themeVolume : 0f, audioDelta * 0.24f);
            _themeAmbienceB.volume = Mathf.MoveTowards(_themeAmbienceB.volume,
                _themeOnA ? 0f : themeVolume, audioDelta * 0.24f);
            _ambience.volume = _titleMode ? 0.07f :
                ((_travelling ? 0.1f : 0.19f) + _mood * (_travelling ? 0.04f : 0.09f));
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
                _nextRailTime = Time.time + RandomRange(2.4f, 5.8f);
                _oneShot.pitch = RandomRange(0.92f, 1.03f);
                _oneShot.PlayOneShot(RandomClip(_railVariants), 0.18f);
            }

            if (ShouldPlayFootstep(_travelling, _player.HasMovementInput, _player.MovementAmount) &&
                Time.time >= _nextStepTime)
            {
                float pace = _player.IsSprinting ? 0.29f : 0.48f;
                _nextStepTime = Time.time + pace;
                _oneShot.pitch = RandomRange(0.88f, 1.08f);
                AudioClip step = _surfaceIndex == 1 ? RandomClip(_metalStepVariants) :
                    _surfaceIndex == 2 ? RandomClip(_wetStepVariants) : RandomClip(_stepVariants);
                _oneShot.PlayOneShot(step, _player.IsSprinting ? 0.48f : 0.27f);
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

        private static bool ShouldPlayFootstep(bool travelling, bool hasMovementInput,
            float movementAmount)
        {
            return !travelling && hasMovementInput && movementAmount > 0.2f;
        }

#if UNITY_EDITOR
        public bool VerifyFootstepGate()
        {
            return ShouldPlayFootstep(false, true, 1f) &&
                !ShouldPlayFootstep(true, true, 1f) &&
                !ShouldPlayFootstep(false, false, 1f) &&
                !ShouldPlayFootstep(false, true, 0.1f);
        }
#endif

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

        private static AudioClip CreateTitleAmbience()
        {
            const int rate = 22050;
            const int seconds = 12;
            int frames = rate * seconds;
            float[] samples = new float[frames * 2];
            System.Random random = new System.Random(2199);
            float filteredLeft = 0f;
            float filteredRight = 0f;
            for (int i = 0; i < frames; i++)
            {
                float time = i / (float)rate;
                filteredLeft = Mathf.Lerp(filteredLeft,
                    (float)(random.NextDouble() * 2.0 - 1.0), 0.0025f);
                filteredRight = Mathf.Lerp(filteredRight,
                    (float)(random.NextDouble() * 2.0 - 1.0), 0.0025f);
                float breathingRoom = 0.76f + Mathf.Sin(time * Mathf.PI * 2f / seconds) * 0.14f;
                float drone = Mathf.Sin(time * Mathf.PI * 2f * 27.5f) * 0.16f;
                drone += Mathf.Sin(time * Mathf.PI * 2f * 41.25f) * 0.07f;
                float beatTime = Mathf.Repeat(time, 6f);
                float bellProgress = Mathf.InverseLerp(2.2f, 3.8f, beatTime);
                float bellEnvelope = beatTime >= 2.2f && beatTime <= 3.8f
                    ? Mathf.Sin(bellProgress * Mathf.PI) * (1f - bellProgress) : 0f;
                float bell = (Mathf.Sin(time * Mathf.PI * 2f * 82.5f) * 0.11f +
                    Mathf.Sin(time * Mathf.PI * 2f * 165f) * 0.035f) * bellEnvelope;
                float left = (drone * breathingRoom + bell + filteredLeft * 0.2f) * 0.62f;
                float right = (drone * breathingRoom + bell * 0.78f + filteredRight * 0.2f) * 0.62f;
                samples[i * 2] = Mathf.Clamp(left, -1f, 1f);
                samples[i * 2 + 1] = Mathf.Clamp(right, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("Midnight Tower Title", frames, 2, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateThemeAmbience(EvacuationTheme theme)
        {
            const int rate = 22050;
            const int seconds = 10;
            int frames = rate * seconds;
            float[] samples = new float[frames * 2];
            System.Random random = new System.Random(4100 + (int)theme * 977);
            float filteredLeft = 0f;
            float filteredRight = 0f;
            float baseFrequency;
            float secondFrequency;
            float noiseAmount;
            switch (theme)
            {
                case EvacuationTheme.Hospital:
                    baseFrequency = 50f;
                    secondFrequency = 100f;
                    noiseAmount = 0.13f;
                    break;
                case EvacuationTheme.Office:
                    baseFrequency = 60f;
                    secondFrequency = 120f;
                    noiseAmount = 0.1f;
                    break;
                case EvacuationTheme.Apartment:
                    baseFrequency = 42f;
                    secondFrequency = 63f;
                    noiseAmount = 0.08f;
                    break;
                case EvacuationTheme.Maintenance:
                    baseFrequency = 36f;
                    secondFrequency = 72f;
                    noiseAmount = 0.17f;
                    break;
                case EvacuationTheme.Flooded:
                    baseFrequency = 31f;
                    secondFrequency = 49f;
                    noiseAmount = 0.2f;
                    break;
                default:
                    baseFrequency = 27f;
                    secondFrequency = 29f;
                    noiseAmount = 0.19f;
                    break;
            }

            for (int i = 0; i < frames; i++)
            {
                float time = i / (float)rate;
                filteredLeft = Mathf.Lerp(filteredLeft,
                    (float)(random.NextDouble() * 2.0 - 1.0), 0.004f);
                filteredRight = Mathf.Lerp(filteredRight,
                    (float)(random.NextDouble() * 2.0 - 1.0), 0.004f);
                float hum = Mathf.Sin(time * Mathf.PI * 2f * baseFrequency) * 0.09f;
                hum += Mathf.Sin(time * Mathf.PI * 2f * secondFrequency) * 0.04f;
                float detailLeft = 0f;
                float detailRight = 0f;
                if (theme == EvacuationTheme.Hospital)
                {
                    float flicker = Mathf.Pow(Mathf.Max(0f,
                        Mathf.Sin(time * Mathf.PI * 2f * 0.4f)), 10f);
                    detailLeft = Mathf.Sin(time * Mathf.PI * 2f * 196f) * flicker * 0.07f;
                    detailRight = detailLeft * 0.72f;
                }
                else if (theme == EvacuationTheme.Office)
                {
                    float serverCycle = 0.75f + Mathf.Sin(time * Mathf.PI * 2f * 0.2f) * 0.2f;
                    detailLeft = Mathf.Sin(time * Mathf.PI * 2f * 180f) * serverCycle * 0.035f;
                    detailRight = Mathf.Sin(time * Mathf.PI * 2f * 181f) * serverCycle * 0.035f;
                }
                else if (theme == EvacuationTheme.Apartment)
                {
                    float pipeCycle = 0.5f + Mathf.Sin(time * Mathf.PI * 2f * 0.1f) * 0.5f;
                    detailLeft = Mathf.Sin(time * Mathf.PI * 2f * 84f) * pipeCycle * 0.055f;
                    detailRight = Mathf.Sin(time * Mathf.PI * 2f * 63f) * pipeCycle * 0.04f;
                }
                else if (theme == EvacuationTheme.Maintenance)
                {
                    float strike = Mathf.Pow(Mathf.Max(0f,
                        Mathf.Sin(time * Mathf.PI * 2f * 0.2f)), 22f);
                    detailLeft = Mathf.Sin(time * Mathf.PI * 2f * 311f) * strike * 0.14f;
                    detailRight = Mathf.Sin(time * Mathf.PI * 2f * 233f) * strike * 0.09f;
                }
                else if (theme == EvacuationTheme.Flooded)
                {
                    float dripLeft = Mathf.Pow(Mathf.Max(0f,
                        Mathf.Sin(time * Mathf.PI * 2f * 0.7f)), 42f);
                    float dripRight = Mathf.Pow(Mathf.Max(0f,
                        Mathf.Sin((time + 0.43f) * Mathf.PI * 2f * 0.5f)), 46f);
                    detailLeft = Mathf.Sin(time * Mathf.PI * 2f * 930f) * dripLeft * 0.15f;
                    detailRight = Mathf.Sin(time * Mathf.PI * 2f * 760f) * dripRight * 0.13f;
                }
                else
                {
                    float breath = 0.4f + Mathf.Sin(time * Mathf.PI * 2f * 0.1f) * 0.4f;
                    detailLeft = filteredLeft * breath * 0.16f;
                    detailRight = -filteredRight * breath * 0.16f;
                }
                float left = hum + filteredLeft * noiseAmount + detailLeft;
                float right = hum * 0.92f + filteredRight * noiseAmount + detailRight;
                samples[i * 2] = Mathf.Clamp(left * 0.72f, -1f, 1f);
                samples[i * 2 + 1] = Mathf.Clamp(right * 0.72f, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create(theme + " Floor Ambience", frames, 2, rate, false);
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

        private static AudioClip CreateSoftFootstep(string clipName, float duration, float tone,
            float brightness, float volume, int seed)
        {
            const int rate = 22050;
            int length = Mathf.RoundToInt(rate * duration);
            float[] samples = new float[length];
            System.Random random = new System.Random(seed);
            float filtered = 0f;
            for (int i = 0; i < length; i++)
            {
                float time = i / (float)rate;
                float progress = time / duration;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                filtered = Mathf.Lerp(filtered, noise, brightness);
                float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.08f));
                float envelope = attack * Mathf.Exp(-progress * 5.2f) * (1f - progress);
                float body = Mathf.Sin(time * Mathf.PI * 2f * tone) * 0.42f;
                samples[i] = (body + filtered * 0.34f) * envelope * volume;
            }
            AudioClip clip = AudioClip.Create(clipName, length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateCableCreak(string clipName, float duration, float from,
            float to, int seed)
        {
            const int rate = 22050;
            int length = Mathf.RoundToInt(rate * duration);
            float[] samples = new float[length];
            System.Random random = new System.Random(seed);
            float filtered = 0f;
            for (int i = 0; i < length; i++)
            {
                float time = i / (float)rate;
                float progress = time / duration;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                filtered = Mathf.Lerp(filtered, noise, 0.009f);
                float frequency = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, progress));
                float envelope = Mathf.Pow(Mathf.Sin(progress * Mathf.PI), 1.6f);
                float steel = Mathf.Sin(time * Mathf.PI * 2f * frequency) * 0.22f;
                steel += Mathf.Sin(time * Mathf.PI * 2f * frequency * 2.03f) * 0.055f;
                samples[i] = (steel + filtered * 0.12f) * envelope;
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
