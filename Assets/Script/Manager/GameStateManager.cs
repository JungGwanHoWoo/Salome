using System;
using System.Collections.Generic;
using UnityEngine;

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    #region ENUMS

    public enum GamePhase
    {
        Title,
        Exploration,
        Investigation,
        Dialogue,
        Cutscene,
        Ending
    }

    public enum Chapter
    {
        Prologue,
        Spring,
        Summer,
        Autumn,
        Winter,
        Finale
    }

    public enum TimeSlot
    {
        Morning,
        Afternoon,
        Evening,
        Night
    }

    #endregion

    #region CURRENT STATE

    public GamePhase CurrentPhase { get; private set; }
    public Chapter CurrentChapter { get; private set; }
    public TimeSlot CurrentTimeSlot { get; private set; }
    public string CurrentLocation { get; private set; }

    #endregion

    #region TIME LIMIT SYSTEM

    [Header("Time Limit Settings")]
    [SerializeField] private int maxTimeActions = 12;  // 챕터당 허용 행동 수
    private int remainingTimeActions;  // 남은 행동 수
    
    public int RemainingTimeActions => remainingTimeActions;
    public int MaxTimeActions => maxTimeActions;
    public bool IsTimeUp => remainingTimeActions <= 0;
    public float TimeProgress => 1f - ((float)remainingTimeActions / maxTimeActions);  // 0~1

    #endregion

    #region WORLD FLAGS

    private HashSet<string> globalFlags = new HashSet<string>();

    #endregion

    #region LOCATION DATA

    private HashSet<string> visitedLocations = new HashSet<string>();

    #endregion

    #region EVENTS

    public event Action<GamePhase> OnPhaseChanged;
    public event Action<Chapter> OnChapterChanged;
    public event Action<TimeSlot> OnTimeChanged;
    public event Action<string> OnLocationChanged;
    public event Action<string> OnFlagAdded;
    public event Action<string> OnFlagRemoved;
    public event Action<int> OnTimeActionUsed;  // 행동 소비 시
    public event Action OnTimeUp;  // 시간 소진 시

    #endregion

    #region UNITY LIFECYCLE

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Initialize();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #endregion

    #region INITIALIZE

    private void Initialize()
    {
        CurrentPhase = GamePhase.Title;
        CurrentChapter = Chapter.Prologue;
        CurrentTimeSlot = TimeSlot.Morning;
        CurrentLocation = "Lobby";
        
        remainingTimeActions = maxTimeActions;
        
        globalFlags.Clear();
        visitedLocations.Clear();
        visitedLocations.Add(CurrentLocation);
    }

    #endregion

    // =========================================================
    // 🔹 PHASE CONTROL
    // =========================================================

    public void SetPhase(GamePhase phase)
    {
        if (CurrentPhase == phase)
            return;

        GamePhase previousPhase = CurrentPhase;
        CurrentPhase = phase;
        OnPhaseChanged?.Invoke(CurrentPhase);

        Debug.Log($"[GameState] Phase Changed: {previousPhase} → {phase}");
    }

    // =========================================================
    // 🔹 CHAPTER CONTROL
    // =========================================================

    public void SetChapter(Chapter chapter)
    {
        if (CurrentChapter == chapter)
            return;

        Chapter previousChapter = CurrentChapter;
        CurrentChapter = chapter;
        OnChapterChanged?.Invoke(CurrentChapter);

        // ✅ 챕터 변경 시 시간 리셋
        ResetTimeForNewChapter();

        Debug.Log($"[GameState] Chapter Changed: {previousChapter} → {chapter}");
    }

    public void AdvanceChapter()
    {
        if (CurrentChapter == Chapter.Finale)
        {
            Debug.LogWarning("[GameState] Already at final chapter");
            return;
        }

        int next = (int)CurrentChapter + 1;
        SetChapter((Chapter)next);
    }

    private void ResetTimeForNewChapter()
    {
        remainingTimeActions = maxTimeActions;
        CurrentTimeSlot = TimeSlot.Morning;
        
        Debug.Log($"[GameState] Time reset for new chapter: {remainingTimeActions} actions available");
    }

    // =========================================================
    // 🔹 TIME CONTROL (탐사 제한시간)
    // =========================================================

    /// <summary>
    /// 시간을 소비하는 행동 (이동, 조사, 대화 등)
    /// </summary>
    public bool ConsumeTimeAction(int amount = 1)
    {
        if (IsTimeUp)
        {
            Debug.LogWarning("[GameState] No time remaining!");
            return false;
        }

        remainingTimeActions -= amount;
        remainingTimeActions = Mathf.Max(0, remainingTimeActions);

        // 시간대 자동 업데이트
        UpdateTimeSlotBasedOnProgress();

        OnTimeActionUsed?.Invoke(remainingTimeActions);
        
        Debug.Log($"[GameState] Time consumed: {amount} | Remaining: {remainingTimeActions}/{maxTimeActions}");

        // 시간 소진 체크
        if (IsTimeUp)
        {
            HandleTimeUp();
        }

        return true;
    }

    /// <summary>
    /// 진행도에 따라 시간대 자동 변경
    /// </summary>
    private void UpdateTimeSlotBasedOnProgress()
    {
        float progress = TimeProgress;
        TimeSlot newSlot;

        if (progress < 0.25f)
            newSlot = TimeSlot.Morning;
        else if (progress < 0.5f)
            newSlot = TimeSlot.Afternoon;
        else if (progress < 0.75f)
            newSlot = TimeSlot.Evening;
        else
            newSlot = TimeSlot.Night;

        if (newSlot != CurrentTimeSlot)
        {
            SetTimeSlot(newSlot);
        }
    }

    private void SetTimeSlot(TimeSlot time)
    {
        if (CurrentTimeSlot == time)
            return;

        TimeSlot previousTime = CurrentTimeSlot;
        CurrentTimeSlot = time;
        OnTimeChanged?.Invoke(CurrentTimeSlot);

        Debug.Log($"[GameState] Time Changed: {previousTime} → {time}");
    }

    /// <summary>
    /// 시간 소진 시 처리
    /// </summary>
    private void HandleTimeUp()
    {
        Debug.LogWarning("[GameState] ⏰ TIME UP!");
        OnTimeUp?.Invoke();
        
        // 여기서 게임오버 또는 강제 진행 처리
        // 예: SetPhase(GamePhase.Ending);
    }


    /// <summary>
    /// 시간을 소비하지 않는 행동인지 체크
    /// </summary>
    public bool CanPerformAction()
    {
        return !IsTimeUp;
    }

    // =========================================================
    // 🔹 LOCATION CONTROL
    // =========================================================

    /// <summary>
    /// 위치 이동 (시간 소비)
    /// </summary>
    public bool MoveToLocation(string location, int timeCost = 1)
    {
        if (string.IsNullOrEmpty(location))
        {
            Debug.LogError("[GameState] Cannot set null or empty location");
            return false;
        }

        if (CurrentLocation == location)
        {
            Debug.Log("[GameState] Already at this location");
            return false;
        }

        // 시간 소비
        if (!ConsumeTimeAction(timeCost))
        {
            return false;
        }

        string previousLocation = CurrentLocation;
        CurrentLocation = location;
        visitedLocations.Add(location);

        OnLocationChanged?.Invoke(CurrentLocation);

        Debug.Log($"[GameState] Location Changed: {previousLocation} → {location} (Cost: {timeCost})");
        return true;
    }

    /// <summary>
    /// 위치만 변경 (시간 소비 없음 - 컷신 등)
    /// </summary>
    public void SetLocationWithoutTimeCost(string location)
    {
        if (string.IsNullOrEmpty(location))
        {
            Debug.LogError("[GameState] Cannot set null or empty location");
            return;
        }

        if (CurrentLocation == location)
            return;

        string previousLocation = CurrentLocation;
        CurrentLocation = location;
        visitedLocations.Add(location);

        OnLocationChanged?.Invoke(CurrentLocation);

        Debug.Log($"[GameState] Location Changed (No Cost): {previousLocation} → {location}");
    }

    public bool HasVisited(string location)
    {
        return visitedLocations.Contains(location);
    }

    public List<string> GetVisitedLocations()
    {
        return new List<string>(visitedLocations);
    }

    // =========================================================
    // 🔹 GLOBAL FLAG CONTROL
    // =========================================================

    public void AddFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag))
        {
            Debug.LogError("[GameState] Cannot add null or empty flag");
            return;
        }

        if (globalFlags.Add(flag))
        {
            OnFlagAdded?.Invoke(flag);
            Debug.Log($"[GameState] Flag Added → {flag}");
        }
    }

    public bool HasFlag(string flag)
    {
        return globalFlags.Contains(flag);
    }

    public void RemoveFlag(string flag)
    {
        if (globalFlags.Remove(flag))
        {
            OnFlagRemoved?.Invoke(flag);
            Debug.Log($"[GameState] Flag Removed → {flag}");
        }
    }

    public List<string> GetAllFlags()
    {
        return new List<string>(globalFlags);
    }

    public void ClearAllFlags()
    {
        globalFlags.Clear();
        Debug.Log("[GameState] All flags cleared");
    }

    // =========================================================
    // 🔹 QUERY METHODS
    // =========================================================

    public bool IsPhase(GamePhase phase)
    {
        return CurrentPhase == phase;
    }

    public bool IsChapter(Chapter chapter)
    {
        return CurrentChapter == chapter;
    }

    public bool IsTimeSlot(TimeSlot timeSlot)
    {
        return CurrentTimeSlot == timeSlot;
    }

    public bool IsAtLocation(string location)
    {
        return CurrentLocation == location;
    }

    public string GetTimeRemainingText()
    {
        return $"{remainingTimeActions} / {maxTimeActions}";
    }

    public string GetTimeSlotDescription()
    {
        switch (CurrentTimeSlot)
        {
            case TimeSlot.Morning:   return "아침";
            case TimeSlot.Afternoon: return "오후";
            case TimeSlot.Evening:   return "저녁";
            case TimeSlot.Night:     return "밤";
            default: return "알 수 없음";
        }
    }

    // =========================================================
    // 🔹 DEBUG METHODS
    // =========================================================

    public void PrintCurrentState()
    {
        Debug.Log($"=== GAME STATE ===\n" +
                  $"Phase: {CurrentPhase}\n" +
                  $"Chapter: {CurrentChapter}\n" +
                  $"Time: {CurrentTimeSlot} ({remainingTimeActions}/{maxTimeActions} actions)\n" +
                  $"Location: {CurrentLocation}\n" +
                  $"Flags: {string.Join(", ", globalFlags)}\n" +
                  $"Visited: {visitedLocations.Count} locations");
    }

    // =========================================================
    // 🔹 SAVE DATA
    // =========================================================

    [System.Serializable]
    public class GameStateSaveData
    {
        public GamePhase phase;
        public Chapter chapter;
        public TimeSlot timeSlot;
        public string location;
        public int remainingTimeActions;
        public int maxTimeActions;
        public List<string> flags;
        public List<string> visitedLocations;
    }

    public GameStateSaveData GetSaveData()
    {
        return new GameStateSaveData
        {
            phase = CurrentPhase,
            chapter = CurrentChapter,
            timeSlot = CurrentTimeSlot,
            location = CurrentLocation,
            remainingTimeActions = remainingTimeActions,
            maxTimeActions = maxTimeActions,
            flags = new List<string>(globalFlags),
            visitedLocations = new List<string>(visitedLocations)
        };
    }

    public void LoadSaveData(GameStateSaveData data)
    {
        if (data == null)
        {
            Debug.LogError("[GameState] Cannot load null save data");
            return;
        }

        CurrentPhase = data.phase;
        CurrentChapter = data.chapter;
        CurrentTimeSlot = data.timeSlot;
        CurrentLocation = data.location;
        remainingTimeActions = data.remainingTimeActions;
        maxTimeActions = data.maxTimeActions;

        globalFlags = new HashSet<string>(data.flags ?? new List<string>());
        visitedLocations = new HashSet<string>(data.visitedLocations ?? new List<string>());

        OnPhaseChanged?.Invoke(CurrentPhase);
        OnChapterChanged?.Invoke(CurrentChapter);
        OnTimeChanged?.Invoke(CurrentTimeSlot);
        OnLocationChanged?.Invoke(CurrentLocation);

        Debug.Log("[GameState] Save data loaded successfully");
    }

    public void ResetToDefault()
    {
        Initialize();
        Debug.Log("[GameState] Reset to default state");
    }
}