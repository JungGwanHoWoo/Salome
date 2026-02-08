using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameFlowManager
/// - 게임 진행 흐름 제어
/// - 행동 가능 여부 판단
/// - 챕터 진행 및 엔딩 조건 체크
/// - 행동력 소비 처리
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    #region Dependencies

    private GameStateManager gameStateManager;
    private TimeManager timeManager;
    private ActionPointManager actionPointManager;

    #endregion

    #region Chapter Progress

    [Header("Chapter Configuration")]
    [SerializeField] private ChapterConfig[] chapterConfigs;

    private Dictionary<GameStateManager.Chapter, ChapterConfig> chapterConfigMap;

    #endregion

    #region Action Costs

    [Header("Action Costs")]
    [SerializeField] private int moveCost = 1;
    [SerializeField] private int talkCost = 2;
    [SerializeField] private int investigateCost = 1;
    [SerializeField] private int restCost = 0;  // 휴식은 시간만 소비

    #endregion

    #region Events

    public event Action OnChapterCompleted;
    public event Action OnGameOver;
    public event Action<ActionType, int> OnActionConsumed;  // 행동 타입, 소비량

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
        // 의존성 자동 검색
        gameStateManager = FindObjectOfType<GameStateManager>();
        timeManager = FindObjectOfType<TimeManager>();
        actionPointManager = FindObjectOfType<ActionPointManager>();

        if (gameStateManager == null)
            Debug.LogError("[GameFlowManager] GameStateManager not found!");
        if (timeManager == null)
            Debug.LogError("[GameFlowManager] TimeManager not found!");
        if (actionPointManager == null)
            Debug.LogError("[GameFlowManager] ActionPointManager not found!");

        // 챕터 설정 맵 생성
        BuildChapterConfigMap();

        // 이벤트 구독
        SubscribeToEvents();

        Debug.Log("[GameFlowManager] Initialized");
    }

    private void BuildChapterConfigMap()
    {
        chapterConfigMap = new Dictionary<GameStateManager.Chapter, ChapterConfig>();

        if (chapterConfigs != null)
        {
            foreach (var config in chapterConfigs)
            {
                chapterConfigMap[config.chapter] = config;
            }
        }

        // 기본 설정이 없으면 자동 생성
        if (chapterConfigMap.Count == 0)
        {
            CreateDefaultChapterConfigs();
        }
    }

    private void CreateDefaultChapterConfigs()
    {
        Debug.LogWarning("[GameFlowManager] No chapter configs found, creating defaults");

        chapterConfigMap = new Dictionary<GameStateManager.Chapter, ChapterConfig>
        {
            { GameStateManager.Chapter.Prologue, new ChapterConfig 
                { 
                    chapter = GameStateManager.Chapter.Prologue,
                    requiredClues = new string[] { "intro_clue" },
                    minActionsRequired = 3
                }
            },
            { GameStateManager.Chapter.Spring, new ChapterConfig 
                { 
                    chapter = GameStateManager.Chapter.Spring,
                    requiredClues = new string[] { "spring_clue_1", "spring_clue_2" },
                    minActionsRequired = 5
                }
            },
            { GameStateManager.Chapter.Summer, new ChapterConfig 
                { 
                    chapter = GameStateManager.Chapter.Summer,
                    requiredClues = new string[] { "summer_clue_1", "summer_clue_2" },
                    minActionsRequired = 5
                }
            },
            { GameStateManager.Chapter.Autumn, new ChapterConfig 
                { 
                    chapter = GameStateManager.Chapter.Autumn,
                    requiredClues = new string[] { "autumn_clue_1", "autumn_clue_2" },
                    minActionsRequired = 5
                }
            },
            { GameStateManager.Chapter.Winter, new ChapterConfig 
                { 
                    chapter = GameStateManager.Chapter.Winter,
                    requiredClues = new string[] { "winter_clue_1", "winter_clue_2" },
                    minActionsRequired = 5
                }
            },
            { GameStateManager.Chapter.Finale, new ChapterConfig 
                { 
                    chapter = GameStateManager.Chapter.Finale,
                    requiredClues = new string[] { "final_truth" },
                    minActionsRequired = 3
                }
            }
        };
    }

    private void SubscribeToEvents()
    {
        if (actionPointManager != null)
        {
            actionPointManager.OnActionPointsZero += HandleActionPointsZero;
        }

        if (gameStateManager != null)
        {
            gameStateManager.OnChapterChanged += HandleChapterChanged;
        }
    }

    #endregion

    // =========================================================
    // 🔹 ACTION VALIDATION (행동 가능 여부 체크)
    // =========================================================

    /// <summary>
    /// 이동 가능 여부
    /// </summary>
    public bool CanMove(string locationID = null)
    {
        // 기본 체크
        if (!CanPerformAnyAction())
            return false;

        // 행동력 체크
        if (!actionPointManager.HasEnoughPoints(moveCost))
        {
            Debug.Log("[GameFlowManager] Not enough action points to move");
            return false;
        }

        // 특정 위치 제약 체크 (옵션)
        if (!string.IsNullOrEmpty(locationID))
        {
            // 예: 특정 플래그가 있어야 갈 수 있는 장소
            if (locationID == "SecretRoom" && !gameStateManager.HasFlag("found_secret_key"))
            {
                Debug.Log("[GameFlowManager] Secret room requires key");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 대화 가능 여부
    /// </summary>
    public bool CanTalk(string npcID)
    {
        if (!CanPerformAnyAction())
            return false;

        if (!actionPointManager.HasEnoughPoints(talkCost))
        {
            Debug.Log("[GameFlowManager] Not enough action points to talk");
            return false;
        }

        // NPC 특정 조건 체크
        if (string.IsNullOrEmpty(npcID))
            return true;

        // 예: 이미 대화한 NPC는 이번 챕터에서 다시 못 만남
        string talkFlag = $"talked_to_{npcID}_{gameStateManager.CurrentChapter}";
        if (gameStateManager.HasFlag(talkFlag))
        {
            Debug.Log($"[GameFlowManager] Already talked to {npcID} this chapter");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 조사 가능 여부
    /// </summary>
    public bool CanInvestigate(string clueID)
    {
        if (!CanPerformAnyAction())
            return false;

        if (!actionPointManager.HasEnoughPoints(investigateCost))
        {
            Debug.Log("[GameFlowManager] Not enough action points to investigate");
            return false;
        }

        // 이미 조사한 단서는 다시 조사 불가
        if (gameStateManager.HasFlag($"investigated_{clueID}"))
        {
            Debug.Log($"[GameFlowManager] Already investigated {clueID}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 휴식 가능 여부 (시간만 소비)
    /// </summary>
    public bool CanRest()
    {
        return CanPerformAnyAction();
    }

    /// <summary>
    /// 기본적인 행동 가능 여부
    /// </summary>
    private bool CanPerformAnyAction()
    {
        if (gameStateManager == null)
            return false;

        // Phase 체크
        var phase = gameStateManager.CurrentPhase;
        if (phase == GameStateManager.GamePhase.Cutscene ||
            phase == GameStateManager.GamePhase.Ending ||
            phase == GameStateManager.GamePhase.Title)
        {
            Debug.Log("[GameFlowManager] Cannot perform actions in current phase");
            return false;
        }

        // 시간 소진 체크
        if (gameStateManager.IsTimeUp)
        {
            Debug.Log("[GameFlowManager] Time is up");
            return false;
        }

        return true;
    }

    // =========================================================
    // 🔹 ACTION CONSUMPTION (행동 소비 처리)
    // =========================================================

    /// <summary>
    /// 행동 소비
    /// </summary>
    public void ConsumeAction(ActionType actionType)
    {
        int cost = GetActionCost(actionType);

        if (cost > 0)
        {
            // 행동력 소비
            actionPointManager.ConsumePoints(cost);
        }

        // 시간 소비 (모든 행동은 1칸 소비)
        gameStateManager.ConsumeTimeAction(1);

        // 행동별 플래그 설정
        ApplyActionFlags(actionType);

        // 이벤트 발생
        OnActionConsumed?.Invoke(actionType, cost);

        Debug.Log($"[GameFlowManager] Consumed {actionType}: {cost} AP, 1 time slot");

        // 챕터 완료 조건 체크
        CheckChapterCompletion();
    }

    private int GetActionCost(ActionType actionType)
    {
        switch (actionType)
        {
            case ActionType.Move:
                return moveCost;
            case ActionType.Talk:
                return talkCost;
            case ActionType.Investigate:
                return investigateCost;
            case ActionType.Rest:
                return restCost;
            default:
                return 0;
        }
    }

    private void ApplyActionFlags(ActionType actionType)
    {
        // 행동 카운트 플래그 (통계용)
        string countFlag = $"action_count_{actionType}";
        // 이건 실제로는 int를 저장해야 하므로, 별도 시스템 필요
        // 여기서는 예시로만 표시
    }

    // =========================================================
    // 🔹 CHAPTER PROGRESSION (챕터 진행)
    // =========================================================

    /// <summary>
    /// 챕터 완료 조건 체크
    /// </summary>
    public void CheckChapterCompletion()
    {
        var currentChapter = gameStateManager.CurrentChapter;

        if (!chapterConfigMap.TryGetValue(currentChapter, out var config))
        {
            Debug.LogWarning($"[GameFlowManager] No config for chapter {currentChapter}");
            return;
        }

        // 필수 단서를 모두 찾았는지 체크
        bool hasAllClues = CheckRequiredClues(config.requiredClues);
        
        if (!hasAllClues)
            return;

        // 최소 행동 수 체크 (너무 빨리 끝나는 것 방지)
        // 이건 별도 카운팅 시스템이 필요할 수 있음
        
        Debug.Log($"[GameFlowManager] Chapter {currentChapter} completed!");
        OnChapterCompleted?.Invoke();

        // 자동 진행 여부 (또는 플레이어가 수동으로 진행)
        // AdvanceToNextChapter();
    }

    private bool CheckRequiredClues(string[] requiredClues)
    {
        if (requiredClues == null || requiredClues.Length == 0)
            return true;

        foreach (var clue in requiredClues)
        {
            if (!gameStateManager.HasFlag($"clue_{clue}"))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 다음 챕터로 진행
    /// </summary>
    public void AdvanceToNextChapter()
    {
        var currentChapter = gameStateManager.CurrentChapter;

        if (currentChapter == GameStateManager.Chapter.Finale)
        {
            Debug.Log("[GameFlowManager] Already at final chapter");
            TriggerEnding();
            return;
        }

        // 챕터 전환 컷신
        gameStateManager.SetPhase(GameStateManager.GamePhase.Cutscene);

        // 다음 챕터로
        gameStateManager.AdvanceChapter();

        // 컷신 후 탐색 모드로
        gameStateManager.SetPhase(GameStateManager.GamePhase.Exploration);

        Debug.Log($"[GameFlowManager] Advanced to {gameStateManager.CurrentChapter}");
    }

    // =========================================================
    // 🔹 GAME ENDING (게임 종료)
    // =========================================================

    /// <summary>
    /// 엔딩 트리거
    /// </summary>
    public void TriggerEnding()
    {
        gameStateManager.SetPhase(GameStateManager.GamePhase.Ending);

        // 엔딩 타입 결정
        EndingType ending = DetermineEnding();

        Debug.Log($"[GameFlowManager] Triggering ending: {ending}");

        // 엔딩 처리 (UI, 컷신 등)
        // EndingManager.ShowEnding(ending);
    }

    private EndingType DetermineEnding()
    {
        // 수집한 단서, 선택지, 플래그에 따라 엔딩 결정
        
        // 모든 진실을 밝혔는지
        bool foundAllTruths = gameStateManager.HasFlag("revealed_all_truths");
        
        // 특정 NPC를 구했는지
        bool savedNPC = gameStateManager.HasFlag("saved_npc");
        
        // 범인을 올바르게 지목했는지
        bool correctCulprit = gameStateManager.HasFlag("correct_culprit");

        if (foundAllTruths && savedNPC && correctCulprit)
            return EndingType.TrueEnding;
        else if (correctCulprit)
            return EndingType.GoodEnding;
        else if (foundAllTruths)
            return EndingType.NormalEnding;
        else
            return EndingType.BadEnding;
    }

    // =========================================================
    // 🔹 EVENT HANDLERS
    // =========================================================

    private void HandleActionPointsZero()
    {
        Debug.LogWarning("[GameFlowManager] Action points depleted!");
        
        // 행동력이 0이 되면 게임오버 또는 강제 휴식
        // 옵션 1: 게임오버
        // TriggerGameOver();
        
        // 옵션 2: 강제로 시간만 보내기
        // ForceRest();
    }

    private void HandleChapterChanged(GameStateManager.Chapter newChapter)
    {
        Debug.Log($"[GameFlowManager] Chapter changed to {newChapter}");
        
        // 챕터 시작 이벤트 처리
        // 예: 챕터별 오프닝 컷신
    }

    private void TriggerGameOver()
    {
        gameStateManager.SetPhase(GameStateManager.GamePhase.Ending);
        OnGameOver?.Invoke();
        
        Debug.Log("[GameFlowManager] GAME OVER");
    }

    // =========================================================
    // 🔹 HELPER METHODS
    // =========================================================

    /// <summary>
    /// 현재 챕터 진행률 (0~1)
    /// </summary>
    public float GetChapterProgress()
    {
        var currentChapter = gameStateManager.CurrentChapter;

        if (!chapterConfigMap.TryGetValue(currentChapter, out var config))
            return 0f;

        if (config.requiredClues == null || config.requiredClues.Length == 0)
            return 1f;

        int foundClues = 0;
        foreach (var clue in config.requiredClues)
        {
            if (gameStateManager.HasFlag($"clue_{clue}"))
                foundClues++;
        }

        return (float)foundClues / config.requiredClues.Length;
    }

    /// <summary>
    /// 행동 가능 횟수 계산
    /// </summary>
    public int GetRemainingActions()
    {
        if (actionPointManager == null)
            return 0;

        int points = actionPointManager.RemainingPoints;
        
        // 가장 저렴한 행동 기준으로 계산
        int minCost = Mathf.Min(moveCost, investigateCost);
        if (minCost <= 0) minCost = 1;

        return points / minCost;
    }

    public void PrintStatus()
    {
        Debug.Log("=== GAME FLOW STATUS ===");
        Debug.Log($"Chapter: {gameStateManager?.CurrentChapter}");
        Debug.Log($"Phase: {gameStateManager?.CurrentPhase}");
        Debug.Log($"Chapter Progress: {GetChapterProgress() * 100:F0}%");
        Debug.Log($"Remaining Actions: {GetRemainingActions()}");
        Debug.Log($"Action Costs - Move:{moveCost} Talk:{talkCost} Investigate:{investigateCost}");
    }
}

// =========================================================
// 📦 데이터 구조체
// =========================================================

/// <summary>
/// 챕터별 설정
/// </summary>
[System.Serializable]
public class ChapterConfig
{
    public GameStateManager.Chapter chapter;
    
    [Tooltip("챕터 완료에 필요한 단서들")]
    public string[] requiredClues;
    
    [Tooltip("최소 행동 횟수 (너무 빨리 끝나는 것 방지)")]
    public int minActionsRequired = 5;
    
    [Tooltip("챕터 제한 시간 (0이면 무제한)")]
    public int maxTimeSlots = 0;
}

/// <summary>
/// 엔딩 타입
/// </summary>
public enum EndingType
{
    BadEnding,      // 나쁜 엔딩
    NormalEnding,   // 보통 엔딩
    GoodEnding,     // 좋은 엔딩
    TrueEnding      // 진엔딩
}