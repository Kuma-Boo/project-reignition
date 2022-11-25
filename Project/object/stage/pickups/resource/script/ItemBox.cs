using Godot;

namespace Project.Gameplay.Objects
{
	public partial class ItemBox : Pickup
	{
		[Export]
		private bool isFlying;
		[Export]
		private PackedScene itemType;
		[Export(PropertyHint.Range, "1, 20")]
		private int amount = 1;
	}
}
