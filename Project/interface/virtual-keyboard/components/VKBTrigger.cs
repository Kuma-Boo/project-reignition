using Godot;

namespace Project.Interface.VirtualKeyboard;

[GlobalClass]
public partial class VKBTrigger : Node
{
	private Control _target;
	private VirtualKeyboard _vkb;
	public override void _Ready()
	{
		_target = GetParent<Control>();
		if (_target is LineEdit lineEdit)
		{
			lineEdit.VirtualKeyboardEnabled = false;
			lineEdit.VirtualKeyboardShowOnFocus = false;
		}
		else if (_target is TextEdit textEdit)
		{
			textEdit.VirtualKeyboardEnabled = false;
			textEdit.VirtualKeyboardShowOnFocus = false;
		}
		_target.Connect("focus_entered", Callable.From(this.Parent_Focused));

		_vkb = GetNode<VirtualKeyboard>("/root/VirtualKeyboard");
	}
	private void Parent_Focused()
	{
		if (_vkb is not null)
			_vkb.ShowKeyboard(_target);
	}
}

public interface ICanTriggerNativeVKB
{
	public bool VirtualKeyboardEnabled { get; set; }
	public bool VirtualKeyboardShowOnFocus { get; set; }
}