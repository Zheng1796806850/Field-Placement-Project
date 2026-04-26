using System;
using System.Collections.Generic;

/// <summary>Maps <see cref="ObjectiveType"/> to evaluator instances. No switch on type in quest flow.</summary>
public static class ObjectiveEvaluatorRegistry
{
    private static readonly Dictionary<ObjectiveType, IObjectiveEvaluator> Evaluators =
        new Dictionary<ObjectiveType, IObjectiveEvaluator>();

    static ObjectiveEvaluatorRegistry()
    {
        Evaluators[ObjectiveType.Collect] = new CollectObjectiveEvaluator();
        Evaluators[ObjectiveType.Build] = new BuildObjectiveEvaluator();
        Evaluators[ObjectiveType.Repair] = new RepairObjectiveEvaluator();
        Evaluators[ObjectiveType.Kill] = new KillObjectiveEvaluator();
        Evaluators[ObjectiveType.ReachArea] = new ReachAreaObjectiveEvaluator();
        Evaluators[ObjectiveType.SurviveNights] = new SurviveNightsObjectiveEvaluator();
        Evaluators[ObjectiveType.PlantAndWater] = new PlantAndWaterObjectiveEvaluator();
        Evaluators[ObjectiveType.CropPlanted] = new CropPlantedObjectiveEvaluator();
        Evaluators[ObjectiveType.PlotWatered] = new PlotWateredObjectiveEvaluator();
    }

    public static IObjectiveEvaluator Get(ObjectiveType type)
    {
        if (!Evaluators.TryGetValue(type, out var ev))
            throw new InvalidOperationException($"No evaluator registered for {type}");
        return ev;
    }

    public static bool TryGet(ObjectiveType type, out IObjectiveEvaluator evaluator)
    {
        return Evaluators.TryGetValue(type, out evaluator);
    }
}
