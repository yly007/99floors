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
        ExitTerminal
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
        private Collider _collider;
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
            bool mimic, int destinationFloor)
        {
            _game = game;
            _player = player;
            IsMimic = mimic;
            DestinationFloor = destinationFloor;
            Archetype = (NpcArchetype)Mathf.Abs(destinationFloor % 6);
            Trust = mimic ? -1 : 1;
            Fear = 2;
            MimicTimeRemaining = 0f;
            _collider = GetComponent<Collider>();
            CharacterController playerController = player.GetComponent<CharacterController>();
            if (_collider != null && playerController != null)
            {
                Physics.IgnoreCollision(_collider, playerController, true);
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
                Trust++;
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
            Trust++;
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
            if (_collider != null)
            {
                _collider.enabled = false;
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
                transform.position += direction.normalized * 1.85f * Time.deltaTime;
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(direction.normalized), Time.deltaTime * 6f);
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
}
