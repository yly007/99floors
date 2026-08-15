using System.Collections.Generic;
using UnityEngine;

namespace NinetyNine
{
    public sealed class EvacuationVfx : MonoBehaviour
    {
        private static readonly Material[] ParticleMaterials = new Material[4];
        private static readonly Stack<ParticleSystem> AvailableSystems = new Stack<ParticleSystem>();
        private static Transform _poolRoot;

        public void Configure(EvacuationTheme theme, FloorEventKind floorEvent, int length)
        {
            CreateDust(theme, length);
            if (theme == EvacuationTheme.Maintenance || floorEvent == FloorEventKind.SequentialBlackout)
            {
                CreateSparks(length);
            }
            if (theme == EvacuationTheme.Flooded || floorEvent == FloorEventKind.RisingWater)
            {
                CreateDrips(length);
            }
        }

        private void CreateDust(EvacuationTheme theme, int length)
        {
            ParticleSystem particles = CreateSystem("FloatingDust",
                new Vector3(0f, 1.4f, 4f + length * 1.35f), 0);
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.015f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.045f);
            main.maxParticles = 180;
            main.gravityModifier = 0f;
            main.startColor = theme == EvacuationTheme.RedHall
                ? new Color(0.4f, 0.04f, 0.02f, 0.42f)
                : new Color(0.48f, 0.62f, 0.58f, 0.28f);
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 9f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(5f, 2.5f, Mathf.Max(8f, length * 2.5f));
            particles.Play();
        }

        private void CreateSparks(int length)
        {
            ParticleSystem particles = CreateSystem("ElectricalSparks", new Vector3(0.8f, 2.65f,
                4f + length * 1.1f), 1);
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.055f);
            main.startColor = new Color(0.25f, 0.86f, 1f, 1f);
            main.gravityModifier = 0.8f;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 1.4f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 24f;
            particles.Play();
        }

        private void CreateDrips(int length)
        {
            ParticleSystem particles = CreateSystem("CeilingDrips", new Vector3(0f, 2.8f,
                4f + length * 1.25f), 2);
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.startColor = new Color(0.12f, 0.44f, 0.5f, 0.7f);
            main.gravityModifier = 1.4f;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 4f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(4f, 0.1f, Mathf.Max(7f, length * 2.2f));
            particles.Play();
        }

        private ParticleSystem CreateSystem(string objectName, Vector3 localPosition, int materialIndex)
        {
            ParticleSystem particles = null;
            while (AvailableSystems.Count > 0 && particles == null)
            {
                particles = AvailableSystems.Pop();
            }
            GameObject root;
            if (particles == null)
            {
                root = new GameObject(objectName);
                particles = root.AddComponent<ParticleSystem>();
            }
            else
            {
                root = particles.gameObject;
                root.name = objectName;
                root.SetActive(true);
                particles.Clear(true);
            }
            root.transform.SetParent(transform, false);
            root.transform.localPosition = localPosition;
            ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = GetParticleMaterial(materialIndex);
            return particles;
        }

        public void ReleaseSystems()
        {
            if (_poolRoot == null)
            {
                _poolRoot = new GameObject("Runtime Particle Pool").transform;
                Object.DontDestroyOnLoad(_poolRoot.gameObject);
            }
            ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem particles = systems[i];
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.transform.SetParent(_poolRoot, false);
                particles.gameObject.SetActive(false);
                AvailableSystems.Push(particles);
            }
        }

        private static Material GetParticleMaterial(int index)
        {
            index = Mathf.Clamp(index, 0, ParticleMaterials.Length - 1);
            if (ParticleMaterials[index] != null)
            {
                return ParticleMaterials[index];
            }

            Shader shader = Shader.Find("NinetyNine/AdditiveParticle") ??
                Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Standard");
            Material material = new Material(shader)
            {
                name = "Runtime Horror Particle " + index,
                hideFlags = HideFlags.HideAndDontSave
            };
            Texture2D texture = Resources.Load<Texture2D>("Art/horror_particle_atlas_v1");
            if (texture != null)
            {
                material.mainTexture = texture;
                int rowFromTop = index / 2;
                material.mainTextureScale = new Vector2(0.5f, 0.5f);
                material.mainTextureOffset = new Vector2((index % 2) * 0.5f,
                    1f - (rowFromTop + 1) * 0.5f);
            }
            ParticleMaterials[index] = material;
            return material;
        }
    }
}
