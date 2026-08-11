#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ChatPhoneTechnicalAssets
{
    public const string ChatPath = "Assets/HowIFall/Data/Chats/test_chat_v1.asset";
    public const string ReturnPath = "Assets/HowIFall/Data/Dialogues/chat_demo_return.asset";
    public const string ConfigPath = "Assets/HowIFall/Resources/ChatPhone/TechnicalChatPhoneConfig.asset";
    private const string ImagePath = "Assets/HowIFall/Data/Chats/chat_technical_placeholder.png";

    [MenuItem("How I Fall/Chat Phone/Create or Repair Technical Assets")]
    public static void CreateOrRepair()
    {
        EnsureFolder("Assets/HowIFall/Data/Chats"); EnsureFolder("Assets/HowIFall/Resources/ChatPhone");
        Sprite sprite = EnsurePlaceholder();
        DialogueSceneData returnScene = AssetDatabase.LoadAssetAtPath<DialogueSceneData>(ReturnPath);
        if (returnScene == null) { returnScene = ScriptableObject.CreateInstance<DialogueSceneData>(); AssetDatabase.CreateAsset(returnScene, ReturnPath); }
        returnScene.sceneId = "chat_demo_return"; returnScene.displayName = "TECH DEMO ONLY - NOT CANON";
        returnScene.lines = new System.Collections.Generic.List<DialogueLine> { new DialogueLine { lineId = "chat_demo_return_complete", text = "TEST: chat complete" } };
        returnScene.choices = new System.Collections.Generic.List<DialogueChoice>(); returnScene.defaultNextScene = null;
        DialogueSceneRegistry registry = AssetDatabase.LoadAssetAtPath<DialogueSceneRegistry>("Assets/HowIFall/Data/Dialogues/DialogueSceneRegistry.asset");
        if (registry != null && !registry.scenes.Contains(returnScene)) { registry.scenes.Add(returnScene); EditorUtility.SetDirty(registry); }
        ChatSceneData chat = AssetDatabase.LoadAssetAtPath<ChatSceneData>(ChatPath);
        if (chat == null) { chat = ScriptableObject.CreateInstance<ChatSceneData>(); AssetDatabase.CreateAsset(chat, ChatPath); }
        chat.chatId = "test_chat_v1"; chat.contactDisplayName = "TEST CONTACT"; chat.returnScene = returnScene;
        chat.entries = new System.Collections.Generic.List<ChatEntry>
        {
            new ChatEntry { entryId = "incoming", kind = ChatEntryKind.Text, sender = ChatSenderSide.Incoming, text = "TEST: incoming message" },
            new ChatEntry { entryId = "image", kind = ChatEntryKind.Image, sender = ChatSenderSide.Incoming, image = sprite },
            new ChatEntry { entryId = "choice", kind = ChatEntryKind.Choice, sender = ChatSenderSide.Player, options = new System.Collections.Generic.List<ChatChoiceOption>
                { new ChatChoiceOption { text = "TEST: reply A", nextEntryId = string.Empty }, new ChatChoiceOption { text = "TEST: reply B", nextEntryId = string.Empty } } }
        };
        ChatPhoneTechnicalConfig config = AssetDatabase.LoadAssetAtPath<ChatPhoneTechnicalConfig>(ConfigPath);
        if (config == null) { config = ScriptableObject.CreateInstance<ChatPhoneTechnicalConfig>(); AssetDatabase.CreateAsset(config, ConfigPath); }
        config.technicalDemoChat = chat;
        EditorUtility.SetDirty(returnScene); EditorUtility.SetDirty(chat); EditorUtility.SetDirty(config); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("[CHAT] TECH DEMO ONLY / NOT CANON assets are ready.");
    }

    private static Sprite EnsurePlaceholder()
    {
        if (!File.Exists(ImagePath))
        {
            var texture = new Texture2D(64, 40, TextureFormat.RGBA32, false);
            for (int y=0;y<texture.height;y++) for(int x=0;x<texture.width;x++) texture.SetPixel(x,y, ((x/8+y/8)%2==0) ? new Color(.12f,.42f,.56f,1) : new Color(.06f,.13f,.19f,1));
            texture.Apply(); File.WriteAllBytes(ImagePath, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(ImagePath, ImportAssetOptions.ForceSynchronousImport);
        }
        TextureImporter importer = AssetImporter.GetAtPath(ImagePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64;
            importer.SaveAndReimport();
        }
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ImagePath))
        {
            if (asset is Sprite sprite)
            {
                return sprite;
            }
        }

        Debug.LogError("[CHAT] Technical placeholder could not be loaded as a Sprite.");
        return null;
    }

    private static void EnsureFolder(string path) { if (!AssetDatabase.IsValidFolder(path)) { string parent=Path.GetDirectoryName(path).Replace('\\','/'); string name=Path.GetFileName(path); EnsureFolder(parent); AssetDatabase.CreateFolder(parent,name); } }
}

public enum ChatPhoneTechnicalDemoLaunchAction
{
    OpenInEditModeAndEnterPlay,
    WaitForControllerInCurrentPlayScene,
    LoadRuntimeScene,
    Reject
}

/// <summary>Pure branching contract for the Editor technical-demo launcher.</summary>
public static class ChatPhoneTechnicalDemoLaunchPlan
{
    public static ChatPhoneTechnicalDemoLaunchAction GetAction(bool isPlaying, bool isVnPrototypeScene)
    {
        if (!isPlaying)
        {
            return ChatPhoneTechnicalDemoLaunchAction.OpenInEditModeAndEnterPlay;
        }

        return isVnPrototypeScene
            ? ChatPhoneTechnicalDemoLaunchAction.WaitForControllerInCurrentPlayScene
            : ChatPhoneTechnicalDemoLaunchAction.LoadRuntimeScene;
    }
}

[InitializeOnLoad]
public static class ChatPhoneTechnicalDemoLauncher
{
    private const string ScenePath = "Assets/HowIFall/Scenes/VNPrototype.unity";
    private const string PendingSessionKey = "HowIFall.ChatPhoneTechnicalDemo.Pending";
    private const double ReadyTimeoutSeconds = 8d;
    private static double waitStartedAt;
    private static string lastUnmetCondition = "not started";
    private static DemoStage stage;

    private enum DemoStage
    {
        None,
        WaitingForVn,
        VnReady,
        ConfigLoaded,
        Starting,
        Started,
        Failed
    }

    static ChatPhoneTechnicalDemoLauncher()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    internal static bool HasPendingStartForTests => SessionState.GetBool(PendingSessionKey, false);

    [MenuItem("How I Fall/Chat Phone/Run Technical Demo (Play Mode)")]
    public static void RunTechnicalDemo()
    {
        if (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[CHAT] Stop Play Mode and run launcher again.");
            return;
        }

        bool isPlaying = EditorApplication.isPlaying;
        if (isPlaying && HasPendingStartForTests)
        {
            Debug.Log("[CHAT] Technical demo start is already pending.");
            return;
        }

        bool isVnPrototypeScene = SceneManager.GetActiveScene().path == ScenePath;
        switch (ChatPhoneTechnicalDemoLaunchPlan.GetAction(isPlaying, isVnPrototypeScene))
        {
            case ChatPhoneTechnicalDemoLaunchAction.OpenInEditModeAndEnterPlay:
                ChatPhoneTechnicalAssets.CreateOrRepair();
                ClearPendingStart();
                // This API is deliberately reachable only from the Edit Mode branch.
                EditorSceneManager.OpenScene(ScenePath);
                MarkPendingStart();
                EditorApplication.isPlaying = true;
                break;

            case ChatPhoneTechnicalDemoLaunchAction.WaitForControllerInCurrentPlayScene:
                QueueStartInPlayMode();
                break;

            case ChatPhoneTechnicalDemoLaunchAction.LoadRuntimeScene:
                MarkPendingStart();
                SceneManager.sceneLoaded -= OnRuntimeSceneLoaded;
                SceneManager.sceneLoaded += OnRuntimeSceneLoaded;
                SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
                break;

            default:
                Debug.LogWarning("[CHAT] Stop Play Mode and run launcher again.");
                break;
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && HasPendingStartForTests)
        {
            QueueStartInPlayMode();
        }
        else if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
        {
            ClearPendingStart();
        }
    }

    private static void OnRuntimeSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnRuntimeSceneLoaded;
        if (scene.path != ScenePath)
        {
            Debug.LogWarning("[CHAT] Runtime technical-demo scene load did not reach VNPrototype.");
            ClearPendingStart();
            return;
        }

        QueueStartInPlayMode();
    }

    private static void QueueStartInPlayMode()
    {
        if (!EditorApplication.isPlaying)
        {
            return;
        }

        if (!HasPendingStartForTests && !MarkPendingStart())
        {
            return;
        }

        waitStartedAt = EditorApplication.timeSinceStartup;
        lastUnmetCondition = "VN runtime has not become ready";
        SetStage(DemoStage.WaitingForVn, "waiting for VN...");
        EditorApplication.update -= TryStartPendingDemo;
        EditorApplication.update += TryStartPendingDemo;
    }

    private static void TryStartPendingDemo()
    {
        if (!EditorApplication.isPlaying || !HasPendingStartForTests)
        {
            ClearPendingStart();
            return;
        }

        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            lastUnmetCondition = "VNPrototype is not the active scene";
            CheckReadyTimeout();
            return;
        }

        VNDialogueController controller = Object.FindFirstObjectByType<VNDialogueController>();
        if (controller == null || !controller.isActiveAndEnabled || !controller.IsRuntimeReady)
        {
            lastUnmetCondition = controller == null ? "VNDialogueController was not found" : "VNDialogueController Start initialization is incomplete";
            CheckReadyTimeout();
            return;
        }

        SetStage(DemoStage.VnReady, "VN ready");
        ChatPhoneTechnicalConfig config = Resources.Load<ChatPhoneTechnicalConfig>(ChatPhoneTechnicalConfig.ResourcesPath);
        if (config == null || config.technicalDemoChat == null)
        {
            Fail("config missing");
            return;
        }

        SetStage(DemoStage.ConfigLoaded, "config loaded");
        SetStage(DemoStage.Starting, "starting test_chat_v1");
        if (!controller.TryStartChat(config.technicalDemoChat, out string failureReason))
        {
            Fail(failureReason);
            return;
        }

        ChatController chat = controller.ActiveChatController;
        if (chat == null || !chat.IsRunning || !chat.IsRuntimeUiActive)
        {
            Fail("StartChat returned true but ChatController/UI is not active");
            return;
        }

        SetStage(DemoStage.Started, "STARTED");
        ClearPendingStart();
    }

    internal static bool TryMarkPendingStartForTests()
    {
        return MarkPendingStart();
    }

    private static bool MarkPendingStart()
    {
        if (HasPendingStartForTests)
        {
            return false;
        }

        SessionState.SetBool(PendingSessionKey, true);
        stage = DemoStage.None;
        return true;
    }

    internal static void ClearPendingStartForTests()
    {
        ClearPendingStart();
    }

    private static void ClearPendingStart()
    {
        SessionState.SetBool(PendingSessionKey, false);
        EditorApplication.update -= TryStartPendingDemo;
        SceneManager.sceneLoaded -= OnRuntimeSceneLoaded;
    }

    private static void CheckReadyTimeout()
    {
        if (EditorApplication.timeSinceStartup - waitStartedAt >= ReadyTimeoutSeconds)
        {
            Fail("VN runtime did not become ready: " + lastUnmetCondition);
        }
    }

    private static void SetStage(DemoStage nextStage, string message)
    {
        if (stage == nextStage)
        {
            return;
        }

        stage = nextStage;
        Debug.Log("[ChatPhoneDemo] " + message);
    }

    private static void Fail(string reason)
    {
        SetStage(DemoStage.Failed, "FAILED: " + (string.IsNullOrWhiteSpace(reason) ? "unknown failure" : reason));
        ClearPendingStart();
    }
}
#endif
