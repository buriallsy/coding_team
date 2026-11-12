using UnityEngine;
using UnityEngine.Rendering;

namespace BorrowingSun
{
    internal sealed class FlowerHarvestVisuals
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly Color Gold = new Color(1f, .64f, .14f);
        private static readonly Color Aqua = new Color(.18f, .94f, .77f);
        private static readonly Color Violet = new Color(.74f, .48f, 1f);
        private static readonly Color Leaf = new Color(.32f, .69f, .25f);

        private readonly MeshFilter[] mushrooms;
        private readonly ParticleSystem mushroomGlow, orchardGlow;
        private readonly GameObject sunnyFruit, rainFruit, rainNightFruit;
        private bool mushroomsVisible = true;
        private FlowerIngredient? visibleFruit;

        internal FlowerHarvestVisuals(Transform mushroomPatch, Transform orchard, MeshFilter[] mushrooms,
            Material fruitMaterial, Material particleMaterial)
        {
            this.mushrooms = mushrooms;
            Mesh mesh = mushrooms[0].sharedMesh;

            sunnyFruit = CreateModel(orchard, "Sunny fruit", new Vector3(-.13f, 1.4f, .36f));
            AddPart(sunnyFruit.transform, mesh, fruitMaterial, Gold, Vector3.zero, new Vector3(.31f, .23f, .26f));
            AddPart(sunnyFruit.transform, mesh, fruitMaterial, Leaf, new Vector3(.07f, .12f, 0),
                new Vector3(.14f, .05f, .08f), -25f);

            rainFruit = CreateModel(orchard, "Rain fruit", new Vector3(.15f, 1.4f, .36f));
            AddPart(rainFruit.transform, mesh, fruitMaterial, Aqua, Vector3.zero, new Vector3(.25f, .27f, .24f));
            AddPart(rainFruit.transform, mesh, fruitMaterial, Aqua, new Vector3(0, .11f, 0),
                new Vector3(.13f, .2f, .13f));

            rainNightFruit = CreateModel(orchard, "Rain night fruit", new Vector3(0, 1.4f, .4f));
            AddPart(rainNightFruit.transform, mesh, fruitMaterial, Violet, new Vector3(-.08f, .03f, 0), Vector3.one * .2f);
            AddPart(rainNightFruit.transform, mesh, fruitMaterial, Violet, new Vector3(.08f, .03f, 0), Vector3.one * .2f);
            AddPart(rainNightFruit.transform, mesh, fruitMaterial, Violet, new Vector3(0, -.08f, .06f), Vector3.one * .19f);

            mushroomGlow = CreateGlow(mushroomPatch, .24f, particleMaterial);
            orchardGlow = CreateGlow(orchard, 1.48f, particleMaterial);
            SetGlow(mushroomGlow, new Color(1f, .83f, .57f), true);
        }

        internal void Refresh(bool mushroomsReady, FlowerIngredient? fruit)
        {
            if (mushroomsReady != mushroomsVisible)
            {
                mushroomsVisible = mushroomsReady;
                foreach (var model in mushrooms)
                    if (model) model.gameObject.SetActive(mushroomsReady);
                SetGlow(mushroomGlow, new Color(1f, .83f, .57f), mushroomsReady);
            }

            if (visibleFruit == fruit) return;
            visibleFruit = fruit;
            sunnyFruit.SetActive(fruit == FlowerIngredient.SunnyFruit);
            rainFruit.SetActive(fruit == FlowerIngredient.WeatherFruit);
            rainNightFruit.SetActive(fruit == FlowerIngredient.RainNightFruit);
            Color color = fruit == FlowerIngredient.SunnyFruit ? Gold :
                fruit == FlowerIngredient.WeatherFruit ? Aqua : Violet;
            SetGlow(orchardGlow, color, fruit.HasValue);
        }

        private static GameObject CreateModel(Transform parent, string name, Vector3 localPosition)
        {
            var model = new GameObject(name);
            model.transform.SetParent(parent, false);
            model.transform.localPosition = localPosition;
            model.SetActive(false);
            return model;
        }

        private static void AddPart(Transform parent, Mesh mesh, Material material, Color color,
            Vector3 position, Vector3 scale, float tilt = 0f)
        {
            var part = new GameObject("Fruit mesh");
            part.transform.SetParent(parent, false);
            part.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, 0f, tilt));
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var properties = new MaterialPropertyBlock();
            properties.SetColor(BaseColor, color);
            renderer.SetPropertyBlock(properties);
        }

        private static ParticleSystem CreateGlow(Transform parent, float height, Material material)
        {
            var glow = new GameObject("Harvest sparkles");
            glow.SetActive(false);
            glow.transform.SetParent(parent, false);
            glow.transform.SetLocalPositionAndRotation(Vector3.up * height, Quaternion.Euler(-90f, 0f, 0f));
            var particles = glow.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 28;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.65f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.28f, .63f);
            main.startSize = new ParticleSystem.MinMaxCurve(.075f, .14f);
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = .23f;
            var emission = particles.emission;
            emission.rateOverTime = 16f;
            var renderer = glow.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            glow.SetActive(true);
            return particles;
        }

        private static void SetGlow(ParticleSystem particles, Color color, bool visible)
        {
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (!visible) return;
            var main = particles.main;
            main.startColor = color;
            particles.Play(false);
            particles.Emit(6);
        }
    }
}
