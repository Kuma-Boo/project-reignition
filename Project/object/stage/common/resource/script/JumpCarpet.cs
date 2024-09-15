using Godot;

namespace Project.Gameplay.Objects;

[Tool]
public partial class JumpCarpet : Launcher
{
	/// <summary> How many times the JumpCarpet needs to be bounced on before reaching a full launch ratio. </summary>
	[Export(PropertyHint.Range, "0, 5, 1")]
	private int maxBounceCount = 3; // Set this to 0 if you only want to use the launcher's primary launch settings.
	private int currentBounceCount;
	private float ratioIncrement;
	[Export]
	private AnimationPlayer animator;

	public override void _Ready()
	{
		// Pre-calculate ratio increment
		if (maxBounceCount != 0)
			ratioIncrement = 1.0f / maxBounceCount;
	}

	public override void _PhysicsProcess(double _)
	{
		if (currentBounceCount == 0)
			return;

		// TODO Refactor PlayerController to have a LandedOnGround signal and use that instead?
		// Reset Jump Carpet when the player lands on ground
		if (Player.IsOnGround)
			Reset();
	}

	public override void Activate(Area3D a)
	{
		animator.Play("launch");
		animator.Seek(0.0);

		launchRatio = ratioIncrement * currentBounceCount;
		base.Activate(a);

		// Increment bounce counter
		currentBounceCount++;
		if (currentBounceCount >= maxBounceCount)
		{
			currentBounceCount = maxBounceCount;
			launchRatio = 1;
		}
	}

	private void Reset()
	{
		launchRatio = 0;
		currentBounceCount = 0;
	}
}
