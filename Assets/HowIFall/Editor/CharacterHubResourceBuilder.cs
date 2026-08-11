using UnityEditor;
using UnityEngine;

public static class CharacterHubResourceBuilder
{
    public const string ConfigPath = "Assets/HowIFall/Resources/CharacterHub/TechnicalCharacterHubConfig.asset";

    public static void RunBatchMode()
    {
        if (!AssetDatabase.IsValidFolder("Assets/HowIFall/Resources")) AssetDatabase.CreateFolder("Assets/HowIFall", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/HowIFall/Resources/CharacterHub")) AssetDatabase.CreateFolder("Assets/HowIFall/Resources", "CharacterHub");
        CharacterHubTechnicalConfig config = AssetDatabase.LoadAssetAtPath<CharacterHubTechnicalConfig>(ConfigPath);
        if (config == null) { config = ScriptableObject.CreateInstance<CharacterHubTechnicalConfig>(); AssetDatabase.CreateAsset(config, ConfigPath); }
        config.visibleProfile = AssetDatabase.LoadAssetAtPath<CharacterProfileDefinition>("Assets/HowIFall/Data/Characters/test_character_a.asset");
        config.lockedProfile = AssetDatabase.LoadAssetAtPath<CharacterProfileDefinition>("Assets/HowIFall/Data/Characters/test_character_b.asset");
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
    }
}
