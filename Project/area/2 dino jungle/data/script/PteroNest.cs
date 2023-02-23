using Godot;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// For that one act in Dinosaur Jungle.
	/// </summary>
	public partial class PteroNest : Node3D
	{
		[Export]
		private Area3D trigger;

		public PteroEgg AssignedEgg { get; set; }
		private CharacterController Character => CharacterController.instance;

		public void SetType(Node3D model) //Adds the sign model as a child
		{
			CallDeferred("add_child", model);
			model.SetDeferred("transform", Transform3D.Identity);
		}

		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			if (Character.Lockon.IsHomingAttacking) //Bounce the player
				Character.Lockon.StartBounce();

			//Check if the target egg is held
			if (PteroEggManager.IsEggHeld(AssignedEgg))
			{
				AssignedEgg.ReturnToNest(this);
				trigger.SetDeferred("monitorable", false);
				trigger.SetDeferred("monitoring", false);
			}
		}
	}
}
