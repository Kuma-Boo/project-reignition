using Godot;

namespace Project.Gameplay.Triggers;

public partial class FootholdTrigger : Area3D
{
	private PlayerController Player => StageSettings.Player;

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;
		Player.SetFoothold(this);
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;
		Player.UnsetFoothold(this);
	}
}