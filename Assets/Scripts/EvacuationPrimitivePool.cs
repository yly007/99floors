using System.Collections.Generic;
using UnityEngine;

namespace NinetyNine
{
    public sealed class EvacuationPooledPrimitive : MonoBehaviour
    {
        public PrimitiveType Type;
    }

    public sealed class EvacuationPrimitivePool
    {
        private readonly Dictionary<PrimitiveType, Stack<GameObject>> _available =
            new Dictionary<PrimitiveType, Stack<GameObject>>();
        private readonly Transform _poolRoot;

        public int TotalCreated { get; private set; }
        public int AvailableCount { get; private set; }

        public EvacuationPrimitivePool(Transform parent)
        {
            _poolRoot = new GameObject("Runtime Primitive Pool").transform;
            _poolRoot.SetParent(parent, false);
            _poolRoot.gameObject.SetActive(true);
        }

        public Transform Rent(PrimitiveType type, string objectName, Transform parent,
            Vector3 position, Vector3 scale, Quaternion rotation, Material material, bool useCollider)
        {
            Stack<GameObject> stack;
            if (!_available.TryGetValue(type, out stack))
            {
                stack = new Stack<GameObject>();
                _available[type] = stack;
            }
            GameObject result = null;
            while (stack.Count > 0 && result == null)
            {
                result = stack.Pop();
                AvailableCount--;
            }
            if (result == null)
            {
                result = GameObject.CreatePrimitive(type);
                EvacuationPooledPrimitive marker = result.AddComponent<EvacuationPooledPrimitive>();
                marker.Type = type;
                TotalCreated++;
            }
            result.name = objectName;
            result.transform.SetParent(parent, false);
            result.transform.localPosition = position;
            result.transform.localRotation = rotation;
            result.transform.localScale = scale;
            Renderer renderer = result.GetComponent<Renderer>();
            renderer.enabled = true;
            renderer.sharedMaterial = material;
            Collider collider = result.GetComponent<Collider>();
            if (collider != null) collider.enabled = useCollider;
            result.SetActive(true);
            return result.transform;
        }

        public void ReleaseHierarchy(Transform hierarchy)
        {
            if (hierarchy == null) return;
            EvacuationPooledPrimitive[] primitives =
                hierarchy.GetComponentsInChildren<EvacuationPooledPrimitive>(true);
            for (int i = 0; i < primitives.Length; i++)
            {
                EvacuationPooledPrimitive marker = primitives[i];
                if (marker == null) continue;
                GameObject value = marker.gameObject;
                Collider collider = value.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
                AudioSource source = value.GetComponent<AudioSource>();
                if (source != null) source.Stop();
                value.SetActive(false);
                value.transform.SetParent(_poolRoot, false);
                Stack<GameObject> stack;
                if (!_available.TryGetValue(marker.Type, out stack))
                {
                    stack = new Stack<GameObject>();
                    _available[marker.Type] = stack;
                }
                stack.Push(value);
                AvailableCount++;
            }
        }
    }
}
