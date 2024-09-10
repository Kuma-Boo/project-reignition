using System;
using Godot;

namespace PathPlacer;
public partial class PathPlacerInspectorPlugin : EditorInspectorPlugin
{
  public override bool _CanHandle(GodotObject @object)
  {
    GD.Print(@object is PathPlacer);
    return @object is PathPlacer;
  }
  public override bool _ParseProperty(GodotObject @object, Variant.Type type, string name, PropertyHint hintType, string hintString, PropertyUsageFlags usageFlags, bool wide)
  {
    GD.Print(name);
    if (name == "position" || name == "PathPosition")
    {
      AddPropertyEditor(name, new PathPlacerSnapEditor());
    }

    return false;
  }
}