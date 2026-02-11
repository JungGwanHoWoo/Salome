using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameFlowManager (게임 규칙에 맞게 개선)
/// - 행동력 기반 지역 진행
/// - 관찰 모드 관리
/// - NPC 호감도 시스템
/// - 범인 지목 및 엔딩
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    #region Dependencies

    private GameStateManager gameStateManager;
    private ActionPointManager actionPointManager;
    private NotebookManager notebookManager;

    #endregion

    #region Region Progress

    [Header("Region Configuration")]
    [SerializeField] private RegionConfig[] regionConfigs;
    
    private int currentRegionIndex = 0;
    private Dictionary<string, RegionConfig> regionMap;

    #endregion

    #region NPC Affinity System

    private Dictionary<string, int> npcAffinity;  // NPC 호감도 (0~100)

    #endregion

    #region Events

    public event Action<string> OnRegionCompleted;  // 지역 완료
    public event Action<string> OnRegionChanged;  // 지역 변경
    public event Action OnAllRegionsCompleted;  // 모든 지역 완료 (범인 지목 시작)
    public event Action<string, int> OnAffinityChanged;  // NPC, 호감도
    public event Action OnObservationModeStarted;  // 관찰 모드 시작
    public event Action OnObservationModeEnded;  // 관찰 모드 종료

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
        actionPointManager = FindObjectOfType<ActionPointManager>();
        notebookManager = FindObjectOfType<NotebookManager>();

        // 데이터 초기화
        regionMap = new Dictionary<string, RegionConfig>();
        npcAffinity = new Dictionary<string, int>();

        // 지역 설정 로드
        LoadRegionConfigs();

        // 이벤트 구독
        SubscribeToEvents();

        Debug.Log("[GameFlowManager] Initialized");
    }

    private void LoadRegionConfigs()
    {
        if (regionConfigs != null)
        {
            foreach (var region in regionConfigs)
            {
                regionMap[region.regionID] = region;
            }
        }

        Debug.Log($"[GameFlowManager] Loaded {regionMap.Count} regions");
    }

    private void SubscribeToEvents()
    {
        if (actionPointManager != null)
        {
            actionPointManager.OnActionPointsZero += HandleActionPointsZero;
        }
    }

    #endregion

    // =========================================================
    // 🔹 ACTION VALIDATION
    // =========================================================

    /// <summary>
    /// 대화 가능 여부
    /// </summary>
    public bool CanTalk(string npcID)
    {
        if (!CanPerformAction())
            return false;

        // AP 충분?
        if (!actionPointManager.HasEnoughPoints(2))  // 대화는 2 AP
        {
            Debug.Log("[GameFlowManager] Not enough AP to talk");
            return false;
        }

        // 이미 이 지역에서 대화했나?
        string talkFlag = $"talked_to_{npcID}_region_{currentRegionIndex}";
        if (gameStateManager.HasFlag(talkFlag))
        {
            Debug.Log($"[GameFlowManager] Already talked to {npcID} in this region");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 관찰 가능 여부
    /// </summary>
    public bool CanObserve()
    {
        if (!CanPerformAction())
            return false;

        // 관찰 모드는 AP 소비 없음
        return true;
    }

    private bool CanPerformAction()
    {
        if (gameStateManager == null)
            return false;

        var phase = gameStateManager.CurrentPhase;
        if (phase == GameStateManager.GamePhase.Cutscene ||
            phase == GameStateManager.GamePhase.Ending)
        {
            return false;
        }

        return true;
    }

    // =========================================================
    // 🔹 ACTION EXECUTION
    // =========================================================

    /// <summary>
    /// NPC와 대화 (알리바이 획득)
    /// </summary>
    public void TalkToNPC(string npcID)
    {
        if (!CanTalk(npcID))
            return;

        // AP 소비 (2)
        actionPointManager.ConsumePoints(2);

        // 대화 플래그 설정
        string talkFlag = $"talked_to_{npcID}_region_{currentRegionIndex}";
        gameStateManager.AddFlag(talkFlag);

        Debug.Log($"[GameFlowManager] Talked to {npcID} (-2 AP)");
    }

    /// <summary>
    /// 관찰 모드 시작
    /// </summary>
    public void StartObservationMode(float duration = 60f)
    {
        if (!CanObserve())
            return;

        // Phase 변경
        gameStateManager.SetPhase(GameStateManager.GamePhase.Investigation);

        // 타이머 시작
        TimeManager.Instance.StartObservationTimer(duration);

        // 이벤트 발생
        OnObservationModeStarted?.Invoke();

        Debug.Log($"[GameFlowManager] Observation mode started ({duration}s)");
    }

    /// <summary>
    /// 관찰 모드 종료
    /// </summary>
    public void EndObservationMode()
    {
        // Phase 복원
        gameStateManager.SetPhase(GameStateManager.GamePhase.Exploration);

        // 타이머 중지
        if (TimeManager.Instance.IsTimerRunning)
        {
            TimeManager.Instance.StopTimer();
        }

        // 이벤트 발생
        OnObservationModeEnded?.Invoke();

        Debug.Log("[GameFlowManager] Observation mode ended");
    }

    // =========================================================
    // 🔹 NPC AFFINITY SYSTEM
    // =========================================================

    /// <summary>
    /// 호감도 증가
    /// </summary>
    public void IncreaseAffinity(string npcID, int amount)
    {
        if (amount <= 0) return;

        if (!npcAffinity.ContainsKey(npcID))
        {
            npcAffinity[npcID] = 0;
        }

        int previousAffinity = npcAffinity[npcID];
        npcAffinity[npcID] = Mathf.Min(npcAffinity[npcID] + amount, 100);

        OnAffinityChanged?.Invoke(npcID, npcAffinity[npcID]);

        Debug.Log($"[GameFlowManager] {npcID} affinity: {previousAffinity} → {npcAffinity[npcID]} (+{amount})");

        // 호감도에 따른 추가 정보 제공
        CheckAffinityBonuses(npcID);
    }

    /// <summary>
    /// 호감도 감소
    /// </summary>
    public void DecreaseAffinity(string npcID, int amount)
    {
        if (amount <= 0) return;

        if (!npcAffinity.ContainsKey(npcID))
        {
            npcAffinity[npcID] = 50;  // 기본값
        }

        int previousAffinity = npcAffinity[npcID];
        npcAffinity[npcID] = Mathf.Max(npcAffinity[npcID] - amount, 0);

        OnAffinityChanged?.Invoke(npcID, npcAffinity[npcID]);

        Debug.Log($"[GameFlowManager] {npcID} affinity: {previousAffinity} → {npcAffinity[npcID]} (-{amount})");
    }

    /// <summary>
    /// 호감도 조회
    /// </summary>
    public int GetAffinity(string npcID)
    {
        return npcAffinity.ContainsKey(npcID) ? npcAffinity[npcID] : 0;
    }

    /// <summary>
    /// 호감도 보너스 체크
    /// </summary>
    private void CheckAffinityBonuses(string npcID)
    {
        int affinity = GetAffinity(npcID);

        // 호감도 단계별 보너스
        if (affinity >= 80 && !gameStateManager.HasFlag($"{npcID}_affinity_80"))
        {
            gameStateManager.AddFlag($"{npcID}_affinity_80");
            Debug.Log($"[GameFlowManager] {npcID} 호감도 80 달성! 진실에 가까운 정보 획득 가능!");
        }
        else if (affinity >= 60 && !gameStateManager.HasFlag($"{npcID}_affinity_60"))
        {
            gameStateManager.AddFlag($"{npcID}_affinity_60");
            Debug.Log($"[GameFlowManager] {npcID} 호감도 60 달성! 추가 정보 해금!");
        }
        else if (affinity >= 40 && !gameStateManager.HasFlag($"{npcID}_affinity_40"))
        {
            gameStateManager.AddFlag($"{npcID}_affinity_40");
            Debug.Log($"[GameFlowManager] {npcID} 호감도 40 달성!");
        }
    }

    // =========================================================
    // 🔹 REGION PROGRESSION
    // =========================================================

    /// <summary>
    /// AP 소진 시 다음 지역으로
    /// </summary>
    private void HandleActionPointsZero()
    {
        Debug.LogWarning("[GameFlowManager] Action points depleted!");

        // 현재 지역 완료
        CompleteCurrentRegion();
    }

    /// <summary>
    /// 현재 지역 완료
    /// </summary>
    public void CompleteCurrentRegion()
    {
        string currentRegion = GetCurrentRegionID();
        
        OnRegionCompleted?.Invoke(currentRegion);

        Debug.Log($"[GameFlowManager] Region completed: {currentRegion}");

        // 다음 지역으로 이동
        MoveToNextRegion();
    }

    /// <summary>
    /// 다음 지역으로 이동
    /// </summary>
    private void MoveToNextRegion()
    {
        currentRegionIndex++;

        // 모든 지역 완료?
        if (currentRegionIndex >= regionConfigs.Length)
        {
            HandleAllRegionsCompleted();
            return;
        }

        // AP 회복
        actionPointManager.ResetPoints();

        // 새 지역 시작
        string newRegion = GetCurrentRegionID();
        OnRegionChanged?.Invoke(newRegion);

        Debug.Log($"[GameFlowManager] Moved to region: {newRegion}");
    }

    /// <summary>
    /// 모든 지역 완료 (범인 지목 단계)
    /// </summary>
    private void HandleAllRegionsCompleted()
    {
        Debug.Log("[GameFlowManager] All regions completed! Time to identify the culprit!");

        OnAllRegionsCompleted?.Invoke();

        // 범인 지목 페이즈로 전환
        gameStateManager.SetPhase(GameStateManager.GamePhase.Investigation);
    }

    /// <summary>
    /// 현재 지역 ID
    /// </summary>
    public string GetCurrentRegionID()
    {
        if (currentRegionIndex < regionConfigs.Length)
        {
            return regionConfigs[currentRegionIndex].regionID;
        }
        return "Finale";
    }

    // =========================================================
    // 🔹 CULPRIT IDENTIFICATION & ENDING
    // =========================================================

    /// <summary>
    /// 범인 지목
    /// </summary>
    public void IdentifyCulprit(string suspectID)
    {
        Debug.Log($"[GameFlowManager] Player identified culprit: {suspectID}");

        // 정답 확인
        bool isCorrect = CheckCulprit(suspectID);

        if (isCorrect)
        {
            gameStateManager.AddFlag("correct_culprit");
            Debug.Log("[GameFlowManager] ✓ Correct culprit!");
        }
        else
        {
            Debug.Log("[GameFlowManager] ✗ Wrong culprit...");
        }

        // 엔딩 결정
        TriggerEnding();
    }

    /// <summary>
    /// 범인 정답 확인
    /// </summary>
    private bool CheckCulprit(string suspectID)
    {
        // 실제 범인 ID (게임 데이터에서 설정)
        string actualCulprit = "Chef";  // 예시
        return suspectID == actualCulprit;
    }

    /// <summary>
    /// 엔딩 트리거
    /// </summary>
    public void TriggerEnding()
    {
        gameStateManager.SetPhase(GameStateManager.GamePhase.Ending);

        EndingType ending = DetermineEnding();

        Debug.Log($"[GameFlowManager] Ending: {ending}");

        // 엔딩 연출
        // EndingManager.ShowEnding(ending);
    }

    /// <summary>
    /// 엔딩 결정
    /// </summary>
    private EndingType DetermineEnding()
    {
        // 수집한 알리바이 (단서) 개수
        int clueCount = notebookManager.DiscoveredCluesCount;
        int totalClues = notebookManager.TotalCluesCount;
        float cluePercent = (float)clueCount / totalClues;

        // 평균 호감도
        int totalAffinity = 0;
        int npcCount = 0;
        foreach (var affinity in npcAffinity.Values)
        {
            totalAffinity += affinity;
            npcCount++;
        }
        float avgAffinity = npcCount > 0 ? (float)totalAffinity / npcCount : 0f;

        // 범인 정답 여부
        bool correctCulprit = gameStateManager.HasFlag("correct_culprit");

        // 엔딩 판정
        if (correctCulprit && cluePercent >= 0.9f && avgAffinity >= 70f)
        {
            return EndingType.TrueEnding;  // 진엔딩
        }
        else if (correctCulprit && cluePercent >= 0.7f)
        {
            return EndingType.GoodEnding;  // 좋은 엔딩
        }
        else if (correctCulprit)
        {
            return EndingType.NormalEnding;  // 보통 엔딩
        }
        else
        {
            return EndingType.BadEnding;  // 나쁜 엔딩
        }
    }

    // =========================================================
    // 🔹 HELPER METHODS
    // =========================================================

    public void PrintStatus()
    {
        Debug.Log("=== GAME FLOW MANAGER STATUS ===");
        Debug.Log($"Current Region: {GetCurrentRegionID()} ({currentRegionIndex + 1}/{regionConfigs.Length})");
        Debug.Log($"AP Remaining: {actionPointManager.RemainingPoints}");
        Debug.Log($"NPC Affinity:");
        foreach (var kvp in npcAffinity)
        {
            Debug.Log($"  {kvp.Key}: {kvp.Value}");
        }
    }
}

// =========================================================
// 📦 DATA STRUCTURES
// =========================================================

/// <summary>
/// 지역 설정
/// </summary>
[System.Serializable]
public class RegionConfig
{
    public string regionID;
    public string regionName;
    public string[] npcsInRegion;  // 이 지역의 NPC들
    public float observationTime = 60f;  // 관찰 제한시간
}

/// <summary>
/// 엔딩 타입
/// </summary>
public enum EndingType
{
    BadEnding,
    NormalEnding,
    GoodEnding,
    TrueEnding
}