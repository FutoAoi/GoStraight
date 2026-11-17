public enum TargetMarkerState { Hidden, Candidate, Locked }

public interface ITargetMarkerView
{
    void Render(TargetMarkerState state);
}

public sealed class TargetMarkerModel
{
    public TargetMarkerState State { get; private set; }
    public void Update(bool available, bool locked, bool visible)
    {
        State = !visible ? TargetMarkerState.Hidden : locked ? TargetMarkerState.Locked
            : available ? TargetMarkerState.Candidate : TargetMarkerState.Hidden;
    }
}

public sealed class TargetMarkerPresenter
{
    private readonly TargetMarkerModel _model;
    private readonly ITargetMarkerView _view;
    public TargetMarkerPresenter(TargetMarkerModel model, ITargetMarkerView view)
    {
        _model = model;
        _view = view;
    }
    public void Update(bool available, bool locked, bool visible)
    {
        _model.Update(available, locked, visible);
        _view.Render(_model.State);
    }
}
