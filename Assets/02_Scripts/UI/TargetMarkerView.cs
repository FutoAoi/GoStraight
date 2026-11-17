using System.Collections.Generic;
using UnityEngine;

public sealed class TargetMarkerView : MonoBehaviour, ITargetMarkerView
{
    [SerializeField] private RectTransform _marker;
    [SerializeField] private GameObject _arrow;
    [SerializeField] private GameObject _lockMark;
    [Header("Offsets from enemy model bounds")]
    [SerializeField] private Vector3 _arrowWorldOffset = Vector3.zero;
    [SerializeField] private Vector3 _lockWorldOffset = Vector3.zero;
    [SerializeField] private Vector2 _arrowScreenOffset = new(0f, 6f);
    [SerializeField] private Vector2 _lockScreenOffset = Vector2.zero;
    private readonly List<Entry> _entries = new();

    private sealed class Entry : ITargetMarkerView
    {
        public RectTransform Rect;
        public GameObject Arrow;
        public GameObject Lock;
        public TargetMarkerPresenter Presenter;
        public void Render(TargetMarkerState state)
        {
            Arrow.SetActive(state == TargetMarkerState.Candidate);
            Lock.SetActive(state == TargetMarkerState.Locked);
        }
    }

    public void Configure(RectTransform marker, GameObject arrow, GameObject lockMark)
    {
        _marker = marker;
        _arrow = arrow;
        _lockMark = lockMark;
    }

    public void RenderTargets(Camera camera, IReadOnlyList<Targetable> targets, Targetable locked)
    {
        Render(TargetMarkerState.Hidden);
        int used = 0;
        if (camera != null && _marker != null && targets != null)
        {
            foreach (var target in targets)
            {
                if (target == null || !target.CanTarget) continue;
                bool isLocked = target == locked;
                var entry = GetEntry(used++);
                var world = target.MarkerPosition(isLocked) + (isLocked ? _lockWorldOffset : _arrowWorldOffset);
                bool visible = Place(camera, entry.Rect, world, isLocked ? _lockScreenOffset : _arrowScreenOffset);
                entry.Presenter.Update(true, isLocked, visible);
                entry.Rect.name = "Marker - " + target.name;
            }
        }
        for (int i = used; i < _entries.Count; i++) _entries[i].Presenter.Update(false, false, false);
    }

    private Entry GetEntry(int index)
    {
        if (index < _entries.Count) return _entries[index];
        var rect = Instantiate(_marker, _marker.parent);
        // Match template references by hierarchy paths, keeping user-edited artwork and sizes.
        var arrowPath = Path(_arrow.transform, _marker);
        var lockPath = Path(_lockMark.transform, _marker);
        var entry = new Entry { Rect = rect, Arrow = rect.Find(arrowPath).gameObject, Lock = rect.Find(lockPath).gameObject };
        rect.gameObject.SetActive(true);
        entry.Presenter = new TargetMarkerPresenter(new TargetMarkerModel(), entry);
        _entries.Add(entry);
        return entry;
    }

    private static string Path(Transform child, Transform root)
    {
        string path = child.name;
        while (child.parent != root) { child = child.parent; path = child.name + "/" + path; }
        return path;
    }

    private bool Place(Camera camera, RectTransform marker, Vector3 world, Vector2 offset)
    {
        var point = camera.WorldToViewportPoint(world);
        if (point.z <= 0 || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) return false;
        var parent = marker.parent as RectTransform;
        var canvas = GetComponentInParent<Canvas>();
        var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var screen = camera.ViewportToScreenPoint(point);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out var local)) return false;
        marker.localPosition = local + offset;
        return true;
    }

    public void Render(TargetMarkerState state)
    {
        if (_arrow != null) _arrow.SetActive(state == TargetMarkerState.Candidate);
        if (_lockMark != null) _lockMark.SetActive(state == TargetMarkerState.Locked);
    }

    public void HideAll()
    {
        Render(TargetMarkerState.Hidden);
        foreach (var entry in _entries) entry.Presenter.Update(false, false, false);
    }
}