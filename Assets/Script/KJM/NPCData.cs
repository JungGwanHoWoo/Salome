using UnityEngine;

[CreateAssetMenu(fileName = "NPCData", menuName = "Game/NPC Data")]
public class NPCData : ScriptableObject
{
    [Header("NPC 기본 정보")]
    public string npcId = "npc_001"; // 고유 ID
    public string npcName = "소피아"; // 이름

    [Header("대화 설정")]
    [TextArea(3, 10)]
    public string npcPrompt = "너는 친절한 안내원이야.";

    [TextArea(2, 5)]
    public string firstLine = "안녕! 무엇을 도와줄까?";
}