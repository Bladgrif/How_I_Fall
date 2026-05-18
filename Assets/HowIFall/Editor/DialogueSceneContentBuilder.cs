using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DialogueSceneContentBuilder
{
    private const string IntroScenePath = "Assets/HowIFall/Data/Dialogues/intro_school_morning.asset";
    private const string ExistingNextScenePath = "Assets/HowIFall/Data/Dialogues/intro_school_meet.asset";
    private const string RegistryPath = "Assets/HowIFall/Data/Dialogues/DialogueSceneRegistry.asset";
    private const string VNPrototypeScenePath = "Assets/HowIFall/Scenes/VNPrototype.unity";

    [MenuItem("How I Fall/Build Intro School Morning Dialogue")]
    public static void BuildIntroSchoolMorningDialogue()
    {
        DialogueSceneData scene = AssetDatabase.LoadAssetAtPath<DialogueSceneData>(IntroScenePath);

        if (scene == null)
        {
            scene = ScriptableObject.CreateInstance<DialogueSceneData>();
            AssetDatabase.CreateAsset(scene, IntroScenePath);
        }

        scene.sceneId = "intro_school_morning";
        scene.backgroundMusic = null;
        scene.stopMusicOnStart = false;
        scene.lines = CreateIntroLines();
        scene.choices = CreateIntroChoices();
        scene.defaultNextScene = AssetDatabase.LoadAssetAtPath<DialogueSceneData>(ExistingNextScenePath);

        EditorUtility.SetDirty(scene);
        UpdateRegistry(scene);
        AssignToVNPrototype(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Intro school morning dialogue was created and assigned.");
    }

    public static void BuildIntroSchoolMorningDialogueBatch()
    {
        BuildIntroSchoolMorningDialogue();
    }

    private static List<DialogueLine> CreateIntroLines()
    {
        Sprite schoolGate = LoadSprite("Assets/HowIFall/Art/Backgrounds/school_gate_morning.png");
        Sprite schoolEntrance = LoadSprite("Assets/HowIFall/Art/Backgrounds/school_entrance_morning.png");
        Sprite corridor = LoadSprite("Assets/HowIFall/Art/Backgrounds/school_corridor_break.png");
        Sprite masha = LoadSprite("Assets/HowIFall/Art/Characters/masha_neutral.png");
        Sprite artem = LoadSprite("Assets/HowIFall/Art/Characters/artem_smile.png");
        Sprite lera = LoadSprite("Assets/HowIFall/Art/Characters/lera_neutral.png");

        return new List<DialogueLine>
        {
            new DialogueLine
            {
                background = schoolGate,
                hideCharacter = true,
                speaker = string.Empty,
                text = "Утро у школьных ворот было слишком тихим для понедельника. Даже вороны на проводах сидели так, будто ждали звонка вместе со мной."
            },
            new DialogueLine
            {
                background = schoolGate,
                hideCharacter = true,
                speaker = "Я",
                text = "Я пришёл раньше обычного, хотя будильник снова не прозвенел. На экране телефона мигало время, которого там не должно было быть: 07:07."
            },
            new DialogueLine
            {
                background = schoolEntrance,
                characterSprite = masha,
                characterPosition = CharacterPosition.Left,
                speaker = "Маша",
                text = "Ты тоже это заметил? Сегодня все как будто говорят тише."
            },
            new DialogueLine
            {
                background = null,
                characterSprite = masha,
                characterPosition = CharacterPosition.Left,
                speaker = "Я",
                text = "Маша старалась улыбаться, но пальцы крепко сжимали ремешок портфеля. Она всегда так делала, когда боялась показать страх."
            },
            new DialogueLine
            {
                background = corridor,
                hideCharacter = true,
                speaker = string.Empty,
                text = "В коридоре пахло мелом, мокрой формой и чем-то сладким, как духи из чужого воспоминания. Свет над лестницей мигнул один раз."
            },
            new DialogueLine
            {
                background = null,
                characterSprite = artem,
                characterPosition = CharacterPosition.Right,
                speaker = "Артём",
                text = "Если это очередная школьная легенда, то она выбрала очень скучное утро для дебюта."
            },
            new DialogueLine
            {
                background = null,
                characterSprite = artem,
                characterPosition = CharacterPosition.Right,
                speaker = "Я",
                text = "Артём сказал это легко, почти весело. Но взгляд у него был не на нас, а на пустую дверь кабинета музыки."
            },
            new DialogueLine
            {
                background = null,
                characterSprite = lera,
                characterPosition = CharacterPosition.Center,
                speaker = "Лера",
                text = "Не стойте здесь. Учителя сегодня не любят, когда кто-то задерживается в коридоре."
            },
            new DialogueLine
            {
                background = null,
                hideCharacter = true,
                speaker = string.Empty,
                text = "Она прошла мимо, не оглянувшись. На секунду мне показалось, что её отражение в окне задержалось дольше, чем она сама."
            },
            new DialogueLine
            {
                background = null,
                hideCharacter = true,
                speaker = string.Empty,
                text = "До звонка оставалась минута. Маша ждала ответа, Артём делал вид, что ему всё равно, а школа вокруг нас будто прислушивалась."
            }
        };
    }

    private static List<DialogueChoice> CreateIntroChoices()
    {
        return new List<DialogueChoice>
        {
            new DialogueChoice
            {
                text = "Подойти к Маше",
                resultText = "Я сделал шаг к Маше. Она выдохнула так тихо, будто всё утро ждала именно этого.",
                romanceDelta = 1,
                selfControlDelta = 1,
                trustMashaDelta = 1
            },
            new DialogueChoice
            {
                text = "Поговорить с Артёмом",
                resultText = "Я повернулся к Артёму. Его шутливая улыбка дрогнула, и он наконец посмотрел на меня серьёзно.",
                selfControlDelta = 1,
                trustArtemDelta = 1
            },
            new DialogueChoice
            {
                text = "Остаться одному",
                resultText = "Я отошёл к окну и попытался собрать мысли. В отражении за моей спиной на миг появилась ещё одна фигура.",
                suspicionDelta = 1,
                leraInterestDelta = 1
            }
        };
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void UpdateRegistry(DialogueSceneData scene)
    {
        DialogueSceneRegistry registry = AssetDatabase.LoadAssetAtPath<DialogueSceneRegistry>(RegistryPath);

        if (registry == null)
        {
            return;
        }

        if (!registry.scenes.Contains(scene))
        {
            registry.scenes.Insert(0, scene);
            EditorUtility.SetDirty(registry);
        }
    }

    private static void AssignToVNPrototype(DialogueSceneData scene)
    {
        var unityScene = EditorSceneManager.OpenScene(VNPrototypeScenePath);
        VNDialogueController controller = Object.FindAnyObjectByType<VNDialogueController>();

        if (controller == null)
        {
            Debug.LogError("DialogueSceneContentBuilder: VNDialogueController was not found in VNPrototype.");
            return;
        }

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("sceneData").objectReferenceValue = scene;
        serializedController.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(unityScene);
        EditorSceneManager.SaveScene(unityScene);
    }
}
