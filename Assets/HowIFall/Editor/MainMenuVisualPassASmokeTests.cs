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
        VerifyTemporaryMenuMusicAssignment();
        VerifyDynamicPrimaryAction(controller);
        VerifyNavigationLayout(controller);
        VerifyNavigationPanel(controller);
        VerifySimpleButtonPresentation(controller);
        VerifyTargetV2Presentation(controller);
        VerifyModalBoundsAndAboutWrapping(controller);
        VerifyLegacyPromptIsNotPlayerFacing();
        VerifyBackgroundMotionIsDisabled();
        VerifyAuthoredBackgroundAndLogo(controller);
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

    private static void VerifyTemporaryMenuMusicAssignment()
    {
        MainMenuMusicPlayer musicPlayer = UnityEngine.Object.FindFirstObjectByType<MainMenuMusicPlayer>(FindObjectsInactive.Include);
        Require(musicPlayer != null, "MainMenu must contain MainMenuMusicPlayer.");
        Require(musicPlayer.musicClip != null, "MainMenuMusicPlayer must have a temporary menu music clip assigned.");

        const string ExpectedMusicPath = "Assets/HowIFall/Audio/Music/HIF_TEMP_menu_ambient.wav";
        Require(AssetDatabase.GetAssetPath(musicPlayer.musicClip) == ExpectedMusicPath,
            "MainMenuMusicPlayer must reference the approved temporary menu AudioClip.");
        Require(AssetDatabase.LoadAssetAtPath<AudioClip>(ExpectedMusicPath) == musicPlayer.musicClip,
            "The approved temporary menu AudioClip must resolve to the serialized MainMenu reference.");
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
            GetHoverEffect(continueButton).OnDeselect(null);
            GetHoverEffect(newGameButton).OnDeselect(null);
            GetHoverEffect(continueButton).AdvanceInteractionFade(MainMenuButtonHoverEffect.InteractionFadeDuration);
            GetHoverEffect(newGameButton).AdvanceInteractionFade(MainMenuButtonHoverEffect.InteractionFadeDuration);
            Require(GetHoverEffect(continueButton).CurrentLabelColor == GetHoverEffect(newGameButton).CurrentLabelColor,
                "Primary Continue must not be permanently brighter than other enabled actions.");

            continueButton.interactable = false;
            controller.ApplyPlayerFacingPresentation();
            Require(GetHoverEffect(continueButton).Role == MainMenuButtonVisualRole.Secondary,
                "Disabled Continue must not retain primary CTA treatment.");
            Require(GetHoverEffect(newGameButton).Role == MainMenuButtonVisualRole.Primary,
                "New Game must become the primary CTA when Continue is unavailable.");
            MainMenuButtonHoverEffect disabledContinue = GetHoverEffect(continueButton);
            disabledContinue.OnSelect(null);
            Require(disabledContinue.CurrentLabelColor.a >= 0.75f && !disabledContinue.IsFocusAccentVisible
                    && !disabledContinue.IsSelectionGlowVisible,
                "Disabled Continue must remain readable and must not present a misleading focus marker or glow.");
        }
        finally
        {
            GetHoverEffect(continueButton).OnDeselect(null);
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

        Require(gaps.Take(3).All(gap => Mathf.Abs(gap - 16f) <= 0.5f),
            "Main Menu actions must keep the approved target v2 tight typography rhythm without overlap.");
        Require(Mathf.Abs(gaps[3] - 44f) <= 0.5f,
            "Target v2 adds deliberate extra separation before Quit.");

        foreach (RectTransform row in rows)
        {
            // anchoredPosition lives in Menu Content space; the authored
            // MainMenuRoot contributes a +140 canvas X offset at 1920x1080.
            float left = row.anchoredPosition.x + 140f;
            float right = left + row.sizeDelta.x;
            Require(left >= 48f && right <= TargetResolution.x - 48f,
                "Main Menu navigation exceeds the 1920x1080 horizontal safe area.");
        }
    }

    private static void VerifySimpleButtonPresentation(MainMenuController controller)
    {
        foreach (Button button in controller.PlayerFacingActionButtons)
        {
            Image image = button.targetGraphic as Image;
            Require(image != null && image.type == Image.Type.Simple,
                "Main Menu actions must not replace the authored menu art with decorative button sprites.");
            Require(image.sprite == null || image.sprite.name == "HIF Nav Selection Ramp Runtime",
                "Main Menu actions may only carry the target v2 left-weighted selection ramp sprite.");
            Require(image.color.a <= 0.01f,
                "Main Menu normal navigation must not use permanent filled button rectangles.");
        }

        Image[] separators = controller.PlayerFacingActionButtons[0].transform.parent.parent
            .GetComponentsInChildren<Image>(true)
            .Where(image => image.transform.parent == controller.PlayerFacingActionButtons[0].transform.parent.parent
                && image.gameObject.name.Contains("Separator"))
            .ToArray();
        Require(separators.All(separator => !separator.gameObject.activeSelf),
            "Main Menu must not show long decorative separators between action groups.");

        Color? normalEnabledColor = null;
        foreach (Button button in controller.PlayerFacingActionButtons)
        {
            Outline outline = button.GetComponent<Outline>();
            Require(outline == null || !outline.enabled, "Main Menu must not use text outlines.");
            TextMeshProUGUI tmpLabel = button.GetComponentInChildren<TextMeshProUGUI>(true);
            Text legacyLabel = button.GetComponentInChildren<Text>(true);
            Require(tmpLabel != null
                ? tmpLabel.fontSize == 36f && tmpLabel.fontStyle == FontStyles.Normal
                    && tmpLabel.characterSpacing == 0f && !tmpLabel.enableAutoSizing
                : legacyLabel != null && legacyLabel.fontSize == 36 && legacyLabel.fontStyle == FontStyle.Normal,
                "Target v2 root labels must keep calm, fixed 36px typography.");
            Graphic label = tmpLabel != null ? (Graphic)tmpLabel : legacyLabel;
            Require(label.GetComponents<Shadow>().All(shadow => !shadow.enabled),
                "Root labels must not carry heavy shadow or outline meshes.");
            Require(label.rectTransform.offsetMin.x == 38f,
                "Root labels must share the target v2 optical left axis.");
            MainMenuButtonHoverEffect effect = GetHoverEffect(button);
            effect.OnPointerExit(null);
            effect.OnDeselect(null);
            effect.AdvanceInteractionFade(MainMenuButtonHoverEffect.InteractionFadeDuration);
            Color normal = effect.CurrentLabelColor;
            Require(!effect.IsFocusAccentVisible && !effect.IsInteractionVisible,
                "Normal actions must not retain an interaction marker.");
            if (button.interactable && effect.Role != MainMenuButtonVisualRole.Destructive)
            {
                Require(!normalEnabledColor.HasValue || normal == normalEnabledColor.Value,
                    "All enabled normal actions must have equal text treatment, including Primary.");
                normalEnabledColor = normal;
            }
            if (effect.Role == MainMenuButtonVisualRole.Destructive && normalEnabledColor.HasValue)
                Require(normal.r < normalEnabledColor.Value.r && normal.a >= 0.95f,
                    "Quit must be quieter, but readable and actionable in its normal state.");
            effect.OnPointerEnter(null);
            effect.AdvanceInteractionFade(MainMenuButtonHoverEffect.InteractionFadeDuration);
            Require(button.interactable ? effect.IsFocusAccentVisible : !effect.IsFocusAccentVisible,
                "Enabled hover must expose the restrained focus accent without enabling it for disabled Continue.");
            Require(button.interactable ? effect.CurrentLabelColor != normal : effect.CurrentLabelColor == normal,
                "Only enabled actions may brighten on hover.");
            float hoverAlpha = ((Image)button.targetGraphic).color.a;
            Require(hoverAlpha >= 0.25f && hoverAlpha <= 0.55f,
                "Hover must expose the target v2 translucent glass plate without becoming a heavy panel.");
            effect.OnPointerExit(null);
            effect.AdvanceInteractionFade(MainMenuButtonHoverEffect.InteractionFadeDuration);
            Require(effect.CurrentLabelColor == normal && !effect.IsInteractionVisible,
                "Pointer exit must clear hover even with retained EventSystem selection.");
            effect.OnSelect(null);
            effect.AdvanceInteractionFade(MainMenuButtonHoverEffect.InteractionFadeDuration);
            Require(button.interactable ? effect.IsFocusAccentVisible : !effect.IsFocusAccentVisible,
                "Keyboard focus must expose the restrained accent only for enabled actions.");
            Require(button.interactable ? effect.IsInteractionVisible && effect.CurrentLabelColor != normal
                    : !effect.IsInteractionVisible && effect.CurrentLabelColor == normal,
                "Keyboard focus must be distinguishable only on enabled actions.");
            Require(controller.PlayerFacingActionButtons.Count(action => GetHoverEffect(action).IsInteractionVisible) <= 1,
                "Only one Main Menu action may be visually active.");
            effect.OnDeselect(null);
            effect.AdvanceInteractionFade(MainMenuButtonHoverEffect.InteractionFadeDuration);
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
        Require(rows.All(row => Mathf.Abs(row.anchoredPosition.x + 28f) <= 0.5f),
            "Main Menu actions must stay in the target v2 left visual column.");
        Require(rows.All(row => row.sizeDelta == new Vector2(324f, 64f)),
            "Main Menu actions must use the approved target v2 1920x1080 row geometry.");

        CanvasScaler scaler = UnityEngine.Object.FindFirstObjectByType<CanvasScaler>(FindObjectsInactive.Include);
        Require(scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize,
            "Main Menu panel must scale with the screen.");
    }

    private static void VerifyTargetV2Presentation(MainMenuController controller)
    {
        // Nav-side navy wash (UI Target v2): authored gradient overlay stays the
        // visual source, with a local depth plateau and a soft tail.
        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        Require(canvas != null, "Main Menu Canvas is missing for the target v2 presentation check.");
        Image gradient = canvas.transform.Find("Left Gradient Overlay")?.GetComponent<Image>();
        Require(gradient != null && gradient.gameObject.activeSelf, "Target v2 requires the left navy wash overlay.");
        RectTransform gradientRect = gradient.rectTransform;
        Require(Mathf.Abs(gradientRect.sizeDelta.x - 1000f) <= 0.5f,
            "Target v2 wash must fade out near 52% of the screen width, not the full width.");
        Require(gradient.color.a >= 0.95f, "Target v2 wash must keep its full authored depth.");


        // Tagline closes the left composition under the navigation column.
        Transform firstRow = controller.PlayerFacingActionButtons[0].transform.parent;
        Transform menuContent = firstRow.parent;
        TextMeshProUGUI tagline = menuContent != null && menuContent.Find("Main Menu Tagline") != null
            ? menuContent.Find("Main Menu Tagline").GetComponent<TextMeshProUGUI>()
            : null;
        Require(tagline != null, "Target v2 left composition must include the runtime tagline.");
        Require(tagline.text.Replace("\r\n", "\n") == "SAME HALLS\nDIFFERENT YOU",
            "Tagline copy must match the approved target v2 composition.");
        Require(!tagline.raycastTarget && tagline.fontSize <= 24f,
            "Tagline must be non-interactive supporting typography.");
        Image dash = menuContent.Find("Main Menu Tagline Dash") != null
            ? menuContent.Find("Main Menu Tagline Dash").GetComponent<Image>()
            : null;
        Require(dash != null && !dash.raycastTarget, "Tagline dash accent is missing.");
        Require(dash.rectTransform.sizeDelta == new Vector2(52f, 3f)
            && dash.color.b >= 0.8f && dash.color.g >= 0.7f && dash.color.r <= 0.2f,
            "Tagline dash must be the target v2 cyan accent bar.");
        RectTransform taglineRect = tagline.rectTransform;
        foreach (Button button in controller.PlayerFacingActionButtons)
        {
            Require(!RectsOverlap(taglineRect, button.transform.parent as RectTransform),
                "Tagline must not overlap a navigation row.");
            Require(!RectsOverlap(dash.rectTransform, button.transform.parent as RectTransform),
                "Tagline dash must not overlap a navigation row.");
        }

        // Interaction language: bright cyan full-height accent bar on hover/focus.
        foreach (Button button in controller.PlayerFacingActionButtons)
        {
            MainMenuButtonHoverEffect effect = button.GetComponent<MainMenuButtonHoverEffect>();
            Require(effect != null, "Main Menu action lost its hover effect for the target v2 check.");
            effect.OnPointerEnter(null);
            effect.AdvanceInteractionFade(MainMenuButtonHoverEffect.InteractionFadeDuration);
            if (button.interactable)
            {
                Image plate = button.targetGraphic as Image;
                Require(plate != null && plate.sprite != null && plate.sprite.name == "HIF Nav Selection Ramp Runtime",
                    "Selected plate must use the target v2 left-weighted ramp sprite, not a flat rectangle.");
                Require(effect.FocusAccentSize == new Vector2(3f, 60f),
                    "Focus accent must use the target v2 full-height cyan bar geometry.");
                Require(effect.FocusAccentAnchoredPosition == Vector2.zero,
                    "Focus accent bar must sit flush with the highlighted row's left edge, without a wash gap before it.");
                Require(effect.FocusAccentColor.b >= 0.9f && effect.FocusAccentColor.g >= 0.75f
                    && effect.FocusAccentColor.r <= 0.2f,
                    "Focus accent must be the bright target v2 cyan, never red.");
                Require(!effect.IsSelectionGlowVisible,
                    "Target v2 uses a local navy ramp without a selection halo.");
            }
            effect.OnPointerExit(null);
            effect.OnDeselect(null);
            effect.AdvanceInteractionFade(MainMenuButtonHoverEffect.InteractionFadeDuration);
        }

        // Title block: the authored sprite carries transparent margins, so the
        // runtime rect is proportional to the authored frame; its centre puts
        // the visible strokes in the top-left corner (approximately 140..464 x 126..378).
        RectTransform logo = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(rect => rect.gameObject.name == "Game Logo");
        Require(logo != null, "Game Logo is missing for the target v2 check.");
        Require(Mathf.Abs(logo.anchoredPosition.x + 8f) <= 2f && Mathf.Abs(logo.anchoredPosition.y + 112f) <= 2f,
            "Target v2 places the visible logo strokes in the top-left corner of the composition.");
        Require(Mathf.Abs(logo.sizeDelta.y - 300f) <= 2f,
            "Target v2 logo keeps the approved title-block scale.");
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

    private static void VerifyAuthoredBackgroundAndLogo(MainMenuController controller)
    {
        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        Require(canvas != null, "Main Menu Canvas is missing.");
        RectTransform background = canvas.transform.Find("Background") as RectTransform;
        Image backgroundImage = background != null ? background.GetComponent<Image>() : null;
        Require(background != null && background.gameObject.activeSelf && backgroundImage != null && backgroundImage.sprite != null,
            "Main Menu must use its authored Background sprite as the visible visual source.");
        Require(background.anchorMin == Vector2.zero && background.anchorMax == Vector2.one
                && background.offsetMin == Vector2.zero && background.offsetMax == Vector2.zero,
            "Authored Main Menu background must stretch to the full Canvas.");
        Require(!UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(transform => transform.name == "Temporary Main Menu Background"),
            "Procedural Temporary Main Menu Background must not remain a visual source.");

        Image gradient = canvas.transform.Find("Left Gradient Overlay")?.GetComponent<Image>();
        Require(gradient != null && gradient.gameObject.activeSelf && gradient.sprite != null,
            "Authored left gradient overlay must remain available for menu readability.");

        RectTransform logo = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(rect => rect.gameObject.name == "Game Logo");
        Image logoImage = logo != null ? logo.GetComponent<Image>() : null;
        Require(logo != null && logo.gameObject.activeSelf && logoImage != null && logoImage.sprite != null,
            "Authored Game Logo must be the player-facing title area.");
        Require(!UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(text => text.gameObject.name == "Main Menu Title"),
            "Runtime-generated Main Menu Title must not duplicate the authored logo.");
        Require(!UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(text => text.gameObject.name == "Main Menu Subtitle"),
            "Main Menu must not expose a player-facing TECH DEMO subtitle.");
        foreach (Button button in controller.PlayerFacingActionButtons)
        {
            RectTransform row = button.transform.parent as RectTransform;
            Require(row != null && !RectsOverlap(logo, row),
                "Authored Game Logo must not overlap a navigation item.");
        }
    }

    private static bool RectsOverlap(RectTransform left, RectTransform right)
    {
        Vector3[] leftCorners = new Vector3[4]; Vector3[] rightCorners = new Vector3[4];
        left.GetWorldCorners(leftCorners); right.GetWorldCorners(rightCorners);
        return leftCorners[0].x < rightCorners[2].x && leftCorners[2].x > rightCorners[0].x
            && leftCorners[0].y < rightCorners[2].y && leftCorners[2].y > rightCorners[0].y;
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
