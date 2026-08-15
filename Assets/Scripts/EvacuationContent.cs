using System;
using System.Collections.Generic;
using UnityEngine;

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
        PassengerMismatch,
        DuplicateElevator,
        ReverseWayfinding,
        EmptyMeeting
    }

    public enum FloorPressure
    {
        Recovery,
        Uneasy,
        Threat,
        Chase,
        Anomaly
    }

    public static class EvacuationFloorEventUtility
    {
        public static bool IsPureAnomaly(FloorEventKind kind)
        {
            return kind == FloorEventKind.DuplicateElevator ||
                kind == FloorEventKind.ReverseWayfinding ||
                kind == FloorEventKind.EmptyMeeting;
        }
    }

    public enum FloorLayoutKind
    {
        LongSpine,
        CentralHub,
        CorridorLoop,
        ApartmentSuites,
        ServiceMaze
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
        public FloorLayoutKind Layout;
        public int Length;
        public bool Blackout;
        public bool Distorted;
        public bool SpawnMonster;
        public bool SpawnNpc;
        public bool SpawnEvidence;
        public bool IsStartingFloor;
        public bool IsExitFloor;
    }

    public static class EvacuationLayoutUtility
    {
        public static void Build(FloorLayoutKind layout, int length, System.Random random,
            List<Vector2Int> mainPath, HashSet<Vector2Int> cells)
        {
            mainPath.Clear();
            cells.Clear();
            Vector2Int cursor = Vector2Int.zero;
            AddPathCell(cursor, mainPath, cells);
            int side = random.NextDouble() < 0.5 ? -1 : 1;

            if (layout == FloorLayoutKind.ServiceMaze)
            {
                for (int i = 1; i < length; i++)
                {
                    int phase = i % 4;
                    Vector2Int direction = phase == 1 || phase == 3
                        ? Vector2Int.up
                        : phase == 2 ? new Vector2Int(side, 0) : new Vector2Int(-side, 0);
                    cursor += direction;
                    AddPathCell(cursor, mainPath, cells);
                }
                return;
            }

            for (int i = 1; i < length; i++)
            {
                if (layout == FloorLayoutKind.LongSpine && i % 3 == 0 &&
                    random.NextDouble() < 0.66)
                {
                    Vector2Int next = cursor + new Vector2Int(
                        random.NextDouble() < 0.5 ? -1 : 1, 0);
                    cursor = cells.Contains(next) ? cursor + Vector2Int.up : next;
                }
                else
                {
                    cursor += Vector2Int.up;
                }
                AddPathCell(cursor, mainPath, cells);
            }

            if (layout == FloorLayoutKind.CentralHub)
            {
                int hub = Mathf.Clamp(length / 2, 2, length - 2);
                Vector2Int center = mainPath[hub];
                for (int offset = -2; offset <= 2; offset++)
                {
                    cells.Add(center + new Vector2Int(offset, 0));
                }
                cells.Add(center + Vector2Int.up + Vector2Int.left);
                cells.Add(center + Vector2Int.up + Vector2Int.right);
            }
            else if (layout == FloorLayoutKind.CorridorLoop)
            {
                int start = 2;
                int end = Mathf.Max(start + 1, length - 3);
                for (int i = start; i <= end; i++)
                {
                    Vector2Int pathCell = mainPath[i];
                    cells.Add(pathCell + new Vector2Int(side, 0));
                    cells.Add(pathCell + new Vector2Int(side * 2, 0));
                }
            }
            else if (layout == FloorLayoutKind.ApartmentSuites)
            {
                for (int i = 2; i < mainPath.Count - 1; i += 2)
                {
                    int roomSide = ((i / 2) & 1) == 0 ? side : -side;
                    Vector2Int entry = mainPath[i] + new Vector2Int(roomSide, 0);
                    cells.Add(entry);
                    cells.Add(entry + new Vector2Int(roomSide, 0));
                    if (i + 1 < mainPath.Count)
                    {
                        cells.Add(entry + Vector2Int.up);
                    }
                }
            }
        }

        private static void AddPathCell(Vector2Int cell, List<Vector2Int> mainPath,
            HashSet<Vector2Int> cells)
        {
            mainPath.Add(cell);
            cells.Add(cell);
        }
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
