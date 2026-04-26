public enum GameplayEventKind
{
    ResourceCollected = 0,
    StructureBuilt = 1,
    StructureRepaired = 2,
    EnemyKilled = 3,
    PlayerEnteredArea = 4,
    NightSurvived = 5,
    CropPlantedAndWatered = 6,
    /// <summary>任意地块成功种下种子；<see cref="GameplayEvent.StringId"/> 为 <see cref="FarmlandPlot.plotId"/>。</summary>
    CropPlanted = 7,
    /// <summary>任意地块完成一次浇水；StringId 为 plotId。</summary>
    PlotWatered = 8
}
