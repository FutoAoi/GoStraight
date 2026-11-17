using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
public class EnemyDamageFlash : MonoBehaviour
{
    [SerializeField] private Color _flashColor = Color.red;
    [SerializeField, Min(0.01f)] private float _duration = 0.35f;
    [SerializeField, Min(0.01f)] private float _blinkInterval = 0.07f;

    private sealed class MaterialSlot
    {
        public Renderer Renderer;
        public int Index;
        public MaterialPropertyBlock Original;
        public MaterialPropertyBlock Flash;
    }

    private readonly List<MaterialSlot> _slots = new();
    private Health _health;
    private bool _flashing, _showingRed;
    private float _started;

    private void Awake()
    {
        _health = GetComponent<Health>();
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) continue;
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null || (!material.HasProperty("_BaseColor") && !material.HasProperty("_Color"))) continue;
                _slots.Add(new MaterialSlot
                {
                    Renderer = renderer,
                    Index = i,
                    Original = new MaterialPropertyBlock(),
                    Flash = new MaterialPropertyBlock()
                });
            }
        }
    }

    private void OnEnable() => _health.Damaged += HandleDamage;

    private void HandleDamage(int damage)
    {
        if (!_flashing)
        {
            foreach (var slot in _slots)
            {
                if (slot.Renderer == null) continue;
                slot.Renderer.GetPropertyBlock(slot.Original, slot.Index);
                slot.Renderer.GetPropertyBlock(slot.Flash, slot.Index);
                var material = slot.Renderer.sharedMaterials[slot.Index];
                if (material.HasProperty("_BaseColor")) slot.Flash.SetColor("_BaseColor", _flashColor);
                if (material.HasProperty("_Color")) slot.Flash.SetColor("_Color", _flashColor);
                // Use a white map during the flash so a green atlas still becomes visibly red.
                if (material.HasProperty("_BaseMap")) slot.Flash.SetTexture("_BaseMap", Texture2D.whiteTexture);
                if (material.HasProperty("_MainTex")) slot.Flash.SetTexture("_MainTex", Texture2D.whiteTexture);
            }
        }
        _started = Time.time;
        _flashing = true;
        SetRed(true);
    }

    private void Update()
    {
        if (!_flashing) return;
        float elapsed = Time.time - _started;
        if (elapsed >= _duration)
        {
            SetRed(false);
            _flashing = false;
            return;
        }
        bool red = Mathf.FloorToInt(elapsed / Mathf.Max(0.01f, _blinkInterval)) % 2 == 0;
        if (red != _showingRed) SetRed(red);
    }

    private void SetRed(bool red)
    {
        _showingRed = red;
        foreach (var slot in _slots)
        {
            if (slot.Renderer != null)
                slot.Renderer.SetPropertyBlock(red ? slot.Flash : slot.Original, slot.Index);
        }
    }

    private void OnDisable()
    {
        if (_health != null) _health.Damaged -= HandleDamage;
        if (_flashing) SetRed(false);
        _flashing = false;
    }
}
