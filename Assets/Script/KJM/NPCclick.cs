using UnityEngine;
using UnityEngine.EventSystems;

public class NPCclick : MonoBehaviour, IPointerClickHandler
{
    [Header("NPC 데이터")]
    public NPCData npcData;

    private bool isFirstMeet = true; // 첫 대화 여부 체크

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"[NPCclick] {npcData.npcName} 클릭됨!");

        if (isFirstMeet)
        {
            // 첫 대화: firstLine 출력
            Dialogue.Instance.StartDialogue(npcData, true);
            isFirstMeet = false;
        }
        else
        {
            // 재대화: 바로 입력창
            Dialogue.Instance.StartDialogue(npcData, false);
        }
    }

    // 대화 초기화 (필요할 때 사용)
    public void ResetDialogue()
    {
        isFirstMeet = true;
    }
}