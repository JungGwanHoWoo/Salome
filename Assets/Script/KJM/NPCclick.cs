using UnityEngine;
using UnityEngine.EventSystems;

public class NPCclick : MonoBehaviour, IPointerClickHandler
{
    [Header("NPC 데이터")]
    public NPCData npcData; // Inspector에서 할당

    private bool hasStarted = false;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (hasStarted) return;

        Debug.Log($"[NPCclick] {npcData.npcName} 클릭됨!");
        Dialogue.Instance.StartDialogue(npcData);
        hasStarted = true;
    }

    // 대화 종료 후 다시 클릭 가능하게
    public void ResetDialogue()
    {
        hasStarted = false;
    }
}