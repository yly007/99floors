using UnityEngine;

namespace NinetyNine
{
    public sealed class EvacuationFloorAnomaly : MonoBehaviour
    {
        private FloorEventKind _kind;
        private NinetyNineEvacuationGame _game;
        private FirstPersonController _player;
        private Light[] _lights;
        private Transform _movingDarkness;
        private Transform _water;
        private Transform _impossibleWall;
        private Transform _eventShutter;
        private Light _terminalBeacon;
        private Vector3 _basePosition;
        private Vector3 _wallBasePosition;
        private float _startedAt;
        private bool _lootReaction;

        public void Configure(FloorEventKind kind, NinetyNineEvacuationGame game,
            FirstPersonController player)
        {
            _kind = kind;
            _game = game;
            _player = player;
            _startedAt = Time.time;
        }

        private void Start()
        {
            _lights = GetComponentsInChildren<Light>(true);
            _movingDarkness = FindChild("MovingDarkness");
            _water = FindChild("EventFlood");
            _impossibleWall = FindChild("ImpossibleWall");
            _eventShutter = FindChild("EventShutter");
            Transform terminalBeacon = FindChild("ExitTerminalBeacon");
            if (terminalBeacon != null) _terminalBeacon = terminalBeacon.GetComponent<Light>();
            if (_movingDarkness != null) _basePosition = _movingDarkness.localPosition;
            if (_impossibleWall != null) _wallBasePosition = _impossibleWall.localPosition;
        }

        public void TriggerLootReaction()
        {
            _lootReaction = true;
            if (_kind == FloorEventKind.LockdownPickup && _game != null)
            {
                _game.ShowTransientMessage("警告灯转红。身后的防火闸正在落下——寻找侧路！", 2.6f);
            }
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
            if (_kind == FloorEventKind.MovingDarkness &&
                _movingDarkness != null)
            {
                _movingDarkness.localPosition = _basePosition + new Vector3(
                    Mathf.Sin(elapsed * 0.47f) * 1.2f,
                    Mathf.Sin(elapsed * 0.31f) * 0.18f,
                    Mathf.Cos(elapsed * 0.39f) * 1.7f);
            }
            if (_kind == FloorEventKind.UnsyncedShadow && _movingDarkness != null && _player != null)
            {
                Vector3 delayedTarget = _player.transform.position - _player.transform.forward * 3.2f;
                delayedTarget.y = _basePosition.y;
                _movingDarkness.position = Vector3.Lerp(_movingDarkness.position, delayedTarget,
                    Time.deltaTime * 0.38f);
            }
            if (_kind == FloorEventKind.RisingWater && _water != null)
            {
                Vector3 position = _water.localPosition;
                position.y = Mathf.Min(0.62f, 0.17f + elapsed * (_lootReaction ? 0.026f : 0.012f));
                _water.localPosition = position;
                if (_game != null)
                {
                    _game.SetFloorMovementPenalty(Mathf.Lerp(0.9f, 0.58f,
                        Mathf.InverseLerp(0.17f, 0.62f, position.y)));
                }
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
            if (_kind == FloorEventKind.LockdownPickup && _eventShutter != null)
            {
                Vector3 position = _eventShutter.localPosition;
                float targetY = _lootReaction ? 1.42f : 4.45f;
                position.y = Mathf.MoveTowards(position.y, targetY, Time.deltaTime * 2.35f);
                _eventShutter.localPosition = position;
            }
            if (_terminalBeacon != null)
            {
                _terminalBeacon.intensity = 1.8f + Mathf.PingPong(elapsed * 1.6f, 2.4f);
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
