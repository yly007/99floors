using UnityEngine;

namespace NinetyNine
{
    public enum MonsterAwarenessState
    {
        Dormant,
        Patrol,
        Suspicious,
        Chase,
        Search,
        Return
    }

    [RequireComponent(typeof(CharacterController))]
    public sealed class EvacuationMonster : MonoBehaviour
    {
        private NinetyNineEvacuationGame _game;
        private FirstPersonController _player;
        private EvacuationAudio _audio;
        private CharacterController _controller;
        private MonsterArchetype _archetype;
        private MonsterAwarenessState _state;
        private Vector3 _home;
        private Vector3 _lastKnownPosition;
        private Vector3 _patrolTarget;
        private float _wakeTime;
        private float _pauseUntil;
        private float _nextAttackTime;
        private float _stateUntil;
        private float _nextPatrolChange;
        private int _lastNoiseSequence;
        private bool _territoryViolated;

        public MonsterAwarenessState State => _state;
        public MonsterArchetype Archetype => _archetype;

        public void Initialize(NinetyNineEvacuationGame game, FirstPersonController player,
            EvacuationAudio audio, float delay, MonsterArchetype archetype)
        {
            _game = game;
            _player = player;
            _audio = audio;
            _controller = GetComponent<CharacterController>();
            _archetype = archetype;
            _home = transform.position;
            _patrolTarget = _home;
            _wakeTime = Time.time + delay;
            _state = MonsterAwarenessState.Dormant;
            _audio.AttachMonsterSource(gameObject);
        }

        public void TriggerChase()
        {
            _lastKnownPosition = _player != null ? _player.transform.position : transform.position;
            SetState(MonsterAwarenessState.Chase, 0f);
        }

        public void NotifyTheft(Vector3 position)
        {
            _territoryViolated = true;
            _lastKnownPosition = position;
            SetState(MonsterAwarenessState.Suspicious, 2.1f);
        }

        private void Update()
        {
            if (_game == null || _player == null || !_game.IsExploring)
            {
                return;
            }

            float distance = Vector3.Distance(transform.position, _player.transform.position);
            bool seesPlayer = CanSeePlayer(distance);
            NoiseSignal heard;
            bool heardPlayer = EvacuationSignals.TryHear(transform.position, HearingMultiplier(),
                _lastNoiseSequence, out heard);
            if (heardPlayer)
            {
                _lastNoiseSequence = heard.Sequence;
                _lastKnownPosition = heard.Position;
            }

            if (_state == MonsterAwarenessState.Dormant && Time.time >= _wakeTime)
            {
                SetState(MonsterAwarenessState.Patrol, 0f);
            }
            if (seesPlayer && CanBecomeHostile())
            {
                _lastKnownPosition = _player.transform.position;
                if (_state != MonsterAwarenessState.Chase) TriggerChase();
            }
            else if (heardPlayer && CanBecomeHostile())
            {
                if (_state != MonsterAwarenessState.Chase)
                {
                    SetState(MonsterAwarenessState.Suspicious, 2.4f);
                }
            }

            _audio.SetMonsterUrgency(gameObject, _state == MonsterAwarenessState.Chase
                ? Mathf.InverseLerp(18f, 1.2f, distance) : 0f);
            if (Time.time < _pauseUntil)
            {
                return;
            }

            switch (_state)
            {
                case MonsterAwarenessState.Patrol:
                    UpdatePatrol();
                    break;
                case MonsterAwarenessState.Suspicious:
                    MoveTowards(_lastKnownPosition, 1.45f);
                    if (Time.time >= _stateUntil) SetState(MonsterAwarenessState.Search, SearchDuration());
                    break;
                case MonsterAwarenessState.Chase:
                    UpdateChase(seesPlayer, distance);
                    break;
                case MonsterAwarenessState.Search:
                    UpdateSearch();
                    break;
                case MonsterAwarenessState.Return:
                    MoveTowards(_home, 1.2f);
                    if (Vector3.Distance(transform.position, _home) < 0.8f)
                    {
                        SetState(MonsterAwarenessState.Patrol, 0f);
                    }
                    break;
            }
        }

        private void UpdateChase(bool seesPlayer, float distance)
        {
            if (seesPlayer)
            {
                _lastKnownPosition = _player.transform.position;
                _stateUntil = Time.time + LostSightGrace();
            }
            else if (Time.time >= _stateUntil)
            {
                SetState(MonsterAwarenessState.Search, SearchDuration());
                return;
            }

            if (_archetype == MonsterArchetype.Watcher && IsObserved())
            {
                return;
            }
            MoveTowards(_lastKnownPosition, ChaseSpeed());

            if (_player.IsInsideElevator && _game.DoorSeal > 0.02f && transform.position.z < 2.9f)
            {
                _game.RepelMonster(this);
                return;
            }
            if (_player.IsInsideElevator && transform.position.z < 2.45f)
            {
                _game.MonsterEnteredElevator();
                return;
            }
            if (distance < 1.25f && Time.time >= _nextAttackTime)
            {
                _nextAttackTime = Time.time + 4f;
                _pauseUntil = Time.time + 2.6f;
                _game.MonsterAttack(transform.position);
            }
        }

        private void UpdatePatrol()
        {
            if (Time.time >= _nextPatrolChange || Vector3.Distance(transform.position, _patrolTarget) < 0.7f)
            {
                Vector2 offset = Random.insideUnitCircle * 4f;
                _patrolTarget = _home + new Vector3(offset.x, 0f, offset.y);
                _nextPatrolChange = Time.time + Random.Range(2f, 5f);
            }
            MoveTowards(_patrolTarget, _archetype == MonsterArchetype.CeilingChild ? 0.8f : 1.05f);
        }

        private void UpdateSearch()
        {
            if (Time.time >= _stateUntil)
            {
                SetState(MonsterAwarenessState.Return, 0f);
                return;
            }
            if (Vector3.Distance(transform.position, _lastKnownPosition) < 0.8f)
            {
                Vector2 offset = Random.insideUnitCircle * 2.6f;
                _lastKnownPosition = transform.position + new Vector3(offset.x, 0f, offset.y);
            }
            MoveTowards(_lastKnownPosition, 1.25f);
        }

        private void MoveTowards(Vector3 target, float speed)
        {
            Vector3 direction = target - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.03f)
            {
                return;
            }
            direction.Normalize();
            Vector3 eye = transform.position + Vector3.up;
            if (Physics.Raycast(eye, direction, 0.8f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
            {
                Vector3 left = Quaternion.Euler(0f, -70f, 0f) * direction;
                Vector3 right = Quaternion.Euler(0f, 70f, 0f) * direction;
                direction = !Physics.Raycast(eye, left, 0.75f) ? left : right;
            }
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(direction), Time.deltaTime * 7f);
            _controller.SimpleMove(direction * speed);
        }

        private bool CanSeePlayer(float distance)
        {
            float sightDistance = SightDistance();
            if (_player.IsCrouching)
            {
                sightDistance *= 0.58f;
            }
            if (!_game.IsFlashlightOn)
            {
                sightDistance *= 0.82f;
            }
            if (_player.IsHidden || distance > sightDistance)
            {
                return false;
            }
            if (_archetype == MonsterArchetype.CeilingChild && !_game.IsFlashlightOn &&
                !_player.IsSprinting)
            {
                return false;
            }
            Vector3 origin = transform.position + Vector3.up * 1.35f;
            Vector3 target = _player.transform.position + Vector3.up * 1.1f;
            Vector3 direction = target - origin;
            if (Vector3.Dot(transform.forward, direction.normalized) < 0.48f && distance > 2.1f)
            {
                return false;
            }
            RaycastHit hit;
            return Physics.Raycast(origin, direction.normalized, out hit, direction.magnitude + 0.2f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponentInParent<FirstPersonController>() == _player;
        }

        private bool IsObserved()
        {
            if (_player.ViewCamera == null) return false;
            Vector3 toMonster = transform.position + Vector3.up * 1.25f - _player.ViewCamera.transform.position;
            if (toMonster.sqrMagnitude > 324f ||
                Vector3.Dot(_player.ViewCamera.transform.forward, toMonster.normalized) < 0.72f)
            {
                return false;
            }
            RaycastHit hit;
            return Physics.Raycast(_player.ViewCamera.transform.position, toMonster.normalized, out hit, 18f) &&
                hit.transform.root == transform;
        }

        private bool CanBecomeHostile()
        {
            return _archetype != MonsterArchetype.Janitor || _territoryViolated ||
                Vector3.Distance(transform.position, _player.transform.position) < 2.2f;
        }

        private float ChaseSpeed()
        {
            switch (_archetype)
            {
                case MonsterArchetype.Watcher: return 3.1f;
                case MonsterArchetype.CeilingChild: return 2.85f;
                case MonsterArchetype.Janitor: return 2.35f;
                default: return 2.55f;
            }
        }

        private float HearingMultiplier()
        {
            return _archetype == MonsterArchetype.CeilingChild ? 1.45f :
                _archetype == MonsterArchetype.Janitor ? 0.85f : 1f;
        }

        private float SightDistance()
        {
            return _archetype == MonsterArchetype.Watcher ? 18f : 14f;
        }

        private float SearchDuration()
        {
            return _archetype == MonsterArchetype.Pursuer ? 6f : 4.5f;
        }

        private float LostSightGrace()
        {
            return _archetype == MonsterArchetype.Pursuer ? 4.5f : 3f;
        }

        private void SetState(MonsterAwarenessState next, float duration)
        {
            bool enteringChase = next == MonsterAwarenessState.Chase && _state != MonsterAwarenessState.Chase;
            _state = next;
            _stateUntil = duration > 0f
                ? Time.time + duration
                : Time.time + (next == MonsterAwarenessState.Chase ? LostSightGrace() : SearchDuration());
            if (enteringChase)
            {
                _pauseUntil = Time.time + 0.72f;
                _audio.PlayThreatCue(transform.position);
                _game.ShowTransientMessage("它发现了你。甩掉视线，别发出声音。", 1.8f);
            }
        }
    }
}
