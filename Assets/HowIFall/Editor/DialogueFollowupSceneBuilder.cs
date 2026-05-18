using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DialogueFollowupSceneBuilder
{
    private const string IntroScenePath = "Assets/HowIFall/Data/Dialogues/intro_school_morning.asset";
    private const string MashaFollowupPath = "Assets/HowIFall/Data/Dialogues/morning_masha_followup.asset";
    private const string ArtemFollowupPath = "Assets/HowIFall/Data/Dialogues/morning_artem_followup.asset";
    private const string AloneFollowupPath = "Assets/HowIFall/Data/Dialogues/morning_alone_followup.asset";
    private const string CommonNextScenePath = "Assets/HowIFall/Data/Dialogues/intro_school_meet.asset";
    private const string RegistryPath = "Assets/HowIFall/Data/Dialogues/DialogueSceneRegistry.asset";

    [MenuItem("How I Fall/Build Morning Choice Followups")]
    public static void BuildMorningChoiceFollowups()
    {
        DialogueSceneData commonNextScene = AssetDatabase.LoadAssetAtPath<DialogueSceneData>(CommonNextScenePath);

        DialogueSceneData mashaFollowup = CreateOrUpdateScene(
            MashaFollowupPath,
            "morning_masha_followup",
            CreateMashaFollowupLines(),
            commonNextScene);

        DialogueSceneData artemFollowup = CreateOrUpdateScene(
            ArtemFollowupPath,
            "morning_artem_followup",
            CreateArtemFollowupLines(),
            commonNextScene);

        DialogueSceneData aloneFollowup = CreateOrUpdateScene(
            AloneFollowupPath,
            "morning_alone_followup",
            CreateAloneFollowupLines(),
            commonNextScene);

        UpdateIntroChoices(mashaFollowup, artemFollowup, aloneFollowup);
        UpdateRegistry(mashaFollowup, artemFollowup, aloneFollowup);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Morning choice follow-up scenes were created and linked.");
    }

    public static void BuildMorningChoiceFollowupsBatch()
    {
        BuildMorningChoiceFollowups();
    }

    private static DialogueSceneData CreateOrUpdateScene(
        string path,
        string sceneId,
        List<DialogueLine> lines,
        DialogueSceneData defaultNextScene)
    {
        DialogueSceneData scene = AssetDatabase.LoadAssetAtPath<DialogueSceneData>(path);

        if (scene == null)
        {
            scene = ScriptableObject.CreateInstance<DialogueSceneData>();
            AssetDatabase.CreateAsset(scene, path);
        }

        scene.sceneId = sceneId;
        scene.backgroundMusic = null;
        scene.stopMusicOnStart = false;
        scene.lines = lines;
        scene.choices = new List<DialogueChoice>();
        scene.defaultNextScene = defaultNextScene;
        EditorUtility.SetDirty(scene);

        return scene;
    }

    private static List<DialogueLine> CreateMashaFollowupLines()
    {
        Sprite corridor = LoadSprite("Assets/HowIFall/Art/Backgrounds/school_corridor_break.png");
        Sprite masha = LoadSprite("Assets/HowIFall/Art/Characters/masha_neutral.png");

        return new List<DialogueLine>
        {
            new DialogueLine
            {
                background = corridor,
                characterSprite = masha,
                characterPosition = CharacterPosition.Left,
                speaker = "Маша",
                text = "Спасибо, что не отвернулся. Я знаю, это звучит странно, но сегодня мне страшно оставаться одной."
            },
            new DialogueLine
            {
                background = null,
                characterSprite = masha,
                characterPosition = CharacterPosition.Left,
                speaker = "Я",
                text = "Рядом с ней коридор казался теплее, будто шум школы отступил на несколько шагов. Но тишина всё равно давила на окна."
            },
            new DialogueLine
            {
                background = null,
                characterSprite = masha,
                characterPosition = CharacterPosition.Left,
                speaker = "Маша",
                text = "Вчера после уроков я слышала, как кто-то звал меня из пустого класса. Голос был похож на мой."
            },
            new DialogueLine
            {
                background = null,
                characterSprite = masha,
                characterPosition = CharacterPosition.Left,
                speaker = "Я",
                text = "Она попыталась улыбнуться, но глаза оставались серьёзными. Я понял, что её благодарность была просьбой не уходить."
            },
            new DialogueLine
            {
                background = null,
                hideCharacter = true,
                speaker = string.Empty,
                text = "Первый звонок прошёл по коридору мягко и глухо, словно его накрыли ладонью."
            }
        };
    }

    private static List<DialogueLine> CreateArtemFollowupLines()
    {
        Sprite corridor = LoadSprite("Assets/HowIFall/Art/Backgrounds/school_corridor_break.png");
        Sprite artem = LoadSprite("Assets/HowIFall/Art/Characters/artem_smile.png");

        return new List<DialogueLine>
        {
            new DialogueLine
            {
                background = corridor,
                characterSprite = artem,
                characterPosition = CharacterPosition.Right,
                speaker = "Артём",
                text = "О, следователь проснулся. Записывай: подозреваемые — вся школа, мотив — понедельник."
            },
            new DialogueLine
            {
                background = null,
                characterSprite = artem,
                characterPosition = CharacterPosition.Right,
                speaker = "Артём",
                text = "Ладно. Шутки в сторону. У кабинета музыки я видел свет, хотя дверь была заперта снаружи."
            },
            new DialogueLine
            {
                background = null,
                characterSprite = artem,
                characterPosition = CharacterPosition.Right,
                speaker = "Я",
                text = "Он говорил тихо, без своей обычной бравады. Значит, увиденное зацепило его сильнее, чем он хотел признать."
            },
            new DialogueLine
            {
                background = null,
                characterSprite = artem,
                characterPosition = CharacterPosition.Right,
                speaker = "Артём",
                text = "И ещё. На стекле двери кто-то пальцем написал моё имя. Изнутри."
            },
            new DialogueLine
            {
                background = null,
                hideCharacter = true,
                speaker = string.Empty,
                text = "Мы оба посмотрели в сторону лестницы. Оттуда донеслась короткая музыкальная нота, хотя уроки ещё не начались."
            }
        };
    }

    private static List<DialogueLine> CreateAloneFollowupLines()
    {
        Sprite corridor = LoadSprite("Assets/HowIFall/Art/Backgrounds/school_corridor_break.png");
        Sprite lera = LoadSprite("Assets/HowIFall/Art/Characters/lera_neutral.png");

        return new List<DialogueLine>
        {
            new DialogueLine
            {
                background = corridor,
                hideCharacter = true,
                speaker = string.Empty,
                text = "Я отошёл к окну и сделал вид, что мне нужно проверить телефон. Экран был чёрным, хотя батарея ещё утром показывала половину заряда."
            },
            new DialogueLine
            {
                background = null,
                hideCharacter = true,
                speaker = "Я",
                text = "В отражении коридор был длиннее, чем на самом деле. Дверь кабинета музыки там стояла приоткрытой."
            },
            new DialogueLine
            {
                background = null,
                characterSprite = lera,
                characterPosition = CharacterPosition.Right,
                speaker = "Лера",
                text = "Если будешь смотреть слишком долго, оно заметит, что ты смотришь."
            },
            new DialogueLine
            {
                background = null,
                characterSprite = lera,
                characterPosition = CharacterPosition.Right,
                speaker = "Я",
                text = "Я обернулся, но Лера уже шла дальше. Её голос остался рядом со мной, как холод на стекле."
            },
            new DialogueLine
            {
                background = null,
                hideCharacter = true,
                speaker = string.Empty,
                text = "Когда прозвенел звонок, отражение в окне улыбнулось на долю секунды позже меня."
            }
        };
    }

    private static void UpdateIntroChoices(
        DialogueSceneData mashaFollowup,
        DialogueSceneData artemFollowup,
        DialogueSceneData aloneFollowup)
    {
        DialogueSceneData intro = AssetDatabase.LoadAssetAtPath<DialogueSceneData>(IntroScenePath);

        if (intro == null)
        {
            Debug.LogError("DialogueFollowupSceneBuilder: intro_school_morning.asset was not found.");
            return;
        }

        if (intro.choices == null || intro.choices.Count < 3)
        {
            Debug.LogError("DialogueFollowupSceneBuilder: intro_school_morning does not have three choices.");
            return;
        }

        intro.choices[0].nextScene = mashaFollowup;
        intro.choices[1].nextScene = artemFollowup;
        intro.choices[2].nextScene = aloneFollowup;
        EditorUtility.SetDirty(intro);
    }

    private static void UpdateRegistry(params DialogueSceneData[] followups)
    {
        DialogueSceneRegistry registry = AssetDatabase.LoadAssetAtPath<DialogueSceneRegistry>(RegistryPath);

        if (registry == null)
        {
            Debug.LogWarning("DialogueFollowupSceneBuilder: DialogueSceneRegistry.asset was not found.");
            return;
        }

        DialogueSceneData intro = AssetDatabase.LoadAssetAtPath<DialogueSceneData>(IntroScenePath);

        if (intro != null && !registry.scenes.Contains(intro))
        {
            registry.scenes.Insert(0, intro);
        }

        foreach (DialogueSceneData scene in followups)
        {
            if (scene != null && !registry.scenes.Contains(scene))
            {
                registry.scenes.Add(scene);
            }
        }

        EditorUtility.SetDirty(registry);
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
