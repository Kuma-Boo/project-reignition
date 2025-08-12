using Godot;
using Project.Core;

namespace Project.Interface;

public partial class Boot : Node
{
	public override void _Ready() => TransitionManager.instance.LoadCommonResources();

	private void StartTitleTransition()
	{
		TransitionManager.QueueSceneChange("res://interface/menu/Menu.tscn");
		TransitionManager.StartTransition(new()
		{
			inSpeed = .5f,
			outSpeed = .5f,
			color = Colors.Black
		});
	}
}
