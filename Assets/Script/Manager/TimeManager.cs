using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// TimeManager (관찰 모드 타이머)
/// - 관찰 모드에서만 작동하는 제한시간 시스템
/// - 주변 사물을 조사할 때 제한시간 부여
/// - 시간 내에 단서/호감도 증가 방법 발견 필요
/// </summary>
public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    #region Timer Settings

    [Header("Observation Timer Settings")]
    [SerializeField] private float defaultObservationTime = 60f;  // 기본 관찰 제한시간 (초)
    [SerializeField] private bool allowTimerPause = true;  // 타이머 일시정지 허용

    #endregion

    #region Timer State

    private bool isTimerRunning = false;
    private float currentTime = 0f;
    private float maxTime = 0f;
    private Coroutine timerCoroutine;

    public bool IsTimerRunning => isTimerRunning;
    public float CurrentTime => currentTime;
    public float MaxTime => maxTime;
    public float RemainingTime => Mathf.Max(0f, currentTime);
    public float TimeProgress => maxTime > 0 ? (maxTime - currentTime) / maxTime : 0f;  // 0~1
    public bool IsTimeUp => currentTime <= 0f;

    #endregion

    #region Events

    public event Action<float> OnTimerStarted;  // 시작 시간
    public event Action OnTimerEnded;  // 타이머 종료
    public event Action<float> OnTimerTick;  // 매 프레임 (남은 시간)
    public event Action<int> OnWarning;  // 경고 (남은 초)
    public event Action OnTimeUp;  // 시간 소진

    #endregion

    #region Warning Settings

    [Header("Warning Times (초)")]
    [SerializeField] private int[] warningTimes = new int[] { 30, 10, 5 };
    private bool[] warningTriggered;

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
        ResetTimer();
        warningTriggered = new bool[warningTimes.Length];
        
        Debug.Log("[TimeManager] Initialized (Observation Timer)");
    }

    private void ResetTimer()
    {
        isTimerRunning = false;
        currentTime = 0f;
        maxTime = 0f;
        
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }
    }

    #endregion

    // =========================================================
    // 🔹 TIMER CONTROL
    // =========================================================

    /// <summary>
    /// 관찰 타이머 시작
    /// </summary>
    public void StartObservationTimer(float duration = -1f)
    {
        // 기본값 사용
        if (duration <= 0f)
            duration = defaultObservationTime;

        // 이미 실행 중이면 중지
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }

        // 초기화
        maxTime = duration;
        currentTime = duration;
        isTimerRunning = true;

        // 경고 초기화
        for (int i = 0; i < warningTriggered.Length; i++)
        {
            warningTriggered[i] = false;
        }

        // 이벤트 발생
        OnTimerStarted?.Invoke(duration);

        // 타이머 코루틴 시작
        timerCoroutine = StartCoroutine(TimerCoroutine());

        Debug.Log($"[TimeManager] Observation timer started: {duration} seconds");
    }

    /// <summary>
    /// 타이머 중지
    /// </summary>
    public void StopTimer()
    {
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }

        isTimerRunning = false;
        OnTimerEnded?.Invoke();

        Debug.Log("[TimeManager] Timer stopped");
    }

    /// <summary>
    /// 타이머 일시정지
    /// </summary>
    public void PauseTimer()
    {
        if (!allowTimerPause)
        {
            Debug.LogWarning("[TimeManager] Timer pause not allowed");
            return;
        }

        if (isTimerRunning)
        {
            isTimerRunning = false;
            Debug.Log("[TimeManager] Timer paused");
        }
    }

    /// <summary>
    /// 타이머 재개
    /// </summary>
    public void ResumeTimer()
    {
        if (!isTimerRunning && currentTime > 0f)
        {
            isTimerRunning = true;
            Debug.Log("[TimeManager] Timer resumed");
        }
    }

    /// <summary>
    /// 타이머 코루틴
    /// </summary>
    private IEnumerator TimerCoroutine()
    {
        while (currentTime > 0f)
        {
            if (isTimerRunning)
            {
                currentTime -= Time.deltaTime;
                
                // 틱 이벤트
                OnTimerTick?.Invoke(currentTime);

                // 경고 체크
                CheckWarnings();

                // 시간 소진 체크
                if (currentTime <= 0f)
                {
                    currentTime = 0f;
                    HandleTimeUp();
                    yield break;
                }
            }

            yield return null;
        }
    }

    /// <summary>
    /// 경고 체크
    /// </summary>
    private void CheckWarnings()
    {
        for (int i = 0; i < warningTimes.Length; i++)
        {
            if (!warningTriggered[i] && currentTime <= warningTimes[i] && currentTime > warningTimes[i] - 1f)
            {
                warningTriggered[i] = true;
                OnWarning?.Invoke(warningTimes[i]);
                Debug.LogWarning($"[TimeManager] Warning: {warningTimes[i]} seconds remaining!");
            }
        }
    }

    /// <summary>
    /// 시간 소진 처리
    /// </summary>
    private void HandleTimeUp()
    {
        isTimerRunning = false;
        OnTimeUp?.Invoke();

        Debug.LogWarning("[TimeManager] ⏰ Time's up!");

        // 관찰 모드 강제 종료
        EndObservationMode();
    }

    /// <summary>
    /// 관찰 모드 종료
    /// </summary>
    private void EndObservationMode()
    {
        // GameStateManager로 페이즈 전환
        var gameState = FindObjectOfType<GameStateManager>();
        if (gameState != null)
        {
            gameState.SetPhase(GameStateManager.GamePhase.Exploration);
        }

        Debug.Log("[TimeManager] Observation mode ended");
    }

    // =========================================================
    // 🔹 TIME MANIPULATION
    // =========================================================

    /// <summary>
    /// 시간 추가 (보너스)
    /// </summary>
    public void AddTime(float seconds)
    {
        if (seconds <= 0f) return;

        currentTime += seconds;
        currentTime = Mathf.Min(currentTime, maxTime);  // 최대 시간 초과 방지

        Debug.Log($"[TimeManager] Time added: +{seconds}s (Current: {currentTime:F1}s)");
    }

    /// <summary>
    /// 시간 감소 (페널티)
    /// </summary>
    public void ReduceTime(float seconds)
    {
        if (seconds <= 0f) return;

        currentTime -= seconds;
        currentTime = Mathf.Max(0f, currentTime);

        Debug.Log($"[TimeManager] Time reduced: -{seconds}s (Current: {currentTime:F1}s)");

        if (currentTime <= 0f)
        {
            HandleTimeUp();
        }
    }

    /// <summary>
    /// 시간 배율 조정 (슬로우 모션 등)
    /// </summary>
    public void SetTimeScale(float scale)
    {
        Time.timeScale = scale;
        Debug.Log($"[TimeManager] Time scale set to {scale}");
    }

    // =========================================================
    // 🔹 QUERY METHODS
    // =========================================================

    /// <summary>
    /// 남은 시간 문자열
    /// </summary>
    public string GetRemainingTimeString()
    {
        int minutes = Mathf.FloorToInt(currentTime / 60f);
        int seconds = Mathf.FloorToInt(currentTime % 60f);
        return $"{minutes:00}:{seconds:00}";
    }

    /// <summary>
    /// 시간 색상 (UI용)
    /// </summary>
    public Color GetTimeColor()
    {
        float percent = currentTime / maxTime;
        
        if (percent > 0.5f)
            return Color.green;
        else if (percent > 0.25f)
            return Color.yellow;
        else
            return Color.red;
    }

    // =========================================================
    // 🔹 SAVE/LOAD
    // =========================================================

    [System.Serializable]
    public class TimeSaveData
    {
        public float currentTime;
        public float maxTime;
        public bool isTimerRunning;
    }

    public TimeSaveData GetSaveData()
    {
        return new TimeSaveData
        {
            currentTime = this.currentTime,
            maxTime = this.maxTime,
            isTimerRunning = this.isTimerRunning
        };
    }

    public void LoadSaveData(TimeSaveData data)
    {
        if (data == null)
        {
            Debug.LogError("[TimeManager] Cannot load null save data");
            return;
        }

        currentTime = data.currentTime;
        maxTime = data.maxTime;
        
        if (data.isTimerRunning && currentTime > 0f)
        {
            // 타이머 재시작
            isTimerRunning = true;
            timerCoroutine = StartCoroutine(TimerCoroutine());
        }

        Debug.Log("[TimeManager] Save data loaded");
    }

    // =========================================================
    // 🔹 DEBUG
    // =========================================================

    public void PrintStatus()
    {
        Debug.Log("=== TIME MANAGER STATUS ===");
        Debug.Log($"Timer Running: {isTimerRunning}");
        Debug.Log($"Current Time: {currentTime:F2}s");
        Debug.Log($"Max Time: {maxTime:F2}s");
        Debug.Log($"Remaining: {GetRemainingTimeString()}");
        Debug.Log($"Progress: {TimeProgress * 100:F1}%");
    }

    #if UNITY_EDITOR
    [ContextMenu("Start Test Timer (60s)")]
    private void DebugStartTimer()
    {
        StartObservationTimer(60f);
    }

    [ContextMenu("Stop Timer")]
    private void DebugStopTimer()
    {
        StopTimer();
    }

    [ContextMenu("Add 10 seconds")]
    private void DebugAddTime()
    {
        AddTime(10f);
    }

    [ContextMenu("Print Status")]
    private void DebugPrintStatus()
    {
        PrintStatus();
    }
    #endif
}