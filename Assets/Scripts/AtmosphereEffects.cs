using System;
using UnityEngine;

namespace NinetyNine
{
    [RequireComponent(typeof(Camera))]
    public sealed class AnalogPostEffect : MonoBehaviour
    {
        private Material _material;

        public NinetyNineGame Game { get; set; }
        public NinetyNineSurvivalGame SurvivalGame { get; set; }
        public NinetyNineEvacuationGame EvacuationGame { get; set; }

        private void OnEnable()
        {
            Shader shader = Shader.Find("Hidden/NinetyNine/AnalogHorror");
            if (shader != null)
            {
                _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (_material == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            float intensity;
            if (EvacuationGame != null)
            {
                intensity = EvacuationGame.Impairment * 0.85f;
            }
            else
            {
                float tension = SurvivalGame != null ? SurvivalGame.Tension : Game != null ? Game.Tension : 0f;
                intensity = 0.35f + tension * 0.65f;
            }
            _material.SetFloat("_Intensity", intensity);
            _material.SetFloat("_TimeSeed", Time.time * 0.37f);
            Graphics.Blit(source, destination, _material);
        }

        private void OnDisable()
        {
            if (_material != null)
            {
                DestroyImmediate(_material);
                _material = null;
            }
        }
    }

    public sealed class ProceduralAudio : MonoBehaviour
    {
        private AudioSource _droneSource;
        private AudioSource _oneShotSource;
        private AudioClip _drone;
        private AudioClip _arrival;
        private AudioClip _deepArrival;
        private AudioClip _correct;
        private AudioClip _wrong;
        private float _targetDroneVolume;
        private float _targetPitch = 1f;

        public void Initialize()
        {
            _droneSource = gameObject.AddComponent<AudioSource>();
            _droneSource.spatialBlend = 0f;
            _droneSource.loop = true;
            _droneSource.playOnAwake = false;
            _droneSource.volume = 0.13f;

            _oneShotSource = gameObject.AddComponent<AudioSource>();
            _oneShotSource.spatialBlend = 0f;
            _oneShotSource.playOnAwake = false;
            _oneShotSource.volume = 0.52f;

            _drone = CreateDrone();
            _arrival = CreateChime(false);
            _deepArrival = CreateChime(true);
            _correct = CreateDecision(true);
            _wrong = CreateDecision(false);
            _droneSource.clip = _drone;
            _droneSource.Play();
            _targetDroneVolume = 0.13f;
        }

        public void SetTravelling(bool travelling)
        {
            _targetDroneVolume = travelling ? 0.31f : 0.13f;
            _targetPitch = travelling ? 0.78f : 1f;
        }

        public void PlayArrival(bool finalFloor)
        {
            _oneShotSource.PlayOneShot(finalFloor ? _deepArrival : _arrival, finalFloor ? 0.9f : 0.62f);
        }

        public void PlayDecision(bool correct)
        {
            _oneShotSource.PlayOneShot(correct ? _correct : _wrong, 0.48f);
        }

        private void Update()
        {
            if (_droneSource == null)
            {
                return;
            }
            _droneSource.volume = Mathf.MoveTowards(_droneSource.volume, _targetDroneVolume,
                Time.deltaTime * 0.16f);
            _droneSource.pitch = Mathf.MoveTowards(_droneSource.pitch, _targetPitch,
                Time.deltaTime * 0.12f);
        }

        private static AudioClip CreateDrone()
        {
            const int sampleRate = 44100;
            const int duration = 5;
            int count = sampleRate * duration;
            float[] samples = new float[count];
            System.Random random = new System.Random(9901);
            float filteredNoise = 0f;

            for (int i = 0; i < count; i++)
            {
                float time = i / (float)sampleRate;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                filteredNoise = Mathf.Lerp(filteredNoise, noise, 0.0025f);
                float hum = Mathf.Sin(time * Mathf.PI * 2f * 43f) * 0.18f;
                hum += Mathf.Sin(time * Mathf.PI * 2f * 86f + 0.7f) * 0.07f;
                hum += Mathf.Sin(time * Mathf.PI * 2f * 17f) * 0.035f;
                samples[i] = Mathf.Clamp((hum + filteredNoise * 0.22f) * 0.72f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Procedural Elevator Drone", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateChime(bool deep)
        {
            const int sampleRate = 44100;
            float duration = deep ? 2.8f : 1.45f;
            int count = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[count];
            float first = deep ? 164.81f : 659.25f;
            float second = deep ? 98f : 783.99f;

            for (int i = 0; i < count; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * (deep ? 1.3f : 3.1f));
                float tone = Mathf.Sin(time * Mathf.PI * 2f * first);
                tone += Mathf.Sin(time * Mathf.PI * 2f * second) * (deep ? 0.55f : 0.38f);
                tone += Mathf.Sin(time * Mathf.PI * 2f * first * 2.01f) * 0.12f;
                samples[i] = tone * envelope * (deep ? 0.34f : 0.24f);
            }

            AudioClip clip = AudioClip.Create(deep ? "Floor 99 Chime" : "Elevator Chime", count, 1,
                sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateDecision(bool positive)
        {
            const int sampleRate = 44100;
            const float duration = 0.34f;
            int count = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[count];

            for (int i = 0; i < count; i++)
            {
                float time = i / (float)sampleRate;
                float progress = time / duration;
                float frequency = positive
                    ? Mathf.Lerp(360f, 620f, progress)
                    : Mathf.Lerp(180f, 72f, progress);
                float envelope = Mathf.Sin(progress * Mathf.PI) * (1f - progress);
                samples[i] = Mathf.Sin(time * Mathf.PI * 2f * frequency) * envelope * 0.32f;
            }

            AudioClip clip = AudioClip.Create(positive ? "Decision Registered" : "Decision Distortion",
                count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
