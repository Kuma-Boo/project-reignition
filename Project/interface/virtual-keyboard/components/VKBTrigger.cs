using Godot;

namespace Project.Interface.VirtualKeyboard;

public partial class VKBTrigger : Node
{
	private Control _target;
	private VirtualKeyboard _vkb;
	public override void _Ready()
	{
		_target = GetParent<LineEdit>();
		_target.Connect("focus_entered", Callable.From(this.Parent_Focused));

		_vkb = GetNode<VirtualKeyboard>("/root/VirtualKeyboard");
	}
	private void Parent_Focused()
	{
		_vkb.ShowKeyboard(_target);
	}
}
