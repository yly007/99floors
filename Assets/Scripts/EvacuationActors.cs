using System.Collections.Generic;
using UnityEngine;

namespace NinetyNine
{
    public enum EvacuationAction
    {
        Descend,
        Stop,
        Door,
        BatterySlot,
        FusePanel,
        Item,
        Npc,
        Hide,
        Evidence,
        RingingPhone,
        ExitTerminal,
        PowerExchange,
        ElevatorParasite
    }

    public enum EvacuationItemKind
    {
        PowerCell,
        EmergencyCell,
        Medkit,
        Stimulant,
        Flashlight,
        FlashBattery,
        Fuse,
        Scrap
    }

    public sealed class EvacuationInteractable : MonoBehaviour
    {
        public EvacuationAction Action { get; private set; }
        public EvacuationItemKind ItemKind { get; private set; }
        public string Label { get; private set; }
        public EvacuationNpc Npc { get; private set; }
        public EvacuationHidingSpot HidingSpot { get; private set; }
        public string EvidenceId { get; private set; }

        public void Configure(EvacuationAction action, string label,
            EvacuationItemKind itemKind = EvacuationItemKind.PowerCell, EvacuationNpc npc = null,
            EvacuationHidingSpot hidingSpot = null, string evidenceId = null)
        {
            Action = action;
            Label = label;
            ItemKind = itemKind;
            Npc = npc;
            HidingSpot = hidingSpot;
            EvidenceId = evidenceId;
        }
    }

    public sealed class EvacuationNpc : MonoBehaviour
    {
        private NinetyNineEvacuationGame _game;
        private FirstPersonController _player;
        private CharacterController _controller;
        private EvacuationNavigationGraph _navigation;
        private Transform _bodyVisual;
        private Transform _headVisual;
        private Vector3 _bodyBasePosition;
        private Vector3 _headBasePosition;
        private bool _following;
        private bool _questioned;

        public bool IsMimic { get; private set; }
        public bool IsOnboard { get; private set; }
        public int DestinationFloor { get; private set; }
        public NpcArchetype Archetype { get; private set; }
        public int Trust { get; private set; }
        public int Fear { get; private set; }
        public float MimicTimeRemaining { get; private set; }
        public string DisplayName => IsMimic ? "沉默的幸存者" : ArchetypeName(Archetype);

        public void Initialize(NinetyNineEvacuationGame game, FirstPersonController player,
            bool mimic, int destinationFloor, EvacuationNavigationGraph navigation = null)
        {
            _game = game;
            _player = player;
            IsMimic = mimic;
            DestinationFloor = destinationFloor;
            Archetype = (NpcArchetype)Mathf.Abs(destinationFloor % 6);
            Trust = mimic ? -1 : 1;
            Fear = 2;
            MimicTimeRemaining = 0f;
            _controller = GetComponent<CharacterController>();
            _navigation = navigation;
            _bodyVisual = transform.Find("Body");
            _headVisual = transform.Find("Head");
            if (_bodyVisual != null) _bodyBasePosition = _bodyVisual.localPosition;
            if (_headVisual != null) _headBasePosition = _headVisual.localPosition;
            CharacterController playerController = player.GetComponent<CharacterController>();
            if (_controller != null && playerController != null)
            {
                Physics.IgnoreCollision(_controller, playerController, true);
            }
        }

        public void BeginFollowing()
        {
            if (IsOnboard)
            {
                _game.TryExpelNpc(this);
                return;
            }
            _following = true;
            _game.ShowTransientMessage("幸存者：请带我去 " + DestinationFloor + " 层。", 2.2f);
        }

        public string Question(out string clueId)
        {
            bool firstQuestion = !_questioned;
            _questioned = true;
            if (firstQuestion && !IsMimic)
            {
                Trust = Mathf.Min(5, Trust + 1);
            }
            clueId = IsMimic ? "contradiction_" + DestinationFloor :
                "witness_" + ((int)Archetype) + "_" + Mathf.Abs(DestinationFloor % 3);
            if (IsMimic)
            {
                return "它先说自己从楼上来，停顿后又说从一楼上来。它没有眨眼。";
            }
            switch (Archetype)
            {
                case NpcArchetype.Medic: return "医生：这里的伤者不是死去，而是被楼层忘掉。";
                case NpcArchetype.Electrician: return "电工：电梯不是消耗电力，它在用电力记住楼层。";
                case NpcArchetype.Guard: return "保安：真正的一楼没有绿色出口灯。";
                case NpcArchetype.Archivist: return "档案员：收集六份互不重复的记录，才能关闭循环。";
                case NpcArchetype.Parent: return "家长：孩子的影子会比身体早一步进入电梯。";
                default: return "孩子只写下数字：99、1、99。";
            }
        }

        public bool Trade()
        {
            if (IsMimic || Trust < 0)
            {
                Fear++;
                return false;
            }
            Trust = Mathf.Min(5, Trust + 1);
            Fear = Mathf.Max(0, Fear - 1);
            return true;
        }

        public bool CanOfferAdministratorDeal => Archetype == NpcArchetype.Archivist &&
            (_questioned || IsMimic);

        public void SetOnboard(Transform cabin, Vector3 slot)
        {
            IsOnboard = true;
            _following = false;
            transform.SetParent(cabin, true);
            transform.localPosition = slot;
            transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            if (_controller != null)
            {
                _controller.enabled = false;
            }
        }

        public void ArmMimic(float duration)
        {
            if (IsMimic)
            {
                MimicTimeRemaining = Mathf.Max(0.1f, duration);
            }
        }

        public bool TickMimic(float deltaTime)
        {
            if (!IsMimic || !IsOnboard || MimicTimeRemaining <= 0f)
            {
                return false;
            }
            MimicTimeRemaining -= deltaTime;
            return MimicTimeRemaining <= 0f;
        }

        private void Update()
        {
            if (!_following || IsOnboard || _game == null || !_game.IsExploring)
            {
                return;
            }
            bool playerInside = _player.IsInsideElevator;
            Vector3 target = playerInside
                ? new Vector3(0f, transform.position.y, 1.75f)
                : _player.transform.position - _player.transform.forward * 1.8f;
            Vector3 direction = target - transform.position;
            direction.y = 0f;
            float stoppingDistance = playerInside ? 0.18f : 1.65f;
            if (direction.magnitude > stoppingDistance)
            {
                Vector3 waypoint;
                if (_navigation != null && _navigation.TryGetNextWaypoint(transform.position,
                    target, out waypoint))
                {
                    direction = waypoint - transform.position;
                    direction.y = 0f;
                }
                Vector3 movement = direction.normalized * 1.85f;
                if (_controller != null && _controller.enabled)
                {
                    _controller.SimpleMove(movement);
                }
                else
                {
                    transform.position += movement * Time.deltaTime;
                }
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(direction.normalized), Time.deltaTime * 6f);
                float gait = Mathf.Sin(Time.time * 8.5f) * 0.035f;
                if (_bodyVisual != null) _bodyVisual.localPosition = _bodyBasePosition + Vector3.up * gait;
                if (_headVisual != null) _headVisual.localPosition = _headBasePosition - Vector3.up * gait * 0.35f;
            }
            else
            {
                if (_bodyVisual != null) _bodyVisual.localPosition = Vector3.Lerp(
                    _bodyVisual.localPosition, _bodyBasePosition, Time.deltaTime * 8f);
                if (_headVisual != null) _headVisual.localPosition = Vector3.Lerp(
                    _headVisual.localPosition, _headBasePosition, Time.deltaTime * 8f);
            }
            if (playerInside && transform.position.z < 2.35f && Mathf.Abs(transform.position.x) < 1.35f)
            {
                _game.NpcBoarded(this);
            }
        }

        private static string ArchetypeName(NpcArchetype archetype)
        {
            switch (archetype)
            {
                case NpcArchetype.Medic: return "受伤的医生";
                case NpcArchetype.Electrician: return "电梯电工";
                case NpcArchetype.Guard: return "夜班保安";
                case NpcArchetype.Archivist: return "档案员";
                case NpcArchetype.Parent: return "寻找孩子的家长";
                default: return "沉默儿童";
            }
        }
    }

    public sealed class EvacuationNavigationGraph
    {
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down
        };

        private readonly Dictionary<Vector2Int, Vector3> _positions =
            new Dictionary<Vector2Int, Vector3>();
        private readonly Queue<Vector2Int> _frontier = new Queue<Vector2Int>();
        private readonly Dictionary<Vector2Int, Vector2Int> _previous =
            new Dictionary<Vector2Int, Vector2Int>();

        public EvacuationNavigationGraph(IEnumerable<Vector2Int> cells)
        {
            foreach (Vector2Int cell in cells)
            {
                _positions[cell] = new Vector3(cell.x * 3f, 0f, 4f + cell.y * 3f);
            }
        }

        public bool TryGetNextWaypoint(Vector3 from, Vector3 target, out Vector3 waypoint)
        {
            Vector2Int start = ClosestCell(from);
            Vector2Int goal = target.z < 2.7f && _positions.ContainsKey(Vector2Int.zero)
                ? Vector2Int.zero : ClosestCell(target);
            if (!_positions.ContainsKey(start) || !_positions.ContainsKey(goal))
            {
                waypoint = target;
                return false;
            }
            if (start == goal)
            {
                waypoint = target;
                return true;
            }

            _frontier.Clear();
            _previous.Clear();
            _frontier.Enqueue(start);
            _previous[start] = start;
            while (_frontier.Count > 0)
            {
                Vector2Int current = _frontier.Dequeue();
                if (current == goal) break;
                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector2Int next = current + Directions[i];
                    if (!_positions.ContainsKey(next) || _previous.ContainsKey(next)) continue;
                    _previous[next] = current;
                    _frontier.Enqueue(next);
                }
            }
            if (!_previous.ContainsKey(goal))
            {
                waypoint = target;
                return false;
            }

            Vector2Int step = goal;
            while (_previous[step] != start && _previous[step] != step)
            {
                step = _previous[step];
            }
            waypoint = _positions[step];
            waypoint.y = from.y;
            return true;
        }

        private Vector2Int ClosestCell(Vector3 position)
        {
            Vector2Int rounded = new Vector2Int(Mathf.RoundToInt(position.x / 3f),
                Mathf.RoundToInt((position.z - 4f) / 3f));
            if (_positions.ContainsKey(rounded)) return rounded;
            float closestDistance = float.MaxValue;
            Vector2Int closest = rounded;
            foreach (KeyValuePair<Vector2Int, Vector3> pair in _positions)
            {
                float distance = (pair.Value - position).sqrMagnitude;
                if (distance >= closestDistance) continue;
                closestDistance = distance;
                closest = pair.Key;
            }
            return closest;
        }
    }
}
