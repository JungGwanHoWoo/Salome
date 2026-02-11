using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class Dialogue : MonoBehaviour
{
    public static Dialogue Instance { get; private set; }

    [Header("UI References")]
    public TypeWriterText aiText;
    public TMP_InputField playerInput;
    public GameObject dialoguePanel;

    private NPCData currentNPC;
    private bool isWaitingForInput = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        dialoguePanel.SetActive(false);
        playerInput.gameObject.SetActive(false);
        playerInput.onSubmit.AddListener(OnPlayerSubmit);
    }

    // NPC 클릭 시 호출 (isFirstMeet으로 첫 대화 여부 판단)
    public void StartDialogue(NPCData npcData, bool isFirstMeet)
    {
        currentNPC = npcData;
        dialoguePanel.SetActive(true);

        if (isFirstMeet)
        {
            // 첫 대화: firstLine 출력
            aiText.gameObject.SetActive(true);
            playerInput.gameObject.SetActive(false);
            aiText.StartTyping(currentNPC.firstLine);
            isWaitingForInput = true;
            Debug.Log($"[Dialogue] {currentNPC.npcName}과의 첫 대화 시작");
        }
        else
        {
            // 재대화: 바로 입력창
            aiText.gameObject.SetActive(false);
            playerInput.gameObject.SetActive(true);
            playerInput.text = "";
            playerInput.ActivateInputField();
            isWaitingForInput = false;
            Debug.Log($"[Dialogue] {currentNPC.npcName}과의 재대화 시작");
        }
    }

    void Update()
    {
        // 엔터 → 입력창 표시
        if (aiText.gameObject.activeInHierarchy && isWaitingForInput && aiText.IsFinished)
        {
            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
            {
                ShowInputField();
            }
        }

        // ESC → 대화 종료
        if (dialoguePanel.activeInHierarchy)
        {
            // NPC 타이핑이 끝났을 때 OR InputField가 활성화되어 있을 때
            bool canClose = (aiText.gameObject.activeInHierarchy && aiText.IsFinished) ||
                            playerInput.gameObject.activeInHierarchy;

            if (canClose && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                EndDialogue();
            }
        }
    }

    void ShowInputField()
    {
        isWaitingForInput = false;
        aiText.gameObject.SetActive(false);
        playerInput.gameObject.SetActive(true);
        playerInput.text = "";
        playerInput.ActivateInputField();
    }

    public void OnPlayerSubmit(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText) || currentNPC == null) return;

        playerInput.gameObject.SetActive(false);
        aiText.gameObject.SetActive(true);

        string josa = HasFinalConsonant(currentNPC.npcName) ? "이" : "가";
        aiText.StartTyping($"{currentNPC.npcName}{josa} 답변을 생각 중입니다...");

        GeminiManager.Instance.AskGemini(
            userText,
            currentNPC.npcPrompt,
            (aiResponse) =>
            {
                aiText.StartTyping(aiResponse);
                isWaitingForInput = true;
            },
            currentNPC.npcId
        );
    }

    // 대화 종료
    public void EndDialogue()
    {
        Debug.Log($"[Dialogue] 대화 종료");
        dialoguePanel.SetActive(false);
        aiText.gameObject.SetActive(false);
        playerInput.gameObject.SetActive(false);
        currentNPC = null;
        isWaitingForInput = false;
    }

    private bool HasFinalConsonant(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        char lastChar = text[text.Length - 1];
        if (lastChar < 0xAC00 || lastChar > 0xD7A3) return false;
        return (lastChar - 0xAC00) % 28 != 0;
    }
}