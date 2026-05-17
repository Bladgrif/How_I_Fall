using UnityEngine;
using UnityEngine.UI;

public class MainMenuAnimator : MonoBehaviour
{
    public CanvasGroup menuCanvasGroup;
    public CanvasGroup titleCanvasGroup;
    public RectTransform titleTransform;
    public Graphic backgroundOverlay;

    public float fadeDuration = 1.2f;
    public float titlePulseSpeed = 1.2f;
    public float titlePulseAmount = 0.035f;
    public float titleFloatAmount = 6f;
    public float overlayPulseSpeed = 0.45f;
    public float overlayAlphaBase = 0.25f;
    public float overlayAlphaAmount = 0.04f;

    private float _startTime;
    private float _startTitleY;
    private Vector3 _startTitleScale = Vector3.one;
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

        if (titleTransform != null)
        {
            _startTitleY = titleTransform.anchoredPosition.y;
            _startTitleScale = titleTransform.localScale;
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

        if (titleTransform != null)
        {
            float pulse = Mathf.Sin(Time.time * titlePulseSpeed);
            float scale = 1f + pulse * titlePulseAmount;
            titleTransform.localScale = _startTitleScale * scale;

            Vector2 anchored = titleTransform.anchoredPosition;
            anchored.y = _startTitleY + pulse * titleFloatAmount;
            titleTransform.anchoredPosition = anchored;
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
