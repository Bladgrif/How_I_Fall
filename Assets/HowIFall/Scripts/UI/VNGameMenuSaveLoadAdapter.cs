using UnityEngine;

/// <summary>
/// Temporarily mounts the existing ManualSaveLoadPanel inside the Game Menu content region.
/// It changes presentation only; ManualSaveLoadPanel and SaveManager retain all behavior and data ownership.
/// </summary>
public sealed class VNGameMenuSaveLoadAdapter
{
    private const float HostPadding = 8f;

    private ManualSaveLoadPanel panel;
    private RectTransform panelRect;
    private RectTransform contentHost;
    private RectTransform embeddedRoot;
    private RectTransformSnapshot panelSnapshot;
    private Transform originalParent;
    private int originalSiblingIndex;
    private Vector2 referenceSize;
    private bool closeButtonWasActive;

    public bool IsMounted => panel != null;
    public ManualSaveLoadPanel Panel => panel;
    public RectTransform EmbeddedRoot => embeddedRoot;

    public bool Mount(ManualSaveLoadPanel target, RectTransform contentHost)
    {
        if (target == null || contentHost == null)
        {
            return false;
        }

        if (panel == target)
        {
            return true;
        }

        Unmount();
        panel = target;
        panelRect = target.transform as RectTransform;
        this.contentHost = contentHost;
        RectTransform panelWindow = target.windowRect;
        if (panelRect == null || panelWindow == null)
        {
            panel = null;
            this.contentHost = null;
            return false;
        }

        originalParent = panelRect.parent;
        originalSiblingIndex = panelRect.GetSiblingIndex();
        panelSnapshot = new RectTransformSnapshot(panelRect);
        referenceSize = panelWindow.rect.size;
        if (referenceSize.x <= 0f || referenceSize.y <= 0f)
        {
            referenceSize = panelWindow.sizeDelta;
        }

        if (referenceSize.x <= 0f || referenceSize.y <= 0f)
        {
            Unmount();
            return false;
        }

        GameObject embeddedObject = new GameObject("Embedded Manual Save Load Root", typeof(RectTransform));
        embeddedRoot = embeddedObject.GetComponent<RectTransform>();
        embeddedRoot.SetParent(contentHost, false);
        embeddedRoot.anchorMin = embeddedRoot.anchorMax = new Vector2(0.5f, 0.5f);
        embeddedRoot.pivot = new Vector2(0.5f, 0.5f);
        embeddedRoot.anchoredPosition = Vector2.zero;
        embeddedRoot.sizeDelta = referenceSize;

        panelRect.SetParent(embeddedRoot, false);
        Stretch(panelRect);
        if (target.closeButton != null)
        {
            closeButtonWasActive = target.closeButton.gameObject.activeSelf;
            target.closeButton.gameObject.SetActive(false);
        }

        RefreshLayout();
        return true;
    }

    public void RefreshLayout()
    {
        if (embeddedRoot == null || contentHost == null || referenceSize.x <= 0f || referenceSize.y <= 0f)
        {
            return;
        }

        Rect hostRect = contentHost.rect;
        float availableWidth = Mathf.Max(1f, hostRect.width - HostPadding * 2f);
        float availableHeight = Mathf.Max(1f, hostRect.height - HostPadding * 2f);
        float scale = Mathf.Min(availableWidth / referenceSize.x, availableHeight / referenceSize.y);
        scale = Mathf.Max(0.01f, scale);
        embeddedRoot.anchoredPosition = Vector2.zero;
        embeddedRoot.sizeDelta = referenceSize;
        embeddedRoot.localScale = new Vector3(scale, scale, 1f);
    }

    public void Unmount()
    {
        if (panel != null && panel.closeButton != null)
        {
            panel.closeButton.gameObject.SetActive(closeButtonWasActive);
        }

        if (panelRect != null)
        {
            if (originalParent != null)
            {
                panelRect.SetParent(originalParent, false);
                panelSnapshot.Apply(panelRect);
                panelRect.SetSiblingIndex(Mathf.Clamp(originalSiblingIndex, 0, originalParent.childCount - 1));
            }
        }

        if (embeddedRoot != null)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(embeddedRoot.gameObject);
            }
            else
            {
                Object.DestroyImmediate(embeddedRoot.gameObject);
            }
        }

        panel = null;
        panelRect = null;
        contentHost = null;
        embeddedRoot = null;
        originalParent = null;
        originalSiblingIndex = 0;
        referenceSize = Vector2.zero;
        closeButtonWasActive = false;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
    }

    private readonly struct RectTransformSnapshot
    {
        private readonly bool valid;
        private readonly Vector2 anchorMin;
        private readonly Vector2 anchorMax;
        private readonly Vector2 anchoredPosition;
        private readonly Vector2 sizeDelta;
        private readonly Vector2 pivot;
        private readonly Vector3 localScale;
        private readonly Quaternion localRotation;

        public RectTransformSnapshot(RectTransform source)
        {
            valid = source != null;
            anchorMin = source != null ? source.anchorMin : Vector2.zero;
            anchorMax = source != null ? source.anchorMax : Vector2.zero;
            anchoredPosition = source != null ? source.anchoredPosition : Vector2.zero;
            sizeDelta = source != null ? source.sizeDelta : Vector2.zero;
            pivot = source != null ? source.pivot : Vector2.zero;
            localScale = source != null ? source.localScale : Vector3.one;
            localRotation = source != null ? source.localRotation : Quaternion.identity;
        }

        public void Apply(RectTransform target)
        {
            if (!valid || target == null)
            {
                return;
            }

            target.anchorMin = anchorMin;
            target.anchorMax = anchorMax;
            target.anchoredPosition = anchoredPosition;
            target.sizeDelta = sizeDelta;
            target.pivot = pivot;
            target.localScale = localScale;
            target.localRotation = localRotation;
        }
    }
}
