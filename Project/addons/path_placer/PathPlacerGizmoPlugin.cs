#if TOOLS
using Godot;

namespace PathPlacer;
public partial class PathPlacerGizmoPlugin : EditorNode3DGizmoPlugin
{
  public PathPlacerGizmoPlugin()
  {
    CreateMaterial("main", new Color(0, 1, 1));
    CreateHandleMaterial("handles");
  }
  public override bool _HasGizmo(Node3D node)
  {
    return node is PathPlacer;
  }
  public override void _Redraw(EditorNode3DGizmo gizmo)
  {
    gizmo.Clear();
    var node = gizmo.GetNode3D() as PathPlacer;
    if (node.TargetPath == null) return; // do not draw anything if there is no path selected

    var path = node.TargetPath.Curve;
    var trackPoint = path.GetClosestPoint(node.GlobalPosition);
    GD.Print(path.GetClosestOffset(node.GlobalPosition));
    GD.Print(node.GlobalPosition, " ", trackPoint);
    var d = node.GlobalPosition - trackPoint;

    Vector3[] lines = {
      d,
      Vector3.Zero
    };
    Vector3[] handles = {
      d,
      Vector3.Zero
    };

    gizmo.AddLines(lines, GetMaterial("main", gizmo), false);
    gizmo.AddHandles(handles, GetMaterial("handles", gizmo), []);
  }
}
#endif