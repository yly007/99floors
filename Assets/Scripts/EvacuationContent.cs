using System;

namespace NinetyNine
{
    public enum MonsterArchetype
    {
        Pursuer,
        Watcher,
        Janitor,
        CeilingChild
    }

    public enum FloorEventKind
    {
        None,
        Blackout,
        SequentialBlackout,
        RisingWater,
        WrongFloorNumber,
        MirroredCorridor,
        ShiftingRooms,
        DistantFootsteps,
        BaitCache,
        LockdownPickup,
        ChasedSurvivor,
        SurvivorCamp,
        PowerExchange,
        RingingPhone,
        UnsyncedShadow,
        MovingDarkness,
        SilentCache,
        ElevatorParasite,
        TimeSlip,
        FalseLobby,
        PassengerMismatch
    }

    public enum FloorPressure
    {
        Recovery,
        Uneasy,
        Threat,
        Chase,
        Anomaly
    }

    [Serializable]
    public sealed class EvacuationFloorPlan
    {
        public int Seed;
        public int FloorNumber;
        public EvacuationTheme Theme;
        public FloorEventKind Event;
        public FloorPressure Pressure;
        public MonsterArchetype Monster;
        public int Length;
        public bool Blackout;
        public bool Distorted;
        public bool SpawnMonster;
        public bool SpawnNpc;
        public bool SpawnEvidence;
        public bool IsStartingFloor;
        public bool IsExitFloor;
    }

    public enum NpcArchetype
    {
        Medic,
        Electrician,
        Guard,
        Archivist,
        Parent,
        SilentChild
    }
}
