using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LocationManager
/// - 게임 내 장소 관리
/// - 이동 처리 및 이동 가능 여부 판단
/// - 장소별 NPC, 단서, 이벤트 관리
/// - 장소 잠금/해금 시스템
/// </summary>
public class LocationManager : MonoBehaviour
{
    public static LocationManager Instance { get; private set; }

    #region Location Data

    [Header("Location Database")]
    [SerializeField] private LocationData[] locationDatabase;

    private Dictionary<string, LocationData> locations;
    private LocationData currentLocation;
    private List<string> visitedLocations;
    private HashSet<string> unlockedLocations;

    #endregion

    #region Current State

    public LocationData CurrentLocation => currentLocation;
    public string CurrentLocationID => currentLocation?.locationID;
    public string CurrentLocationName => currentLocation?.locationName;

    #endregion

    #region Events

    public event Action<LocationData, LocationData> OnLocationChanged;  // (previous, current)
    public event Action<string> OnLocationUnlocked;  // 장소 해금
    public event Action<string> OnLocationVisited;  // 첫 방문
    public event Action<LocationData> OnLocationEntered;  // 장소 진입
    public event Action<LocationData> OnLocationExited;  // 장소 퇴장

    #endregion

    #region Dependencies

    private GameStateManager gameStateManager;
    private TimeManager timeManager;

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
        timeManager = FindObjectOfType<TimeManager>();

        // 데이터 초기화
        locations = new Dictionary<string, LocationData>();
        visitedLocations = new List<string>();
        unlockedLocations = new HashSet<string>();

        // 위치 데이터베이스 구축
        BuildLocationDatabase();

        Debug.Log("[LocationManager] Initialized");
    }

    private void BuildLocationDatabase()
    {
        if (locationDatabase == null || locationDatabase.Length == 0)
        {
            Debug.LogWarning("[LocationManager] No location data found, creating defaults");
            CreateDefaultLocations();
            return;
        }

        foreach (var location in locationDatabase)
        {
            if (location != null && !string.IsNullOrEmpty(location.locationID))
            {
                locations[location.locationID] = location;

                // 초기 해금 장소
                if (location.isInitiallyUnlocked)
                {
                    unlockedLocations.Add(location.locationID);
                }
            }
        }

        Debug.Log($"[LocationManager] Loaded {locations.Count} locations");
    }

    private void CreateDefaultLocations()
    {
        // 기본 장소들 생성
        locationDatabase = new LocationData[]
        {
            new LocationData
            {
                locationID = "MainHall",
                locationName = "메인 홀",
                description = "저택의 중앙 홀. 모든 방으로 통하는 중심지다.",
                isInitiallyUnlocked = true,
                moveCost = 0
            },
            new LocationData
            {
                locationID = "Library",
                locationName = "서재",
                description = "수많은 책들이 가득한 서재. 어딘가 단서가 숨어있을 것 같다.",
                isInitiallyUnlocked = true,
                moveCost = 1
            },
            new LocationData
            {
                locationID = "Bedroom",
                locationName = "침실",
                description = "고풍스러운 침실. 주인의 흔적이 남아있다.",
                isInitiallyUnlocked = true,
                moveCost = 1
            },
            new LocationData
            {
                locationID = "Kitchen",
                locationName = "주방",
                description = "넓은 주방. 요리 도구들이 정리되어 있다.",
                isInitiallyUnlocked = true,
                moveCost = 1
            },
            new LocationData
            {
                locationID = "Garden",
                locationName = "정원",
                description = "아름다운 정원. 밤에는 또 다른 분위기를 자아낸다.",
                isInitiallyUnlocked = true,
                moveCost = 1,
                timeRestrictions = new[] { GameStateManager.TimeSlot.Morning, 
                                          GameStateManager.TimeSlot.Afternoon }
            },
            new LocationData
            {
                locationID = "SecretRoom",
                locationName = "비밀의 방",
                description = "숨겨진 방. 이곳에 진실이 있을지도...",
                isInitiallyUnlocked = false,
                moveCost = 2,
                requiredFlags = new[] { "found_secret_key" }
            }
        };

        BuildLocationDatabase();
    }

    /// <summary>
    /// 초기 위치 설정
    /// </summary>
    public void SetInitialLocation(string locationID)
    {
        if (!locations.TryGetValue(locationID, out var location))
        {
            Debug.LogError($"[LocationManager] Location not found: {locationID}");
            return;
        }

        currentLocation = location;
        visitedLocations.Add(locationID);

        Debug.Log($"[LocationManager] Initial location set: {location.locationName}");
    }

    #endregion

    // =========================================================
    // 🔹 LOCATION MOVEMENT
    // =========================================================

    /// <summary>
    /// 장소 이동
    /// </summary>
    public bool MoveTo(string locationID)
    {
        // 존재하는 장소인지 확인
        if (!locations.TryGetValue(locationID, out var targetLocation))
        {
            Debug.LogError($"[LocationManager] Location not found: {locationID}");
            return false;
        }

        // 이동 가능 여부 확인
        if (!CanMoveTo(locationID, out string reason))
        {
            Debug.Log($"[LocationManager] Cannot move to {locationID}: {reason}");
            return false;
        }

        // 이전 위치 저장
        LocationData previousLocation = currentLocation;

        // 퇴장 이벤트
        if (previousLocation != null)
        {
            OnLocationExited?.Invoke(previousLocation);
        }

        // 위치 변경
        currentLocation = targetLocation;

        // 방문 기록
        bool isFirstVisit = !visitedLocations.Contains(locationID);
        if (isFirstVisit)
        {
            visitedLocations.Add(locationID);
            OnLocationVisited?.Invoke(locationID);
            
            // GameStateManager와 동기화
            gameStateManager?.AddFlag($"visited_{locationID}");
        }

        // 진입 이벤트
        OnLocationEntered?.Invoke(currentLocation);

        // 위치 변경 이벤트
        OnLocationChanged?.Invoke(previousLocation, currentLocation);

        Debug.Log($"[LocationManager] Moved: {previousLocation?.locationName ?? "None"} → {currentLocation.locationName}");

        // 장소 진입 효과 적용
        ApplyLocationEffects(currentLocation);

        return true;
    }

    /// <summary>
    /// 이동 가능 여부 확인
    /// </summary>
    public bool CanMoveTo(string locationID, out string reason)
    {
        reason = "";

        // 존재하는 장소인가?
        if (!locations.TryGetValue(locationID, out var targetLocation))
        {
            reason = "존재하지 않는 장소입니다.";
            return false;
        }

        // 현재 위치와 같은가?
        if (currentLocation != null && currentLocation.locationID == locationID)
        {
            reason = "이미 이 장소에 있습니다.";
            return false;
        }

        // 해금되어 있는가?
        if (!IsLocationUnlocked(locationID))
        {
            reason = "아직 갈 수 없는 장소입니다.";
            return false;
        }

        // 시간 제약 확인
        if (!CheckTimeRestrictions(targetLocation))
        {
            reason = GetTimeRestrictionMessage(targetLocation);
            return false;
        }

        // 필요한 플래그가 있는가?
        if (!CheckRequiredFlags(targetLocation))
        {
            reason = "필요한 조건을 만족하지 못했습니다.";
            return false;
        }

        // 챕터 제약 확인
        if (!CheckChapterRestrictions(targetLocation))
        {
            reason = "이 챕터에서는 갈 수 없는 장소입니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 시간 제약 확인
    /// </summary>
    private bool CheckTimeRestrictions(LocationData location)
    {
        if (location.timeRestrictions == null || location.timeRestrictions.Length == 0)
            return true;

        if (timeManager == null)
            return true;

        var currentTime = timeManager.CurrentPeriod;

        foreach (var allowedTime in location.timeRestrictions)
        {
            if (currentTime == allowedTime)
                return true;
        }

        return false;
    }

    private string GetTimeRestrictionMessage(LocationData location)
    {
        if (location.timeRestrictions == null || location.timeRestrictions.Length == 0)
            return "";

        string times = string.Join(", ", Array.ConvertAll(location.timeRestrictions, 
            t => timeManager?.GetTimePeriodName(t) ?? t.ToString()));

        return $"이 장소는 {times}에만 갈 수 있습니다.";
    }

    /// <summary>
    /// 필수 플래그 확인
    /// </summary>
    private bool CheckRequiredFlags(LocationData location)
    {
        if (location.requiredFlags == null || location.requiredFlags.Length == 0)
            return true;

        if (gameStateManager == null)
            return true;

        foreach (var flag in location.requiredFlags)
        {
            if (!gameStateManager.HasFlag(flag))
                return false;
        }

        return true;
    }

    /// <summary>
    /// 챕터 제약 확인
    /// </summary>
    private bool CheckChapterRestrictions(LocationData location)
    {
        if (location.chapterRestrictions == null || location.chapterRestrictions.Length == 0)
            return true;

        if (gameStateManager == null)
            return true;

        var currentChapter = gameStateManager.CurrentChapter;

        foreach (var allowedChapter in location.chapterRestrictions)
        {
            if (currentChapter == allowedChapter)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 장소 진입 효과 적용
    /// </summary>
    private void ApplyLocationEffects(LocationData location)
    {
        // 자동 이벤트 트리거
        if (!string.IsNullOrEmpty(location.onEnterEvent))
        {
            TriggerLocationEvent(location.onEnterEvent);
        }

        // 첫 방문 이벤트
        if (!string.IsNullOrEmpty(location.onFirstVisitEvent) && 
            visitedLocations.Count == 1 && visitedLocations.Contains(location.locationID))
        {
            TriggerLocationEvent(location.onFirstVisitEvent);
        }

        // 배경음악 변경
        if (!string.IsNullOrEmpty(location.bgmName))
        {
            // AudioManager.PlayBGM(location.bgmName);
            Debug.Log($"[LocationManager] BGM changed: {location.bgmName}");
        }
    }

    private void TriggerLocationEvent(string eventName)
    {
        Debug.Log($"[LocationManager] Triggering event: {eventName}");
        // EventManager.TriggerEvent(eventName);
    }

    // =========================================================
    // 🔹 LOCATION UNLOCK SYSTEM
    // =========================================================

    /// <summary>
    /// 장소 해금
    /// </summary>
    public bool UnlockLocation(string locationID)
    {
        if (!locations.ContainsKey(locationID))
        {
            Debug.LogError($"[LocationManager] Cannot unlock non-existent location: {locationID}");
            return false;
        }

        if (unlockedLocations.Contains(locationID))
        {
            Debug.Log($"[LocationManager] Location already unlocked: {locationID}");
            return false;
        }

        unlockedLocations.Add(locationID);
        OnLocationUnlocked?.Invoke(locationID);

        Debug.Log($"[LocationManager] Location unlocked: {locations[locationID].locationName}");
        return true;
    }

    /// <summary>
    /// 장소 잠금
    /// </summary>
    public bool LockLocation(string locationID)
    {
        if (unlockedLocations.Remove(locationID))
        {
            Debug.Log($"[LocationManager] Location locked: {locationID}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 장소가 해금되어 있는지 확인
    /// </summary>
    public bool IsLocationUnlocked(string locationID)
    {
        return unlockedLocations.Contains(locationID);
    }

    // =========================================================
    // 🔹 LOCATION QUERIES
    // =========================================================

    /// <summary>
    /// 장소 정보 가져오기
    /// </summary>
    public LocationData GetLocation(string locationID)
    {
        locations.TryGetValue(locationID, out var location);
        return location;
    }

    /// <summary>
    /// 모든 해금된 장소 목록
    /// </summary>
    public List<LocationData> GetUnlockedLocations()
    {
        List<LocationData> result = new List<LocationData>();

        foreach (var locationID in unlockedLocations)
        {
            if (locations.TryGetValue(locationID, out var location))
            {
                result.Add(location);
            }
        }

        return result;
    }

    /// <summary>
    /// 이동 가능한 장소 목록 (현재 조건에서)
    /// </summary>
    public List<LocationData> GetAvailableLocations()
    {
        List<LocationData> result = new List<LocationData>();

        foreach (var locationID in unlockedLocations)
        {
            if (CanMoveTo(locationID, out _))
            {
                if (locations.TryGetValue(locationID, out var location))
                {
                    result.Add(location);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 방문한 장소 목록
    /// </summary>
    public List<string> GetVisitedLocations()
    {
        return new List<string>(visitedLocations);
    }

    /// <summary>
    /// 장소 방문 여부
    /// </summary>
    public bool HasVisited(string locationID)
    {
        return visitedLocations.Contains(locationID);
    }

    /// <summary>
    /// 현재 장소의 NPC 목록
    /// </summary>
    public List<string> GetNPCsInCurrentLocation()
    {
        if (currentLocation == null || currentLocation.npcsPresent == null)
            return new List<string>();

        return new List<string>(currentLocation.npcsPresent);
    }

    /// <summary>
    /// 현재 장소의 단서 목록
    /// </summary>
    public List<string> GetCluesInCurrentLocation()
    {
        if (currentLocation == null || currentLocation.cluesAvailable == null)
            return new List<string>();

        // 아직 발견하지 않은 단서만 반환
        List<string> undiscoveredClues = new List<string>();

        foreach (var clue in currentLocation.cluesAvailable)
        {
            if (gameStateManager != null && !gameStateManager.HasFlag($"clue_{clue}"))
            {
                undiscoveredClues.Add(clue);
            }
        }

        return undiscoveredClues;
    }

    /// <summary>
    /// 이동 비용 계산
    /// </summary>
    public int GetMoveCost(string locationID)
    {
        if (locations.TryGetValue(locationID, out var location))
        {
            return location.moveCost;
        }

        return 1;  // 기본값
    }

    // =========================================================
    // 🔹 LOCATION DISCOVERY
    // =========================================================

    /// <summary>
    /// 장소 발견 (탐색을 통해 새로운 장소를 찾음)
    /// </summary>
    public void DiscoverLocation(string locationID)
    {
        if (!locations.ContainsKey(locationID))
        {
            Debug.LogError($"[LocationManager] Cannot discover non-existent location: {locationID}");
            return;
        }

        // 자동으로 해금
        UnlockLocation(locationID);

        // 발견 플래그 추가
        gameStateManager?.AddFlag($"discovered_{locationID}");

        Debug.Log($"[LocationManager] New location discovered: {locations[locationID].locationName}");
    }

    // =========================================================
    // 🔹 SAVE/LOAD
    // =========================================================

    [System.Serializable]
    public class LocationSaveData
    {
        public string currentLocationID;
        public List<string> visitedLocations;
        public List<string> unlockedLocations;
    }

    public LocationSaveData GetSaveData()
    {
        return new LocationSaveData
        {
            currentLocationID = currentLocation?.locationID,
            visitedLocations = new List<string>(visitedLocations),
            unlockedLocations = new List<string>(unlockedLocations)
        };
    }

    public void LoadSaveData(LocationSaveData data)
    {
        if (data == null)
        {
            Debug.LogError("[LocationManager] Cannot load null save data");
            return;
        }

        // 방문 기록 복원
        visitedLocations = data.visitedLocations ?? new List<string>();
        
        // 해금 상태 복원
        unlockedLocations = new HashSet<string>(data.unlockedLocations ?? new List<string>());

        // 현재 위치 복원
        if (!string.IsNullOrEmpty(data.currentLocationID))
        {
            if (locations.TryGetValue(data.currentLocationID, out var location))
            {
                currentLocation = location;
            }
        }

        Debug.Log("[LocationManager] Save data loaded");
    }

    // =========================================================
    // 🔹 DEBUG
    // =========================================================

    public void PrintStatus()
    {
        Debug.Log("=== LOCATION MANAGER STATUS ===");
        Debug.Log($"Current Location: {CurrentLocationName ?? "None"}");
        Debug.Log($"Unlocked Locations: {unlockedLocations.Count}");
        Debug.Log($"Visited Locations: {visitedLocations.Count}");
        Debug.Log($"Available to Move: {GetAvailableLocations().Count}");
        
        if (currentLocation != null)
        {
            Debug.Log($"\nCurrent Location Details:");
            Debug.Log($"- NPCs: {(currentLocation.npcsPresent != null ? currentLocation.npcsPresent.Length : 0)}");
            Debug.Log($"- Clues: {GetCluesInCurrentLocation().Count} undiscovered");
        }
    }

    #if UNITY_EDITOR
    [ContextMenu("Unlock All Locations")]
    private void DebugUnlockAll()
    {
        foreach (var locationID in locations.Keys)
        {
            UnlockLocation(locationID);
        }
        Debug.Log("[LocationManager] All locations unlocked");
    }

    [ContextMenu("Print Available Locations")]
    private void DebugPrintAvailable()
    {
        var available = GetAvailableLocations();
        Debug.Log($"Available Locations ({available.Count}):");
        foreach (var loc in available)
        {
            Debug.Log($"- {loc.locationName} (Cost: {loc.moveCost})");
        }
    }

    [ContextMenu("Print Status")]
    private void DebugPrintStatus()
    {
        PrintStatus();
    }
    #endif
}

// =========================================================
// 📦 LOCATION DATA STRUCTURE
// =========================================================

/// <summary>
/// 장소 데이터
/// </summary>
[System.Serializable]
public class LocationData
{
    [Header("Basic Info")]
    public string locationID;
    public string locationName;
    [TextArea(3, 5)]
    public string description;

    [Header("Access Settings")]
    public bool isInitiallyUnlocked = false;
    public int moveCost = 1;  // 이동에 필요한 AP

    [Header("Restrictions")]
    public GameStateManager.TimeSlot[] timeRestrictions;  // 특정 시간대에만 접근 가능
    public string[] requiredFlags;  // 필요한 플래그들
    public GameStateManager.Chapter[] chapterRestrictions;  // 특정 챕터에만 접근 가능

    [Header("Content")]
    public string[] npcsPresent;  // 이 장소에 있는 NPC들
    public string[] cluesAvailable;  // 이 장소에서 찾을 수 있는 단서들

    [Header("Events")]
    public string onEnterEvent;  // 진입 시 발생하는 이벤트
    public string onFirstVisitEvent;  // 첫 방문 시 발생하는 이벤트

    [Header("Presentation")]
    public string bgmName;  // 이 장소의 배경음악
    public Sprite backgroundImage;  // 배경 이미지 (옵션)
}