using UnityEngine;

public interface IInteractable
{
    /// <summary>显示在提示UI里的文本，比如 "Press E to Enter"</summary>
    string GetPrompt();

    /// <summary>是否允许交互（例如缺少种子就不允许种植）</summary>
    bool CanInteract(GameObject interactor);

    /// <summary>执行交互逻辑</summary>
    void Interact(GameObject interactor);

    /// <summary>用于优先级（门/拾取/重要物）</summary>
    int Priority { get; }
}
