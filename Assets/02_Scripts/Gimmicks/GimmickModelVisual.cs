using System;
using System.Collections.Generic;
using UnityEngine;

// A visual-only skin. Existing colliders, pivots and gimmick events remain on their original objects.
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class GimmickModelVisual : MonoBehaviour
{
    public enum ModelPart { All, CrateBody, CrateLid, DoorPanel }
    [SerializeField] private GameObject _sourcePrefab;
    [SerializeField] private ModelPart _part;
    [SerializeField] private Vector3 _size = Vector3.one;
    [SerializeField] private Vector3 _center;
    [NonSerialized] private GameObject _instance;
    [NonSerialized] private MeshRenderer _placeholder;
    private readonly List<Material> _convertedMaterials = new();
#if UNITY_EDITOR
    private bool _scheduled;
#endif

    private void OnEnable() => Rebuild();
    private void OnDisable() => Clear();
    private void OnDestroy() => Clear();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_scheduled) return;
        _scheduled = true;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            _scheduled = false;
            if (isActiveAndEnabled) Rebuild();
        };
    }
#endif

    public void Configure(GameObject source, ModelPart part)
    {
        _sourcePrefab = source;
        _part = part;
        Rebuild();
    }

    private void Rebuild()
    {
        Clear();
        _placeholder = GetComponent<MeshRenderer>();
        if (_sourcePrefab == null || !gameObject.scene.IsValid()) return;
        _instance = new GameObject("__SciFiVisual");
        _instance.hideFlags = HideFlags.DontSaveInEditor;
        _instance.transform.SetParent(transform, false);
        _instance.SetActive(false);
        var model = Instantiate(_sourcePrefab, _instance.transform, false);
        model.name = _sourcePrefab.name;
        // Imported visual prefabs must never contribute their own collision or gameplay scripts.
        foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var body in model.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
        foreach (var behaviour in model.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
        foreach (var particles in model.GetComponentsInChildren<ParticleSystem>(true))
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var materialCache = new Dictionary<Material, Material>();
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            bool lid = renderer.name.IndexOf("Crate_Top", StringComparison.OrdinalIgnoreCase) >= 0;
            bool keep = renderer is MeshRenderer && (_part == ModelPart.All ||
                (_part == ModelPart.CrateBody && !lid) || (_part == ModelPart.CrateLid && lid) ||
                (_part == ModelPart.DoorPanel && renderer.name.StartsWith("Door_Vert", StringComparison.Ordinal)));
            renderer.enabled = keep;
            if (!keep) continue;
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (source == null || (source.shader != null && source.shader.name.StartsWith("Universal Render Pipeline/"))) continue;
                if (!materialCache.TryGetValue(source, out var converted))
                {
                    converted = ConvertMaterial(source);
                    materialCache[source] = converted;
                }
                materials[i] = converted;
            }
            renderer.sharedMaterials = materials;
        }
        bool found = false;
        Bounds bounds = default;
        foreach (var mesh in model.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer = mesh.GetComponent<MeshRenderer>();
            if (mesh.sharedMesh == null || renderer == null || !renderer.enabled) continue;
            Bounds local = mesh.sharedMesh.bounds;
            Matrix4x4 matrix = transform.worldToLocalMatrix * mesh.transform.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = local.center + Vector3.Scale(local.extents, new Vector3(
                    (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                point = matrix.MultiplyPoint3x4(point);
                if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                else bounds.Encapsulate(point);
            }
        }
        if (!found) { Clear(); return; }
        Vector3 scale = new(Mathf.Max(0.001f, _size.x) / Mathf.Max(0.001f, bounds.size.x),
            Mathf.Max(0.001f, _size.y) / Mathf.Max(0.001f, bounds.size.y),
            Mathf.Max(0.001f, _size.z) / Mathf.Max(0.001f, bounds.size.z));
        _instance.transform.localScale = scale;
        _instance.transform.localPosition = _center - Vector3.Scale(bounds.center, scale);
        _instance.SetActive(true);
        if (_placeholder != null) _placeholder.enabled = false;
    }

    private Material ConvertMaterial(Material source)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return source;
        var target = new Material(shader) { name = source.name + " (Gimmick URP)", hideFlags = HideFlags.HideAndDontSave };
        CopyTexture(source, target, "_BaseMap", "_BaseColorMap", "_MainTex", "_BaseMap");
        CopyTexture(source, target, "_BumpMap", "_NormalMap", "_BumpMap", "_BumpMap");
        CopyTexture(source, target, "_EmissionMap", "_EmissiveColorMap", "_EmissionMap", "_EmissionMap");
        if (source.HasProperty("_BaseColor")) target.SetColor("_BaseColor", source.GetColor("_BaseColor"));
        else if (source.HasProperty("_Color")) target.SetColor("_BaseColor", source.GetColor("_Color"));
        if (target.GetTexture("_BumpMap") != null) target.EnableKeyword("_NORMALMAP");
        if (source.HasProperty("_EmissionColor")) target.SetColor("_EmissionColor", source.GetColor("_EmissionColor"));
        else if (source.HasProperty("_EmissiveColor")) target.SetColor("_EmissionColor", source.GetColor("_EmissiveColor"));
        if (target.GetTexture("_EmissionMap") != null) target.EnableKeyword("_EMISSION");
        _convertedMaterials.Add(target);
        return target;
    }

    private static void CopyTexture(Material source, Material target, string first, string second, string third, string output)
    {
        foreach (string property in new[] { first, second, third })
        {
            if (!source.HasProperty(property) || source.GetTexture(property) == null) continue;
            target.SetTexture(output, source.GetTexture(property));
            target.SetTextureScale(output, source.GetTextureScale(property));
            target.SetTextureOffset(output, source.GetTextureOffset(property));
            return;
        }
    }

    private void Clear()
    {
        if (_placeholder != null) _placeholder.enabled = true;
        if (_instance != null) { _instance.SetActive(false); Release(_instance); _instance = null; }
        foreach (var material in _convertedMaterials) if (material != null) Release(material);
        _convertedMaterials.Clear();
    }

    private static void Release(UnityEngine.Object obj)
    {
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}
