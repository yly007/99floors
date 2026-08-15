using UnityEngine;

namespace NinetyNine
{
    public sealed class EvacuationFloorAnomaly : MonoBehaviour
    {
        private FloorEventKind _kind;
        private Light[] _lights;
        private Transform _movingDarkness;
        private Transform _water;
        private Vector3 _basePosition;
        private float _startedAt;

        public void Configure(FloorEventKind kind)
        {
            _kind = kind;
            _startedAt = Time.time;
        }

        private void Start()
        {
            _lights = GetComponentsInChildren<Light>(true);
            _movingDarkness = FindChild("MovingDarkness");
            _water = FindChild("EventFlood");
            if (_movingDarkness != null) _basePosition = _movingDarkness.localPosition;
        }

        private void Update()
        {
            float elapsed = Time.time - _startedAt;
            if (_kind == FloorEventKind.SequentialBlackout && _lights != null)
            {
                int extinguished = Mathf.Clamp(Mathf.FloorToInt((elapsed - 3f) / 0.75f), 0, _lights.Length);
                for (int i = 0; i < _lights.Length; i++)
                {
                    _lights[i].enabled = i >= extinguished;
                }
            }
            if ((_kind == FloorEventKind.MovingDarkness || _kind == FloorEventKind.UnsyncedShadow) &&
                _movingDarkness != null)
            {
                _movingDarkness.localPosition = _basePosition + new Vector3(
                    Mathf.Sin(elapsed * 0.47f) * 1.2f,
                    Mathf.Sin(elapsed * 0.31f) * 0.18f,
                    Mathf.Cos(elapsed * 0.39f) * 1.7f);
            }
            if (_kind == FloorEventKind.RisingWater && _water != null)
            {
                Vector3 position = _water.localPosition;
                position.y = Mathf.Min(0.62f, 0.17f + elapsed * 0.012f);
                _water.localPosition = position;
            }
        }

        private Transform FindChild(string childName)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == childName) return children[i];
            }
            return null;
        }
    }
}
