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
    public SfxId typingSfxId = SfxId.UI_Typing;

    [Header("Time Scale")]
    [Tooltip("若为 false，对话期间不调用 GameStateManager.SetPaused（剧情导演自行冻结昼夜等）；仍锁定玩家输入与暂停菜单。")]
    public bool freezeTimeScaleDuringDialogue = true;

    [Header("Speaker labels (prefix tags)")]
    public string narratorDisplayName = "Narrator";
    public string mysteryDisplayName = "???";
    [Tooltip("内心独白行使用标签 I: / INNER: 时的说话人显示名。")]
    public string innerThoughtSpeakerName = "Inner Voice";

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
    private AudioSource _typingAudioSource;

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

        _typingAudioSource = gameObject.GetComponent<AudioSource>();
        if (_typingAudioSource == null)
            _typingAudioSource = gameObject.AddComponent<AudioSource>();
        _typingAudioSource.playOnAwake = false;
        _typingAudioSource.loop = true;
        _typingAudioSource.spatialBlend = 0f;
        _typingAudioSource.volume = 1f;
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
        if (freezeTimeScaleDuringDialogue && gsm != null && !gsm.IsPaused)
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

        if (_pausedByDialogue && freezeTimeScaleDuringDialogue)
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
        StopTypingSfx();

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
        StopTypingSfx();
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
            string headNorm = head.ToUpperInvariant();

            if (IsPlayerTag(headNorm))
            {
                speaker = _playerDisplayName;
                content = tail;
            }
            else if (IsNpcTag(headNorm))
            {
                speaker = _defaultNpcName;
                content = tail;
            }
            else if (IsNarratorTag(headNorm))
            {
                speaker = string.IsNullOrWhiteSpace(narratorDisplayName) ? "Narrator" : narratorDisplayName;
                content = tail;
            }
            else if (IsInnerThoughtTag(headNorm))
            {
                speaker = string.IsNullOrWhiteSpace(innerThoughtSpeakerName) ? "Inner Voice" : innerThoughtSpeakerName;
                content = tail;
            }
            else if (IsMysteryTag(headNorm))
            {
                speaker = string.IsNullOrWhiteSpace(mysteryDisplayName) ? "???" : mysteryDisplayName;
                content = tail;
            }
        }

        return new ParsedDialogueLine
        {
            speaker = speaker,
            content = content
        };
    }

    private static bool IsPlayerTag(string headUpper)
    {
        return headUpper is "P" or "PLAYER" or "YOU";
    }

    private static bool IsNpcTag(string headUpper)
    {
        return headUpper is "N" or "NPC";
    }

    private static bool IsNarratorTag(string headUpper)
    {
        return headUpper is "S" or "SYS" or "NARR" or "NARRATOR";
    }

    private static bool IsInnerThoughtTag(string headUpper)
    {
        return headUpper is "I" or "INNER" or "THOUGHT";
    }

    private static bool IsMysteryTag(string headUpper)
    {
        return headUpper is "?" or "??" or "???" or "Q" or "UNKNOWN" or "M" or "MYSTERY";
    }

    private IEnumerator TypeLine(string text)
    {
        _isTyping = true;
        if (dialogueText == null)
        {
            StopTypingSfx();
            _isTyping = false;
            yield break;
        }

        string line = text ?? string.Empty;
        if (!string.IsNullOrEmpty(line))
            StartTypingSfx();
        else
            StopTypingSfx();
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
        StopTypingSfx();
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
        StopTypingSfx();
        _isTyping = false;
    }

    public void ForceCloseDialogue()
    {
        if (!_isRunning)
            return;

        _onComplete = null;
        _isRunning = false;
        _lines.Clear();
        _isTyping = false;
        if (_typeRoutine != null)
        {
            StopCoroutine(_typeRoutine);
            _typeRoutine = null;
        }
        StopTypingSfx();
        if (panelRoot != null)
            panelRoot.SetActive(false);

        ExitDialogueMode();
    }

    private void StartTypingSfx()
    {
        if (_typingAudioSource == null) return;
        var player = SfxPlayer.Instance;
        if (player == null) return;
        if (_typingAudioSource.isPlaying) return;

        var clip = player.PickClip(typingSfxId);
        if (clip == null) return;

        _typingAudioSource.clip = clip;
        _typingAudioSource.pitch = 1f;
        _typingAudioSource.Play();
    }

    private void StopTypingSfx()
    {
        if (_typingAudioSource != null && _typingAudioSource.isPlaying)
            _typingAudioSource.Stop();
    }
}

