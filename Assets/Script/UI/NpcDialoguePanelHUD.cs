using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class NpcDialoguePanelHUD : MonoBehaviour
{
    private struct ParsedDialogueLine
    {
        public string speaker;
        public string content;
    }

    public static NpcDialoguePanelHUD Instance { get; private set; }

    [Header("UI")]
    public GameObject panelRoot;
    public TextMeshProUGUI npcNameText;
    public TextMeshProUGUI dialogueText;
    public Button nextButton;
    public Button confirmButton;

    [Header("Input")]
    public bool allowKeyboardAdvance = true;
    public KeyCode advanceKeyPrimary = KeyCode.E;
    public KeyCode advanceKeySecondary = KeyCode.Space;

    [Header("Typewriter")]
    public bool useTypewriter = true;
    [Min(1f)] public float charactersPerSecond = 40f;

    [Header("Dialogue Hide Targets")]
    public GameObject[] uiToHideDuringDialogue = Array.Empty<GameObject>();

    [Header("Refs (auto find if null)")]
    public PlayerInteractor2D playerInteractor;
    public PlayerCombat2D playerCombat;
    public PauseMenuController pauseMenuController;

    private readonly List<ParsedDialogueLine> _lines = new List<ParsedDialogueLine>(8);
    private readonly List<bool> _uiPrevActive = new List<bool>(8);
    private int _lineIndex;
    private bool _isRunning;
    private bool _isTyping;
    private bool _pausedByDialogue;
    private bool _prevInteractorEnabled;
    private Action _onComplete;
    private Coroutine _typeRoutine;
    private string _defaultNpcName = "";
    private string _playerDisplayName = "Player";

    public bool IsRunning => _isRunning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(AdvanceOrFinish);
            nextButton.onClick.AddListener(AdvanceOrFinish);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(AdvanceOrFinish);
            confirmButton.onClick.AddListener(AdvanceOrFinish);
        }
    }

    private void Update()
    {
        if (!_isRunning || !allowKeyboardAdvance)
            return;

        if (Input.GetKeyDown(advanceKeyPrimary) || Input.GetKeyDown(advanceKeySecondary))
            AdvanceOrFinish();
    }

    public void BeginDialogue(string npcName, IReadOnlyList<string> lines, Action onComplete)
    {
        BeginDialogue(npcName, "Player", lines, onComplete);
    }

    public void BeginDialogue(string npcName, string playerDisplayName, IReadOnlyList<string> lines, Action onComplete)
    {
        if (lines == null || lines.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        _onComplete = onComplete;
        _lines.Clear();
        _defaultNpcName = string.IsNullOrWhiteSpace(npcName) ? "NPC" : npcName;
        _playerDisplayName = string.IsNullOrWhiteSpace(playerDisplayName) ? "Player" : playerDisplayName;
        for (int i = 0; i < lines.Count; i++)
        {
            string s = lines[i];
            if (!string.IsNullOrWhiteSpace(s))
                _lines.Add(ParseLine(s));
        }

        if (_lines.Count == 0)
        {
            _onComplete?.Invoke();
            _onComplete = null;
            return;
        }

        ResolveRefs();
        EnterDialogueMode();

        _isRunning = true;
        _lineIndex = 0;

        if (panelRoot != null && !panelRoot.activeSelf)
            panelRoot.SetActive(true);

        ApplyLine();
    }

    private void ResolveRefs()
    {
        if (playerInteractor == null)
            playerInteractor = FindFirstObjectByType<PlayerInteractor2D>(FindObjectsInactive.Include);
        if (playerCombat == null)
            playerCombat = FindFirstObjectByType<PlayerCombat2D>(FindObjectsInactive.Include);
        if (pauseMenuController == null)
            pauseMenuController = FindFirstObjectByType<PauseMenuController>(FindObjectsInactive.Include);
    }

    private void EnterDialogueMode()
    {
        var gsm = GameStateManager.Instance;
        if (gsm != null && !gsm.IsPaused)
        {
            gsm.SetPaused(true);
            _pausedByDialogue = true;
        }
        else
        {
            _pausedByDialogue = false;
        }

        if (playerInteractor != null)
        {
            _prevInteractorEnabled = playerInteractor.InputEnabled;
            playerInteractor.SetInputEnabled(false);
        }

        if (playerCombat != null)
            playerCombat.PushExternalInputBlock();

        if (pauseMenuController != null)
            pauseMenuController.SetExternalPauseBlocked(true);

        _uiPrevActive.Clear();
        int n = uiToHideDuringDialogue != null ? uiToHideDuringDialogue.Length : 0;
        for (int i = 0; i < n; i++)
        {
            GameObject go = uiToHideDuringDialogue[i];
            bool was = go != null && go.activeSelf;
            _uiPrevActive.Add(was);
            if (go != null && was)
                go.SetActive(false);
        }
    }

    private void ExitDialogueMode()
    {
        for (int i = 0; i < _uiPrevActive.Count; i++)
        {
            if (uiToHideDuringDialogue == null || i >= uiToHideDuringDialogue.Length)
                break;
            GameObject go = uiToHideDuringDialogue[i];
            if (go != null && _uiPrevActive[i])
                go.SetActive(true);
        }
        _uiPrevActive.Clear();

        if (pauseMenuController != null)
            pauseMenuController.SetExternalPauseBlocked(false);

        if (playerCombat != null)
            playerCombat.PopExternalInputBlock();

        if (playerInteractor != null)
            playerInteractor.SetInputEnabled(_prevInteractorEnabled);

        if (_pausedByDialogue)
        {
            var gsm = GameStateManager.Instance;
            if (gsm != null)
                gsm.SetPaused(false);
        }
        _pausedByDialogue = false;
    }

    private void ApplyLine()
    {
        if (_lineIndex < 0 || _lineIndex >= _lines.Count)
            return;

        ParsedDialogueLine line = _lines[_lineIndex];
        if (npcNameText != null)
            npcNameText.text = line.speaker;

        if (_typeRoutine != null)
        {
            StopCoroutine(_typeRoutine);
            _typeRoutine = null;
        }

        if (dialogueText != null)
            dialogueText.text = "";
        _isTyping = false;

        if (useTypewriter)
            _typeRoutine = StartCoroutine(TypeLine(line.content));
        else if (dialogueText != null)
            dialogueText.text = line.content;

        bool isLast = _lineIndex >= _lines.Count - 1;
        if (nextButton != null) nextButton.gameObject.SetActive(!isLast);
        if (confirmButton != null) confirmButton.gameObject.SetActive(isLast);
    }

    private void AdvanceOrFinish()
    {
        if (!_isRunning)
            return;

        if (_isTyping)
        {
            CompleteCurrentLineImmediately();
            return;
        }

        if (_lineIndex < _lines.Count - 1)
        {
            _lineIndex++;
            ApplyLine();
            return;
        }

        Action callback = _onComplete;
        _onComplete = null;

        _isRunning = false;
        _lines.Clear();
        _isTyping = false;
        if (_typeRoutine != null)
        {
            StopCoroutine(_typeRoutine);
            _typeRoutine = null;
        }
        if (panelRoot != null)
            panelRoot.SetActive(false);

        ExitDialogueMode();
        callback?.Invoke();
    }

    private ParsedDialogueLine ParseLine(string raw)
    {
        string speaker = _defaultNpcName;
        string content = raw ?? string.Empty;

        int sep = content.IndexOf(':');
        if (sep > 0)
        {
            string head = content.Substring(0, sep).Trim();
            string tail = content.Substring(sep + 1).TrimStart();
            if (IsPlayerTag(head))
            {
                speaker = _playerDisplayName;
                content = tail;
            }
            else if (IsNpcTag(head))
            {
                speaker = _defaultNpcName;
                content = tail;
            }
        }

        return new ParsedDialogueLine
        {
            speaker = speaker,
            content = content
        };
    }

    private bool IsPlayerTag(string tag)
    {
        return string.Equals(tag, "P", StringComparison.OrdinalIgnoreCase)
               || string.Equals(tag, "PLAYER", StringComparison.OrdinalIgnoreCase)
               || string.Equals(tag, "YOU", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsNpcTag(string tag)
    {
        return string.Equals(tag, "N", StringComparison.OrdinalIgnoreCase)
               || string.Equals(tag, "NPC", StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerator TypeLine(string text)
    {
        _isTyping = true;
        if (dialogueText == null)
        {
            _isTyping = false;
            yield break;
        }

        string line = text ?? string.Empty;
        float cps = Mathf.Max(1f, charactersPerSecond);
        float interval = 1f / cps;
        float timer = 0f;
        int shown = 0;

        while (shown < line.Length)
        {
            timer += Time.unscaledDeltaTime;
            while (timer >= interval && shown < line.Length)
            {
                timer -= interval;
                shown++;
                dialogueText.text = line.Substring(0, shown);
            }
            yield return null;
        }

        dialogueText.text = line;
        _isTyping = false;
        _typeRoutine = null;
    }

    private void CompleteCurrentLineImmediately()
    {
        if (_lineIndex < 0 || _lineIndex >= _lines.Count)
            return;
        if (_typeRoutine != null)
        {
            StopCoroutine(_typeRoutine);
            _typeRoutine = null;
        }

        if (dialogueText != null)
            dialogueText.text = _lines[_lineIndex].content;
        _isTyping = false;
    }
}

