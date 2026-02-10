using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;

[System.Serializable]
public class GeminiRequest
{
    public Content[] contents;
}

[System.Serializable]
public class Content
{
    public Part[] parts;
    public string role;
}

[System.Serializable]
public class Part
{
    public string text;
}

[System.Serializable]
public class GeminiResponse
{
    public Candidate[] candidates;
}

[System.Serializable]
public class Candidate
{
    public Content content;
}

public class GeminiManager : MonoBehaviour
{
    public static GeminiManager Instance { get; private set; }

    [Header("Gemini API Settings")]
    [SerializeField] private string apiKey = "MY_API_KEY_HERE";

    private const string API_BASE_URL = "https://generativelanguage.googleapis.com/v1beta/";
    private const string MODEL_NAME = "models/gemini-2.5-flash";

    private Dictionary<string, List<Content>> conversationHistory = new Dictionary<string, List<Content>>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AskGemini(string userMessage, string npcPrompt, System.Action<string> onComplete, string npcId = "default")
    {
        StartCoroutine(SendRequestCoroutine(userMessage, npcPrompt, onComplete, npcId));
    }

    private IEnumerator SendRequestCoroutine(string userMessage, string npcPrompt, System.Action<string> onComplete, string npcId)
    {
        string url = $"{API_BASE_URL}{MODEL_NAME}:generateContent?key={apiKey}";

        if (!conversationHistory.ContainsKey(npcId))
        {
            conversationHistory[npcId] = new List<Content>();

            conversationHistory[npcId].Add(new Content
            {
                role = "user",
                parts = new Part[] { new Part { text = npcPrompt } }
            });

            conversationHistory[npcId].Add(new Content
            {
                role = "model",
                parts = new Part[] { new Part { text = "이해했습니다." } }
            });
        }

        conversationHistory[npcId].Add(new Content
        {
            role = "user",
            parts = new Part[] { new Part { text = userMessage } }
        });

        GeminiRequest requestData = new GeminiRequest
        {
            contents = conversationHistory[npcId].ToArray()
        };

        string jsonBody = JsonUtility.ToJson(requestData);
        Debug.Log($"[Gemini] 요청 전송 중입니다. (NPC: {npcId}, 히스토리 {conversationHistory[npcId].Count}개)");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Gemini] 응답 성공!");

                try
                {
                    GeminiResponse response = JsonUtility.FromJson<GeminiResponse>(request.downloadHandler.text);
                    string aiResult = response.candidates[0].content.parts[0].text;

                    conversationHistory[npcId].Add(new Content
                    {
                        role = "model",
                        parts = new Part[] { new Part { text = aiResult } }
                    });

                    onComplete?.Invoke(aiResult);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Gemini] 파싱 오류: {e.Message}");
                    onComplete?.Invoke("응답을 이해할 수 없습니다.");
                }
            }
            else
            {
                Debug.LogError($"[Gemini] 오류 {request.responseCode}: {request.downloadHandler.text}");
                onComplete?.Invoke("지금은 대답할 수 없습니다.");
            }
        }
    }

    public void ResetConversation(string npcId)
    {
        if (conversationHistory.ContainsKey(npcId))
        {
            conversationHistory.Remove(npcId);
            Debug.Log($"[Gemini] {npcId} 대화 히스토리 초기화");
        }
    }

    public void ResetAllConversations()
    {
        conversationHistory.Clear();
        Debug.Log($"[Gemini] 모든 대화 히스토리 초기화");
    }
}