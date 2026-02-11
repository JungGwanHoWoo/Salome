using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NotebookManager
/// - 수첩/노트 시스템 관리
/// - 단서 수집 및 정리
/// - 추리 시스템
/// - 인물 관계도
/// </summary>
public class NotebookManager : MonoBehaviour
{
    public static NotebookManager Instance { get; private set; }

    #region Data

    [Header("Clue Database")]
    [SerializeField] private ClueDatabase clueDatabase;

    [Header("Character Database")]
    [SerializeField] private CharacterDatabase characterDatabase;

    private Dictionary<string, ClueData> allClues;
    private Dictionary<string, CharacterData> allCharacters;
    
    private HashSet<string> discoveredClues;      // 발견한 단서들
    private HashSet<string> metCharacters;        // 만난 인물들
    private List<DeductionEntry> deductions;      // 추리 기록
    private Dictionary<string, string> characterRelations;  // 인물 관계

    #endregion

    #region Current State

    public int DiscoveredCluesCount => discoveredClues.Count;
    public int TotalCluesCount => allClues.Count;
    public int MetCharactersCount => metCharacters.Count;
    public int DeductionsCount => deductions.Count;

    #endregion

    #region Events

    public event Action<ClueData> OnClueDiscovered;
    public event Action<CharacterData> OnCharacterMet;
    public event Action<DeductionEntry> OnDeductionMade;
    public event Action<string, string> OnRelationRevealed;  // character1, character2
    public event Action OnNotebookUpdated;

    #endregion

    #region Dependencies

    private GameStateManager gameStateManager;

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

        // 데이터 초기화
        allClues = new Dictionary<string, ClueData>();
        allCharacters = new Dictionary<string, CharacterData>();
        discoveredClues = new HashSet<string>();
        metCharacters = new HashSet<string>();
        deductions = new List<DeductionEntry>();
        characterRelations = new Dictionary<string, string>();

        // 데이터베이스 로드
        LoadClueDatabase();
        LoadCharacterDatabase();

        Debug.Log("[NotebookManager] Initialized");
    }

    private void LoadClueDatabase()
    {
        if (clueDatabase == null || clueDatabase.clues == null)
        {
            Debug.LogWarning("[NotebookManager] No clue database assigned, creating defaults");
            CreateDefaultClues();
            return;
        }

        foreach (var clue in clueDatabase.clues)
        {
            if (clue != null && !string.IsNullOrEmpty(clue.clueID))
            {
                allClues[clue.clueID] = clue;
            }
        }

        Debug.Log($"[NotebookManager] Loaded {allClues.Count} clues");
    }

    private void LoadCharacterDatabase()
    {
        if (characterDatabase == null || characterDatabase.characters == null)
        {
            Debug.LogWarning("[NotebookManager] No character database assigned");
            return;
        }

        foreach (var character in characterDatabase.characters)
        {
            if (character != null && !string.IsNullOrEmpty(character.characterID))
            {
                allCharacters[character.characterID] = character;
            }
        }

        Debug.Log($"[NotebookManager] Loaded {allCharacters.Count} characters");
    }

    private void CreateDefaultClues()
    {
        // 기본 단서 생성 (예시)
        var defaultClues = new ClueData[]
        {
            new ClueData
            {
                clueID = "bloody_knife",
                clueName = "피 묻은 칼",
                description = "주방에서 발견한 칼. 핏자국이 선명하게 남아있다.",
                category = ClueCategory.Evidence,
                importance = ClueImportance.Critical
            },
            new ClueData
            {
                clueID = "torn_letter",
                clueName = "찢어진 편지",
                description = "누군가의 편지. 내용이 일부 찢어져 있다.",
                category = ClueCategory.Document,
                importance = ClueImportance.Important
            }
        };

        foreach (var clue in defaultClues)
        {
            allClues[clue.clueID] = clue;
        }
    }

    #endregion

    // =========================================================
    // 🔹 CLUE MANAGEMENT
    // =========================================================

    /// <summary>
    /// 단서 추가
    /// </summary>
    public bool AddClue(string clueID)
    {
        if (string.IsNullOrEmpty(clueID))
        {
            Debug.LogError("[NotebookManager] Cannot add null or empty clue ID");
            return false;
        }

        // 이미 발견한 단서인가?
        if (discoveredClues.Contains(clueID))
        {
            Debug.Log($"[NotebookManager] Clue already discovered: {clueID}");
            return false;
        }

        // 단서 데이터 확인
        if (!allClues.TryGetValue(clueID, out var clueData))
        {
            Debug.LogError($"[NotebookManager] Clue not found in database: {clueID}");
            return false;
        }

        // 단서 추가
        discoveredClues.Add(clueID);

        // 플래그 설정
        gameStateManager?.AddFlag($"clue_{clueID}");
        gameStateManager?.AddFlag($"investigated_{clueID}");

        // 이벤트 발생
        OnClueDiscovered?.Invoke(clueData);
        OnNotebookUpdated?.Invoke();

        Debug.Log($"[NotebookManager] Clue discovered: {clueData.clueName}");

        // 자동 추리 체크
        CheckAutoDeductions(clueID);

        return true;
    }

    /// <summary>
    /// 단서 소유 여부
    /// </summary>
    public bool HasClue(string clueID)
    {
        return discoveredClues.Contains(clueID);
    }

    /// <summary>
    /// 단서 데이터 가져오기
    /// </summary>
    public ClueData GetClue(string clueID)
    {
        allClues.TryGetValue(clueID, out var clue);
        return clue;
    }

    /// <summary>
    /// 발견한 모든 단서
    /// </summary>
    public List<ClueData> GetDiscoveredClues()
    {
        List<ClueData> result = new List<ClueData>();

        foreach (var clueID in discoveredClues)
        {
            if (allClues.TryGetValue(clueID, out var clue))
            {
                result.Add(clue);
            }
        }

        return result;
    }

    /// <summary>
    /// 카테고리별 단서 가져오기
    /// </summary>
    public List<ClueData> GetCluesByCategory(ClueCategory category)
    {
        List<ClueData> result = new List<ClueData>();

        foreach (var clueID in discoveredClues)
        {
            if (allClues.TryGetValue(clueID, out var clue))
            {
                if (clue.category == category)
                {
                    result.Add(clue);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 중요도별 단서 가져오기
    /// </summary>
    public List<ClueData> GetCluesByImportance(ClueImportance importance)
    {
        List<ClueData> result = new List<ClueData>();

        foreach (var clueID in discoveredClues)
        {
            if (allClues.TryGetValue(clueID, out var clue))
            {
                if (clue.importance == importance)
                {
                    result.Add(clue);
                }
            }
        }

        return result;
    }

    // =========================================================
    // 🔹 CHARACTER MANAGEMENT
    // =========================================================

    /// <summary>
    /// 인물 등록 (처음 만남)
    /// </summary>
    public bool MeetCharacter(string characterID)
    {
        if (string.IsNullOrEmpty(characterID))
        {
            Debug.LogError("[NotebookManager] Cannot meet null or empty character ID");
            return false;
        }

        // 이미 만난 인물인가?
        if (metCharacters.Contains(characterID))
        {
            Debug.Log($"[NotebookManager] Character already met: {characterID}");
            return false;
        }

        // 인물 데이터 확인
        if (!allCharacters.TryGetValue(characterID, out var characterData))
        {
            Debug.LogWarning($"[NotebookManager] Character not found in database: {characterID}");
            // 데이터가 없어도 일단 등록
            metCharacters.Add(characterID);
            return true;
        }

        // 인물 추가
        metCharacters.Add(characterID);

        // 플래그 설정
        gameStateManager?.AddFlag($"met_{characterID}");

        // 이벤트 발생
        OnCharacterMet?.Invoke(characterData);
        OnNotebookUpdated?.Invoke();

        Debug.Log($"[NotebookManager] Met character: {characterData.characterName}");

        return true;
    }

    /// <summary>
    /// 인물을 만났는지 확인
    /// </summary>
    public bool HasMetCharacter(string characterID)
    {
        return metCharacters.Contains(characterID);
    }

    /// <summary>
    /// 인물 데이터 가져오기
    /// </summary>
    public CharacterData GetCharacter(string characterID)
    {
        allCharacters.TryGetValue(characterID, out var character);
        return character;
    }

    /// <summary>
    /// 만난 모든 인물
    /// </summary>
    public List<CharacterData> GetMetCharacters()
    {
        List<CharacterData> result = new List<CharacterData>();

        foreach (var characterID in metCharacters)
        {
            if (allCharacters.TryGetValue(characterID, out var character))
            {
                result.Add(character);
            }
        }

        return result;
    }

    /// <summary>
    /// 인물 관계 설정
    /// </summary>
    public void SetCharacterRelation(string character1, string character2, string relationship)
    {
        string key = GetRelationKey(character1, character2);
        
        if (!characterRelations.ContainsKey(key))
        {
            characterRelations[key] = relationship;
            
            OnRelationRevealed?.Invoke(character1, character2);
            OnNotebookUpdated?.Invoke();

            Debug.Log($"[NotebookManager] Relation revealed: {character1} - {character2} ({relationship})");
        }
    }

    /// <summary>
    /// 인물 관계 가져오기
    /// </summary>
    public string GetCharacterRelation(string character1, string character2)
    {
        string key = GetRelationKey(character1, character2);
        characterRelations.TryGetValue(key, out var relation);
        return relation;
    }

    private string GetRelationKey(string char1, string char2)
    {
        // 정렬해서 동일한 키 생성 (A-B == B-A)
        if (string.Compare(char1, char2) < 0)
            return $"{char1}:{char2}";
        else
            return $"{char2}:{char1}";
    }

    // =========================================================
    // 🔹 DEDUCTION SYSTEM
    // =========================================================

    /// <summary>
    /// 추리 시도
    /// </summary>
    public DeductionResult MakeDeduction(string[] requiredClues, string deductionText, string resultFlag)
    {
        // 필요한 단서를 모두 가지고 있는가?
        foreach (var clueID in requiredClues)
        {
            if (!HasClue(clueID))
            {
                Debug.Log($"[NotebookManager] Missing clue for deduction: {clueID}");
                return new DeductionResult
                {
                    success = false,
                    message = "필요한 단서가 부족합니다.",
                    missingClues = GetMissingClues(requiredClues)
                };
            }
        }

        // 추리 성공
        var deduction = new DeductionEntry
        {
            deductionText = deductionText,
            usedClues = new List<string>(requiredClues),
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            chapter = gameStateManager?.CurrentChapter ?? GameStateManager.Chapter.Prologue
        };

        deductions.Add(deduction);

        // 결과 플래그 설정
        if (!string.IsNullOrEmpty(resultFlag))
        {
            gameStateManager?.AddFlag(resultFlag);
        }

        // 이벤트 발생
        OnDeductionMade?.Invoke(deduction);
        OnNotebookUpdated?.Invoke();

        Debug.Log($"[NotebookManager] Deduction made: {deductionText}");

        return new DeductionResult
        {
            success = true,
            message = "추리에 성공했습니다!",
            deduction = deduction
        };
    }

    /// <summary>
    /// 자동 추리 체크 (특정 단서 조합 시 자동 발동)
    /// </summary>
    private void CheckAutoDeductions(string newClueID)
    {
        // 예: 피 묻은 칼 + 주방 출입 기록 = 주방장 의심
        if (HasClue("bloody_knife") && HasClue("kitchen_access_log"))
        {
            if (!gameStateManager.HasFlag("deduction_suspect_chef"))
            {
                MakeDeduction(
                    new[] { "bloody_knife", "kitchen_access_log" },
                    "칼이 주방에서 나왔고, 주방장만 주방에 접근할 수 있었다. 주방장을 의심해봐야겠다.",
                    "deduction_suspect_chef"
                );
            }
        }

        // 다른 자동 추리 조건들...
    }

    /// <summary>
    /// 부족한 단서 목록
    /// </summary>
    private List<string> GetMissingClues(string[] requiredClues)
    {
        List<string> missing = new List<string>();

        foreach (var clueID in requiredClues)
        {
            if (!HasClue(clueID))
            {
                missing.Add(clueID);
            }
        }

        return missing;
    }

    /// <summary>
    /// 모든 추리 기록
    /// </summary>
    public List<DeductionEntry> GetAllDeductions()
    {
        return new List<DeductionEntry>(deductions);
    }

    /// <summary>
    /// 추리 모드 열기
    /// </summary>
    public void OpenDeductionMode()
    {
        Debug.Log("[NotebookManager] Deduction mode opened");
        
        // Phase 변경
        if (gameStateManager != null)
        {
            gameStateManager.SetPhase(GameStateManager.GamePhase.Investigation);
        }

        // UI 표시
        // NotebookUI.ShowDeductionPanel();
    }

    // =========================================================
    // 🔹 NOTEBOOK SECTIONS
    // =========================================================

    /// <summary>
    /// 수첩 진행도 계산
    /// </summary>
    public float GetCompletionProgress()
    {
        if (allClues.Count == 0)
            return 0f;

        return (float)discoveredClues.Count / allClues.Count;
    }

    /// <summary>
    /// 챕터별 단서 수집 현황
    /// </summary>
    public Dictionary<GameStateManager.Chapter, int> GetCluesByChapter()
    {
        Dictionary<GameStateManager.Chapter, int> result = new Dictionary<GameStateManager.Chapter, int>();

        foreach (var clueID in discoveredClues)
        {
            if (allClues.TryGetValue(clueID, out var clue))
            {
                if (clue.relatedChapter != null)
                {
                    var chapter = clue.relatedChapter.Value;
                    if (!result.ContainsKey(chapter))
                        result[chapter] = 0;
                    
                    result[chapter]++;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 핵심 단서 누락 확인
    /// </summary>
    public List<ClueData> GetMissingCriticalClues()
    {
        List<ClueData> missing = new List<ClueData>();

        foreach (var clue in allClues.Values)
        {
            if (clue.importance == ClueImportance.Critical && !discoveredClues.Contains(clue.clueID))
            {
                missing.Add(clue);
            }
        }

        return missing;
    }

    // =========================================================
    // 🔹 HINTS SYSTEM
    // =========================================================

    /// <summary>
    /// 힌트 제공
    /// </summary>
    public string GetHint()
    {
        // 현재 챕터의 미발견 핵심 단서 힌트
        var currentChapter = gameStateManager?.CurrentChapter ?? GameStateManager.Chapter.Prologue;
        
        foreach (var clue in allClues.Values)
        {
            if (clue.relatedChapter == currentChapter && 
                clue.importance == ClueImportance.Critical &&
                !discoveredClues.Contains(clue.clueID))
            {
                return clue.hint ?? "더 자세히 조사해보세요.";
            }
        }

        return "현재 찾을 수 있는 모든 단서를 발견했습니다.";
    }

    // =========================================================
    // 🔹 SAVE/LOAD
    // =========================================================

    [System.Serializable]
    public class NotebookSaveData
    {
        public List<string> discoveredClues;
        public List<string> metCharacters;
        public List<DeductionEntry> deductions;
        public Dictionary<string, string> characterRelations;
    }

    public NotebookSaveData GetSaveData()
    {
        return new NotebookSaveData
        {
            discoveredClues = new List<string>(discoveredClues),
            metCharacters = new List<string>(metCharacters),
            deductions = new List<DeductionEntry>(deductions),
            characterRelations = new Dictionary<string, string>(characterRelations)
        };
    }

    public void LoadSaveData(NotebookSaveData data)
    {
        if (data == null)
        {
            Debug.LogError("[NotebookManager] Cannot load null save data");
            return;
        }

        discoveredClues = new HashSet<string>(data.discoveredClues ?? new List<string>());
        metCharacters = new HashSet<string>(data.metCharacters ?? new List<string>());
        deductions = data.deductions ?? new List<DeductionEntry>();
        characterRelations = data.characterRelations ?? new Dictionary<string, string>();

        OnNotebookUpdated?.Invoke();

        Debug.Log("[NotebookManager] Save data loaded");
    }

    // =========================================================
    // 🔹 DEBUG
    // =========================================================

    public void PrintStatus()
    {
        Debug.Log("=== NOTEBOOK MANAGER STATUS ===");
        Debug.Log($"Discovered Clues: {discoveredClues.Count} / {allClues.Count}");
        Debug.Log($"Met Characters: {metCharacters.Count} / {allCharacters.Count}");
        Debug.Log($"Deductions Made: {deductions.Count}");
        Debug.Log($"Completion: {GetCompletionProgress() * 100:F1}%");

        var criticalMissing = GetMissingCriticalClues();
        if (criticalMissing.Count > 0)
        {
            Debug.Log($"Missing Critical Clues: {criticalMissing.Count}");
        }
    }

    #if UNITY_EDITOR
    [ContextMenu("Add Test Clue")]
    private void DebugAddClue()
    {
        AddClue("bloody_knife");
    }

    [ContextMenu("Meet Test Character")]
    private void DebugMeetCharacter()
    {
        MeetCharacter("Butler");
    }

    [ContextMenu("Print Status")]
    private void DebugPrintStatus()
    {
        PrintStatus();
    }

    [ContextMenu("Show Hint")]
    private void DebugShowHint()
    {
        Debug.Log($"Hint: {GetHint()}");
    }
    #endif
}

// =========================================================
// 📦 DATA STRUCTURES
// =========================================================

/// <summary>
/// 단서 데이터베이스 (ScriptableObject 권장)
/// </summary>
[System.Serializable]
public class ClueDatabase
{
    public ClueData[] clues;
}

/// <summary>
/// 단서 데이터
/// </summary>
[System.Serializable]
public class ClueData
{
    [Header("Basic Info")]
    public string clueID;
    public string clueName;
    [TextArea(3, 5)]
    public string description;

    [Header("Classification")]
    public ClueCategory category;
    public ClueImportance importance;
    public GameStateManager.Chapter? relatedChapter;

    [Header("Discovery")]
    public string locationFound;  // 발견 장소
    public string hint;  // 힌트 텍스트

    [Header("Visual")]
    public Sprite clueImage;

    [Header("Relations")]
    public string[] relatedClues;  // 관련된 다른 단서들
    public string[] relatedCharacters;  // 관련 인물들
}

/// <summary>
/// 단서 카테고리
/// </summary>
public enum ClueCategory
{
    Evidence,      // 증거물
    Document,      // 문서
    Testimony,     // 증언
    Photo,         // 사진
    Personal,      // 개인물품
    Environmental  // 환경 단서
}

/// <summary>
/// 단서 중요도
/// </summary>
public enum ClueImportance
{
    Minor,         // 부수적
    Important,     // 중요
    Critical       // 핵심
}

/// <summary>
/// 인물 데이터베이스
/// </summary>
[System.Serializable]
public class CharacterDatabase
{
    public CharacterData[] characters;
}

/// <summary>
/// 인물 데이터
/// </summary>
[System.Serializable]
public class CharacterData
{
    [Header("Basic Info")]
    public string characterID;
    public string characterName;
    [TextArea(2, 4)]
    public string description;

    [Header("Profile")]
    public int age;
    public string occupation;
    public string alibi;  // 알리바이

    [Header("Visual")]
    public Sprite portrait;

    [Header("Relations")]
    public CharacterRole role;
    public int suspicionLevel;  // 0-100
}

/// <summary>
/// 인물 역할
/// </summary>
public enum CharacterRole
{
    Victim,        // 피해자
    Suspect,       // 용의자
    Witness,       // 목격자
    Investigator,  // 수사관
    Neutral        // 중립
}

/// <summary>
/// 추리 기록
/// </summary>
[System.Serializable]
public class DeductionEntry
{
    public string deductionText;
    public List<string> usedClues;
    public string timestamp;
    public GameStateManager.Chapter chapter;
}

/// <summary>
/// 추리 결과
/// </summary>
public class DeductionResult
{
    public bool success;
    public string message;
    public DeductionEntry deduction;
    public List<string> missingClues;
}