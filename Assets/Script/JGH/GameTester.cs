using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// 매니저 시스템 테스트용 스크립트 (New Input System 대응)
/// </summary>
public class GameTester : MonoBehaviour
{
    [Header("Test Buttons")]
    public Button startButton;
    public Button moveButton;
    public Button talkButton;
    public Button observeButton;
    public Button clueButton;

    [Header("Test Settings")]
    public string testLocationID = "Library";
    public string testNPCID = "Butler";
    public string testClueID = "test_clue";

    private void Start()
    {
        SetupButtons();
    }

    private void SetupButtons()
    {
        if (startButton != null)
            startButton.onClick.AddListener(OnStartGame);

        if (moveButton != null)
            moveButton.onClick.AddListener(OnMoveTest);

        if (talkButton != null)
            talkButton.onClick.AddListener(OnTalkTest);

        if (observeButton != null)
            observeButton.onClick.AddListener(OnObserveTest);

        if (clueButton != null)
            clueButton.onClick.AddListener(OnClueTest);
    }

    // =========================================================
    // 🔹 테스트 메서드들
    // =========================================================

    public void OnStartGame()
    {
        Debug.Log("[TEST] 게임 시작!");
        GameManager.Instance?.StartGame();
    }

    public void OnMoveTest()
    {
        Debug.Log($"[TEST] {testLocationID}로 이동 시도");
        bool success = GameManager.Instance?.RequestMove(testLocationID) ?? false;

        Debug.Log(success ? "[TEST] ✓ 이동 성공!" : "[TEST] ✗ 이동 실패!");
    }

    public void OnTalkTest()
    {
        Debug.Log($"[TEST] {testNPCID}와 대화 시도");
        bool success = GameManager.Instance?.RequestDialogue(testNPCID) ?? false;

        Debug.Log(success ? "[TEST] ✓ 대화 시작!" : "[TEST] ✗ 대화 실패!");
    }

    public void OnObserveTest()
    {
        Debug.Log("[TEST] 관찰 모드 시작");
        bool success = GameManager.Instance?.RequestObservation(60f) ?? false;

        Debug.Log(success ? "[TEST] ✓ 관찰 모드 시작! (60초)" : "[TEST] ✗ 관찰 모드 실패!");
    }

    public void OnClueTest()
    {
        Debug.Log($"[TEST] {testClueID} 발견 시도");
        bool success = GameManager.Instance?.DiscoverClue(testClueID) ?? false;

        Debug.Log(success ? "[TEST] ✓ 단서 발견!" : "[TEST] ✗ 단서 발견 실패!");
    }

    // =========================================================
    // 🔹 디버그 키보드 단축키 (New Input System)
    // =========================================================

    private void Update()
    {
        if (Keyboard.current == null) return;

        // 1번 키: 게임 시작
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            OnStartGame();

        // 2번 키: 이동
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            OnMoveTest();

        // 3번 키: 대화
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            OnTalkTest();

        // 4번 키: 관찰
        if (Keyboard.current.digit4Key.wasPressedThisFrame)
            OnObserveTest();

        // 5번 키: 단서
        if (Keyboard.current.digit5Key.wasPressedThisFrame)
            OnClueTest();

        // S키: 상태 출력
        if (Keyboard.current.sKey.wasPressedThisFrame)
            PrintAllStatus();

        // R키: AP 리셋
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            ActionPointManager.Instance?.ResetPoints();
            Debug.Log("[TEST] AP 리셋!");
        }
    }

    private void PrintAllStatus()
    {
        Debug.Log("========== 전체 상태 출력 ==========");

        ActionPointManager.Instance?.PrintStatus();
        LocationManager.Instance?.PrintStatus();
        TimeManager.Instance?.PrintStatus();

        Debug.Log("====================================");
    }
}
