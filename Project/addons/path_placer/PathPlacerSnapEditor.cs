using Godot;

namespace PathPlacer;
public partial class PathPlacerSnapEditor : EditorProperty
{
  private Path3D _path;
  private Curve3D _curve;
  private PathPlacer _node;
  private bool _updating = false;
  public override void _Ready()
  {
    _node = GetEditedObject() as PathPlacer;
    GD.Print(_node);
    UpdateTrack();
  }
  private void UpdateTrack()
  {
    if (_node.TargetPath != null)
    {
      _path = _node.TargetPath;
      _curve = _path.Curve;
    }
  }
  public override void _UpdateProperty()
  {
    var propName = GetEditedProperty();
    GD.Print(propName);
    switch (propName)
    {
      case "TargetPath":
        UpdateTrack();
        break;
      case "PathPosition":
        if (_updating) break;
        _updating = true;
        GD.Print("update path position");
        _node.Position = _curve.SampleBaked(_node.PathPosition, true);
        _updating = false;
        break;
        // case "position":
        //   _updating = true;
        //   var localPos = _node.Position * _path.GlobalTransform;
        //   var offset = _curve.GetClosestOffset(localPos);
        //   _node.PathPosition = offset;
        //   var newPos = _curve.SampleBaked(offset, true);
        //   GD.Print(offset, _path.GlobalTransform * _node.Position, _path.GlobalTransform * newPos, _curve.GetClosestPoint(localPos));
        //   // _node.Position = newPos;
        //   _updating = false;
        //   break;
    }
  }
}