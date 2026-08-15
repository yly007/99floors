using System.Collections.Generic;
using UnityEngine;

namespace NinetyNine
{
    public enum NoiseKind
    {
        Footstep,
        Sprint,
        Breathing,
        Door,
        Pickup,
        Flashlight,
        Machinery
    }

    public struct NoiseSignal
    {
        public Vector3 Position;
        public float Loudness;
        public NoiseKind Kind;
        public float CreatedAt;
        public int Sequence;
    }

    public static class EvacuationSignals
    {
        private const int Capacity = 32;
        private static readonly List<NoiseSignal> Signals = new List<NoiseSignal>(Capacity);
        private static int _sequence;

        public static void Emit(Vector3 position, float loudness, NoiseKind kind)
        {
            if (loudness <= 0f)
            {
                return;
            }
            if (Signals.Count >= Capacity)
            {
                Signals.RemoveAt(0);
            }
            Signals.Add(new NoiseSignal
            {
                Position = position,
                Loudness = loudness,
                Kind = kind,
                CreatedAt = Time.time,
                Sequence = ++_sequence
            });
        }

        public static bool TryHear(Vector3 listener, float hearingMultiplier, int afterSequence,
            out NoiseSignal result)
        {
            result = default(NoiseSignal);
            float bestScore = 0f;
            for (int i = Signals.Count - 1; i >= 0; i--)
            {
                NoiseSignal signal = Signals[i];
                if (signal.Sequence <= afterSequence || Time.time - signal.CreatedAt > 2.2f)
                {
                    continue;
                }
                float distance = Vector3.Distance(listener, signal.Position);
                float score = signal.Loudness * Mathf.Max(0.1f, hearingMultiplier) - distance;
                if (score > bestScore)
                {
                    bestScore = score;
                    result = signal;
                }
            }
            return bestScore > 0f;
        }

        public static void Clear()
        {
            Signals.Clear();
        }
    }
}
