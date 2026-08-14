using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Scene-local owner for an authored map/location selection screen.</summary>
public sealed class MapSceneController : MonoBehaviour
{
    private static readonly Color BackdropColor = new Color(0.012f, 0.028f, 0.055f, 0.96f);
    private static readonly Color AvailableColor = new Color(0.16f, 0.44f, 0.62f, 0.54f);
    private static readonly Color LockedColor = new Color(0.09f, 0.13f, 0.18f, 0.55f);
    private static Sprite runtimeBackgroundSprite;
    private VNDialogueController dialogueController;
    private GameObject root;
    private RectTransform displayedImageRect;
    private Image mapBackgroundImage;
    private readonly Dictionary<string, Button> locationButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
    private MapSceneData activeMap;
    private SpecialModeLease activeLease;
    private bool dialogueShellSuppressed;

    public bool IsRunning => activeMap != null && activeLease != null;
    public bool IsRuntimeUiActive => root != null && root.activeInHierarchy;
    public RectTransform DisplayedImageRect => displayedImageRect;
    public MapSceneData ActiveMap => activeMap;

    public static bool TryCreateRuntime(VNDialogueController controller, out MapSceneController result, out string failureReason)
    {
        result = null; failureReason = string.Empty;
        if (controller == null) { failureReason = "controller not ready"; return false; }
        MapSceneController existing = controller.GetComponent<MapSceneController>();
        if (existing != null) { result = existing; return true; }
        Canvas canvas = controller.GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        if (canvas == null || !canvas.gameObject.activeInHierarchy) { failureReason = "Canvas/UI unavailable"; return false; }
        MapSceneController created = controller.gameObject.AddComponent<MapSceneController>();
        created.dialogueController = controller; created.BuildRuntimeUi(canvas); created.root.SetActive(false);
        result = created; return true;
    }

    public bool TryStart(MapSceneData map, out string failureReason)
    {
        failureReason = string.Empty;
        if (dialogueController == null || map == null || IsRunning) { failureReason = "map controller is unavailable or already active"; return false; }
        if (SceneFlowManager.IsReplayModeActive) { failureReason = "Replay active"; return false; }
        if (!map.TryValidate(dialogueController, out string diagnostic)) { failureReason = "map data invalid: " + diagnostic; return false; }
        if (!dialogueController.TryEnterSpecialMode(this, SpecialModePolicy.InteractiveScene, out SpecialModeLease lease)) { failureReason = "another special mode active"; return false; }
        if (!dialogueController.TrySuppressDialogueShell(this)) { dialogueController.ExitSpecialMode(lease); failureReason = "dialogue shell unavailable"; return false; }
        activeMap = map; activeLease = lease; dialogueShellSuppressed = true; mapBackgroundImage.sprite = map.background != null ? map.background : runtimeBackgroundSprite; BuildLocations(); root.SetActive(true); root.transform.SetAsLastSibling(); Refresh(); return true;
    }

    public bool TryActivateLocation(string locationId)
    {
        if (!TryGetLocation(locationId, out MapLocationData location) || GameState.Instance == null || !location.IsAvailable(GameState.Instance)) return false;
        SpecialModeLease lease = activeLease; activeLease = null; activeMap = null; root.SetActive(false);
        if (dialogueShellSuppressed) { dialogueController.ReleaseDialogueShellSuppression(this); dialogueShellSuppressed = false; }
        dialogueController.ExitSpecialMode(lease);
        return dialogueController.TryRouteToScene(location.destinationScene);
    }

    public bool IsLocationAvailable(string locationId) => TryGetLocation(locationId, out MapLocationData location) && GameState.Instance != null && location.IsAvailable(GameState.Instance);
    public Button GetLocationButton(string locationId) => locationButtons.TryGetValue(locationId, out Button button) ? button : null;
    public void Refresh()
    {
        if (!IsRunning) return;
        foreach (MapLocationData location in activeMap.locations)
        {
            if (location == null || !locationButtons.TryGetValue(location.locationId, out Button button)) continue;
            bool available = GameState.Instance != null && location.IsAvailable(GameState.Instance);
            button.interactable = available;
            button.GetComponent<Image>().color = available ? AvailableColor : LockedColor;
            button.GetComponentInChildren<TextMeshProUGUI>(true).text = available ? location.displayName : location.displayName + "\nLOCKED";
        }
    }

    private bool TryGetLocation(string id, out MapLocationData location) { location = activeMap != null ? activeMap.locations.Find(x => x != null && x.locationId == id) : null; return location != null; }
    private void BuildRuntimeUi(Canvas canvas)
    {
        root = CreateUiObject(canvas.transform, "Map Locations Runtime View"); Stretch(root.GetComponent<RectTransform>());
        root.AddComponent<Image>().color = BackdropColor;
        TextMeshProUGUI title = CreateText(root.transform, "Technical Label", "TECH DEMO ONLY / NOT CANON", 22f); SetAnchors(title.rectTransform, new Vector2(.1f,.93f), new Vector2(.9f,.985f));
        GameObject container = CreateUiObject(root.transform, "Aspect Fit Map Container"); SetAnchors(container.GetComponent<RectTransform>(), new Vector2(.055f,.095f), new Vector2(.945f,.90f));
        GameObject imageObject = CreateUiObject(container.transform, "Displayed Map Image"); displayedImageRect = imageObject.GetComponent<RectTransform>(); Stretch(displayedImageRect);
        mapBackgroundImage = imageObject.AddComponent<Image>(); mapBackgroundImage.sprite = runtimeBackgroundSprite ??= CreateBackground(); mapBackgroundImage.preserveAspect = true;
        AspectRatioFitter fitter = imageObject.AddComponent<AspectRatioFitter>(); fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fitter.aspectRatio = 16f / 9f;
    }
    private void BuildLocations()
    {
        foreach (Button button in locationButtons.Values) if (button != null) Destroy(button.gameObject); locationButtons.Clear();
        foreach (MapLocationData location in activeMap.locations)
        {
            GameObject item = CreateUiObject(displayedImageRect, "Location " + location.locationId); RectTransform rect = item.GetComponent<RectTransform>(); rect.anchorMin = location.normalizedRect.min; rect.anchorMax = location.normalizedRect.max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            item.AddComponent<Image>().color = AvailableColor; Button button = item.AddComponent<Button>();
            TextMeshProUGUI label = CreateText(item.transform, "Label", location.displayName, 18f); Stretch(label.rectTransform, 8, 8, 8, 8);
            string id = location.locationId; button.onClick.AddListener(() => TryActivateLocation(id)); locationButtons.Add(id, button);
        }
    }
    private void OnDisable() { Cleanup(); }
    private void OnDestroy() { Cleanup(); }
    private void Cleanup()
    {
        if (activeLease == null) return; SpecialModeLease lease = activeLease; activeLease = null; activeMap = null;
        if (dialogueShellSuppressed && dialogueController != null) { dialogueController.ReleaseDialogueShellSuppression(this); dialogueShellSuppressed = false; }
        dialogueController?.ExitSpecialMode(lease); if (root != null) root.SetActive(false);
    }
    private static GameObject CreateUiObject(Transform parent, string name) { var result = new GameObject(name, typeof(RectTransform)); result.transform.SetParent(parent, false); return result; }
    private static TextMeshProUGUI CreateText(Transform parent, string name, string value, float size) { var result=CreateUiObject(parent,name).AddComponent<TextMeshProUGUI>(); result.font=TMP_Settings.defaultFontAsset; result.text=value; result.fontSize=size; result.fontStyle=FontStyles.Bold; result.alignment=TextAlignmentOptions.Center; result.color=new Color(.89f,.96f,1f,1f); return result; }
    private static void Stretch(RectTransform rect, float left=0,float top=0,float right=0,float bottom=0) { rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(left,bottom);rect.offsetMax=new Vector2(-right,-top); }
    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max) { rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero; }
    private static Sprite CreateBackground() { var texture=new Texture2D(320,180,TextureFormat.RGBA32,false); var pixels=new Color[320*180]; for(int y=0;y<180;y++)for(int x=0;x<320;x++){float shade=.05f+(y/180f)*.08f; pixels[y*320+x]=new Color(shade,shade+.035f,shade+.09f,1f);} texture.SetPixels(pixels);texture.Apply(false,true);var sprite=Sprite.Create(texture,new Rect(0,0,320,180),new Vector2(.5f,.5f),180);sprite.hideFlags=HideFlags.HideAndDontSave;return sprite; }
}
