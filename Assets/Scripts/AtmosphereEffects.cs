using UnityEngine;

namespace NinetyNine
{
    [RequireComponent(typeof(Camera))]
    public sealed class AnalogPostEffect : MonoBehaviour
    {
        public static float DisplayBrightness { get; set; } = 1f;

        private Material _material;

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

            float intensity = EvacuationGame != null ? EvacuationGame.Impairment * 0.85f : 0f;
            _material.SetFloat("_Intensity", intensity);
            _material.SetFloat("_TimeSeed", Time.time * 0.37f);
            _material.SetFloat("_Brightness", Mathf.Clamp(DisplayBrightness, 0.72f, 1.35f));
            Graphics.Blit(source, destination, _material);
        }

        private void OnDisable()
        {
            if (_material != null)
            {
                if (Application.isPlaying) Destroy(_material);
                else DestroyImmediate(_material);
                _material = null;
            }
        }
    }
}
