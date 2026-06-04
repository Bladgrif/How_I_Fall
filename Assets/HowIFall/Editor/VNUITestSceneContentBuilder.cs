using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class VNUITestSceneContentBuilder
{
    public const string UITestScenePath = "Assets/HowIFall/Data/Dialogues/ui_test_scene.asset";

    private const string RegistryPath = "Assets/HowIFall/Data/Dialogues/DialogueSceneRegistry.asset";
    private const string BackgroundPath = "Assets/HowIFall/Art/Backgrounds/demo_vn_background.png";
    private const string FallbackBackgroundPath = "Assets/HowIFall/Art/Backgrounds/demo_vn_background.png";
    private const string PlaceholderCharacterPath = "Assets/HowIFall/Art/Characters/Placeholders/placeholder_female_student_default.png";

    [MenuItem("How I Fall/Build VN UI Test Dialogue")]
    public static void BuildUITestSceneAssetMenu()
    {
        BuildUITestSceneAsset();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("VN UI test dialogue scene was created.");
    }

    public static DialogueSceneData BuildUITestSceneAsset()
    {
        ConfigureSpriteImportSettings(BackgroundPath);
        ConfigureSpriteImportSettings(PlaceholderCharacterPath);

        DialogueSceneData scene = AssetDatabase.LoadAssetAtPath<DialogueSceneData>(UITestScenePath);

        if (scene == null)
        {
            scene = ScriptableObject.CreateInstance<DialogueSceneData>();
            AssetDatabase.CreateAsset(scene, UITestScenePath);
        }

        scene.sceneId = "ui_test_scene";
        scene.backgroundMusic = null;
        scene.stopMusicOnStart = false;
        scene.lines = CreateUITestLines();
        scene.choices = CreateUITestChoices();
        scene.defaultNextScene = null;

        EditorUtility.SetDirty(scene);
        UpdateRegistry(scene);
        return scene;
    }

    private static List<DialogueLine> CreateUITestLines()
    {
        Sprite background = LoadSprite(BackgroundPath);
        if (background == null)
        {
            Debug.LogWarning($"VN UI test background was not found at {BackgroundPath}. Trying fallback: {FallbackBackgroundPath}");
            background = LoadSprite(FallbackBackgroundPath);
        }

        if (background == null)
        {
            Debug.LogWarning($"VN UI test fallback background was not found at {FallbackBackgroundPath}. The UI test scene will use an empty background.");
        }

        Sprite placeholder = LoadSprite(PlaceholderCharacterPath);

        return new List<DialogueLine>
        {
            new DialogueLine
            {
                background = background,
                characterSprite = placeholder,
                characterPosition = CharacterPosition.Left,
                hideCharacter = false,
                speaker = "Девушка",
                text = "Это тестовая сцена интерфейса. Здесь проверяем окно диалога, имя персонажа и быстрые кнопки."
            },
            new DialogueLine
            {
                background = null,
                characterSprite = placeholder,
                characterPosition = CharacterPosition.Left,
                hideCharacter = false,
                speaker = "Я",
                text = "Сюжетные реплики и финальные персонажи будут подключены позже."
            },
            new DialogueLine
            {
                background = null,
                characterSprite = placeholder,
                characterPosition = CharacterPosition.Left,
                hideCharacter = false,
                speaker = string.Empty,
                text = "Пока эта сцена нужна только для настройки визуального интерфейса."
            }
        };
    }

    private static List<DialogueChoice> CreateUITestChoices()
    {
        const string resultText = "Выбор сохранён. Это тестовая ветка интерфейса.";

        return new List<DialogueChoice>
        {
            new DialogueChoice
            {
                text = "Ответить спокойно",
                resultText = resultText,
                selfControlDelta = 1
            },
            new DialogueChoice
            {
                text = "Пошутить",
                resultText = resultText,
                romanceDelta = 1
            },
            new DialogueChoice
            {
                text = "Промолчать",
                resultText = resultText,
                suspicionDelta = 1
            }
        };
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void ConfigureSpriteImportSettings(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"Texture importer was not found for {path}.");
            return;
        }

        bool changed = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static void UpdateRegistry(DialogueSceneData scene)
    {
        DialogueSceneRegistry registry = AssetDatabase.LoadAssetAtPath<DialogueSceneRegistry>(RegistryPath);

        if (registry == null || scene == null)
        {
            return;
        }

        if (!registry.scenes.Contains(scene))
        {
            registry.scenes.Insert(0, scene);
            EditorUtility.SetDirty(registry);
        }
    }
}
