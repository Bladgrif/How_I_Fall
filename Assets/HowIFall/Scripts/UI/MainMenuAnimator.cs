using UnityEngine;
using UnityEngine.UI;

public class MainMenuAnimator : MonoBehaviour
{
    public CanvasGroup menuCanvasGroup;
    public CanvasGroup titleCanvasGroup;
    public RectTransform backgroundTransform;
    public Graphic backgroundOverlay;

    public float fadeDuration = 1.2f;
    public float backgroundZoomAmount = 0.025f;
    public float backgroundMoveAmount = 10f;
    public float backgroundMotionSpeed = 0.12f;
    public float overlayPulseSpeed = 0.45f;
    public float overlayAlphaBase = 0.25f;
    public float overlayAlphaAmount = 0.04f;

    private float _startTime;
    private Vector3 _startBackgroundScale = Vector3.one;
    private Vector2 _startBackgroundAnchoredPosition = Vector2.zero;
    private Color _overlayColor = Color.black;

    private void Start()
    {
        _startTime = Time.time;

        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = 0f;
        }

        if (titleCanvasGroup != null)
        {
            titleCanvasGroup.alpha = 0f;
        }

        if (backgroundTransform != null)
        {
            _startBackgroundScale = backgroundTransform.localScale;
            _startBackgroundAnchoredPosition = backgroundTransform.anchoredPosition;
        }

        if (backgroundOverlay != null)
        {
            _overlayColor = backgroundOverlay.color;
        }
    }

    private void Update()
    {
        float elapsed = Time.time - _startTime;
        float fadeT = fadeDuration > 0f ? Mathf.Clamp01(elapsed / fadeDuration) : 1f;

        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = fadeT;
        }

        if (titleCanvasGroup != null)
        {
            titleCanvasGroup.alpha = fadeT;
        }

        if (backgroundTransform != null)
        {
            float t = Time.time * backgroundMotionSpeed;
            float zoom = 1f + Mathf.Sin(t) * backgroundZoomAmount;
            backgroundTransform.localScale = _startBackgroundScale * zoom;

            Vector2 anchored = _startBackgroundAnchoredPosition;
            anchored.x += Mathf.Sin(t * 0.7f) * backgroundMoveAmount;
            anchored.y += Mathf.Cos(t * 0.6f) * backgroundMoveAmount * 0.5f;
            backgroundTransform.anchoredPosition = anchored;
        }

        if (backgroundOverlay != null)
        {
            float pulse = Mathf.Sin(Time.time * overlayPulseSpeed);
            float alpha = overlayAlphaBase + pulse * overlayAlphaAmount;
            var color = _overlayColor;
            color.a = Mathf.Clamp01(alpha);
            backgroundOverlay.color = color;
        }
    }
}
