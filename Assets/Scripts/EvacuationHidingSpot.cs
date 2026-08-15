using UnityEngine;

namespace NinetyNine
{
    public sealed class EvacuationHidingSpot : MonoBehaviour
    {
        private Transform _hidingPoint;
        private Vector3 _exitPosition;

        public void Configure(Vector3 hidingPosition, Quaternion hidingRotation, Vector3 exitPosition)
        {
            GameObject point = new GameObject("HidingPoint");
            point.transform.SetParent(transform.parent, true);
            point.transform.position = transform.position + hidingPosition;
            point.transform.rotation = hidingRotation;
            _hidingPoint = point.transform;
            _exitPosition = transform.position + exitPosition;
        }

        public void Enter(FirstPersonController player)
        {
            if (player != null && _hidingPoint != null)
            {
                player.EnterHidingSpot(_hidingPoint, _exitPosition);
            }
        }
    }
}
