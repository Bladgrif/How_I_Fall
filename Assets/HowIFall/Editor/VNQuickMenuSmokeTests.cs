using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class VNQuickMenuSmokeTests
{
    private const string VnScenePath = "Assets/HowIFall/Scenes/VNPrototype.unity";

    [MenuItem("How I Fall/Tests/Run Quick Menu Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
        Debug.Log("How I Fall quick menu smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        EditorSceneManager.OpenScene(VnScenePath);
        VNQuickMenu menu = UnityEngine.Object.FindFirstObjectByType<VNQuickMenu>(FindObjectsInactive.Include);
        Require(menu != null, "VNPrototype must contain one VNQuickMenu.");
        Require(UnityEngine.Object.FindObjectsByType<VNQuickMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, "VNPrototype must contain a single Quick Menu.");
        Require(menu.dialogueController != null && menu.root != null, "Quick Menu controller/root references are required.");
        Require(menu.historyButton != null && menu.skipButton != null && menu.autoButton != null, "Quick Menu History, Skip and Auto references are required.");
        Require(menu.saveButton != null && menu.quickSaveButton != null && menu.quickLoadButton != null && menu.loadButton != null, "Quick Menu save references are required.");
        Require(menu.settingsButton != null && menu.mainMenuButton != null, "Quick Menu Settings and Menu references are required.");
        menu.ApplyPlayerFacingPresentation();
        Require(menu.rollbackButton != null && menu.rollbackButton.transform.parent == menu.root.transform,
            "Quick Menu must own the runtime rollback button as a strip member.");
        TextMeshProUGUI rollbackLabel = menu.rollbackButton.GetComponentInChildren<TextMeshProUGUI>(true);
        Require(rollbackLabel != null && rollbackLabel.text == "Назад",
            "Quick Menu rollback action must keep the compact 'Назад' label.");
        Require(menu.rollbackButton.gameObject.activeSelf,
            "Quick Menu rollback action must be visible in ordinary reading.");
        Button[] expectedOrder = { menu.rollbackButton, menu.historyButton, menu.skipButton, menu.autoButton, menu.quickSaveButton };
        Button[] actualOrder = menu.root.GetComponentsInChildren<Button>(true)
            .Where(button => button.transform.parent == menu.root.transform && button.gameObject.activeSelf)
            .OrderBy(button => button.transform.GetSiblingIndex())
            .ToArray();
        Require(actualOrder.SequenceEqual(expectedOrder),
            "Quick Menu normal order must be Rollback / History / Skip / Auto / Quick Save.");
        Require(!menu.saveButton.gameObject.activeSelf && !menu.quickLoadButton.gameObject.activeSelf
            && !menu.loadButton.gameObject.activeSelf && !menu.settingsButton.gameObject.activeSelf
            && !menu.mainMenuButton.gameObject.activeSelf,
            "Ordinary Quick Menu must hide Save, Quick Load, Load, Preferences and Menu navigation actions.");
        Require(menu.charactersButton != null
            && !menu.charactersButton.gameObject.activeSelf
            && !menu.charactersButton.transform.IsChildOf(menu.root.transform),
            "Characters must remain a hidden deferred launcher outside the Quick Menu strip.");
        Require(typeof(VNDialogueController).GetMethod(nameof(VNDialogueController.OpenGameMenu)) != null,
            "Esc must retain the existing Game Menu route.");
        Require(typeof(VNDialogueController).GetMethod(nameof(VNDialogueController.RequestQuickSave)) != null,
            "Quick Save must retain the existing VN controller entry point.");
        Require(typeof(VNDialogueController).GetMethod(nameof(VNDialogueController.RequestQuickLoad)) != null, "Quick Load must use the VN controller entry point.");
        Require(typeof(ManualSaveLoadPanel).GetMethod(nameof(ManualSaveLoadPanel.RequestQuickLoad)) != null, "Quick Load must use the existing ManualSaveLoadPanel pipeline.");
        Require(menu.root.transform as RectTransform != null
            && (menu.root.transform as RectTransform).sizeDelta.x >= 450f,
            "Quick Menu strip must stay a compact single centered row after adding the rollback action.");
        Require(typeof(VNDialogueController).GetMethod(nameof(VNDialogueController.TryRollback), new Type[0]) != null,
            "Quick Menu rollback must route through the existing TryRollback contract shared with the mouse wheel.");
        VerifyBottomCenterStripPresentation(menu);
        VerifyFlatStripLanguage(menu);
        VerifyHoverContrast(menu);
        VerifyCenteredStripDialogueClearance();
        VerifyLogicalCanvasResolutionContract();
        VerifyPreferencesModalVisibilityOwnership();
    }

    private static void VerifyBottomCenterStripPresentation(VNQuickMenu menu)
    {
        RectTransform rootRect = menu.root.transform as RectTransform;
        Require(rootRect != null, "Quick Menu root must be a RectTransform.");
        Require(Mathf.Approximately(rootRect.anchorMin.x, 0.5f) && Mathf.Approximately(rootRect.anchorMax.x, 0.5f)
            && Mathf.Approximately(rootRect.pivot.x, 0.5f) && Mathf.Abs(rootRect.anchoredPosition.x) <= 0.01f,
            "Quick Menu must be anchored to the horizontal bottom-center axis.");
        Require(Mathf.Approximately(rootRect.anchoredPosition.y, 22f),
            "Quick Menu must keep its compact bottom offset below the reading field.");
    }

    private static void VerifyFlatStripLanguage(VNQuickMenu menu)
    {
        // UI Target v1 reading language: flat labels over one shared soft band with
        // thin cyan separators instead of per-item chip plates.
        Button[] stripButtons = { menu.rollbackButton, menu.historyButton, menu.skipButton, menu.autoButton, menu.quickSaveButton };
        foreach (Button button in stripButtons)
        {
            Require(button != null, "Flat strip verification requires every strip action.");
            Image plate = button.GetComponent<Image>();
            Require(plate != null && Mathf.Approximately(plate.color.a, 0f),
                "Strip actions must not render per-item chip plates; the plate stays only as the hit area.");
            Transform underline = button.transform.Find("Underline");
            Require(underline != null, "Every strip action must own its cyan interaction underline.");
            Require(button.targetGraphic == underline.GetComponent<Image>(),
                "The underline must be the button target graphic so the ColorBlock ladder drives hover and focus.");
            Require(button.transform.Find("Active Underline") != null,
                "Auto and Skip need the persistent ACTIVE underline element.");
        }

        Transform band = menu.root.transform.Find("Strip Band");
        Require(band != null, "The strip must keep one shared soft band behind all actions.");
        LayoutElement bandElement = band.GetComponent<LayoutElement>();
        Require(bandElement != null && bandElement.ignoreLayout,
            "The shared band must be excluded from the strip layout flow.");
        Require((menu.root.transform as RectTransform).Find("Strip Separator 0") != null
            && (menu.root.transform as RectTransform).Find("Strip Separator 3") != null,
            "The strip must keep thin cyan separators between adjacent actions.");
        Require(menu.rollbackButton.GetComponentInChildren<TextMeshProUGUI>(true).fontSize >= 16f,
            "Flat strip labels must stay readable without the old chip background.");
    }

    private static void VerifyHoverContrast(VNQuickMenu menu)
    {
        ColorBlock colors = menu.historyButton.colors;
        Require(Mathf.Approximately(colors.normalColor.a, 0f),
            "The resting state must keep the flat underline hidden (no visible chrome at rest).");
        Require(colors.highlightedColor.a >= 1.5f,
            "Quick Menu hover must reveal the resting underline alpha (0.55) clearly while staying below the ACTIVE underline.");
        Require(colors.selectedColor.a >= 1.3f && colors.selectedColor.a < colors.highlightedColor.a,
            "Quick Menu keyboard selection must stay visible but not stronger than hover.");
        Require(colors.pressedColor.a > colors.highlightedColor.a,
            "Quick Menu pressed feedback must stay stronger than hover.");
        Image underline = menu.historyButton.transform.Find("Underline").GetComponent<Image>();
        Require(Mathf.Approximately(underline.color.a, 0.55f) && underline.color.b > 0.9f && underline.color.r < 0.1f,
            "The hover underline must be a cyan accent revealed from a calm resting base.");
        GameObject activeMark = menu.autoButton.transform.Find("Active Underline").gameObject;
        Require(!activeMark.activeSelf,
            "The persistent ACTIVE underline must stay hidden while the mode is off.");
    }

    private static void VerifyCenteredStripDialogueClearance()
    {
        GameObject zoneOwner = new GameObject("QuickMenuCenteringSmokeZone", typeof(RectTransform));
        GameObject dialogueOwner = new GameObject("QuickMenuCenteringSmokeShell", typeof(RectTransform));
        GameObject menuOwner = new GameObject("QuickMenuCenteringSmokeRoot", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        GameObject controllerOwner = new GameObject("QuickMenuCenteringSmokeController");
        try
        {
            RectTransform zone = zoneOwner.GetComponent<RectTransform>();
            zone.anchorMin = Vector2.zero;
            zone.anchorMax = Vector2.zero;
            zone.pivot = Vector2.zero;
            zone.sizeDelta = new Vector2(1920f, 1080f);

            RectTransform dialogueRect = dialogueOwner.GetComponent<RectTransform>();
            dialogueRect.SetParent(zone, false);
            dialogueRect.anchorMin = dialogueRect.anchorMax = new Vector2(0.5f, 0f);
            dialogueRect.pivot = new Vector2(0.5f, 0f);
            dialogueRect.anchoredPosition = new Vector2(0f, 92f);
            dialogueRect.sizeDelta = new Vector2(1320f, 180f);

            VNDialogueController controller = controllerOwner.AddComponent<VNDialogueController>();
            controller.dialogueUiRoot = dialogueOwner;

            VNQuickMenu menu = menuOwner.AddComponent<VNQuickMenu>();
            menu.dialogueController = controller;
            menu.root = menuOwner;
            menuOwner.transform.SetParent(zone, false);

            menu.ApplyPlayerFacingPresentation();
            menu.RefreshEffectiveVisibility();

            RectTransform menuRect = menuOwner.GetComponent<RectTransform>();
            Require(Mathf.Approximately(menuRect.anchorMin.x, 0.5f) && Mathf.Approximately(menuRect.pivot.x, 0.5f)
                && Mathf.Abs(menuRect.anchoredPosition.x) <= 0.01f,
                "Runtime Quick Menu presentation must sit on the horizontal bottom-center axis.");

            menu.RefreshDialogueSafeArea();
            Bounds shellBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(zone, dialogueRect);
            Bounds menuBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(zone, menuRect);
            Require(Mathf.Abs(shellBounds.center.x - menuBounds.center.x) <= 1f,
                "Quick Menu must share the centered reading composition axis.");
            if (menu.IsEffectivelyVisible)
            {
                Require(shellBounds.min.y >= menuBounds.max.y + 12f,
                    "Quick Menu must never collide with the reading text above it.");
                Require(dialogueRect.anchoredPosition.y >= 92f - 0.01f,
                    "Safe-area lift must not pull the centered reading field below its composition offset.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(menuOwner);
            UnityEngine.Object.DestroyImmediate(controllerOwner);
            UnityEngine.Object.DestroyImmediate(dialogueOwner);
            UnityEngine.Object.DestroyImmediate(zoneOwner);
        }
    }

    private static void VerifyLogicalCanvasResolutionContract()
    {
        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        Require(canvas != null, "VNPrototype must contain a Canvas.");
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        Require(scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
            && scaler.referenceResolution == new Vector2(1920f, 1080f),
            "1280x720 containment relies on the shared 1920x1080 logical canvas reference.");
    }

    private static void VerifyPreferencesModalVisibilityOwnership()
    {
        const string quickMenuPreferenceKey = "hif_show_quick_menu";
        bool preferenceKeyExisted = PlayerPrefs.HasKey(quickMenuPreferenceKey);
        int persistedPreferenceBefore = PlayerPrefs.GetInt(quickMenuPreferenceKey, int.MinValue);
        GameSettings runtimeSettings = SettingsManager.Instance != null ? SettingsManager.Instance.CurrentSettings : null;
        System.Reflection.FieldInfo runtimePreferenceField = runtimeSettings?.GetType().GetField("showQuickMenu");
        bool? runtimePreferenceBefore = runtimePreferenceField != null
            ? (bool?)runtimePreferenceField.GetValue(runtimeSettings)
            : null;

        GameObject owner = new GameObject("Quick Menu Preferences Modal Ownership Test");
        GameObject root = new GameObject("Quick Menu Root");
        root.transform.SetParent(owner.transform, false);
        VNQuickMenu menu = owner.AddComponent<VNQuickMenu>();
        menu.root = root;

        try
        {
            root.SetActive(true);
            menu.SetPreferencesModalHidden(true);
            Require(!root.activeSelf, "Gameplay Preferences must temporarily hide the Quick Menu root.");

            menu.SetPlayerInterfaceHidden(true);
            menu.SetPreferencesModalHidden(false);
            Require(!root.activeSelf, "Closing Preferences must not force the Quick Menu visible through the H/clean-view blocker.");

            menu.SetPlayerInterfaceHidden(false);
            bool expectedVisible = !runtimePreferenceBefore.HasValue || runtimePreferenceBefore.Value;
            Require(root.activeSelf == expectedVisible, "Closing Preferences must restore the current effective Quick Menu visibility policy.");

            Require(PlayerPrefs.HasKey(quickMenuPreferenceKey) == preferenceKeyExisted
                && PlayerPrefs.GetInt(quickMenuPreferenceKey, int.MinValue) == persistedPreferenceBefore,
                "Preferences modal ownership must not mutate the persistent Quick Menu preference.");
            Require(!runtimePreferenceBefore.HasValue
                || (bool)runtimePreferenceField.GetValue(runtimeSettings) == runtimePreferenceBefore.Value,
                "Preferences modal ownership must not mutate the runtime Quick Menu preference.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
