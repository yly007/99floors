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
        private readonly Stack<Light> _availableLights = new Stack<Light>();
        private readonly List<Light> _lightBuffer = new List<Light>();
        private readonly List<EvacuationPooledPrimitive> _primitiveBuffer =
            new List<EvacuationPooledPrimitive>();
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

        public Light RentLight(string objectName, Transform parent, Vector3 position, Color color,
            float intensity, float range)
        {
            Light light = null;
            while (_availableLights.Count > 0 && light == null)
            {
                light = _availableLights.Pop();
            }
            if (light == null)
            {
                GameObject lightObject = new GameObject(objectName);
                light = lightObject.AddComponent<Light>();
            }
            light.gameObject.name = objectName;
            light.transform.SetParent(parent, false);
            light.transform.localPosition = position;
            light.transform.localRotation = Quaternion.identity;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.enabled = true;
            light.gameObject.SetActive(true);
            return light;
        }

        public void ReleaseHierarchy(Transform hierarchy)
        {
            if (hierarchy == null) return;
            _lightBuffer.Clear();
            hierarchy.GetComponentsInChildren(true, _lightBuffer);
            for (int i = 0; i < _lightBuffer.Count; i++)
            {
                Light light = _lightBuffer[i];
                if (light == null) continue;
                light.enabled = false;
                light.gameObject.SetActive(false);
                light.transform.SetParent(_poolRoot, false);
                _availableLights.Push(light);
            }
            _primitiveBuffer.Clear();
            hierarchy.GetComponentsInChildren(true, _primitiveBuffer);
            for (int i = 0; i < _primitiveBuffer.Count; i++)
            {
                EvacuationPooledPrimitive marker = _primitiveBuffer[i];
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
            _lightBuffer.Clear();
            _primitiveBuffer.Clear();
        }
    }
}
