using UnityEngine;

namespace NinetyNine
{
    public sealed class EvacuationFloorAnomaly : MonoBehaviour
    {
        private FloorEventKind _kind;
        private Light[] _lights;
        private Transform _movingDarkness;
        private Transform _water;
        private Transform _impossibleWall;
        private Vector3 _basePosition;
        private Vector3 _wallBasePosition;
        private float _startedAt;
        private bool _lootReaction;

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
            _impossibleWall = FindChild("ImpossibleWall");
            if (_movingDarkness != null) _basePosition = _movingDarkness.localPosition;
            if (_impossibleWall != null) _wallBasePosition = _impossibleWall.localPosition;
        }

        public void TriggerLootReaction()
        {
            _lootReaction = true;
            if (_kind == FloorEventKind.SequentialBlackout)
            {
                _startedAt = Mathf.Min(_startedAt, Time.time - 5f);
            }
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
                position.y = Mathf.Min(0.62f, 0.17f + elapsed * (_lootReaction ? 0.026f : 0.012f));
                _water.localPosition = position;
            }
            if ((_kind == FloorEventKind.ShiftingRooms || _kind == FloorEventKind.MirroredCorridor) &&
                _impossibleWall != null)
            {
                float shift = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(_lootReaction ? 0.3f : 5f, _lootReaction ? 2.2f : 10f, elapsed));
                float direction = _kind == FloorEventKind.MirroredCorridor ? -1f : 1f;
                _impossibleWall.localPosition = _wallBasePosition +
                    new Vector3(direction * shift * 1.65f, 0f, shift * 0.65f);
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
