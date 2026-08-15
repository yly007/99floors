using System.Collections.Generic;

namespace NinetyNine
{
    public enum ExitResolution
    {
        FalseLoop,
        EscapedAlone,
        ShutDownBuilding,
        NewAdministrator
    }

    public sealed class EvacuationStorySystem
    {
        private readonly HashSet<string> _clues = new HashSet<string>();

        public int ClueCount => _clues.Count;

        public bool Discover(string clueId)
        {
            return !string.IsNullOrEmpty(clueId) && _clues.Add(clueId);
        }

        public void Reset()
        {
            _clues.Clear();
        }

        public ExitResolution Resolve(bool carriesMimic, int rescued, bool acceptedAdministrator)
        {
            if (acceptedAdministrator) return ExitResolution.NewAdministrator;
            if (_clues.Count < 3) return ExitResolution.FalseLoop;
            if (_clues.Count >= 6 && rescued >= 1 && !carriesMimic)
            {
                return ExitResolution.ShutDownBuilding;
            }
            return ExitResolution.EscapedAlone;
        }
    }
}
