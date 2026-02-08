using System;
using UnityEngine;

/// <summary>
/// ActionPointManager
/// - 행동력(AP) 관리
/// - 행동 비용 계산 및 소비
/// - 행동력 회복 시스템
/// </summary>
public class ActionPointManager : MonoBehaviour
{
    public static ActionPointManager Instance { get; private set; }

    #region Action Point Settings

    [Header("Action Point Configuration")]
    [SerializeField] private int maxActionPoints = 20;  // 최대 행동력
    [SerializeField] private int startingActionPoints = 20;  // 시작 행동력

    private int currentActionPoints;  // 현재 행동력

    #endregion

    #region Recovery Settings

    [Header("Recovery Settings")]
    [SerializeField] private bool enableAutoRecovery = false;  // 자동 회복 사용 여부
    [SerializeField] private int recoveryPerTimeSlot = 2;  // 시간 슬롯당 회복량
    [SerializeField] private int restRecoveryAmount = 5;  // 휴식 시 회복량

    #endregion

    #region Warning Settings

    [Header("Warning Settings")]
    [SerializeField] private int lowAPWarningThreshold = 5;  // 낮은 AP 경고 기준
    [SerializeField] private int criticalAPThreshold = 2;  // 위험 수준 기준

    #endregion

    #region Action Point State

    public int CurrentActionPoints => currentActionPoints;
    public int MaxActionPoints => maxActionPoints;
    public int RemainingPoints => currentActionPoints;
    public bool IsEmpty => currentActionPoints <= 0;
    public bool IsLow => currentActionPoints <= lowAPWarningThreshold;
    public bool IsCritical => currentActionPoints <= criticalAPThreshold;
    public float APPercent => maxActionPoints > 0 ? (float)currentActionPoints / maxActionPoints : 0f;

    #endregion

    #region Events

    public event Action<int, int> OnActionPointsChanged;  // (current, max)
    public event Action<int> OnActionPointsConsumed;  // 소비량
    public event Action<int> OnActionPointsRecovered;  // 회복량
    public event Action OnActionPointsZero;  // AP 0 도달
    public event Action OnActionPointsLow;  // AP 부족 경고
    public event Action OnActionPointsCritical;  // AP 위험 수준

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

    private void Start()
    {
        // TimeManager 이벤트 구독 (자동 회복용)
        if (enableAutoRecovery)
        {
            var timeManager = FindObjectOfType<TimeManager>();
            if (timeManager != null)
            {
                timeManager.OnTimeSlotChanged += HandleTimeSlotChanged;
            }
        }
    }

    #endregion

    #region Initialization

    public void Initialize()
    {
        ResetPoints();
        Debug.Log("[ActionPointManager] Initialized");
    }

    /// <summary>
    /// 행동력 초기화
    /// </summary>
    public void ResetPoints()
    {
        currentActionPoints = startingActionPoints;
        OnActionPointsChanged?.Invoke(currentActionPoints, maxActionPoints);
        
        Debug.Log($"[ActionPointManager] Action points reset: {currentActionPoints}/{maxActionPoints}");
    }

    #endregion

    // =========================================================
    // 🔹 ACTION POINT CONSUMPTION
    // =========================================================

    /// <summary>
    /// 행동력 소비
    /// </summary>
    public bool ConsumePoints(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("[ActionPointManager] Cannot consume negative or zero points");
            return false;
        }

        if (!HasEnoughPoints(amount))
        {
            Debug.LogWarning($"[ActionPointManager] Not enough AP: need {amount}, have {currentActionPoints}");
            OnActionPointsLow?.Invoke();
            return false;
        }

        int previousPoints = currentActionPoints;
        currentActionPoints -= amount;
        currentActionPoints = Mathf.Max(0, currentActionPoints);

        // 이벤트 발생
        OnActionPointsConsumed?.Invoke(amount);
        OnActionPointsChanged?.Invoke(currentActionPoints, maxActionPoints);

        Debug.Log($"[ActionPointManager] AP consumed: -{amount} " +
                  $"({previousPoints} → {currentActionPoints})");

        // 경고 체크
        CheckAPWarnings();

        // AP 소진 체크
        if (currentActionPoints <= 0)
        {
            HandleAPZero();
        }

        return true;
    }

    /// <summary>
    /// 충분한 행동력이 있는지 확인
    /// </summary>
    public bool HasEnoughPoints(int required)
    {
        return currentActionPoints >= required;
    }

    /// <summary>
    /// 특정 행동 타입의 비용 확인 및 소비
    /// </summary>
    public bool ConsumeActionCost(ActionType actionType)
    {
        int cost = GetActionCost(actionType);
        return ConsumePoints(cost);
    }

    /// <summary>
    /// 행동 타입별 비용 반환
    /// </summary>
    private int GetActionCost(ActionType actionType)
    {
        // GameFlowManager와 동기화해야 함
        switch (actionType)
        {
            case ActionType.Move:
                return 1;
            case ActionType.Talk:
                return 2;
            case ActionType.Investigate:
                return 1;
            case ActionType.Rest:
                return 0;  // 휴식은 AP 소비 안 함
            default:
                return 0;
        }
    }

    // =========================================================
    // 🔹 ACTION POINT RECOVERY
    // =========================================================

    /// <summary>
    /// 행동력 회복
    /// </summary>
    public void RecoverPoints(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("[ActionPointManager] Cannot recover negative or zero points");
            return;
        }

        int previousPoints = currentActionPoints;
        currentActionPoints += amount;
        currentActionPoints = Mathf.Min(currentActionPoints, maxActionPoints);

        int actualRecovered = currentActionPoints - previousPoints;

        if (actualRecovered > 0)
        {
            OnActionPointsRecovered?.Invoke(actualRecovered);
            OnActionPointsChanged?.Invoke(currentActionPoints, maxActionPoints);

            Debug.Log($"[ActionPointManager] AP recovered: +{actualRecovered} " +
                      $"({previousPoints} → {currentActionPoints})");
        }
    }

    /// <summary>
    /// 행동력 완전 회복
    /// </summary>
    public void FullRecover()
    {
        int recovered = maxActionPoints - currentActionPoints;
        
        if (recovered > 0)
        {
            currentActionPoints = maxActionPoints;
            
            OnActionPointsRecovered?.Invoke(recovered);
            OnActionPointsChanged?.Invoke(currentActionPoints, maxActionPoints);

            Debug.Log($"[ActionPointManager] AP fully recovered: +{recovered}");
        }
    }

    /// <summary>
    /// 휴식으로 회복
    /// </summary>
    public void Rest()
    {
        RecoverPoints(restRecoveryAmount);
        Debug.Log($"[ActionPointManager] Rested: recovered {restRecoveryAmount} AP");
    }

    /// <summary>
    /// 시간 경과에 따른 자동 회복
    /// </summary>
    private void HandleTimeSlotChanged(int remainingSlots)
    {
        if (enableAutoRecovery && recoveryPerTimeSlot > 0)
        {
            RecoverPoints(recoveryPerTimeSlot);
            Debug.Log($"[ActionPointManager] Auto recovery: +{recoveryPerTimeSlot} AP");
        }
    }

    // =========================================================
    // 🔹 ACTION POINT MANIPULATION
    // =========================================================

    /// <summary>
    /// 최대 행동력 증가 (레벨업, 업그레이드 등)
    /// </summary>
    public void IncreaseMaxAP(int amount)
    {
        if (amount <= 0) return;

        int previousMax = maxActionPoints;
        maxActionPoints += amount;

        // 현재 AP도 동일하게 증가
        currentActionPoints += amount;

        OnActionPointsChanged?.Invoke(currentActionPoints, maxActionPoints);

        Debug.Log($"[ActionPointManager] Max AP increased: {previousMax} → {maxActionPoints}");
    }

    /// <summary>
    /// 최대 행동력 설정
    /// </summary>
    public void SetMaxAP(int newMax)
    {
        if (newMax <= 0)
        {
            Debug.LogWarning("[ActionPointManager] Cannot set max AP to zero or negative");
            return;
        }

        maxActionPoints = newMax;
        currentActionPoints = Mathf.Min(currentActionPoints, maxActionPoints);

        OnActionPointsChanged?.Invoke(currentActionPoints, maxActionPoints);

        Debug.Log($"[ActionPointManager] Max AP set to: {maxActionPoints}");
    }

    /// <summary>
    /// 현재 행동력 직접 설정 (디버그/치트)
    /// </summary>
    public void SetCurrentAP(int amount)
    {
        amount = Mathf.Clamp(amount, 0, maxActionPoints);
        
        if (currentActionPoints == amount) return;

        currentActionPoints = amount;
        OnActionPointsChanged?.Invoke(currentActionPoints, maxActionPoints);

        Debug.Log($"[ActionPointManager] Current AP set to: {currentActionPoints}");

        CheckAPWarnings();
    }

    // =========================================================
    // 🔹 WARNING SYSTEM
    // =========================================================

    private void CheckAPWarnings()
    {
        if (currentActionPoints <= criticalAPThreshold && currentActionPoints > 0)
        {
            OnActionPointsCritical?.Invoke();
            Debug.LogWarning($"[ActionPointManager] 🚨 CRITICAL: Only {currentActionPoints} AP remaining!");
        }
        else if (currentActionPoints <= lowAPWarningThreshold && currentActionPoints > criticalAPThreshold)
        {
            OnActionPointsLow?.Invoke();
            Debug.LogWarning($"[ActionPointManager] ⚠️ LOW AP: {currentActionPoints} remaining");
        }
    }

    private void HandleAPZero()
    {
        Debug.LogWarning("[ActionPointManager] ⚠️ ACTION POINTS DEPLETED!");
        OnActionPointsZero?.Invoke();

        // 추가 처리 (게임오버, 강제 휴식 등)
    }

    // =========================================================
    // 🔹 QUERY METHODS
    // =========================================================

    /// <summary>
    /// 특정 행동을 수행할 수 있는지
    /// </summary>
    public bool CanPerformAction(ActionType actionType)
    {
        int cost = GetActionCost(actionType);
        return HasEnoughPoints(cost);
    }

    /// <summary>
    /// 행동 가능 횟수 (최소 비용 기준)
    /// </summary>
    public int GetPossibleActions()
    {
        // 가장 저렴한 행동 비용
        int minCost = Mathf.Min(1, 1, 1);  // Move, Talk, Investigate 중 최소
        
        if (minCost <= 0) return int.MaxValue;
        
        return currentActionPoints / minCost;
    }

    /// <summary>
    /// AP 상태 문자열
    /// </summary>
    public string GetAPStatusString()
    {
        return $"{currentActionPoints} / {maxActionPoints} AP";
    }

    /// <summary>
    /// AP 색상 (UI용)
    /// </summary>
    public Color GetAPColor()
    {
        if (IsCritical)
            return Color.red;
        else if (IsLow)
            return Color.yellow;
        else
            return Color.green;
    }

    // =========================================================
    // 🔹 SAVE/LOAD
    // =========================================================

    [System.Serializable]
    public class ActionPointSaveData
    {
        public int currentActionPoints;
        public int maxActionPoints;
    }

    public ActionPointSaveData GetSaveData()
    {
        return new ActionPointSaveData
        {
            currentActionPoints = this.currentActionPoints,
            maxActionPoints = this.maxActionPoints
        };
    }

    public void LoadSaveData(ActionPointSaveData data)
    {
        if (data == null)
        {
            Debug.LogError("[ActionPointManager] Cannot load null save data");
            return;
        }

        currentActionPoints = data.currentActionPoints;
        maxActionPoints = data.maxActionPoints;

        OnActionPointsChanged?.Invoke(currentActionPoints, maxActionPoints);

        Debug.Log("[ActionPointManager] Save data loaded");
    }

    // =========================================================
    // 🔹 DEBUG
    // =========================================================

    public void PrintStatus()
    {
        Debug.Log("=== ACTION POINT MANAGER STATUS ===");
        Debug.Log($"Current AP: {currentActionPoints} / {maxActionPoints}");
        Debug.Log($"AP Percent: {APPercent * 100:F1}%");
        Debug.Log($"Possible Actions: {GetPossibleActions()}");
        Debug.Log($"Status: {(IsCritical ? "CRITICAL" : IsLow ? "LOW" : "OK")}");
        Debug.Log($"Auto Recovery: {(enableAutoRecovery ? $"Enabled (+{recoveryPerTimeSlot}/slot)" : "Disabled")}");
    }

    #if UNITY_EDITOR
    [ContextMenu("Consume 1 AP")]
    private void DebugConsumeAP()
    {
        ConsumePoints(1);
    }

    [ContextMenu("Recover 5 AP")]
    private void DebugRecoverAP()
    {
        RecoverPoints(5);
    }

    [ContextMenu("Full Recover")]
    private void DebugFullRecover()
    {
        FullRecover();
    }

    [ContextMenu("Rest")]
    private void DebugRest()
    {
        Rest();
    }

    [ContextMenu("Set AP to 2 (Critical)")]
    private void DebugSetCritical()
    {
        SetCurrentAP(2);
    }

    [ContextMenu("Print Status")]
    private void DebugPrintStatus()
    {
        PrintStatus();
    }
    #endif
}