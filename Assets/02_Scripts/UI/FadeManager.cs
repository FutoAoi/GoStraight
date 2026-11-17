using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Persistent screen fade. Callbacks run only after the screen is fully covered.</summary>
[DisallowMultipleComponent]
public sealed class FadeManager : MonoBehaviour
{
    private static FadeManager _instance;
    private CanvasGroup _cover;
    public bool IsTransitioning { get; private set; }
    public static bool IsBusy => _instance != null && _instance.IsTransitioning;

    public static FadeManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindAnyObjectByType<FadeManager>();
            if (_instance == null)
                _instance = new GameObject("FadeManager").AddComponent<FadeManager>();
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        CreateCover();
    }

    private void CreateCover()
    {
        var root = new GameObject("Fade Overlay", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasGroup), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        _cover = root.GetComponent<CanvasGroup>();
        _cover.alpha = 0f;
        _cover.blocksRaycasts = false;
        var panel = new GameObject("Black", typeof(RectTransform), typeof(Image));
        var rect = panel.GetComponent<RectTransform>();
        rect.SetParent(root.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = Color.black;
    }

    public bool Transition(Action whenBlack, float fadeOut = 0.6f, float fadeIn = 0.6f,
        float delay = 0f, Action onFinished = null)
    {
        return RunTransition(() => InvokeAction(whenBlack), fadeOut, fadeIn, delay, onFinished);
    }

    /// <summary>The coroutine can perform asynchronous work while the screen stays black.</summary>
    public bool RunTransition(Func<IEnumerator> whileBlack, float fadeOut = 0.6f,
        float fadeIn = 0.6f, float delay = 0f, Action onFinished = null)
    {
        if (IsTransitioning) return false;
        IsTransitioning = true;
        _cover.blocksRaycasts = true;
        StartCoroutine(TransitionRoutine(whileBlack, fadeOut, fadeIn, delay, onFinished));
        return true;
    }

    public bool LoadScene(string sceneName, float fadeOut = 0.6f, float fadeIn = 0.6f)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"Scene '{sceneName}' is not available in the Build Profile.", this);
            return false;
        }
        return RunTransition(() => LoadSceneRoutine(sceneName), fadeOut, fadeIn);
    }

    // FadeOut covers the screen; FadeIn reveals it. Both use unscaled time.
    public bool FadeOut(float seconds = 0.6f) => StartSingleFade(1f, seconds);
    public bool FadeIn(float seconds = 0.6f) => StartSingleFade(0f, seconds);

    private bool StartSingleFade(float alpha, float seconds)
    {
        if (IsTransitioning) return false;
        IsTransitioning = true;
        _cover.blocksRaycasts = true;
        StartCoroutine(SingleFade(alpha, seconds));
        return true;
    }

    private IEnumerator SingleFade(float alpha, float seconds)
    {
        try { yield return AnimateAlpha(alpha, seconds); }
        finally
        {
            IsTransitioning = false;
            if (_cover != null) _cover.blocksRaycasts = _cover.alpha > 0f;
        }
    }

    private IEnumerator TransitionRoutine(Func<IEnumerator> operation, float fadeOut,
        float fadeIn, float delay, Action onFinished)
    {
        try
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            yield return AnimateAlpha(1f, fadeOut);
            // Run the operation safely so a failed callback never leaves a black screen.
            yield return RunSafely(operation);
            yield return null; // Allow newly created scene objects to initialize under black.
            yield return AnimateAlpha(0f, fadeIn);
        }
        finally
        {
            if (_cover != null) { _cover.alpha = 0f; _cover.blocksRaycasts = false; }
            IsTransitioning = false;
            onFinished?.Invoke();
        }
    }

    private IEnumerator AnimateAlpha(float target, float seconds)
    {
        float from = _cover.alpha;
        float duration = Mathf.Max(0f, seconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _cover.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        _cover.alpha = target;
    }

    private IEnumerator RunSafely(Func<IEnumerator> operation)
    {
        var stack = new Stack<IEnumerator>();
        Exception error = null;
        try { if (operation != null) { var routine = operation(); if (routine != null) stack.Push(routine); } }
        catch (Exception ex) { error = ex; }
        while (error == null && stack.Count > 0)
        {
            object current = null;
            bool moved = false;
            try
            {
                moved = stack.Peek().MoveNext();
                if (moved) current = stack.Peek().Current;
                else (stack.Pop() as IDisposable)?.Dispose();
            }
            catch (Exception ex) { error = ex; }
            if (error != null) break;
            if (!moved) continue;
            if (current is IEnumerator nested) stack.Push(nested);
            else yield return current;
        }
        while (stack.Count > 0)
        {
            try { (stack.Pop() as IDisposable)?.Dispose(); }
            catch (Exception ex) { Debug.LogException(ex, this); }
        }
        if (error != null) Debug.LogException(error, this);
    }

    private static IEnumerator InvokeAction(Action action) { action?.Invoke(); yield break; }
    private static IEnumerator LoadSceneRoutine(string sceneName)
    {
        var loading = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (loading != null) yield return loading;
    }

    private void OnDestroy() { if (_instance == this) _instance = null; }
}
