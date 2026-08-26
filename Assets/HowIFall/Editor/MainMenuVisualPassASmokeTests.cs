using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MainMenuVisualPassASmokeTests
{
    private const string MainMenuScenePath = "Assets/HowIFall/Scenes/MainMenu.unity";
    private static readonly Vector2 TargetResolution = new Vector2(1920f, 1080f);

    [MenuItem("How I Fall/Tests/Run Main Menu Visual Pass A Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
        Debug.Log("How I Fall Main Menu Visual Pass A smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        EditorSceneManager.OpenScene(MainMenuScenePath);
        MainMenuController controller = UnityEngine.Object.FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
        Require(controller != null, "MainMenu must contain MainMenuController.");
        Require(controller.ApplyPlayerFacingPresentation(), "Main Menu visual presentation could not be applied.");

        VerifyFinalActionSet(controller);
        VerifyDynamicPrimaryAction(controller);
        VerifyNavigationLayout(controller);
        VerifyNavigationPanel(controller);
        VerifySimpleButtonPresentation(controller);
        VerifyMainMenuLogoIsNotPlayerFacing();
        VerifyModalBoundsAndAboutWrapping(controller);
        VerifyLegacyPromptIsNotPlayerFacing();
        VerifyBackgroundMotionIsDisabled();
    }

    private static void VerifyFinalActionSet(MainMenuController controller)
    {
        string[] labels = controller.PlayerFacingActionButtons
            .Select(GetButtonLabel)
            .ToArray();
        Require(labels.SequenceEqual(new[] { "Продолжить", "Новая игра", "Загрузить", "Настройки", "Выйти" }),
            "Main Menu must expose only Continue / New Game / Load / Preferences / Quit in that order.");
        Require(controller.PlayerFacingActionButtons.Count == 5,
            "Main Menu must expose exactly five player-facing actions.");
    }

    private static void VerifyDynamicPrimaryAction(MainMenuController controller)
    {
        Button continueButton = controller.continueButton;
        Button newGameButton = controller.PlayerFacingActionButtons[1];
        bool originalInteractable = continueButton.interactable;
        try
        {
            continueButton.interactable = true;
            controller.ApplyPlayerFacingPresentation();
            Require(GetHoverEffect(continueButton).Role == MainMenuButtonVisualRole.Primary,
                "Continue must be the primary CTA when a compatible save exists.");
            Require(GetHoverEffect(newGameButton).Role == MainMenuButtonVisualRole.Secondary,
                "New Game must be secondary while Continue is available.");

            continueButton.interactable = false;
            controller.ApplyPlayerFacingPresentation();
            Require(GetHoverEffect(continueButton).Role == MainMenuButtonVisualRole.Secondary,
                "Disabled Continue must not retain primary CTA treatment.");
            Require(GetHoverEffect(newGameButton).Role == MainMenuButtonVisualRole.Primary,
                "New Game must become the primary CTA when Continue is unavailable.");
            Require(GetHoverEffect(continueButton).CurrentLabelColor.a >= 0.75f,
                "Disabled Continue must remain readable.");
        }
        finally
        {
            continueButton.interactable = originalInteractable;
            controller.ApplyPlayerFacingPresentation();
        }
    }

    private static void VerifyNavigationLayout(MainMenuController controller)
    {
        RectTransform[] rows = controller.PlayerFacingActionButtons
            .Select(button => button.transform.parent as RectTransform)
            .ToArray();
        Require(rows.All(row => row != null), "Every Main Menu action must keep a RectTransform row.");

        Vector2 expectedSize = rows[0].sizeDelta;
        float[] gaps = new float[rows.Length - 1];
        for (int index = 0; index < rows.Length; index++)
        {
            RectTransform row = rows[index];
            Require(row.anchorMin == new Vector2(0f, 0.5f) && row.anchorMax == new Vector2(0f, 0.5f),
                "Main Menu navigation must stay anchored to the left safe area.");
            Require(row.sizeDelta == expectedSize,
                "Every Main Menu action must use the same rectangular button geometry.");

            if (index > 0)
            {
                float previousBottom = rows[index - 1].anchoredPosition.y - rows[index - 1].sizeDelta.y * 0.5f;
                float currentTop = row.anchoredPosition.y + row.sizeDelta.y * 0.5f;
                gaps[index - 1] = previousBottom - currentTop;
                Require(gaps[index - 1] >= 0f, "Main Menu action rows must not overlap.");
            }
        }

        Require(gaps.All(gap => gap >= 8f && gap <= 12f) || gaps[3] > gaps[2],
            "Main Menu actions must use compact spacing with Quit visibly separated.");
        Require(gaps.Take(3).All(gap => gap >= 8f && gap <= 12f),
            "The first four Main Menu actions must use consistent compact spacing.");
        Require(gaps[3] > gaps[2], "Quit must be visually separated from the main action group.");

        foreach (RectTransform row in rows)
        {
            float left = row.anchoredPosition.x;
            float right = row.anchoredPosition.x + row.sizeDelta.x;
            Require(left >= 48f && right <= TargetResolution.x - 48f,
                "Main Menu navigation exceeds the 1920x1080 horizontal safe area.");
        }
    }

    private static void VerifySimpleButtonPresentation(MainMenuController controller)
    {
        foreach (Button button in controller.PlayerFacingActionButtons)
        {
            Image image = button.targetGraphic as Image;
            Require(image != null && image.sprite == null && image.type == Image.Type.Simple,
                "Main Menu actions must use plain rectangular UI buttons without decorative sprites.");
        }

        Image[] separators = controller.PlayerFacingActionButtons[0].transform.parent.parent
            .GetComponentsInChildren<Image>(true)
            .Where(image => image.transform.parent == controller.PlayerFacingActionButtons[0].transform.parent.parent
                && image.gameObject.name.Contains("Separator"))
            .ToArray();
        Require(separators.All(separator => !separator.gameObject.activeSelf),
            "Main Menu must not show long decorative separators between action groups.");

        foreach (Button button in controller.PlayerFacingActionButtons)
        {
            Image image = button.targetGraphic as Image;
            Outline outline = button.GetComponent<Outline>();
            Require(image.color.b >= image.color.r && (outline == null || outline.effectColor.b >= outline.effectColor.r),
                "Main Menu button presentation must not use a red accent.");
        }
    }

    private static void VerifyNavigationPanel(MainMenuController controller)
    {
        RectTransform panel = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(rect => rect.gameObject.name == "Main Menu Navigation Panel");
        Require(panel != null,
            "Main Menu layout container is missing.");

        Image panelImage = panel.GetComponent<Image>();
        Require(panelImage != null && panelImage.sprite == null && panelImage.type == Image.Type.Simple,
            "Navigation container must not introduce decorative art.");
        Require(panelImage.color.a <= 0.01f,
            "Navigation container must not have a player-facing rectangular background.");
        Require(!panel.GetComponent<Outline>().enabled && !panel.GetComponent<Shadow>().enabled,
            "Navigation container must not show a player-facing border or shadow.");

        RectTransform[] rows = controller.PlayerFacingActionButtons
            .Select(button => button.transform.parent as RectTransform)
            .ToArray();
        Require(rows.All(row => row.anchoredPosition.x >= 192f && row.anchoredPosition.x <= 240f),
            "Main Menu actions must stay in the left visual column.");
        Require(rows.All(row => row.sizeDelta.x >= 280f && row.sizeDelta.x <= 340f
                && row.sizeDelta.y >= 44f && row.sizeDelta.y <= 52f),
            "Main Menu actions must use compact 1920x1080 button geometry.");

        CanvasScaler scaler = UnityEngine.Object.FindFirstObjectByType<CanvasScaler>(FindObjectsInactive.Include);
        Require(scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize,
            "Main Menu panel must scale with the screen.");
    }

    private static void VerifyMainMenuLogoIsNotPlayerFacing()
    {
        RectTransform logo = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(rect => rect.gameObject.name == "Game Logo");
        Require(logo == null || !logo.gameObject.activeSelf,
            "The Main Menu logo must not be player-facing.");
    }

    private static void VerifyModalBoundsAndAboutWrapping(MainMenuController controller)
    {
        GameObject[] panels = { GetPrivate<GameObject>(controller, "exitConfirmPanel") };

        foreach (GameObject panel in panels)
        {
            Require(panel != null, "Main Menu modal panel reference is missing.");
            RectTransform window = panel.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(rect => rect.transform.parent == panel.transform && rect.gameObject.name.Contains("Window"));
            Require(window != null, "Main Menu modal window is missing.");
            Require(window.sizeDelta.x <= TargetResolution.x - 96f && window.sizeDelta.y <= TargetResolution.y - 72f,
                "Exit confirmation exceeds 1920x1080 bounds.");
        }
    }

    private static void VerifyLegacyPromptIsNotPlayerFacing()
    {
        Transform legacyPrompt = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(transform => transform.name == "Press Any Button");
        Require(legacyPrompt == null || !legacyPrompt.gameObject.activeSelf,
            "Legacy player-facing 'Press any button' prompt must be inactive in Main Menu.");
    }

    private static void VerifyBackgroundMotionIsDisabled()
    {
        MainMenuAnimator animator = UnityEngine.Object.FindFirstObjectByType<MainMenuAnimator>(FindObjectsInactive.Include);
        Require(animator != null, "Main Menu animator is missing.");
        Require(Mathf.Approximately(animator.backgroundZoomAmount, 0f)
                && Mathf.Approximately(animator.backgroundMoveAmount, 0f)
                && Mathf.Approximately(animator.backgroundMotionSpeed, 0f)
                && Mathf.Approximately(animator.overlayPulseSpeed, 0f),
            "Main Menu background motion must be disabled.");
    }

    private static MainMenuButtonHoverEffect GetHoverEffect(Button button)
    {
        MainMenuButtonHoverEffect effect = button.GetComponent<MainMenuButtonHoverEffect>();
        Require(effect != null, "Main Menu action lost its hover and selected-state presentation.");
        return effect;
    }

    private static string GetButtonLabel(Button button)
    {
        TextMeshProUGUI tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            return tmp.text;
        }

        Text legacy = button.GetComponentInChildren<Text>(true);
        return legacy != null ? legacy.text : string.Empty;
    }

    private static T GetPrivate<T>(object owner, string fieldName) where T : class
    {
        return typeof(MainMenuController)
            .GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(owner) as T;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
