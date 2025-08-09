using Godot;

namespace Project.Interface.VirtualKeyboard;

public partial class VirtualKey : Button
{
  public char[] Key = ['\0', '\0', '\0'];
  [Signal]
  public delegate void VirtualKeyPressEventHandler(char character);
  private VirtualKeyboard.KeySet keySet = VirtualKeyboard.KeySet.Lower;
  public override void _EnterTree()
  {
    // GetParent().Connect(VirtualKeyboard.SignalName.KeySetChange, Callable.From<int>(VirtualKeyboard_KeySetChange));
    UpdateLabel();
  }
  private void UpdateLabel()
  {
    Text = Key[0].ToString();
  }
  private void VirtualKeyboard_KeySetChange(int state)
  {

  }
  public override void _Pressed()
  {
    EmitSignal(SignalName.VirtualKeyPress, Key[0]);
  }
}
