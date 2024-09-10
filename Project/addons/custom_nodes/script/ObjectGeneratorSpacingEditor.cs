using Godot;

namespace Project.CustomNodes;
public partial class ObjectGeneratorSpacingEditor : EditorProperty
{
  private ObjectGenerator _node;
  public override void _Ready()
  {
    _node = GetEditedObject() as ObjectGenerator;
    // we cant bind to children changing because the editor is not focused as they are
  }
  public override void _UpdateProperty()
  {
    GD.Print("update alignment");
    _node.AlignChildren();
  }
}