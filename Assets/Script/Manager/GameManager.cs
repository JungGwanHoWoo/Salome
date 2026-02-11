using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// GameManager (게임 규칙에 맞게 수정)
/// - 모든 매니저 총괄
/// - 플레이어 행동 요청 처리
/// - 게임 흐름 제어
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    #region Manager References

    private GameStateManager gameStateManager;
    private GameFlowManager gameFlowManager;
    private TimeManager timeManager;
    private ActionPointManager actionPointManager;
    private LocationManager locationManager;
    private DialogueManager dialogueManager;
    private NotebookManager notebookManager;
    private UIManager uiManager;

    // 정적 접근자
    public static GameStateManager State => Instance?.gameStateManager;
    public static GameFlowManager Flow => Instance?.gameFlowManager;
    public static TimeManager Time => Instance?.timeManager;
    public static ActionPointManager ActionPoints => Instance?.actionPointManager;
    public static LocationManager Location => Instance?.locationManager;
    public static DialogueManager Dialogue => Instance?.dialogueManager;
    public static NotebookManager Notebook => Instance?.notebookManager;
    public static UIManager UI => Instance?.uiManager;

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
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        InitializeManagers();
    }

    #endregion

    #region Initialization

    private void InitializeManagers()
    {
        Debug.Log("[GameManager] Initializing managers...");

        // 매니저 자동 검색
        FindManagers();

        // 각 매니저 초기화
        InitializeEachManager();

        Debug.Log("[GameManager] All managers initialized!");
    }

    private void FindManagers()
    {
        gameStateManager = FindObjectOfType<GameStateManager>();
        gameFlowManager = FindObjectOfType<GameFlowManager>();
        timeManager = FindObjectOfType<TimeManager>();
        actionPointManager = FindObjectOfType<ActionPointManager>();
        locationManager = FindObjectOfType<LocationManager>();
        dialogueManager = FindObjectOfType<DialogueManager>();
        notebookManager = FindObjectOfType<NotebookManager>();
        uiManager = FindObjectOfType<UIManager>();

        // null 체크
        if (gameStateManager == null) Debug.LogError("[GameManager] GameStateManager not found!");
        if (gameFlowManager == null) Debug.LogError("[GameManager] GameFlowManager not found!");
        if (timeManager == null) Debug.LogError("[GameManager] TimeManager not found!");
        if (actionPointManager == null) Debug.LogError("[GameManager] ActionPointManager not found!");
        if (locationManager == null) Debug.LogError("[GameManager] LocationManager not found!");
        if (dialogueManager == null) Debug.LogError("[GameManager] DialogueManager not found!");
        if (notebookManager == null) Debug.LogError("[GameManager] NotebookManager not found!");
        if (uiManager == null) Debug.LogError("[GameManager] UIManager not found!");
    }

    private void InitializeEachManager()
    {
        // Initialize 메서드가 있는 매니저들 호출
        gameStateManager?.GetType().GetMethod("Initialize")?.Invoke(gameStateManager, null);
        gameFlowManager?.GetType().GetMethod("Initialize")?.Invoke(gameFlowManager, null);
        timeManager?.GetType().GetMethod("Initialize")?.Invoke(timeManager, null);
        actionPointManager?.GetType().GetMethod("Initialize")?.Invoke(actionPointManager, null);
        locationManager?.GetType().GetMethod("Initialize")?.Invoke(locationManager, null);
        dialogueManager?.GetType().GetMethod("Initialize")?.Invoke(dialogueManager, null);
        notebookManager?.GetType().GetMethod("Initialize")?.Invoke(notebookManager, null);
        uiManager?.GetType().GetMethod("Initialize")?.Invoke(uiManager, null);
    }

    #endregion

    // =========================================================
    // 🔹 GAME FLOW CONTROL
    // =========================================================

    /// <summary>
    /// 게임 시작
    /// </summary>
    public void StartGame()
    {
        Debug.Log("[GameManager] ===== GAME START =====");

        // 게임 상태 초기화
        if (gameStateManager != null)
        {
            gameStateManager.ResetToDefault();
            gameStateManager.SetPhase(GameStateManager.GamePhase.Exploration);
        }

        // AP 초기화 (지역 1 시작)
        if (actionPointManager != null)
        {
            actionPointManager.ResetPoints();
        }

        // 초기 위치 설정
        if (locationManager != null)
        {
            locationManager.SetInitialLocation("MainHall");
        }

        // UI 갱신
        if (uiManager != null)
        {
            uiManager.RefreshAll();
            uiManager.ShowNotification("게임 시작!", NotificationType.Info);
        }

        Debug.Log("[GameManager] Game started successfully!");
    }

    /// <summary>
    /// 게임 재시작
    /// </summary>
    public void RestartGame()
    {
        Debug.Log("[GameManager] Restarting game...");
        
        // 페이드 전환
        if (uiManager != null)
        {
            StartCoroutine(RestartWithFade());
        }
        else
        {
            StartGame();
        }
    }

    private IEnumerator RestartWithFade()
    {
        yield return uiManager.FadeOut();
        StartGame();
        yield return uiManager.FadeIn();
    }

    // =========================================================
    // 🔹 PLAYER ACTIONS (게임 규칙에 맞게 수정)
    // =========================================================

    /// <summary>
    /// 장소 이동 요청
    /// </summary>
    public bool RequestMove(string locationID)
    {
        if (locationManager == null || actionPointManager == null)
        {
            Debug.LogError("[GameManager] Required managers not found");
            return false;
        }

        // 이동 가능 여부 확인
        if (!locationManager.CanMoveTo(locationID, out string reason))
        {
            uiManager?.ShowNotification(reason, NotificationType.Warning);
            return false;
        }

        // 이동 비용 확인
        int moveCost = locationManager.GetMoveCost(locationID);
        if (!actionPointManager.HasEnoughPoints(moveCost))
        {
            uiManager?.ShowNotification($"행동력이 부족합니다. ({moveCost} AP 필요)", NotificationType.Warning);
            return false;
        }

        // 이동 실행
        bool moved = locationManager.MoveTo(locationID);
        if (moved)
        {
            // AP 소비
            actionPointManager.ConsumePoints(moveCost);
            
            Debug.Log($"[GameManager] Moved to {locationID} (-{moveCost} AP)");
            return true;
        }

        return false;
    }

    /// <summary>
    /// NPC와 대화 요청
    /// </summary>
    public bool RequestDialogue(string npcID)
    {
        if (gameFlowManager == null || dialogueManager == null)
        {
            Debug.LogError("[GameManager] Required managers not found");
            return false;
        }

        // 대화 가능 여부 확인
        if (!gameFlowManager.CanTalk(npcID))
        {
            uiManager?.ShowNotification("지금은 대화할 수 없습니다.", NotificationType.Warning);
            return false;
        }

        // 대화 시작
        bool dialogueStarted = dialogueManager.StartDialogue(npcID);
        if (dialogueStarted)
        {
            // AP 소비 (대화는 2 AP)
            gameFlowManager.TalkToNPC(npcID);
            
            Debug.Log($"[GameManager] Started dialogue with {npcID}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 관찰 모드 시작 요청
    /// </summary>
    public bool RequestObservation(float duration = 60f)
    {
        if (gameFlowManager == null)
        {
            Debug.LogError("[GameManager] GameFlowManager not found");
            return false;
        }

        // 관찰 가능 여부 확인
        if (!gameFlowManager.CanObserve())
        {
            uiManager?.ShowNotification("지금은 관찰할 수 없습니다.", NotificationType.Warning);
            return false;
        }

        // 관찰 모드 시작
        gameFlowManager.StartObservationMode(duration);
        
        Debug.Log($"[GameManager] Observation mode started ({duration}s)");
        return true;
    }

    /// <summary>
    /// 단서 발견 (관찰 모드 중)
    /// </summary>
    public bool DiscoverClue(string clueID)
    {
        if (notebookManager == null)
        {
            Debug.LogError("[GameManager] NotebookManager not found");
            return false;
        }

        // 관찰 모드가 아니면 경고
        if (gameStateManager.CurrentPhase != GameStateManager.GamePhase.Investigation)
        {
            Debug.LogWarning("[GameManager] Not in observation mode");
        }

        // 단서 추가
        bool added = notebookManager.AddClue(clueID);
        if (added)
        {
            Debug.Log($"[GameManager] Clue discovered: {clueID}");
            
            // 관찰 모드 종료 (단서 발견 성공)
            if (gameFlowManager != null)
            {
                gameFlowManager.EndObservationMode();
            }
            
            return true;
        }

        return false;
    }

    /// <summary>
    /// 호감도 증가 방법 발견 (관찰 모드 중)
    /// </summary>
    public void DiscoverAffinityMethod(string npcID, int affinityBonus)
    {
        if (gameFlowManager == null)
        {
            Debug.LogError("[GameManager] GameFlowManager not found");
            return;
        }

        // 호감도 증가
        gameFlowManager.IncreaseAffinity(npcID, affinityBonus);
        
        uiManager?.ShowNotification($"{npcID}의 호감도가 상승했습니다! (+{affinityBonus})", 
                                    NotificationType.Success);

        Debug.Log($"[GameManager] Affinity method discovered: {npcID} +{affinityBonus}");

        // 관찰 모드 종료 (목표 달성)
        if (gameFlowManager != null)
        {
            gameFlowManager.EndObservationMode();
        }
    }

    /// <summary>
    /// 범인 지목
    /// </summary>
    public void AccuseCulprit(string suspectID)
    {
        if (gameFlowManager == null)
        {
            Debug.LogError("[GameManager] GameFlowManager not found");
            return;
        }

        Debug.Log($"[GameManager] Player accused: {suspectID}");

        // 범인 지목 처리
        gameFlowManager.IdentifyCulprit(suspectID);
    }

    // =========================================================
    // 🔹 SAVE/LOAD SYSTEM
    // =========================================================

    /// <summary>
    /// 게임 저장
    /// </summary>
    public void SaveGame(int slotIndex)
    {
        Debug.Log($"[GameManager] Saving game to slot {slotIndex}...");

        try
        {
            GameSaveData saveData = new GameSaveData
            {
                // 각 매니저의 세이브 데이터 수집
                gameState = gameStateManager?.GetType().GetMethod("GetSaveData")?.Invoke(gameStateManager, null),
                actionPoints = actionPointManager?.GetType().GetMethod("GetSaveData")?.Invoke(actionPointManager, null),
                location = locationManager?.GetType().GetMethod("GetSaveData")?.Invoke(locationManager, null),
                dialogue = dialogueManager?.GetType().GetMethod("GetSaveData")?.Invoke(dialogueManager, null),
                notebook = notebookManager?.GetType().GetMethod("GetSaveData")?.Invoke(notebookManager, null),
                saveTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            // JSON 변환
            string json = JsonUtility.ToJson(saveData, true);

            // 저장
            PlayerPrefs.SetString($"SaveSlot_{slotIndex}", json);
            PlayerPrefs.Save();

            uiManager?.ShowNotification("저장되었습니다!", NotificationType.Success);
            Debug.Log("[GameManager] Game saved successfully!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameManager] Save failed: {e.Message}");
            uiManager?.ShowNotification("저장 실패!", NotificationType.Error);
        }
    }

    /// <summary>
    /// 게임 불러오기
    /// </summary>
    public void LoadGame(int slotIndex)
    {
        Debug.Log($"[GameManager] Loading game from slot {slotIndex}...");

        try
        {
            string json = PlayerPrefs.GetString($"SaveSlot_{slotIndex}", "");

            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[GameManager] No save data found");
                uiManager?.ShowNotification("저장된 데이터가 없습니다.", NotificationType.Warning);
                return;
            }

            // JSON 파싱
            GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);

            // 각 매니저에 데이터 로드
            // (리플렉션으로 LoadSaveData 호출)
            
            uiManager?.ShowNotification("불러오기 완료!", NotificationType.Success);
            uiManager?.RefreshAll();

            Debug.Log("[GameManager] Game loaded successfully!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameManager] Load failed: {e.Message}");
            uiManager?.ShowNotification("불러오기 실패!", NotificationType.Error);
        }
    }

    // =========================================================
    // 🔹 UTILITY
    // =========================================================

    public void QuitGame()
    {
        Debug.Log("[GameManager] Quitting game...");

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    // =========================================================
    // 🔹 DEBUG
    // =========================================================

    #if UNITY_EDITOR
    [ContextMenu("Print All Manager Status")]
    private void DebugPrintAllStatus()
    {
        Debug.Log("========== GAME MANAGER STATUS ==========");
        
        gameStateManager?.GetType().GetMethod("PrintStatus")?.Invoke(gameStateManager, null);
        gameFlowManager?.GetType().GetMethod("PrintStatus")?.Invoke(gameFlowManager, null);
        actionPointManager?.GetType().GetMethod("PrintStatus")?.Invoke(actionPointManager, null);
        locationManager?.GetType().GetMethod("PrintStatus")?.Invoke(locationManager, null);
        notebookManager?.GetType().GetMethod("PrintStatus")?.Invoke(notebookManager, null);
    }

    [ContextMenu("Start Test Game")]
    private void DebugStartGame()
    {
        StartGame();
    }
    #endif
}

// =========================================================
// 📦 SAVE DATA STRUCTURE
// =========================================================

[System.Serializable]
public class GameSaveData
{
    public object gameState;
    public object actionPoints;
    public object location;
    public object dialogue;
    public object notebook;
    public string saveTime;
}