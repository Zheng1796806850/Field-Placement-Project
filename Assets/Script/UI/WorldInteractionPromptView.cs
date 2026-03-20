using UnityEngine;

/// <summary>
/// Controls prompt UI visibility and optional billboard behavior.
/// </summary>
public class WorldInteractionPromptView : MonoBehaviour
{
    [Header("Visibility")]
    public CanvasGroup canvasGroup;
    public bool useCanvasGroup = true;
    public bool useGameObjectActiveFallback = false;
    public bool startHidden = true;

    [Header("Billboard")]
    public bool faceMainCamera = true;

    private bool _isVisible;

    public bool IsVisible => _isVisible;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null && useCanvasGroup)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (startHidden)
            SetVisible(false);
    }

    private void Update()
    {
        if (!faceMainCamera)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        transform.forward = cam.transform.forward;
    }

    public void SetVisible(bool visible)
    {
        _isVisible = visible;

        if (useCanvasGroup && canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (useGameObjectActiveFallback)
        {
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }
    }
}
