using System;
using Godot;

namespace Project.CustomNodes;
public partial class ObjectGeneratorInspectorPlugin : EditorInspectorPlugin
{
  public override bool _CanHandle(GodotObject @object)
  {
    return @object is ObjectGenerator;
  }
  public override bool _ParseProperty(GodotObject @object, Variant.Type type, string name, PropertyHint hintType, string hintString, PropertyUsageFlags usageFlags, bool wide)
  {
    switch (name)
    {
      case "amount":
        AddPropertyEditor(name, new ObjectGeneratorAmountEditor());
        break;
      case "spacing":
      case "disablePathY":
      case "progressOffset":
      case "shape":
      case "orientation":
        AddPropertyEditor(name, new ObjectGeneratorSpacingEditor());
        break;
    }
    return false;
  }
}