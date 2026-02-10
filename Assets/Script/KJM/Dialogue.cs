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

    private NPCData currentNPC; // 현재 대화 중인 NPC
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

    // NPC 클릭 시 호출 (NPCData를 받음)
    public void StartDialogue(NPCData npcData)
    {
        currentNPC = npcData;

        dialoguePanel.SetActive(true);
        aiText.gameObject.SetActive(true);
        playerInput.gameObject.SetActive(false);

        aiText.StartTyping(currentNPC.firstLine);
        isWaitingForInput = true;

        Debug.Log($"[DialogueManager] {currentNPC.npcName}과의 대화 시작");
    }

    void Update()
    {
        if (aiText.gameObject.activeInHierarchy && isWaitingForInput && aiText.IsFinished)
        {
            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
            {
                ShowInputField();
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

        GeminiManager.Instance.AskGemini(userText, currentNPC.npcPrompt, (aiResponse) =>
        {
            aiText.StartTyping(aiResponse);
            isWaitingForInput = true;
        }, currentNPC.npcId);
    }

    // 대화 종료
    public void EndDialogue()
    {
        dialoguePanel.SetActive(false);
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