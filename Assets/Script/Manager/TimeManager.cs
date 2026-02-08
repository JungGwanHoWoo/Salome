using System;
using UnityEngine;

/// <summary>
/// TimeManager
/// - 게임 내 시간 관리 (Morning, Afternoon, Evening, Night)
/// - 시간 경과에 따른 이벤트 처리
/// - NPC 스케줄, 환경 변화 등과 연동
/// </summary>
public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    #region Time Settings

    [Header("Time Configuration")]
    [SerializeField] private int maxTimeSlots = 12;  // 챕터당 최대 시간 슬롯
    [SerializeField] private bool autoAdvanceTime = true;  // 행동 시 자동 시간 진행

    private int currentTimeSlot;  // 현재 시간 슬롯 (0부터 시작)
    private GameStateManager.TimeSlot currentPeriod;  // 현재 시간대

    #endregion

    #region Time State

    public int CurrentTimeSlot => currentTimeSlot;
    public int MaxTimeSlots => maxTimeSlots;
    public int RemainingTimeSlots => maxTimeSlots - currentTimeSlot;
    public GameStateManager.TimeSlot CurrentPeriod => currentPeriod;
    public bool IsTimeUp => currentTimeSlot >= maxTimeSlots;
    public float TimeProgress => (float)currentTimeSlot / maxTimeSlots;  // 0~1

    #endregion

    #region Events

    public event Action<int> OnTimeSlotChanged;  // 시간 슬롯 변경 (남은 슬롯 수)
    public event Action<GameStateManager.TimeSlot> OnTimePeriodChanged;  // 시간대 변경
    public event Action OnTimeUp;  // 시간 소진
    public event Action<int> OnTimeWarning;  // 시간 경고 (남은 슬롯)

    #endregion

    #region Time Period Configuration

    [Header("Time Period Thresholds")]
    [SerializeField] [Range(0f, 1f)] private float afternoonThreshold = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float eveningThreshold = 0.50f;
    [SerializeField] [Range(0f, 1f)] private float nightThreshold = 0.75f;

    [Header("Warning Settings")]
    [SerializeField] private int warningThreshold = 3;  // 남은 시간 N칸 이하일 때 경고

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
        ResetTime();
        Debug.Log("[TimeManager] Initialized");
    }

    /// <summary>
    /// 시간 초기화 (챕터 시작 시)
    /// </summary>
    public void ResetTime()
    {
        currentTimeSlot = 0;
        currentPeriod = GameStateManager.TimeSlot.Morning;
        
        Debug.Log($"[TimeManager] Time reset: {maxTimeSlots} slots available");
    }

    #endregion

    // =========================================================
    // 🔹 TIME PROGRESSION
    // =========================================================

    /// <summary>
    /// 시간 경과 (1 슬롯 소비)
    /// </summary>
    public bool AdvanceTime(int slots = 1)
    {
        if (IsTimeUp)
        {
            Debug.LogWarning("[TimeManager] Cannot advance time - time is up!");
            OnTimeUp?.Invoke();
            return false;
        }

        int previousSlot = currentTimeSlot;
        currentTimeSlot += slots;
        currentTimeSlot = Mathf.Min(currentTimeSlot, maxTimeSlots);

        // 시간대 업데이트
        UpdateTimePeriod();

        // 이벤트 발생
        OnTimeSlotChanged?.Invoke(RemainingTimeSlots);

        Debug.Log($"[TimeManager] Time advanced: {previousSlot} → {currentTimeSlot} " +
                  $"({RemainingTimeSlots} slots remaining)");

        // 경고 체크
        CheckTimeWarning();

        // 시간 소진 체크
        if (IsTimeUp)
        {
            HandleTimeUp();
        }

        return true;
    }

    /// <summary>
    /// 시간대 업데이트 (진행도에 따라)
    /// </summary>
    private void UpdateTimePeriod()
    {
        float progress = TimeProgress;
        GameStateManager.TimeSlot newPeriod;

        if (progress < afternoonThreshold)
            newPeriod = GameStateManager.TimeSlot.Morning;
        else if (progress < eveningThreshold)
            newPeriod = GameStateManager.TimeSlot.Afternoon;
        else if (progress < nightThreshold)
            newPeriod = GameStateManager.TimeSlot.Evening;
        else
            newPeriod = GameStateManager.TimeSlot.Night;

        if (newPeriod != currentPeriod)
        {
            GameStateManager.TimeSlot previousPeriod = currentPeriod;
            currentPeriod = newPeriod;

            OnTimePeriodChanged?.Invoke(currentPeriod);

            Debug.Log($"[TimeManager] Time period changed: {previousPeriod} → {currentPeriod}");

            // 시간대 변경에 따른 추가 처리
            ApplyTimePeriodEffects(currentPeriod);
        }
    }

    /// <summary>
    /// 시간대 효과 적용
    /// </summary>
    private void ApplyTimePeriodEffects(GameStateManager.TimeSlot period)
    {
        // 시간대에 따른 게임 변화
        switch (period)
        {
            case GameStateManager.TimeSlot.Morning:
                // 밝은 조명, 활동적인 NPC들
                Debug.Log("[TimeManager] Morning effects applied");
                break;

            case GameStateManager.TimeSlot.Afternoon:
                // 약간 어두워짐
                Debug.Log("[TimeManager] Afternoon effects applied");
                break;

            case GameStateManager.TimeSlot.Evening:
                // 일부 NPC는 특정 장소로 이동
                Debug.Log("[TimeManager] Evening effects applied");
                break;

            case GameStateManager.TimeSlot.Night:
                // 어두운 조명, 일부 장소 접근 불가/가능
                Debug.Log("[TimeManager] Night effects applied");
                break;
        }

        // 여기서 다른 매니저들에게 알림
        // LightingManager.SetTimePeriod(period);
        // NPCScheduleManager.UpdateSchedules(period);
    }

    /// <summary>
    /// 시간 경고 체크
    /// </summary>
    private void CheckTimeWarning()
    {
        if (RemainingTimeSlots <= warningThreshold && RemainingTimeSlots > 0)
        {
            OnTimeWarning?.Invoke(RemainingTimeSlots);
            Debug.LogWarning($"[TimeManager] ⚠️ Time warning: {RemainingTimeSlots} slots remaining!");
        }
    }

    /// <summary>
    /// 시간 소진 처리
    /// </summary>
    private void HandleTimeUp()
    {
        Debug.LogWarning("[TimeManager] ⏰ TIME UP!");
        OnTimeUp?.Invoke();

        // GameStateManager와 연동
        var gameState = FindObjectOfType<GameStateManager>();
        if (gameState != null)
        {
            // 엔딩으로 전환하거나 챕터 종료
            // gameState.SetPhase(GameStateManager.GamePhase.Ending);
        }
    }

    // =========================================================
    // 🔹 TIME MANIPULATION
    // =========================================================

    /// <summary>
    /// 시간 되돌리기 (특수 아이템 등)
    /// </summary>
    public void RewindTime(int slots)
    {
        if (slots <= 0) return;

        int previousSlot = currentTimeSlot;
        currentTimeSlot = Mathf.Max(0, currentTimeSlot - slots);

        UpdateTimePeriod();
        OnTimeSlotChanged?.Invoke(RemainingTimeSlots);

        Debug.Log($"[TimeManager] Time rewound: {previousSlot} → {currentTimeSlot} " +
                  $"(+{slots} slots recovered)");
    }

    /// <summary>
    /// 시간 추가 (보너스)
    /// </summary>
    public void AddTimeSlots(int slots)
    {
        if (slots <= 0) return;

        maxTimeSlots += slots;
        OnTimeSlotChanged?.Invoke(RemainingTimeSlots);

        Debug.Log($"[TimeManager] Time slots added: +{slots} (Total: {maxTimeSlots})");
    }

    /// <summary>
    /// 특정 시간대로 강제 변경 (컷신 등)
    /// </summary>
    public void SetTimePeriod(GameStateManager.TimeSlot period)
    {
        if (currentPeriod == period) return;

        GameStateManager.TimeSlot previousPeriod = currentPeriod;
        currentPeriod = period;

        OnTimePeriodChanged?.Invoke(currentPeriod);

        Debug.Log($"[TimeManager] Time period forced: {previousPeriod} → {currentPeriod}");

        ApplyTimePeriodEffects(currentPeriod);
    }

    /// <summary>
    /// 특정 시간 슬롯으로 설정 (디버그/치트)
    /// </summary>
    public void SetTimeSlot(int slot)
    {
        slot = Mathf.Clamp(slot, 0, maxTimeSlots);
        
        if (currentTimeSlot == slot) return;

        currentTimeSlot = slot;
        UpdateTimePeriod();
        OnTimeSlotChanged?.Invoke(RemainingTimeSlots);

        Debug.Log($"[TimeManager] Time slot set to: {currentTimeSlot}");
    }

    // =========================================================
    // 🔹 QUERY METHODS
    // =========================================================

    /// <summary>
    /// 현재 시간대인지 확인
    /// </summary>
    public bool IsTimePeriod(GameStateManager.TimeSlot period)
    {
        return currentPeriod == period;
    }

    /// <summary>
    /// 충분한 시간이 남았는지
    /// </summary>
    public bool HasEnoughTime(int requiredSlots)
    {
        return RemainingTimeSlots >= requiredSlots;
    }

    /// <summary>
    /// 현재 시간 정보 문자열
    /// </summary>
    public string GetCurrentTimeString()
    {
        return $"{GetTimePeriodName(currentPeriod)} ({currentTimeSlot}/{maxTimeSlots})";
    }

    /// <summary>
    /// 시간대 이름
    /// </summary>
    public string GetTimePeriodName(GameStateManager.TimeSlot period)
    {
        switch (period)
        {
            case GameStateManager.TimeSlot.Morning:   return "아침";
            case GameStateManager.TimeSlot.Afternoon: return "오후";
            case GameStateManager.TimeSlot.Evening:   return "저녁";
            case GameStateManager.TimeSlot.Night:     return "밤";
            default: return "알 수 없음";
        }
    }

    /// <summary>
    /// 남은 시간 퍼센트 (0~100)
    /// </summary>
    public float GetRemainingTimePercent()
    {
        if (maxTimeSlots <= 0) return 0f;
        return ((float)RemainingTimeSlots / maxTimeSlots) * 100f;
    }

    // =========================================================
    // 🔹 SAVE/LOAD
    // =========================================================

    [System.Serializable]
    public class TimeSaveData
    {
        public int currentTimeSlot;
        public int maxTimeSlots;
        public GameStateManager.TimeSlot currentPeriod;
    }

    public TimeSaveData GetSaveData()
    {
        return new TimeSaveData
        {
            currentTimeSlot = this.currentTimeSlot,
            maxTimeSlots = this.maxTimeSlots,
            currentPeriod = this.currentPeriod
        };
    }

    public void LoadSaveData(TimeSaveData data)
    {
        if (data == null)
        {
            Debug.LogError("[TimeManager] Cannot load null save data");
            return;
        }

        currentTimeSlot = data.currentTimeSlot;
        maxTimeSlots = data.maxTimeSlots;
        currentPeriod = data.currentPeriod;

        // 이벤트 발생
        OnTimeSlotChanged?.Invoke(RemainingTimeSlots);
        OnTimePeriodChanged?.Invoke(currentPeriod);

        Debug.Log("[TimeManager] Save data loaded");
    }

    // =========================================================
    // 🔹 DEBUG
    // =========================================================

    public void PrintStatus()
    {
        Debug.Log("=== TIME MANAGER STATUS ===");
        Debug.Log($"Current Time: {GetCurrentTimeString()}");
        Debug.Log($"Time Period: {currentPeriod}");
        Debug.Log($"Progress: {TimeProgress * 100:F1}%");
        Debug.Log($"Remaining: {RemainingTimeSlots} / {maxTimeSlots} slots");
        Debug.Log($"Time Up: {IsTimeUp}");
    }

    #if UNITY_EDITOR
    [ContextMenu("Advance Time (1 slot)")]
    private void DebugAdvanceTime()
    {
        AdvanceTime(1);
    }

    [ContextMenu("Rewind Time (1 slot)")]
    private void DebugRewindTime()
    {
        RewindTime(1);
    }

    [ContextMenu("Set Morning")]
    private void DebugSetMorning()
    {
        SetTimePeriod(GameStateManager.TimeSlot.Morning);
    }

    [ContextMenu("Set Night")]
    private void DebugSetNight()
    {
        SetTimePeriod(GameStateManager.TimeSlot.Night);
    }

    [ContextMenu("Print Status")]
    private void DebugPrintStatus()
    {
        PrintStatus();
    }
    #endif
}