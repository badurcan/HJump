using System.Collections;
using UnityEngine;

// Generic reusable full-screen fade (not stage-specific). Drives a CanvasGroup's alpha on a
// full-screen Image; the Image's own color/opacity is authored once in the Inspector (e.g.
// solid white) and left alone - this component is the single source of truth for visibility.
// Uses unscaled time throughout so a fade plays at a consistent real-world pace even while
// something else (e.g. a slow-motion hit-stop beat) is manipulating Time.timeScale.
public class ScreenFader : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;

    // FadeOut/FadeIn are split out (rather than only exposing the combined FadeOutAndIn) so a
    // caller can do something hidden-from-the-player between them - e.g. StageEndTrigger
    // repositioning the camera while the screen is fully white, before fading back in.
    public IEnumerator FadeOut(float fadeOutTime)
    {
        canvasGroup.blocksRaycasts = true;

        float t = 0f;
        while (t < fadeOutTime)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(t / fadeOutTime);
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    public IEnumerator FadeIn(float fadeInTime)
    {
        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeInTime);
            yield return null;
        }
        canvasGroup.alpha = 0f;

        canvasGroup.blocksRaycasts = false;
    }

    public IEnumerator FadeOutAndIn(float fadeOutTime, float holdTime, float fadeInTime)
    {
        yield return FadeOut(fadeOutTime);
        yield return new WaitForSecondsRealtime(holdTime);
        yield return FadeIn(fadeInTime);
    }
}
