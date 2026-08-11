using UnityEngine;

/// <summary>Static, non-canonical data used by the Character Hub technical demo.</summary>
[CreateAssetMenu(menuName = "How I Fall/Character Profile Definition", fileName = "CharacterProfileDefinition")]
public sealed class CharacterProfileDefinition : ScriptableObject
{
    public string characterId;
    public string displayName;
    public Sprite portrait;
    [TextArea(2, 8)] public string biography;
    public CharacterRelationshipSource relationshipSource;
}
