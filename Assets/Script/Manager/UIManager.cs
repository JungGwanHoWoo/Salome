using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;  // TextMeshPro 추가

/// <summary>
/// UIManager (TextMeshPro 지원)
/// - 모든 UI 요소의 중앙 관리
/// - UI 표시/숨김 제어
/// - HUD 업데이트
/// - 알림/팝업 관리
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    #region UI Panels

    [Header("Main Panels")]
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject notebookPanel;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject investigationPanel;

    [Header("HUD Elements - TextMeshPro")]
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private Slider timeProgressBar;
    [SerializeField] private TextMeshProUGUI timePeriodText;

    [SerializeField] private TextMeshProUGUI apText;
    [SerializeField] private Slider apBar;
    [SerializeField] private Image apBarFill;

    [SerializeField] private TextMeshProUGUI locationText;
    [SerializeField] private TextMeshProUGUI chapterText;

    [Header("Notification System")]
    [SerializeField] private GameObject notificationPrefab;
    [SerializeField] private Transform notificationContainer;
    [SerializeField] private float notificationDuration = 3f;

    [Header("Popup System")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private TextMeshProUGUI popupTitleText;
    [SerializeField] private TextMeshProUGUI popupMessageText;
    [SerializeField] private Button popupConfirmButton;
    [SerializeField] private Button popupCancelButton;

    [Header("Loading Screen")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Slider loadingProgressBar;
    [SerializeField] private TextMeshProUGUI loadingText;

    [Header("Fade")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 0.5f;

    #endregion

    #region UI State

    private Dictionary<UIPanel, GameObject> uiPanels;
    private Queue<NotificationData> notificationQueue;
    private bool isShowingNotification = false;
    private Action currentPopupCallback;

    public bool IsAnyPanelOpen => pauseMenuPanel != null && pauseMenuPanel.activeSelf ||
                                   settingsPanel != null && settingsPanel.activeSelf ||
                                   notebookPanel != null && notebookPanel.activeSelf;

    #endregion

    #region Dependencies

    private GameStateManager gameStateManager;
    private TimeManager timeManager;
    private ActionPointManager actionPointManager;
    private LocationManager locationManager;

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
        FindDependencies();
        InitializePanels();
        SubscribeToEvents();
        RefreshAll();
    }

    private void Update()
    {
        HandleInput();
    }

    #endregion

    #region Initialization

    public void Initialize()
    {
        notificationQueue = new Queue<NotificationData>();

        // 초기 상태 설정
        HideAllPanels();
        ShowHUD();

        Debug.Log("[UIManager] Initialized");
    }

    private void FindDependencies()
    {
        gameStateManager = FindObjectOfType<GameStateManager>();
        timeManager = FindObjectOfType<TimeManager>();
        actionPointManager = FindObjectOfType<ActionPointManager>();
        locationManager = FindObjectOfType<LocationManager>();
    }

    private void InitializePanels()
    {
        uiPanels = new Dictionary<UIPanel, GameObject>();

        if (hudPanel != null) uiPanels[UIPanel.HUD] = hudPanel;
        if (pauseMenuPanel != null) uiPanels[UIPanel.PauseMenu] = pauseMenuPanel;
        if (settingsPanel != null) uiPanels[UIPanel.Settings] = settingsPanel;
        if (notebookPanel != null) uiPanels[UIPanel.Notebook] = notebookPanel;
        if (dialoguePanel != null) uiPanels[UIPanel.Dialogue] = dialoguePanel;
        if (investigationPanel != null) uiPanels[UIPanel.Investigation] = investigationPanel;

        // 팝업 버튼 설정
        if (popupConfirmButton != null)
            popupConfirmButton.onClick.AddListener(OnPopupConfirm);

        if (popupCancelButton != null)
            popupCancelButton.onClick.AddListener(OnPopupCancel);
    }

    private void SubscribeToEvents()
    {
        // GameStateManager
        if (gameStateManager != null)
        {
            gameStateManager.OnPhaseChanged += HandlePhaseChanged;
            gameStateManager.OnChapterChanged += HandleChapterChanged;
            // gameStateManager.OnLocationChanged += HandleLocationChanged;
        }

        // TimeManager
        if (timeManager != null)
        {
            timeManager.OnTimerTick += HandleTimerTick;
            timeManager.OnWarning += HandleTimeWarning;
            timeManager.OnTimeUp += HandleTimeUp;
        }

        // ActionPointManager
        if (actionPointManager != null)
        {
            actionPointManager.OnActionPointsChanged += HandleAPChanged;
            actionPointManager.OnActionPointsLow += HandleAPLow;
            actionPointManager.OnActionPointsCritical += HandleAPCritical;
        }

        // LocationManager
        if (locationManager != null)
        {
            locationManager.OnLocationChanged += HandleLocationChangedData;
        }
    }

    #endregion

    // =========================================================
    // 🔹 PANEL MANAGEMENT
    // =========================================================

    public void ShowPanel(UIPanel panel)
    {
        if (uiPanels.TryGetValue(panel, out var panelObj))
        {
            panelObj.SetActive(true);
            Debug.Log($"[UIManager] Showing panel: {panel}");
        }
    }

    public void HidePanel(UIPanel panel)
    {
        if (uiPanels.TryGetValue(panel, out var panelObj))
        {
            panelObj.SetActive(false);
            Debug.Log($"[UIManager] Hiding panel: {panel}");
        }
    }

    public void TogglePanel(UIPanel panel)
    {
        if (uiPanels.TryGetValue(panel, out var panelObj))
        {
            panelObj.SetActive(!panelObj.activeSelf);
        }
    }

    public void HideAllPanels()
    {
        foreach (var panel in uiPanels.Values)
        {
            if (panel != null)
                panel.SetActive(false);
        }
    }

    public void ShowHUD()
    {
        ShowPanel(UIPanel.HUD);
    }

    public void HideHUD()
    {
        HidePanel(UIPanel.HUD);
    }

    // =========================================================
    // 🔹 HUD UPDATE (TextMeshPro)
    // =========================================================

    public void RefreshAll()
    {
        UpdateTimeDisplay();
        UpdateAPDisplay();
        UpdateLocationDisplay();
        UpdateChapterDisplay();
    }

    private void UpdateTimeDisplay()
    {
        if (timeManager == null) return;

        // 시간 텍스트
        if (timeText != null)
        {
            if (timeManager.IsTimerRunning)
            {
                timeText.text = timeManager.GetRemainingTimeString();
            }
            else
            {
                timeText.text = "--:--";
            }
        }

        // 시간 프로그레스 바
        if (timeProgressBar != null && timeManager.IsTimerRunning)
        {
            timeProgressBar.value = 1f - timeManager.TimeProgress;

            // 경고 색상
            Image fillImage = timeProgressBar.fillRect?.GetComponent<Image>();
            if (fillImage != null)
            {
                fillImage.color = timeManager.GetTimeColor();
            }
        }
    }

    private void UpdateAPDisplay()
    {
        if (actionPointManager == null) return;

        // AP 텍스트
        if (apText != null)
        {
            apText.text = actionPointManager.GetAPStatusString();
        }

        // AP 바
        if (apBar != null)
        {
            apBar.value = actionPointManager.APPercent;
        }

        // AP 바 색상
        if (apBarFill != null)
        {
            apBarFill.color = actionPointManager.GetAPColor();
        }
    }

    private void UpdateLocationDisplay()
    {
        if (locationManager == null) return;

        if (locationText != null)
        {
            locationText.text = locationManager.CurrentLocationName ?? "???";
        }
    }

    private void UpdateChapterDisplay()
    {
        if (gameStateManager == null) return;

        if (chapterText != null)
        {
            chapterText.text = GetChapterName(gameStateManager.CurrentChapter);
        }
    }

    private string GetChapterName(GameStateManager.Chapter chapter)
    {
        switch (chapter)
        {
            case GameStateManager.Chapter.Prologue: return "서막";
            case GameStateManager.Chapter.Chapther1: return "1장";
            case GameStateManager.Chapter.Chapther2: return "2장";
            case GameStateManager.Chapter.Chapther3: return "3장";
            case GameStateManager.Chapter.Finale: return "최종장";
            default: return "???";
        }
    }

    // =========================================================
    // 🔹 NOTIFICATION SYSTEM (TextMeshPro)
    // =========================================================

    public void ShowNotification(string message, NotificationType type = NotificationType.Info)
    {
        var notification = new NotificationData
        {
            message = message,
            type = type,
            duration = notificationDuration
        };

        notificationQueue.Enqueue(notification);

        if (!isShowingNotification)
        {
            StartCoroutine(ProcessNotificationQueue());
        }
    }

    private IEnumerator ProcessNotificationQueue()
    {
        isShowingNotification = true;

        while (notificationQueue.Count > 0)
        {
            var notification = notificationQueue.Dequeue();
            yield return StartCoroutine(DisplayNotification(notification));
        }

        isShowingNotification = false;
    }

    private IEnumerator DisplayNotification(NotificationData notification)
    {
        if (notificationPrefab == null || notificationContainer == null)
        {
            Debug.LogWarning("[UIManager] Notification system not set up");
            yield break;
        }

        // 알림 생성
        GameObject notifObj = Instantiate(notificationPrefab, notificationContainer);

        // TextMeshPro 텍스트 설정
        TextMeshProUGUI notifText = notifObj.GetComponentInChildren<TextMeshProUGUI>();
        if (notifText != null)
        {
            notifText.text = notification.message;
        }

        // 색상 설정
        Image notifBg = notifObj.GetComponent<Image>();
        if (notifBg != null)
        {
            notifBg.color = GetNotificationColor(notification.type);
        }

        // 애니메이션 (슬라이드 인)
        yield return StartCoroutine(SlideInNotification(notifObj));

        // 표시 시간
        yield return new WaitForSeconds(notification.duration);

        // 애니메이션 (슬라이드 아웃)
        yield return StartCoroutine(SlideOutNotification(notifObj));

        // 제거
        Destroy(notifObj);
    }

    private IEnumerator SlideInNotification(GameObject notifObj)
    {
        RectTransform rect = notifObj.GetComponent<RectTransform>();
        if (rect == null) yield break;

        Vector3 startPos = rect.anchoredPosition + new Vector2(300f, 0f);
        Vector3 endPos = rect.anchoredPosition;
        float elapsed = 0f;
        float duration = 0.3f;

        while (elapsed < duration)
        {
            rect.anchoredPosition = Vector3.Lerp(startPos, endPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rect.anchoredPosition = endPos;
    }

    private IEnumerator SlideOutNotification(GameObject notifObj)
    {
        RectTransform rect = notifObj.GetComponent<RectTransform>();
        if (rect == null) yield break;

        Vector3 startPos = rect.anchoredPosition;
        Vector3 endPos = rect.anchoredPosition + new Vector2(300f, 0f);
        float elapsed = 0f;
        float duration = 0.3f;

        while (elapsed < duration)
        {
            rect.anchoredPosition = Vector3.Lerp(startPos, endPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private Color GetNotificationColor(NotificationType type)
    {
        switch (type)
        {
            case NotificationType.Info:
                return new Color(0.2f, 0.6f, 1f, 0.9f);
            case NotificationType.Success:
                return new Color(0.2f, 0.8f, 0.2f, 0.9f);
            case NotificationType.Warning:
                return new Color(1f, 0.8f, 0.2f, 0.9f);
            case NotificationType.Error:
                return new Color(1f, 0.3f, 0.3f, 0.9f);
            default:
                return Color.white;
        }
    }

    // =========================================================
    // 🔹 POPUP SYSTEM (TextMeshPro)
    // =========================================================

    public void ShowPopup(string title, string message, Action onConfirm = null, Action onCancel = null)
    {
        if (popupPanel == null)
        {
            Debug.LogWarning("[UIManager] Popup panel not assigned");
            return;
        }

        // 팝업 내용 설정
        if (popupTitleText != null)
            popupTitleText.text = title;

        if (popupMessageText != null)
            popupMessageText.text = message;

        // 콜백 저장
        currentPopupCallback = onConfirm;

        // 취소 버튼 표시 여부
        if (popupCancelButton != null)
        {
            popupCancelButton.gameObject.SetActive(onCancel != null);
        }

        // 팝업 표시
        popupPanel.SetActive(true);

        Debug.Log($"[UIManager] Showing popup: {title}");
    }

    private void OnPopupConfirm()
    {
        currentPopupCallback?.Invoke();
        HidePopup();
    }

    private void OnPopupCancel()
    {
        HidePopup();
    }

    private void HidePopup()
    {
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }

        currentPopupCallback = null;
    }

    // =========================================================
    // 🔹 LOADING SCREEN (TextMeshPro)
    // =========================================================

    public void ShowLoading(string message = "로딩 중...")
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }

        if (loadingText != null)
        {
            loadingText.text = message;
        }

        if (loadingProgressBar != null)
        {
            loadingProgressBar.value = 0f;
        }
    }

    public void UpdateLoadingProgress(float progress)
    {
        if (loadingProgressBar != null)
        {
            loadingProgressBar.value = progress;
        }
    }

    public void HideLoading()
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
    }

    // =========================================================
    // 🔹 FADE EFFECTS
    // =========================================================

    public IEnumerator FadeOut(float duration = -1f)
    {
        if (duration < 0f) duration = fadeDuration;

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);

            Color color = fadeImage.color;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                color.a = Mathf.Lerp(0f, 1f, elapsed / duration);
                fadeImage.color = color;
                elapsed += Time.deltaTime;
                yield return null;
            }

            color.a = 1f;
            fadeImage.color = color;
        }
    }

    public IEnumerator FadeIn(float duration = -1f)
    {
        if (duration < 0f) duration = fadeDuration;

        if (fadeImage != null)
        {
            Color color = fadeImage.color;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                color.a = Mathf.Lerp(1f, 0f, elapsed / duration);
                fadeImage.color = color;
                elapsed += Time.deltaTime;
                yield return null;
            }

            color.a = 0f;
            fadeImage.color = color;
            fadeImage.gameObject.SetActive(false);
        }
    }

    public IEnumerator FadeTransition(Action onFaded = null)
    {
        yield return StartCoroutine(FadeOut());
        onFaded?.Invoke();
        yield return StartCoroutine(FadeIn());
    }

    // =========================================================
    // 🔹 EVENT HANDLERS
    // =========================================================

    private void HandlePhaseChanged(GameStateManager.GamePhase newPhase)
    {
        Debug.Log($"[UIManager] Phase changed to {newPhase}");

        switch (newPhase)
        {
            case GameStateManager.GamePhase.Dialogue:
                HideHUD();
                break;

            case GameStateManager.GamePhase.Exploration:
                ShowHUD();
                break;

            case GameStateManager.GamePhase.Investigation:
                ShowHUD();
                break;

            case GameStateManager.GamePhase.Cutscene:
                HideHUD();
                break;
        }
    }

    private void HandleChapterChanged(GameStateManager.Chapter newChapter)
    {
        UpdateChapterDisplay();
        ShowNotification($"챕터: {GetChapterName(newChapter)}", NotificationType.Info);
    }

    private void HandleLocationChanged(string previousLocation, string newLocation)
    {
        UpdateLocationDisplay();
    }

    private void HandleLocationChangedData(LocationData previous, LocationData current)
    {
        UpdateLocationDisplay();
    }

    private void HandleTimerTick(float remainingTime)
    {
        UpdateTimeDisplay();
    }

    private void HandleTimeWarning(int remainingSeconds)
    {
        ShowNotification($"⚠️ 시간이 얼마 남지 않았습니다! ({remainingSeconds}초)",
                        NotificationType.Warning);
    }

    private void HandleTimeUp()
    {
        ShowNotification("⏰ 시간이 모두 소진되었습니다!", NotificationType.Error);
    }

    private void HandleAPChanged(int current, int max)
    {
        UpdateAPDisplay();
    }

    private void HandleAPLow()
    {
        ShowNotification("⚠️ 행동력이 부족합니다!", NotificationType.Warning);
    }

    private void HandleAPCritical()
    {
        ShowNotification("🚨 행동력이 거의 없습니다!", NotificationType.Error);
    }

    // =========================================================
    // 🔹 INPUT HANDLING
    // =========================================================

    private void HandleInput()
    {
        // ESC - 일시정지 메뉴
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (pauseMenuPanel != null && !pauseMenuPanel.activeSelf)
            {
                ShowPauseMenu();
            }
            else if (pauseMenuPanel != null && pauseMenuPanel.activeSelf)
            {
                HidePauseMenu();
            }
        }


        // Tab - 수첩
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            TogglePanel(UIPanel.Notebook);
        }
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (pauseMenuPanel != null && !pauseMenuPanel.activeSelf)
            {
                ShowPauseMenu();
            }
            else if (pauseMenuPanel != null && pauseMenuPanel.activeSelf)
            {
                HidePauseMenu();
            }
        }
    }

    // =========================================================
    // 🔹 MENU FUNCTIONS
    // =========================================================

    public void ShowPauseMenu()
    {
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
            Time.timeScale = 0f;
        }
    }

    public void HidePauseMenu()
    {
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
            Time.timeScale = 1f;
        }
    }

    public void OnResumeButtonClicked()
    {
        HidePauseMenu();
    }

    public void OnSettingsButtonClicked()
    {
        ShowPanel(UIPanel.Settings);
    }

    public void OnMainMenuButtonClicked()
    {
        ShowPopup(
            "메인 메뉴로",
            "메인 메뉴로 돌아가시겠습니까?\n저장하지 않은 진행상황은 사라집니다.",
            onConfirm: () =>
            {
                Time.timeScale = 1f;
                Debug.Log("Return to main menu");
            }
        );
    }

    public void OnQuitButtonClicked()
    {
        ShowPopup(
            "게임 종료",
            "게임을 종료하시겠습니까?",
            onConfirm: () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        );
    }

    // =========================================================
    // 🔹 DEBUG
    // =========================================================

    public void PrintStatus()
    {
        Debug.Log("=== UI MANAGER STATUS ===");
        Debug.Log($"HUD Active: {hudPanel?.activeSelf}");
        Debug.Log($"Any Panel Open: {IsAnyPanelOpen}");
        Debug.Log($"Notification Queue: {notificationQueue?.Count ?? 0}");
    }

#if UNITY_EDITOR
    [ContextMenu("Show Test Notification")]
    private void DebugShowNotification()
    {
        ShowNotification("테스트 알림입니다!", NotificationType.Info);
    }

    [ContextMenu("Show Test Popup")]
    private void DebugShowPopup()
    {
        ShowPopup("테스트", "팝업 테스트입니다.",
                 onConfirm: () => Debug.Log("Confirmed"));
    }

    [ContextMenu("Refresh All")]
    private void DebugRefreshAll()
    {
        RefreshAll();
    }
#endif
}

// =========================================================
// 📦 DATA STRUCTURES
// =========================================================

public enum UIPanel
{
    HUD,
    PauseMenu,
    Settings,
    Notebook,
    Dialogue,
    Investigation
}

public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}

public class NotificationData
{
    public string message;
    public NotificationType type;
    public float duration;
}