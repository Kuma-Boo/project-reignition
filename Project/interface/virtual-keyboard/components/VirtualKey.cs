using Godot;

namespace Project.Interface.VirtualKeyboard;

public partial class VirtualKey : Button
{
  private int _keySet = 0;
  public char[] Key = ['\0', '\0', '\0'];
  [Signal]
  public delegate void VirtualKeyPressEventHandler(char character);
  private VirtualKeyboard.KeySet keySet = VirtualKeyboard.KeySet.Lower;
  public VirtualKeyboard ParentKeyboard;
  public override void _EnterTree()
  {
    ParentKeyboard.Connect(VirtualKeyboard.SignalName.KeySetChange, Callable.From<int>(VirtualKeyboard_KeySetChange));
    UpdateLabel();
  }
  private void UpdateLabel()
  {
    Text = Key[_keySet].ToString();
  }
  private void VirtualKeyboard_KeySetChange(int state)
  {
    _keySet = state;
    UpdateLabel();
  }
  public override void _Pressed()
  {
    EmitSignal(SignalName.VirtualKeyPress, Key[_keySet]);
  }
}
