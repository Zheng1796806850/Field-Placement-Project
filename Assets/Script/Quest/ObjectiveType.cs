/// <summary>Quest objective categories; progression is handled only via <see cref="ObjectiveEvaluatorRegistry"/>.</summary>
public enum ObjectiveType
{
    Collect = 0,
    Build = 1,
    Repair = 2,
    Kill = 3,
    ReachArea = 4,
    SurviveNights = 5,
    /// <summary>After plant + first successful water on a plot (see <see cref="FarmlandPlot"/> quest fields).</summary>
    PlantAndWater = 6,
    /// <summary>任意地块种下种子；<see cref="ObjectiveDefinition.targetId"/> 空则任意 plotId。</summary>
    CropPlanted = 7,
    /// <summary>任意地块浇水一次；targetId 空则任意。</summary>
    PlotWatered = 8
}
