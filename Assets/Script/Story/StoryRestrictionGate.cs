using UnityEngine;

/// <summary>由 <see cref="LinearStoryDirector"/> 驱动；<see cref="ZoneTeleportTrigger2D"/> 在交互前查询。</summary>
public static class StoryRestrictionGate
{
    static bool s_blockFrontYard;
    static bool s_blockTown;
    static bool s_blockPlanting;
    static string s_frontYardMessage = "";
    static string s_townMessage = "";
    static string s_plantingMessage = "";

    public static void SetFrontYardBlocked(bool blocked, string denyMessage)
    {
        s_blockFrontYard = blocked;
        s_frontYardMessage = denyMessage ?? "";
    }

    public static void SetTownBlocked(bool blocked, string denyMessage)
    {
        s_blockTown = blocked;
        s_townMessage = denyMessage ?? "";
    }

    public static void ClearAll()
    {
        s_blockFrontYard = false;
        s_blockTown = false;
        s_blockPlanting = false;
        s_frontYardMessage = "";
        s_townMessage = "";
        s_plantingMessage = "";
    }

    public static void SetPlantingBlocked(bool blocked, string denyMessage)
    {
        s_blockPlanting = blocked;
        s_plantingMessage = denyMessage ?? "";
    }

    public static bool TryGetPlantDeniedMessage(out string message)
    {
        message = null;
        if (!s_blockPlanting)
            return false;

        message = string.IsNullOrWhiteSpace(s_plantingMessage) ? "You can't plant yet." : s_plantingMessage;
        return true;
    }

    public static bool IsTravelBlocked(ZoneTeleportTrigger2D.LinearStoryTravelGate gate)
    {
        return TryGetDenyMessage(gate, out _);
    }

    public static bool TryGetDenyMessage(ZoneTeleportTrigger2D.LinearStoryTravelGate gate, out string message)
    {
        message = null;
        if (gate == ZoneTeleportTrigger2D.LinearStoryTravelGate.None)
            return false;

        if (gate == ZoneTeleportTrigger2D.LinearStoryTravelGate.FrontYard && s_blockFrontYard)
        {
            message = string.IsNullOrWhiteSpace(s_frontYardMessage) ? "You can't go there yet." : s_frontYardMessage;
            return true;
        }

        if (gate == ZoneTeleportTrigger2D.LinearStoryTravelGate.Town && s_blockTown)
        {
            message = string.IsNullOrWhiteSpace(s_townMessage) ? "You can't go there yet." : s_townMessage;
            return true;
        }

        return false;
    }
}
