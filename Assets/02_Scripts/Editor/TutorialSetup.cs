using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TutorialSetup
{
    [MenuItem("GameObject/GoStraight/Tutorial Area", false, 10)]
    private static void CreateArea(MenuCommand command)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var context = command.context as GameObject;
        CreateAreaInScene(context);
    }

    [MenuItem("Tools/GoStraight/Create Tutorial Area")]
    public static void CreateAreaFromTools() => CreateAreaInScene(null);

    private static void CreateAreaInScene(GameObject context)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        // Capture the destination before creation changes the editor's selection.
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        var scene = context != null && context.scene.IsValid() ? context.scene
            : stage != null ? stage.scene : SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return;
        var position = context != null ? context.transform.position
            : Selection.activeTransform != null && Selection.activeTransform.gameObject.scene == scene
                ? Selection.activeTransform.position : Vector3.zero;
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Tutorial Area");
        var go = new GameObject("Tutorial Area - Jump");
        SceneManager.MoveGameObjectToScene(go, scene);
        // A prefab stage must own newly created objects beneath its root.
        if (stage != null && scene == stage.scene)
            go.transform.SetParent(stage.prefabContentsRoot.transform, false);
        go.transform.position = position;
        go.AddComponent<BoxCollider>();
        go.AddComponent<TutorialArea>();
        var box = go.GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(4, 3, 4);
        box.center = Vector3.up * 1.5f;
        // Register the complete object, including all components and destination scene.
        Undo.RegisterCreatedObjectUndo(go, "Create Tutorial Area");
        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        SceneVisibilityManager.instance.Show(go, true);
        EditorSceneManager.MarkSceneDirty(scene);
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.Frame(new Bounds(position + Vector3.up * 1.5f, new Vector3(6, 5, 6)), false);
        // UI creation has its own editor update, separate from the hierarchy's creation command.
        if (stage == null) EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && scene.IsValid() && scene.isLoaded && Ensure(scene))
                EditorSceneManager.MarkSceneDirty(scene);
        };
        Debug.Log("Tutorial Area created in " + scene.name + ". If hidden in the Hierarchy, clear its search filter.", go);
    }
    [MenuItem("Tools/GoStraight/Show or Create Tutorial UI")]
    public static void Show()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (Ensure(scene)) EditorSceneManager.MarkSceneDirty(scene);
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponent<TutorialPromptView>() != null) Selection.activeGameObject = root;
    }
    public static bool Ensure(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return false;
        foreach (var root in scene.GetRootGameObjects())
        {
            var existing = root.GetComponentInChildren<TutorialPromptView>(true);
            if (existing != null) return ApplyAppearance(existing.gameObject, false);
        }
        var ui = new GameObject("TutorialUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        SceneManager.MoveGameObjectToScene(ui, scene);
        var canvas = ui.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var scaler = ui.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        var panel = Rect("Prompt Panel", ui.transform, Vector2.zero, new Vector2(480, 90));
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, 0f);
        panel.anchoredPosition = new Vector2(0, 110);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(.035f, .065f, .105f, .94f);
        bg.raycastTarget = false;
        var group = panel.gameObject.AddComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;
        var label = Label("Instruction", panel, new Vector2(-60, 0), new Vector2(300, 70), "ジャンプ");
        var key = Rect("Button Badge", panel, new Vector2(160, 0), new Vector2(120, 56));
        var keyBg = key.gameObject.AddComponent<Image>();
        keyBg.color = new Color(.18f, .4f, .52f, 1f);
        keyBg.raycastTarget = false;
        var button = Label("Button Text", key, Vector2.zero, new Vector2(116, 52), "SPACE");
        var font = UiFontAssets.Latin;
        if (font != null) button.font = font;
        button.fontSize = 23;
        ui.AddComponent<TutorialPromptView>().Configure(group, label, button);
        ApplyAppearance(ui, true);
        return true;
    }

    [MenuItem("Tools/GoStraight/Apply Tutorial UI Theme")]
    public static void ApplyTheme()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        foreach (var view in Object.FindObjectsByType<TutorialPromptView>(FindObjectsInactive.Include))
            if (view.gameObject.scene.IsValid() && ApplyAppearance(view.gameObject, true))
                EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
    }

    private static bool ApplyAppearance(GameObject ui, bool force)
    {
        const string assets = "Assets/00_InportAssets/";
        var japanese = UiFontAssets.Japanese;
        var latin = UiFontAssets.Latin;
        bool changed = false;
        foreach (var text in ui.GetComponentsInChildren<TMP_Text>(true))
        {
            var font = text.name == "Instruction" ? japanese : latin;
            if (font == null || text.font == font) continue;
            if (!force && text.font != null && AssetDatabase.GetAssetPath(text.font).StartsWith("Assets/")) continue;
            Undo.RecordObject(text, "Style Tutorial Font");
            text.font = font;
            text.fontStyle = FontStyles.Normal;
            PrefabUtility.RecordPrefabInstancePropertyModifications(text);
            EditorUtility.SetDirty(text);
            changed = true;
        }
        foreach (var image in ui.GetComponentsInChildren<Image>(true))
        {
            string name = image.name;
            string file = name == "Prompt Panel" ? "panel_glass.png" : name == "Button Badge" ? "button_rectangle.png" : null;
            if (file == null || (!force && image.sprite != null)) continue;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assets + "kenney_ui-pack-space-expansion/PNG/Extra/Double/" + file);
            if (sprite == null) continue;
            Undo.RecordObject(image, "Style Tutorial Panel");
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = name == "Prompt Panel" ? new Color(.06f, .12f, .18f, .96f) : new Color(.22f, .4f, .52f, 1f);
            image.raycastTarget = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            EditorUtility.SetDirty(image);
            changed = true;
        }
        return changed;
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
    private static TMP_Text Label(string name, Transform parent, Vector2 position, Vector2 size, string value)
    {
        var text = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = UiFontAssets.Japanese;
        text.text = value;
        text.fontSize = 30;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(.85f, .96f, 1f);
        text.raycastTarget = false;
        return text;
    }
}
