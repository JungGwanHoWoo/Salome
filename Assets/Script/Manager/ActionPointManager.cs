using System;
using UnityEngine;

/// <summary>
/// ActionPointManager (게임 규칙에 맞게 수정)
/// - 행동력(AP) 관리
/// - 대화 시 AP 소비
/// - AP 소진 시 다음 지역으로 이동 트리거
/// </summary>
public class ActionPointManager : MonoBehaviour
{
    public static ActionPointManager Instance { get; private set; }

    #region Action Point Settings

    [Header("Action Point Configuration")]
    [SerializeField] private int maxActionPoints = 20;
    [SerializeField] private int startingActionPoints = 20;

    #endregion

    #region Action Point State

    private int currentActionPoints;

    public int MaxActionPoints => maxActionPoints;
    public int CurrentActionPoints => currentActionPoints;
    public int RemainingPoints => currentActionPoints;
    public float APPercent => maxActionPoints > 0 ? (float)currentActionPoints / maxActionPoints : 0f;

    #endregion

    #region State Check

    public bool IsEmpty => currentActionPoints <= 0;
    public bool IsLow => currentActionPoints <= 5;
    public bool IsCritical => currentActionPoints <= 2;

    #endregion

    #region Events

    public event Action<int, int> OnActionPointsChanged;  // (current, max)
    public event Action<int> OnActionPointsConsumed;  // amount
    public event Action<int> OnActionPointsRecovered;  // amount
    public event Action OnActionPointsLow;
    public event Action OnActionPointsCritical;
    public event Action OnActionPointsZero;

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
        currentActionPoints = startingActionPoints;
        
        Debug.Log($"[ActionPointManager] Initialized with {currentActionPoints}/{maxActionPoints} AP");
    }

    #endregion

    // =========================================================
    // 🔹 ACTION POINT CONSUMPTION
    // =========================================================

    /// <summary>
    /// AP 소비
    /// </summary>
    public bool ConsumePoints(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("[ActionPointManager] Cannot consume zero or negative AP");
            return false;
        }

        if (currentActionPoints < amount)
        {
            Debug.LogWarning($"[ActionPointManager] Not enough AP! Need {amount}, have {currentActionPoints}");
            return false;
        }

        int previousAP = currentActionPoints;
        currentActionPoints -= amount;

        // 이벤트 발생
        OnActionPointsConsumed?.Invoke(amount);
        OnActionPointsChanged?.Invoke(currentActionPoints, maxActionPoints);

        Debug.Log($"[ActionPointManager] AP consumed: {previousAP} → {currentActionPoints} (-{amount})");

        // 경고 체크
        CheckWarnings();

        // AP 소진 체크
        if (currentActionPoints <= 0)
        {
            HandleActionPointsZero();
        }

        return true;
    }

    /// <summary>
    /// 충분한 AP가 있는지 확인
    /// </summary>
    public bool HasEnoughPoints(int required)
    {
        return currentActionPoints >= required;
    }

    // =========================================================
    // 🔹 ACTION POINT RECOVERY
    // =========================================================

    /// <summary>
    /// AP 회복
    /// </summary>
    public void RecoverPoints(int amount)
    {
        if (amount <= 0) return;

        int previousAP = currentActionPoints;
        currentActionPoints += amount;
        currentActionPoints = Mathf.Min(currentActionPoints, maxActionPoints);

        // 이벤트 발생
        OnActionPointsRecovered?.Invoke(amount);
        OnActionPointsChanged?.Invoke(currentActionPoints, maxActionPoints);

        Debug.Log($"[ActionPointManager] AP recovered: {previousAP} → {currentActionPoints} (+{amount})");
    }

    /// <summary>
    /// 완전 회복
    /// </summary>
    public void FullRecover()
    {
        int previousAP = currentActionPoints;
        currentActionPoints = maxActionPoints;

        OnActionPointsChanged?.Invoke(currentActionPoints, maxActionPoints);

        Debug.Log($"[ActionPointManager] AP fully recovered: {previousAP} → {currentActionPoints}");
    }

    /// <summary>
    /// AP 리셋 (지역 전환 시)
    /// </summary>
    public void ResetPoints()
    {
        currentActionPoints = startingActionPoints;
        OnActionPointsChanged?.Invoke(currentActionPoints, maxActionPoints);

        Debug.Log($"[ActionPointManager] AP reset to {currentActionPoints}");
    }

    // =========================================================
    // 🔹 WARNING SYSTEM
    // =========================================================

    private void CheckWarnings()
    {
        if (currentActionPoints == 2)
        {
            OnActionPointsCritical?.Invoke();
        }
        else if (currentActionPoints == 5)
        {
            OnActionPointsLow?.Invoke();
        }
    }

    private void HandleActionPointsZero()
    {
        Debug.LogWarning("[ActionPointManager] ⚠️ Action Points depleted!");
        OnActionPointsZero?.Invoke();
    }

    // =========================================================
    // 🔹 UI HELPERS
    // =========================================================

    /// <summary>
    /// AP 상태 색상 (UI용)
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

    /// <summary>
    /// AP 상태 문자열
    /// </summary>
    public string GetAPStatusString()
    {
        return $"{currentActionPoints} / {maxActionPoints} AP";
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
        Debug.Log($"Percentage: {APPercent * 100:F1}%");
        Debug.Log($"State: {(IsEmpty ? "Empty" : IsLow ? "Low" : IsCritical ? "Critical" : "Normal")}");
    }

    #if UNITY_EDITOR
    [ContextMenu("Consume 2 AP (Talk)")]
    private void DebugConsumeTalk()
    {
        ConsumePoints(2);
    }

    [ContextMenu("Consume 1 AP (Move)")]
    private void DebugConsumeMove()
    {
        ConsumePoints(1);
    }

    [ContextMenu("Recover 5 AP")]
    private void DebugRecover()
    {
        RecoverPoints(5);
    }

    [ContextMenu("Full Recover")]
    private void DebugFullRecover()
    {
        FullRecover();
    }

    [ContextMenu("Reset AP")]
    private void DebugReset()
    {
        ResetPoints();
    }

    [ContextMenu("Print Status")]
    private void DebugPrintStatus()
    {
        PrintStatus();
    }
    #endif
}