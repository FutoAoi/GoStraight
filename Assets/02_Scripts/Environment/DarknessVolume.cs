using UnityEngine;
using UnityEngine.Rendering;

// Place the root at the doorway. Local +Z points into the dark room.
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class DarknessVolume : MonoBehaviour
{
    [SerializeField] private MeshRenderer _volumeRenderer;
    [SerializeField] private Vector3 _size = new(8f, 5f, 16f);
    [SerializeField] private Color _fogColor = Color.black;
    [SerializeField, Range(0f, 2f)] private float _density = 0.5f;
    [SerializeField, Min(0f)] private float _clearDistance = 0f;
    private MaterialPropertyBlock _properties;
    private bool _dirty = true;
    private static readonly int ColorId = Shader.PropertyToID("_FogColor");
    private static readonly int DensityId = Shader.PropertyToID("_Density");
    private static readonly int ClearId = Shader.PropertyToID("_ClearDistance");

    private void OnEnable()
    {
        if (_volumeRenderer != null) _volumeRenderer.enabled = true;
        Apply();
    }
    private void OnDisable()
    {
        if (_volumeRenderer != null) _volumeRenderer.enabled = false;
    }
    private void OnValidate() => _dirty = true;
    private void Update() { if (_dirty) Apply(); }

    private void Apply()
    {
        _dirty = false;
        if (_volumeRenderer == null) return;
        _size = new Vector3(Mathf.Max(0.01f, _size.x), Mathf.Max(0.01f, _size.y), Mathf.Max(0.01f, _size.z));
        var box = _volumeRenderer.transform;
        box.localPosition = new Vector3(0f, _size.y * 0.5f, _size.z * 0.5f);
        box.localRotation = Quaternion.identity;
        box.localScale = _size;
        _volumeRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _volumeRenderer.receiveShadows = false;
        if (_properties == null) _properties = new MaterialPropertyBlock();
        _volumeRenderer.GetPropertyBlock(_properties);
        _properties.SetColor(ColorId, _fogColor);
        _properties.SetFloat(DensityId, Mathf.Max(0f, _density));
        _properties.SetFloat(ClearId, Mathf.Max(0f, _clearDistance));
        _volumeRenderer.SetPropertyBlock(_properties);
    }

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 previous = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.4f, 0.3f, 0.8f, 0.8f);
        Gizmos.DrawWireCube(new Vector3(0f, _size.y * 0.5f, _size.z * 0.5f), _size);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(new Vector3(0f, 0.1f, 0f), new Vector3(0f, 0.1f, Mathf.Min(2f, _size.z)));
        Gizmos.matrix = previous;
        Gizmos.color = previousColor;
    }
}
