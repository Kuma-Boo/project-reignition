using Godot;

namespace Project.Gameplay;

public partial class PlayerStateController : Node
{
	private PlayerController Player;
	public void Initialize(PlayerController player) => Player = player;
	
	[Export]
	private AnimationPlayer hitboxAnimator;
	public void ChangeHitbox(StringName hitboxAnimation)
	{
		hitboxAnimator.Play(hitboxAnimation);
		hitboxAnimator.Advance(0);
		hitboxAnimator.Play(hitboxAnimation);
	}
	
	[Export]
	public LaunchState launcherState;
	public void StartLauncher(LaunchSettings settings)
	{
		if (!launcherState.UpdateSettings(settings)) // Failed to start launcher state
			return;

		Player.StateMachine.ChangeState(launcherState);
	}

	public bool CanJumpDash { get; set; }

	[Signal]
	public delegate void AttackStateChangeEventHandler();
	/// <summary> Keeps track of how much attack the player will deal. </summary>
	public AttackStates AttackState
	{
		get => attackState;
		set
		{
			attackState = value;
			EmitSignal(SignalName.AttackStateChange);
		}
	}
	private AttackStates attackState;
	public enum AttackStates
	{
		None, // Player is not attacking
		Weak, // Player will deal a single point of damage 
		Strong, // Double Damage -- Perfect homing attacks
		OneShot, // Destroy enemies immediately (i.e. Speedbreak and Crest of Fire)
	}
	public void ResetAttackState() => attackState = AttackStates.None;
}
