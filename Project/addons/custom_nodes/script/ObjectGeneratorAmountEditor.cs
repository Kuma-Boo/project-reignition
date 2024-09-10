using Godot;

namespace Project.CustomNodes;
public partial class ObjectGeneratorAmountEditor : EditorProperty
{
  private ObjectGenerator _node;
  public override void _Ready()
  {
    _node = GetEditedObject() as ObjectGenerator;
    // we cant bind to children changing because the editor is not focused as they are
  }
  public override void _UpdateProperty()
  {
    var propName = GetEditedProperty();
    switch (propName)
    {
      case "amount":
        var amount = (int)_node.Get(propName);
        if (_node.GetChildCount() != amount)
        {
          _node.GenerateChildren();
        }
        break;
    }
  }
}