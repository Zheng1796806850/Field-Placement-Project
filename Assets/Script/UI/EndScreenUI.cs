using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndScreenUI : MonoBehaviour
{
    [Header("UI")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI reasonText;
    public Button restartButton;
    public Button quitButton;

    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        HideInstant();

        if (restartButton != null)
            restartButton.onClick.AddListener(() => GameFlowManager.Instance?.RestartScene());

        if (quitButton != null)
            quitButton.onClick.AddListener(() => GameFlowManager.Instance?.QuitToDesktop());
    }

    public void Show(GameResult result, string reason)
    {
        if (titleText != null)
            titleText.text = (result == GameResult.Victory) ? "YOU WIN" : "YOU LOSE";

        if (reasonText != null)
            reasonText.text = string.IsNullOrWhiteSpace(reason) ? "" : reason;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }
        else
        {
            gameObject.SetActive(true);
        }
    }

    public void HideInstant()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
