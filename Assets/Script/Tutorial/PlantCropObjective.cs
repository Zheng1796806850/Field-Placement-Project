using System.Collections.Generic;
using UnityEngine;

public class PlantCropObjective : TutorialObjective
{
    public List<FarmlandPlot> targetPlots = new List<FarmlandPlot>();
    public List<string> requiredCropIds = new List<string>();
    [Min(1)] public int requiredPlantedCount = 1;
    [Tooltip("If true, each plot must be watered (PlantedWatered or ready to harvest), not only planted dry.")]
    public bool requireWaterAfterPlant = true;

    private readonly HashSet<FarmlandPlot> _done = new HashSet<FarmlandPlot>();

    protected override void OnBegin()
    {
        _done.Clear();

        if (targetPlots == null || targetPlots.Count == 0)
        {
            var all = FindObjectsByType<FarmlandPlot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            targetPlots = new List<FarmlandPlot>(all);
        }
    }

    private void Update()
    {
        if (IsCompleted) return;
        if (targetPlots == null || targetPlots.Count == 0) return;

        for (int i = 0; i < targetPlots.Count; i++)
        {
            var p = targetPlots[i];
            if (p == null) continue;
            if (_done.Contains(p)) continue;

            FarmlandPlotEntry e = p.BuildSnapshotEntry();
            if (e == null) continue;

            bool satisfies = requireWaterAfterPlant
                ? e.plotState == (int)FarmlandPlot.PlotState.PlantedWatered ||
                  e.plotState == (int)FarmlandPlot.PlotState.ReadyToHarvest
                : e.plotState == (int)FarmlandPlot.PlotState.PlantedDry ||
                  e.plotState == (int)FarmlandPlot.PlotState.PlantedWatered ||
                  e.plotState == (int)FarmlandPlot.PlotState.ReadyToHarvest;

            if (!satisfies) continue;

            if (requiredCropIds != null && requiredCropIds.Count > 0)
            {
                bool cropMatch = false;
                for (int k = 0; k < requiredCropIds.Count; k++)
                {
                    if (requiredCropIds[k] == e.cropId)
                    {
                        cropMatch = true;
                        break;
                    }
                }

                if (!cropMatch) continue;
            }

            _done.Add(p);
        }

        if (_done.Count >= requiredPlantedCount)
            Complete();
    }

    public override string GetProgressText()
    {
        if (requireWaterAfterPlant)
            return $"Plant & water {_done.Count}/{requiredPlantedCount}";
        return $"Plant crop {_done.Count}/{requiredPlantedCount}";
    }
}
