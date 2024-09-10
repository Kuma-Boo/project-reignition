using Godot;

namespace PathPlacer;
[Tool]
public partial class PathPlacer : Node3D
{
  public delegate void PropertyChangedEventHandler();
  [Export]
  public Path3D TargetPath;
  /// <summary>
  /// This is the absolute position along the target path based on path length
  /// </summary>
  [Export(PropertyHint.Range, "0,1000,0.1,or_greater,or_less")]
  public float PathPosition = 0f;
  [Export]
  public bool TrackRotation = false;
  public override void _Ready()
  {
  }
  // public override void _ValidateProperty(Dictionary property)
  // {
  //   // hide transform property
  //   switch (property["name"].ToString())
  //   {
  //     case "Transform":
  //     case "position":
  //     case "rotation":
  //       property["usage"] = Variant.CreateFrom((int)PropertyUsageFlags.NoEditor);
  //       break;
  //     default:
  //       break;
  //   }
  // }

}