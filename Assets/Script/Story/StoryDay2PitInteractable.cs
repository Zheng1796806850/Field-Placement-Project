using UnityEngine;

/// <summary>Day2 坑洞 NPC：在 <see cref="LinearStoryDirector"/> 到达交付阶段时处理物品检测、扣除与斧头升级。</summary>
public class StoryDay2PitInteractable : MonoBehaviour, IInteractable
{
    [Header("Identity")]
    public string promptText = "Press E to listen";
    public int priority = 40;

    [Header("Refs")]
    public LinearStoryDirector storyDirector;

    public int Priority => priority;

    void Awake()
    {
        if (storyDirector == null)
            storyDirector = FindFirstObjectByType<LinearStoryDirector>();
    }

    public string GetPrompt() => promptText;

    public bool CanInteract(GameObject interactor)
    {
        if (storyDirector == null || !storyDirector.IsLinearStoryActive)
            return false;
        if (!storyDirector.IsDay2PitInteractablePhase)
            return false;
        return ResolvePlayer(interactor) != null;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor)) return;
        storyDirector.HandleDay2PitInteract(interactor);
    }

    static Transform ResolvePlayer(GameObject interactor)
    {
        if (interactor == null) return null;
        var mover = interactor.GetComponentInParent<PlayerMovementController>();
        if (mover != null) return mover.transform;
        var root = interactor.transform.root;
        return root != null ? root : interactor.transform;
    }
}
