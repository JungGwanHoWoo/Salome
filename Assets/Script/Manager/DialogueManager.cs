using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DialogueManager
/// - 대화 시스템 관리
/// - NPC 대화 처리
/// - 선택지 시스템
/// - 대화 진행 상태 관리
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    #region Dialogue Data

    [Header("Dialogue Database")]
    [SerializeField] private DialogueDatabase dialogueDatabase;

    private Dictionary<string, NPCDialogueData> npcDialogues;
    private DialogueNode currentNode;
    private NPCDialogueData currentNPC;
    private List<string> dialogueHistory;

    #endregion

    #region Dialogue State

    private bool isDialogueActive = false;
    private bool isWaitingForChoice = false;
    private int currentLineIndex = 0;

    public bool IsDialogueActive => isDialogueActive;
    public bool IsWaitingForChoice => isWaitingForChoice;
    public NPCDialogueData CurrentNPC => currentNPC;

    #endregion

    #region Events

    public event Action<NPCDialogueData> OnDialogueStarted;
    public event Action OnDialogueEnded;
    public event Action<DialogueLine> OnDialogueLineDisplayed;
    public event Action<DialogueChoice[]> OnChoicesPresented;
    public event Action<int> OnChoiceSelected;
    public event Action<string> OnFlagTriggered;

    #endregion

    #region Dependencies

    private GameStateManager gameStateManager;
    private GameFlowManager gameFlowManager;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    #endregion

    #region Initialization

    public void Initialize()
    {
        // 의존성 검색
        gameStateManager = FindObjectOfType<GameStateManager>();
        gameFlowManager = FindObjectOfType<GameFlowManager>();

        // 데이터 초기화
        npcDialogues = new Dictionary<string, NPCDialogueData>();
        dialogueHistory = new List<string>();

        // 대화 데이터베이스 로드
        LoadDialogueDatabase();

        Debug.Log("[DialogueManager] Initialized");
    }

    private void LoadDialogueDatabase()
    {
        if (dialogueDatabase == null)
        {
            Debug.LogWarning("[DialogueManager] No dialogue database assigned, creating defaults");
            CreateDefaultDialogues();
            return;
        }

        if (dialogueDatabase.npcDialogues != null)
        {
            foreach (var npcData in dialogueDatabase.npcDialogues)
            {
                if (npcData != null && !string.IsNullOrEmpty(npcData.npcID))
                {
                    npcDialogues[npcData.npcID] = npcData;
                }
            }
        }

        Debug.Log($"[DialogueManager] Loaded dialogues for {npcDialogues.Count} NPCs");
    }

    private void CreateDefaultDialogues()
    {
        // 기본 대화 데이터 생성 (예시)
        var butler = new NPCDialogueData
        {
            npcID = "Butler",
            npcName = "집사",
            defaultGreeting = "무엇을 도와드릴까요?"
        };

        npcDialogues["Butler"] = butler;
    }

    #endregion

    // =========================================================
    // 🔹 DIALOGUE CONTROL
    // =========================================================

    /// <summary>
    /// 대화 시작
    /// </summary>
    public bool StartDialogue(string npcID, string nodeID = "start")
    {
        if (isDialogueActive)
        {
            Debug.LogWarning("[DialogueManager] Dialogue already active");
            return false;
        }

        // NPC 데이터 가져오기
        if (!npcDialogues.TryGetValue(npcID, out var npcData))
        {
            Debug.LogError($"[DialogueManager] NPC not found: {npcID}");
            return false;
        }

        currentNPC = npcData;

        // 대화 노드 찾기
        DialogueNode node = FindDialogueNode(npcData, nodeID);

        if (node == null)
        {
            Debug.LogWarning($"[DialogueManager] Node not found: {nodeID}, using default greeting");
            ShowDefaultGreeting(npcData);
            return false;
        }

        // 대화 조건 체크
        if (!CheckDialogueConditions(node))
        {
            Debug.Log($"[DialogueManager] Dialogue conditions not met for {nodeID}");
            ShowDefaultGreeting(npcData);
            return false;
        }

        // 대화 시작
        currentNode = node;
        currentLineIndex = 0;
        isDialogueActive = true;
        isWaitingForChoice = false;

        // Phase 변경
        if (gameStateManager != null)
        {
            gameStateManager.SetPhase(GameStateManager.GamePhase.Dialogue);
        }

        // 이벤트 발생
        OnDialogueStarted?.Invoke(npcData);

        // 대화 기록
        string dialogueKey = $"talked_to_{npcID}_{gameStateManager?.CurrentChapter}";
        gameStateManager?.AddFlag(dialogueKey);
        dialogueHistory.Add($"{npcID}:{nodeID}");

        Debug.Log($"[DialogueManager] Started dialogue: {npcData.npcName} - {nodeID}");

        // 첫 대사 표시
        DisplayCurrentLine();

        return true;
    }

    /// <summary>
    /// 대화 종료
    /// </summary>
    public void EndDialogue()
    {
        if (!isDialogueActive)
            return;

        Debug.Log("[DialogueManager] Dialogue ended");

        isDialogueActive = false;
        isWaitingForChoice = false;
        currentNode = null;
        currentNPC = null;
        currentLineIndex = 0;

        // Phase 복원
        if (gameStateManager != null)
        {
            gameStateManager.SetPhase(GameStateManager.GamePhase.Exploration);
        }

        OnDialogueEnded?.Invoke();
    }

    /// <summary>
    /// 다음 대사로 진행
    /// </summary>
    public void AdvanceDialogue()
    {
        if (!isDialogueActive)
        {
            Debug.LogWarning("[DialogueManager] No active dialogue");
            return;
        }

        if (isWaitingForChoice)
        {
            Debug.LogWarning("[DialogueManager] Waiting for choice selection");
            return;
        }

        if (currentNode == null || currentNode.lines == null)
        {
            EndDialogue();
            return;
        }

        currentLineIndex++;

        // 모든 대사를 다 읽었는가?
        if (currentLineIndex >= currentNode.lines.Length)
        {
            // 선택지가 있는가?
            if (currentNode.choices != null && currentNode.choices.Length > 0)
            {
                PresentChoices();
            }
            // 다음 노드가 있는가?
            else if (!string.IsNullOrEmpty(currentNode.nextNodeID))
            {
                TransitionToNode(currentNode.nextNodeID);
            }
            // 대화 종료
            else
            {
                EndDialogue();
            }
        }
        else
        {
            // 다음 대사 표시
            DisplayCurrentLine();
        }
    }

    /// <summary>
    /// 현재 대사 표시
    /// </summary>
    private void DisplayCurrentLine()
    {
        if (currentNode == null || currentNode.lines == null || 
            currentLineIndex >= currentNode.lines.Length)
        {
            return;
        }

        DialogueLine line = currentNode.lines[currentLineIndex];

        // 이벤트 발생
        OnDialogueLineDisplayed?.Invoke(line);

        Debug.Log($"[DialogueManager] {line.speakerName}: {line.text}");

        // 대사별 효과 적용
        ApplyLineEffects(line);
    }

    /// <summary>
    /// 대사 효과 적용
    /// </summary>
    private void ApplyLineEffects(DialogueLine line)
    {
        // 플래그 트리거
        if (!string.IsNullOrEmpty(line.flagToSet))
        {
            gameStateManager?.AddFlag(line.flagToSet);
            OnFlagTriggered?.Invoke(line.flagToSet);
            Debug.Log($"[DialogueManager] Flag set: {line.flagToSet}");
        }

        // 감정 애니메이션
        if (line.emotion != EmotionType.None)
        {
            // NPCAnimator.PlayEmotion(line.emotion);
            Debug.Log($"[DialogueManager] Emotion: {line.emotion}");
        }

        // 사운드 재생
        if (!string.IsNullOrEmpty(line.soundEffect))
        {
            // AudioManager.PlaySFX(line.soundEffect);
        }
    }

    // =========================================================
    // 🔹 CHOICE SYSTEM
    // =========================================================

    /// <summary>
    /// 선택지 제시
    /// </summary>
    private void PresentChoices()
    {
        if (currentNode == null || currentNode.choices == null || currentNode.choices.Length == 0)
        {
            EndDialogue();
            return;
        }

        // 조건을 만족하는 선택지만 필터링
        List<DialogueChoice> availableChoices = new List<DialogueChoice>();

        foreach (var choice in currentNode.choices)
        {
            if (IsChoiceAvailable(choice))
            {
                availableChoices.Add(choice);
            }
        }

        if (availableChoices.Count == 0)
        {
            Debug.LogWarning("[DialogueManager] No available choices");
            EndDialogue();
            return;
        }

        isWaitingForChoice = true;

        // 이벤트 발생
        OnChoicesPresented?.Invoke(availableChoices.ToArray());

        Debug.Log($"[DialogueManager] Presenting {availableChoices.Count} choices");
    }

    /// <summary>
    /// 선택지 사용 가능 여부
    /// </summary>
    private bool IsChoiceAvailable(DialogueChoice choice)
    {
        if (choice == null)
            return false;

        // 필수 플래그 확인
        if (choice.requiredFlags != null && choice.requiredFlags.Length > 0)
        {
            foreach (var flag in choice.requiredFlags)
            {
                if (!gameStateManager.HasFlag(flag))
                    return false;
            }
        }

        // 금지 플래그 확인
        if (choice.forbiddenFlags != null && choice.forbiddenFlags.Length > 0)
        {
            foreach (var flag in choice.forbiddenFlags)
            {
                if (gameStateManager.HasFlag(flag))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 선택지 선택
    /// </summary>
    public void SelectChoice(int choiceIndex)
    {
        if (!isWaitingForChoice)
        {
            Debug.LogWarning("[DialogueManager] Not waiting for choice");
            return;
        }

        if (currentNode == null || currentNode.choices == null || 
            choiceIndex < 0 || choiceIndex >= currentNode.choices.Length)
        {
            Debug.LogError($"[DialogueManager] Invalid choice index: {choiceIndex}");
            return;
        }

        DialogueChoice selectedChoice = currentNode.choices[choiceIndex];

        Debug.Log($"[DialogueManager] Choice selected: {selectedChoice.choiceText}");

        isWaitingForChoice = false;

        // 선택 효과 적용
        ApplyChoiceEffects(selectedChoice);

        // 이벤트 발생
        OnChoiceSelected?.Invoke(choiceIndex);

        // 다음 노드로 이동
        if (!string.IsNullOrEmpty(selectedChoice.nextNodeID))
        {
            TransitionToNode(selectedChoice.nextNodeID);
        }
        else
        {
            EndDialogue();
        }
    }

    /// <summary>
    /// 선택지 효과 적용
    /// </summary>
    private void ApplyChoiceEffects(DialogueChoice choice)
    {
        // 플래그 설정
        if (!string.IsNullOrEmpty(choice.flagToSet))
        {
            gameStateManager?.AddFlag(choice.flagToSet);
            OnFlagTriggered?.Invoke(choice.flagToSet);
            Debug.Log($"[DialogueManager] Choice flag set: {choice.flagToSet}");
        }

        // 호감도 변화 (확장 가능)
        if (choice.affinityChange != 0)
        {
            // NPCAffinityManager.ChangeAffinity(currentNPC.npcID, choice.affinityChange);
            Debug.Log($"[DialogueManager] Affinity change: {choice.affinityChange}");
        }
    }

    // =========================================================
    // 🔹 NODE NAVIGATION
    // =========================================================

    /// <summary>
    /// 노드로 이동
    /// </summary>
    private void TransitionToNode(string nodeID)
    {
        if (currentNPC == null)
        {
            Debug.LogError("[DialogueManager] No current NPC");
            EndDialogue();
            return;
        }

        DialogueNode nextNode = FindDialogueNode(currentNPC, nodeID);

        if (nextNode == null)
        {
            Debug.LogWarning($"[DialogueManager] Next node not found: {nodeID}");
            EndDialogue();
            return;
        }

        // 조건 체크
        if (!CheckDialogueConditions(nextNode))
        {
            Debug.Log($"[DialogueManager] Next node conditions not met: {nodeID}");
            EndDialogue();
            return;
        }

        currentNode = nextNode;
        currentLineIndex = 0;

        Debug.Log($"[DialogueManager] Transitioned to node: {nodeID}");

        DisplayCurrentLine();
    }

    /// <summary>
    /// 대화 노드 찾기
    /// </summary>
    private DialogueNode FindDialogueNode(NPCDialogueData npcData, string nodeID)
    {
        if (npcData.dialogueNodes == null)
            return null;

        foreach (var node in npcData.dialogueNodes)
        {
            if (node.nodeID == nodeID)
                return node;
        }

        return null;
    }

    /// <summary>
    /// 대화 조건 확인
    /// </summary>
    private bool CheckDialogueConditions(DialogueNode node)
    {
        if (node.conditions == null)
            return true;

        // 필수 플래그
        if (node.conditions.requiredFlags != null)
        {
            foreach (var flag in node.conditions.requiredFlags)
            {
                if (!gameStateManager.HasFlag(flag))
                    return false;
            }
        }

        // 금지 플래그
        if (node.conditions.forbiddenFlags != null)
        {
            foreach (var flag in node.conditions.forbiddenFlags)
            {
                if (gameStateManager.HasFlag(flag))
                    return false;
            }
        }

        // 챕터 조건
        if (node.conditions.requiredChapter != null)
        {
            if (gameStateManager.CurrentChapter != node.conditions.requiredChapter)
                return false;
        }

        // 시간 조건
        if (node.conditions.requiredTimeSlot != null)
        {
            var timeManager = FindObjectOfType<TimeManager>();
            if (timeManager != null && timeManager.CurrentPeriod != node.conditions.requiredTimeSlot)
                return false;
        }

        return true;
    }

    // =========================================================
    // 🔹 HELPER METHODS
    // =========================================================

    /// <summary>
    /// 기본 인사말 표시
    /// </summary>
    private void ShowDefaultGreeting(NPCDialogueData npcData)
    {
        Debug.Log($"[DialogueManager] {npcData.npcName}: {npcData.defaultGreeting}");
        
        // 간단한 인사만 표시하고 바로 종료
        // UI에서 처리 필요
    }

    /// <summary>
    /// NPC와 대화했는지 확인
    /// </summary>
    public bool HasTalkedTo(string npcID)
    {
        if (gameStateManager == null)
            return false;

        string dialogueKey = $"talked_to_{npcID}_{gameStateManager.CurrentChapter}";
        return gameStateManager.HasFlag(dialogueKey);
    }

    /// <summary>
    /// 특정 대화를 진행했는지 확인
    /// </summary>
    public bool HasPlayedDialogue(string npcID, string nodeID)
    {
        string key = $"{npcID}:{nodeID}";
        return dialogueHistory.Contains(key);
    }

    /// <summary>
    /// NPC 정보 가져오기
    /// </summary>
    public NPCDialogueData GetNPCData(string npcID)
    {
        npcDialogues.TryGetValue(npcID, out var data);
        return data;
    }

    /// <summary>
    /// 대화 스킵
    /// </summary>
    public void SkipDialogue()
    {
        if (!isDialogueActive)
            return;

        Debug.Log("[DialogueManager] Dialogue skipped");
        EndDialogue();
    }

    // =========================================================
    // 🔹 SAVE/LOAD
    // =========================================================

    [System.Serializable]
    public class DialogueSaveData
    {
        public List<string> dialogueHistory;
    }

    public DialogueSaveData GetSaveData()
    {
        return new DialogueSaveData
        {
            dialogueHistory = new List<string>(dialogueHistory)
        };
    }

    public void LoadSaveData(DialogueSaveData data)
    {
        if (data == null)
        {
            Debug.LogError("[DialogueManager] Cannot load null save data");
            return;
        }

        dialogueHistory = data.dialogueHistory ?? new List<string>();

        Debug.Log("[DialogueManager] Save data loaded");
    }

    // =========================================================
    // 🔹 DEBUG
    // =========================================================

    public void PrintStatus()
    {
        Debug.Log("=== DIALOGUE MANAGER STATUS ===");
        Debug.Log($"Dialogue Active: {isDialogueActive}");
        Debug.Log($"Waiting for Choice: {isWaitingForChoice}");
        Debug.Log($"Current NPC: {currentNPC?.npcName ?? "None"}");
        Debug.Log($"Current Node: {currentNode?.nodeID ?? "None"}");
        Debug.Log($"Line Index: {currentLineIndex}");
        Debug.Log($"Dialogue History: {dialogueHistory.Count} entries");
    }

    #if UNITY_EDITOR
    [ContextMenu("Print Status")]
    private void DebugPrintStatus()
    {
        PrintStatus();
    }

    [ContextMenu("End Current Dialogue")]
    private void DebugEndDialogue()
    {
        EndDialogue();
    }
    #endif
}

// =========================================================
// 📦 DIALOGUE DATA STRUCTURES
// =========================================================

/// <summary>
/// 대화 데이터베이스 (ScriptableObject로 사용 권장)
/// </summary>
[System.Serializable]
public class DialogueDatabase
{
    public NPCDialogueData[] npcDialogues;
}

/// <summary>
/// NPC별 대화 데이터
/// </summary>
[System.Serializable]
public class NPCDialogueData
{
    [Header("NPC Info")]
    public string npcID;
    public string npcName;
    public Sprite npcPortrait;
    public string defaultGreeting = "안녕하세요.";

    [Header("Dialogue Nodes")]
    public DialogueNode[] dialogueNodes;
}

/// <summary>
/// 대화 노드 (대화 트리의 한 단위)
/// </summary>
[System.Serializable]
public class DialogueNode
{
    [Header("Node Info")]
    public string nodeID;
    public string nodeName;  // 에디터용

    [Header("Conditions")]
    public DialogueConditions conditions;

    [Header("Content")]
    public DialogueLine[] lines;

    [Header("Navigation")]
    public DialogueChoice[] choices;
    public string nextNodeID;  // 선택지 없을 때 다음 노드
}

/// <summary>
/// 대화 한 줄
/// </summary>
[System.Serializable]
public class DialogueLine
{
    public string speakerName;  // "집사", "탐정" 등
    [TextArea(2, 4)]
    public string text;

    [Header("Effects")]
    public EmotionType emotion = EmotionType.None;
    public string soundEffect;
    public string flagToSet;  // 이 대사를 하면 설정되는 플래그
}

/// <summary>
/// 대화 선택지
/// </summary>
[System.Serializable]
public class DialogueChoice
{
    [TextArea(1, 3)]
    public string choiceText;

    [Header("Conditions")]
    public string[] requiredFlags;  // 선택지를 보려면 필요한 플래그
    public string[] forbiddenFlags;  // 이 플래그가 있으면 선택지 숨김

    [Header("Effects")]
    public string flagToSet;  // 선택 시 설정되는 플래그
    public int affinityChange = 0;  // 호감도 변화

    [Header("Navigation")]
    public string nextNodeID;  // 이 선택지를 고르면 가는 노드
}

/// <summary>
/// 대화 조건
/// </summary>
[System.Serializable]
public class DialogueConditions
{
    public string[] requiredFlags;
    public string[] forbiddenFlags;
    public GameStateManager.Chapter? requiredChapter;
    public GameStateManager.TimeSlot? requiredTimeSlot;
}

/// <summary>
/// 감정 타입
/// </summary>
public enum EmotionType
{
    None,
    Happy,
    Sad,
    Angry,
    Surprised,
    Confused,
    Worried,
    Thinking
}