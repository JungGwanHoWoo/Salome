using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class TypeWriterText : MonoBehaviour
{
    public float typingSpeed = 0.05f;

    private TextMeshProUGUI textMesh;
    private Coroutine typingCoroutine;
    private string fullText;
    private bool isTyping = false;

    public bool IsFinished => !isTyping;

    void Awake()
    {
        textMesh = GetComponent<TextMeshProUGUI>();
        textMesh.text = "";
    }

    void Update()
    {
        if (isTyping && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            SkipTyping();
        }
    }

    public void StartTyping(string newText)
    {
        fullText = newText;

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeText());
    }

    IEnumerator TypeText()
    {
        isTyping = true;
        textMesh.text = "";

        foreach (char c in fullText)
        {
            textMesh.text += c;
            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
    }

    void SkipTyping()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        textMesh.text = fullText;
        isTyping = false;
    }
}
