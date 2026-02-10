using UnityEngine;

/// <summary>
/// GameManager
/// - 게임 전체 흐름의 시작점
/// - 다른 Manager들의 생성 및 초기화 담당
/// - 매니저 간 통신 중재
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    #region Manager References

    // ✅ 자동으로 찾기 (Inspector 할당 불필요)
    private GameStateManager gameStateManager;
    private GameFlowManager gameFlowManager;
    private TimeManager timeManager;
    private ActionPointManager actionPointManager;
    private LocationManager locationManager;
    private DialogueManager dialogueManager;
    private NotebookManager notebookManager;
    private UIManager uiManager;

    // ✅ 외부 접근용 프로퍼티 (읽기 전용)
    public static GameStateManager State => Instance?.gameStateManager;
    public static GameFlowManager Flow => Instance?.gameFlowManager;
    public static TimeManager Time => Instance?.timeManager;
    public static ActionPointManager ActionPoints => Instance?.actionPointManager;
    public static LocationManager Location => Instance?.locationManager;
    public static DialogueManager Dialogue => Instance?.dialogueManager;
    public static NotebookManager Notebook => Instance?.notebookManager;
    public static UIManager UI => Instance?.uiManager;

    #endregion

    private bool isInitialized = false;

    #region Unity Lifecycle

    private void Awake()
    {
        // Singleton 처리
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        FindAndValidateManagers();
        InitializeManagers();
    }

    private void Start()
    {
        StartGame();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #endregion

    #region Manager Discovery

    /// <summary>
    /// Scene에서 매니저들을 자동으로 찾음
    /// </summary>
    private void FindAndValidateManagers()
    {
        // ✅ 자동 검색
        gameStateManager = FindObjectOfType<GameStateManager>();
        gameFlowManager = FindObjectOfType<GameFlowManager>();
        timeManager = FindObjectOfType<TimeManager>();
        actionPointManager = FindObjectOfType<ActionPointManager>();
        locationManager = FindObjectOfType<LocationManager>();
        dialogueManager = FindObjectOfType<DialogueManager>();
        notebookManager = FindObjectOfType<NotebookManager>();
        uiManager = FindObjectOfType<UIManager>();

        // ✅ 필수 매니저 검증 (없으면 에러)
        ValidateManager(gameStateManager, "GameStateManager");
        ValidateManager(gameFlowManager, "GameFlowManager");
        ValidateManager(timeManager, "TimeManager");
        ValidateManager(actionPointManager, "ActionPointManager");
        ValidateManager(locationManager, "LocationManager");
        ValidateManager(dialogueManager, "DialogueManager");
        ValidateManager(notebookManager, "NotebookManager");
        ValidateManager(uiManager, "UIManager");
    }

    private void ValidateManager<T>(T manager, string managerName) where T : Object
    {
        if (manager == null)
        {
            Debug.LogError($"[GameManager] {managerName} not found in scene!");
        }
    }

    #endregion

    #region Initialization

    /// <summary>
    /// 모든 Manager 초기화
    /// </summary>
    private void InitializeManagers()
    {
        if (isInitialized)
        {
            Debug.LogWarning("[GameManager] Already initialized");
            return;
        }

        Debug.Log("[GameManager] Initializing managers...");

        // ✅ 초기화 순서 중요 (의존성 순서대로)
        // 1. 상태 관련 (의존성 없음)
        InitializeIfExists(gameStateManager, "GameState");
        InitializeIfExists(timeManager, "Time");
        InitializeIfExists(actionPointManager, "ActionPoint");
        
        // 2. 콘텐츠 관련
        InitializeIfExists(locationManager, "Location");
        InitializeIfExists(notebookManager, "Notebook");
        InitializeIfExists(dialogueManager, "Dialogue");
        
        // 3. 흐름 제어 (다른 매니저 참조)
        InitializeIfExists(gameFlowManager, "GameFlow");
        
        // 4. UI (마지막, 모든 데이터 필요)
        InitializeIfExists(uiManager, "UI");

        isInitialized = true;
        Debug.Log("[GameManager] ✅ All managers initialized");
    }

    private void InitializeIfExists(MonoBehaviour manager, string name)
    {
        if (manager == null)
        {
            Debug.LogWarning($"[GameManager] {name}Manager is null, skipping initialization");
            return;
        }

        // ✅ 리플렉션으로 Initialize() 메서드 호출
        var initMethod = manager.GetType().GetMethod("Initialize", 
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        
        if (initMethod != null)
        {
            initMethod.Invoke(manager, null);
            Debug.Log($"[GameManager] {name}Manager initialized");
        }
        else
        {
            Debug.LogWarning($"[GameManager] {name}Manager has no Initialize() method");
        }
    }

    #endregion

    #region Game Flow Control

    /// <summary>
    /// 게임 시작
    /// </summary>
    public void StartGame()
    {
        if (!isInitialized)
        {
            Debug.LogError("[GameManager] Cannot start game - not initialized");
            return;
        }

        Debug.Log("[GameManager] Starting game...");

        // ✅ null 체크 후 호출
        gameStateManager?.ResetToDefault();
        // timeManager?.ResetTime();
        actionPointManager?.ResetPoints();

        // 시작 위치 지정
        locationManager?.SetInitialLocation("MainHall");

        // UI 초기화
        uiManager?.Initialize();  // UI는 데이터 로드 후 갱신

        Debug.Log("[GameManager] ✅ Game started");
    }

    /// <summary>
    /// 게임 재시작
    /// </summary>
    public void RestartGame()
    {
        Debug.Log("[GameManager] Restarting game...");
        
        // 모든 상태 초기화
        StartGame();
    }

    // /// <summary>
    // /// 게임 일시정지
    // /// </summary>
    // public void PauseGame()
    // {
    //     Time.timeScale = 0f;
    //     Debug.Log("[GameManager] Game paused");
    // }

    // /// <summary>
    // /// 게임 재개
    // /// </summary>
    // public void ResumeGame()
    // {
    //     Time.timeScale = 1f;
    //     Debug.Log("[GameManager] Game resumed");
    // }

    #endregion

    #region Player Actions (UI/Input에서 호출)

    /// <summary>
    /// 위치 이동 요청
    /// </summary>
    public bool RequestMove(string locationID)
    {
        if (!CanPerformAction())
            return false;

        // ✅ 이동 가능 여부 체크
        // if (!gameFlowManager.CanMove(locationID))
        // {
        //     Debug.Log($"[GameManager] Cannot move to {locationID}");
        //     return false;
        // }

        // 이동 실행
        bool success = locationManager.MoveTo(locationID);
        
        // if (success)
        // {
        //     // 행동력 소비
        //     gameFlowManager.ConsumeAction(ActionType.Move);
        //     Debug.Log($"[GameManager] Moved to {locationID}");
        // }

        return success;
    }

    /// <summary>
    /// NPC 대화 요청
    /// </summary>
    public bool RequestDialogue(string npcID)
    {
        if (!CanPerformAction())
            return false;

        if (!gameFlowManager.CanTalk(npcID))
        {
            Debug.Log($"[GameManager] Cannot talk to {npcID}");
            return false;
        }

        // 대화 시작
        dialogueManager.StartDialogue(npcID);
        
        // 행동력 소비 (대화 완료 후에 소비하는 게 나을 수도 있음)
        // gameFlowManager.ConsumeAction(ActionType.Talk);
        
        Debug.Log($"[GameManager] Started dialogue with {npcID}");
        return true;
    }

    /// <summary>
    /// 조사 요청
    /// </summary>
    public bool RequestInvestigation(string clueID)
    {
        if (!CanPerformAction())
            return false;

        // if (!gameFlowManager.CanInvestigate(clueID))
        // {
        //     Debug.Log($"[GameManager] Cannot investigate {clueID}");
        //     return false;
        // }

        // 단서 획득
        bool success = notebookManager.AddClue(clueID);
        
        // if (success)
        // {
        //     gameFlowManager.ConsumeAction(ActionType.Investigate);
        //     Debug.Log($"[GameManager] Investigated {clueID}");
        // }

        return success;
    }

    /// <summary>
    /// 추리 시도 (행동력 소비 안 함)
    /// </summary>
    public void RequestDeduction()
    {
        // 추리는 행동력 소비 안 함 (플레이어의 사고)
        notebookManager?.OpenDeductionMode();
    }

    /// <summary>
    /// 행동 가능 여부 체크
    /// </summary>
    private bool CanPerformAction()
    {
        if (gameStateManager == null)
        {
            Debug.LogError("[GameManager] GameStateManager is null");
            return false;
        }

        // 특정 페이즈에서만 행동 가능
        var currentPhase = gameStateManager.CurrentPhase;
        
        if (currentPhase == GameStateManager.GamePhase.Cutscene ||
            currentPhase == GameStateManager.GamePhase.Ending)
        {
            Debug.Log("[GameManager] Cannot perform action during cutscene/ending");
            return false;
        }

        return true;
    }

    #endregion

    #region Save/Load

    /// <summary>
    /// 게임 저장
    /// </summary>
    // public void SaveGame(int slotIndex = 0)
    // {
    //     Debug.Log($"[GameManager] Saving game to slot {slotIndex}...");
        
    //     // 각 매니저에서 데이터 수집
    //     var saveData = new GameSaveData
    //     {
    //         stateData = gameStateManager?.GetSaveData(),
    //         timeData = timeManager?.GetSaveData(),
    //         actionData = actionPointManager?.GetSaveData(),
    //         locationData = locationManager?.GetSaveData(),
    //         notebookData = notebookManager?.GetSaveData(),
    //         // ... 다른 데이터들
    //     };

    //     // JSON으로 저장
    //     string json = JsonUtility.ToJson(saveData, true);
    //     PlayerPrefs.SetString($"SaveSlot_{slotIndex}", json);
    //     PlayerPrefs.Save();

    //     Debug.Log("[GameManager] ✅ Game saved");
    // }

    /// <summary>
    /// 게임 로드
    /// </summary>
    // public void LoadGame(int slotIndex = 0)
    // {
    //     Debug.Log($"[GameManager] Loading game from slot {slotIndex}...");

    //     string json = PlayerPrefs.GetString($"SaveSlot_{slotIndex}", "");
        
    //     if (string.IsNullOrEmpty(json))
    //     {
    //         Debug.LogWarning("[GameManager] No save data found");
    //         return;
    //     }

    //     GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);

    //     // 각 매니저에 데이터 로드
    //     gameStateManager?.LoadSaveData(saveData.stateData);
    //     timeManager?.LoadSaveData(saveData.timeData);
    //     actionPointManager?.LoadSaveData(saveData.actionData);
    //     locationManager?.LoadSaveData(saveData.locationData);
    //     notebookManager?.LoadSaveData(saveData.notebookData);

    //     // UI 갱신
    //     uiManager?.RefreshAll();

    //     Debug.Log("[GameManager] ✅ Game loaded");
    // }

    #endregion

    #region Debug

    // public void PrintStatus()
    // {
    //     Debug.Log("=== GAME MANAGER STATUS ===");
    //     Debug.Log($"Initialized: {isInitialized}");
    //     Debug.Log($"GameState: {gameStateManager?.CurrentPhase}");
    //     Debug.Log($"Location: {gameStateManager?.CurrentLocation}");
    //     Debug.Log($"Time: {timeManager?.GetCurrentTime()}");
    //     Debug.Log($"Action Points: {actionPointManager?.GetRemainingPoints()}");
    // }

    #endregion
}

// =========================================================
// 📦 데이터 구조체
// =========================================================

/// <summary>
/// 행동 타입
/// </summary>
public enum ActionType
{
    Move,         // 이동
    Talk,         // 대화
    Investigate,  // 조사
    Rest          // 휴식 (시간만 소비)
}

/// <summary>
/// 전체 세이브 데이터
/// </summary>
[System.Serializable]
public class GameSaveData
{
    public GameStateManager.GameStateSaveData stateData;
    public object timeData;  // TimeManager.SaveData로 교체
    public object actionData;  // ActionPointManager.SaveData로 교체
    public object locationData;
    public object notebookData;
}