using Godot;

namespace Project.Gameplay.Objects;

public partial class DashPanel : Area3D
{
	[Export(PropertyHint.Range, "0, 2")]
	private float speedRatio;
	[Export]
	private float length; //How long for the boost pad to last
	private bool isQueued; //For when the player collides with the dash panel from the air
	[Export]
	private bool alignToPath; //Forces the player to stay aligned to the path. Useful when a dash panel is right before a corner.

	[Export(PropertyHint.NodePathValidTypes, "AudioStreamPlayer3D")]
	private NodePath sfxPlayer;
	private AudioStreamPlayer3D SfxPlayer { get; set; }
	private CharacterController Character => CharacterController.instance;

	public override void _Ready() => SfxPlayer = GetNodeOrNull<AudioStreamPlayer3D>(sfxPlayer);

	public override void _PhysicsProcess(double _)
	{
		if (!isQueued) return;

		Activate();
	}

	public void OnEntered(Area3D _) => isQueued = true;

	private void Activate()
	{
		if (!Character.IsOnGround) return; //Can't activate when player is in the air

		SfxPlayer.Play();
		isQueued = false;
		Character.ResetActionState();

		//Only apply speed boost when player is moving slow. Don't slow them down!
		if (Character.GroundSettings.GetSpeedRatio(Character.MoveSpeed) < speedRatio)
		{
			Character.MoveSpeed = Character.GroundSettings.Speed * speedRatio;
			Character.Effect.PlayVoice("dash panel");
		}

		LockoutResource lockout = new()
		{
			movementMode = LockoutResource.MovementModes.Replace,
			spaceMode = LockoutResource.SpaceModes.Local,
			movementAngle = 0,
			speedRatio = speedRatio,
			disableActions = true,
			overrideSpeed = true,
			tractionMultiplier = -1,
			frictionMultiplier = 0,
			length = length,
			priority = -1, //Not using priority
		};

		if (alignToPath)
		{
			lockout.movementAngle = 0f;
			lockout.spaceMode = LockoutResource.SpaceModes.PathFollower;
			Character.MovementAngle = Character.PathFollower.ForwardAngle;
			lockout.recenterPlayer = Character.IsLockoutActive && Character.ActiveLockoutData.recenterPlayer;
		}
		else
		{
			Character.MovementAngle = ExtensionMethods.CalculateForwardAngle(this.Forward(), Character.PathFollower.Up());
		}

		Character.Animator.StopBrake();
		Character.AddLockoutData(lockout);
	}
}