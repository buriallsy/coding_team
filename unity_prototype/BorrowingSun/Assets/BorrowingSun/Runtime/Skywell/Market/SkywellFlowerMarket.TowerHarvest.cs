using UnityEngine;

namespace BorrowingSun
{
    public sealed partial class SkywellFlowerMarket
    {
        internal sealed class TowerHarvestSpot
        {
            internal Transform Root;
            internal bool IsOrchard;
            internal float ReadyAt;
            internal FlowerIngredient? Produce;
            internal bool Ready => Produce.HasValue && Time.time >= ReadyAt;
            internal bool JustRegrew => Ready && ReadyAt > 0f && Time.time - ReadyAt < 3f;
        }

        private FlowerHarvestVisuals[] towerHarvestVisuals;
        internal TowerHarvestSpot[] TowerHarvestSpots { get; private set; } = System.Array.Empty<TowerHarvestSpot>();

        private void EnsureTowerHarvestSpots()
        {
            if (towerHarvestVisuals != null || connectionMushrooms == null || connectionOrchards == null ||
                connectionMushrooms.Length == 0 || connectionMushrooms.Length != connectionOrchards.Length)
                return;
            if (!orchardCanopy || !harvestParticleMaterial ||
                mushroomModels == null || mushroomModels.Length == 0 || !mushroomModels[0])
                return;

            if (orchardLabel) orchardLabel.transform.localPosition = Vector3.up * 1.65f;
            towerHarvestVisuals = new FlowerHarvestVisuals[connectionMushrooms.Length];
            TowerHarvestSpots = new TowerHarvestSpot[connectionMushrooms.Length * 2];
            for (int index = 0; index < connectionMushrooms.Length; index++)
            {
                var patch = connectionMushrooms[index];
                var tree = connectionOrchards[index];
                var caps = new MeshFilter[mushroomModels.Length];
                for (int model = 0; model < caps.Length; model++)
                    caps[model] = patch.GetChild(mushroomModels[model].transform.GetSiblingIndex()).GetComponent<MeshFilter>();

                var label = tree.GetComponentInChildren<TextMesh>();
                if (label)
                {
                    label.text = "果树";
                    label.transform.localPosition = Vector3.up * 1.65f;
                }

                towerHarvestVisuals[index] = new FlowerHarvestVisuals(patch, tree, caps,
                    orchardCanopy.sharedMaterial, harvestParticleMaterial);
                TowerHarvestSpots[index * 2] = new TowerHarvestSpot { Root = patch };
                TowerHarvestSpots[index * 2 + 1] = new TowerHarvestSpot { Root = tree, IsOrchard = true };
            }
        }

        private FlowerIngredient? TowerProduce(TowerHarvestSpot spot)
        {
            if (!Run.CanGather) return null;
            if (!spot.IsOrchard) return FlowerIngredient.Mushroom;
            bool wet = connectionWeather && connectionWeather.IsRain &&
                connectionWeather.Contains(spot.Root.position);
            return Run.GetOrchardProduce(wet, !wet && SunlightAvailable &&
                IsOrchardUnoccluded(spot.Root.position + Vector3.up * 1.6f));
        }

        private void RefreshTowerHarvestSpots()
        {
            if (towerHarvestVisuals == null) return;
            for (int index = 0; index < towerHarvestVisuals.Length; index++)
            {
                var patch = TowerHarvestSpots[index * 2];
                var tree = TowerHarvestSpots[index * 2 + 1];
                patch.Produce = TowerProduce(patch);
                tree.Produce = TowerProduce(tree);
                towerHarvestVisuals[index].Refresh(patch.Ready, tree.Ready ? tree.Produce : null);
            }
        }

        private TowerHarvestSpot FindTowerSpot(Vector3 feet)
        {
            TowerHarvestSpot closest = null;
            float distance = float.PositiveInfinity;
            foreach (var spot in TowerHarvestSpots)
            {
                if (!Near(feet, spot.Root)) continue;
                float candidate = (feet - spot.Root.position).sqrMagnitude;
                if (candidate >= distance) continue;
                closest = spot;
                distance = candidate;
            }
            return closest;
        }

        private bool TryHarvestTowerSpot(CourtPlayer actor)
        {
            var spot = FindTowerSpot(actor.FeetPosition);
            if (spot == null || !Run.CanGather) return false;
            if (!CanStandOnGround(actor)) Say("站稳后再采集。");
            else if (TowerProduce(spot) is FlowerIngredient ingredient) Harvest(ingredient, ref spot.ReadyAt);
            else Say(Run.IsNight ? "果树待结果 · 对中台施雨可采雨夜果。" : "果树待结果 · 需要直射阳光或中台降雨。");
            return true;
        }

        private string TowerHarvestPrompt(Vector3 feet)
        {
            var spot = FindTowerSpot(feet);
            if (spot == null || !Run.CanGather) return null;
            if (Feedback != null) return Feedback;
            if (Time.time < spot.ReadyAt) return "采集点正在恢复";
            if (TowerProduce(spot) is FlowerIngredient ingredient) return "E 采" + IngredientName(ingredient);
            return Run.IsNight ? "果树待结果 · 对中台施雨" : "果树待结果 · 需要直射阳光或降雨";
        }

        private void ResetTowerHarvestSpots()
        {
            foreach (var spot in TowerHarvestSpots) spot.ReadyAt = 0f;
        }

        private void HideTowerHarvestSpots()
        {
            if (towerHarvestVisuals == null) return;
            foreach (var visual in towerHarvestVisuals) visual.Refresh(false, null);
        }
    }
}
