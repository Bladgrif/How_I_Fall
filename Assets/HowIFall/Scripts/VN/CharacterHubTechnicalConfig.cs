using UnityEngine;

/// <summary>TECH DEMO ONLY / NOT CANON. References the two Character Hub test profiles.</summary>
public sealed class CharacterHubTechnicalConfig : ScriptableObject
{
    public const string ResourcesPath = "CharacterHub/TechnicalCharacterHubConfig";
    public CharacterProfileDefinition visibleProfile;
    public CharacterProfileDefinition lockedProfile;
}
