using System.Collections.Generic;
using UnityEngine;

namespace NinetyNine
{
    public sealed class EvacuationFloorDirector
    {
        private readonly Queue<EvacuationTheme> _recentThemes = new Queue<EvacuationTheme>();
        private FloorPressure _lastPressure = FloorPressure.Recovery;

        public EvacuationFloorPlan CreatePlan(int runSeed, int floorNumber, float power, int visited)
        {
            int seed = unchecked(runSeed * 397) ^ floorNumber * 7919;
            System.Random random = new System.Random(seed);
            bool start = floorNumber == 99;
            bool exit = floorNumber == 1;
            EvacuationTheme theme = start ? EvacuationTheme.Office : PickTheme(random);
            float depth = Mathf.InverseLerp(99f, 1f, floorNumber);
            FloorPressure pressure = PickPressure(random, depth, start, exit);
            FloorEventKind floorEvent = start ? FloorEventKind.None :
                (FloorEventKind)random.Next(1, System.Enum.GetValues(typeof(FloorEventKind)).Length);
            MonsterArchetype monster = (MonsterArchetype)random.Next(0, 4);
            FloorLayoutKind layout = start ? FloorLayoutKind.LongSpine :
                exit ? FloorLayoutKind.CentralHub : (FloorLayoutKind)random.Next(0, 5);

            EvacuationFloorPlan result = new EvacuationFloorPlan
            {
                Seed = seed,
                FloorNumber = floorNumber,
                Theme = theme,
                Event = exit ? FloorEventKind.FalseLobby : floorEvent,
                Pressure = pressure,
                Monster = monster,
                Layout = layout,
                Length = start ? 4 : exit ? 8 : random.Next(11, 17),
                Blackout = !start && !exit && (floorEvent == FloorEventKind.Blackout ||
                    floorEvent == FloorEventKind.SequentialBlackout || random.NextDouble() < 0.14),
                Distorted = !start && (pressure == FloorPressure.Anomaly ||
                    floorEvent == FloorEventKind.MirroredCorridor || floorEvent == FloorEventKind.ShiftingRooms),
                SpawnMonster = !start && !exit && (pressure == FloorPressure.Threat ||
                    pressure == FloorPressure.Chase || random.NextDouble() < Mathf.Lerp(0.1f, 0.42f, depth)),
                SpawnNpc = !start && !exit && random.NextDouble() < 0.34,
                SpawnEvidence = !start && (visited % 3 == 2 || random.NextDouble() < 0.24),
                IsStartingFloor = start,
                IsExitFloor = exit
            };
            if (floorEvent == FloorEventKind.ChasedSurvivor)
            {
                result.SpawnNpc = true;
                result.SpawnMonster = false;
            }
            else if (floorEvent == FloorEventKind.SurvivorCamp ||
                floorEvent == FloorEventKind.PassengerMismatch)
            {
                result.SpawnNpc = true;
            }
            else if (floorEvent == FloorEventKind.BaitCache ||
                floorEvent == FloorEventKind.LockdownPickup)
            {
                result.SpawnMonster = true;
            }
            if (EvacuationFloorEventUtility.IsPureAnomaly(floorEvent))
            {
                result.SpawnMonster = false;
                result.SpawnNpc = false;
            }
            if (result.SpawnMonster)
            {
                result.SpawnNpc = false;
            }
            Remember(theme, pressure);
            return result;
        }

        private EvacuationTheme PickTheme(System.Random random)
        {
            EvacuationTheme value = (EvacuationTheme)random.Next(0, 6);
            for (int i = 0; i < 8 && _recentThemes.Contains(value); i++)
            {
                value = (EvacuationTheme)random.Next(0, 6);
            }
            return value;
        }

        private FloorPressure PickPressure(System.Random random, float depth, bool start, bool exit)
        {
            if (start || exit || _lastPressure == FloorPressure.Chase)
            {
                return FloorPressure.Recovery;
            }
            double roll = random.NextDouble();
            if (roll < 0.12 + depth * 0.12) return FloorPressure.Anomaly;
            if (roll < 0.3 + depth * 0.2) return FloorPressure.Chase;
            if (roll < 0.58) return FloorPressure.Threat;
            return roll < 0.84 ? FloorPressure.Uneasy : FloorPressure.Recovery;
        }

        private void Remember(EvacuationTheme theme, FloorPressure pressure)
        {
            _recentThemes.Enqueue(theme);
            while (_recentThemes.Count > 2) _recentThemes.Dequeue();
            _lastPressure = pressure;
        }
    }
}
