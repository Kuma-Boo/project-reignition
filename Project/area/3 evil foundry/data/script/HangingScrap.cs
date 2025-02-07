using Godot;
using Project.Core;

namespace Project.Gameplay.Objects;

/// <summary> Hanging scrap found in Evil Foundry. </summary>
public partial class HangingScrap : DestructableObject
{
	[Export(PropertyHint.Range, "0,5,.1")] private float dropDelaySeconds = 1.0f;
	private bool isDropping;
	private float dropTimer;
	private PlayerController Player => StageSettings.Player;

	/// <summary> The number of seconds to delay dropping when the player stands on the platform. </summary>
	private readonly float StandingDelaySeconds = 0.5f;

	protected override void ProcessObject()
	{
		base.ProcessObject();
		ProcessDrop();
	}

	protected override void ProcessPlayerCollision()
	{
		if (Player.IsOnGround && !Player.IsJumpDashOrHomingAttack && animator.CurrentAnimation != "start-drop")
		{
			QueueDrop(StandingDelaySeconds);
			return;
		}

		base.ProcessPlayerCollision();
	}

	public override void Respawn()
	{
		base.Respawn();
		dropTimer = 0.0f;
		isDropping = false;
	}

	public override void Shatter()
	{
		animator.Play("hide-base-mesh");
		animator.Advance(0.0);
		base.Shatter();
	}

	/// <summary> Call this from a signal. </summary>
	private void QueueDrop() => QueueDrop(dropDelaySeconds);
	private void QueueDrop(float time)
	{
		if (isDropping && time > dropTimer)
			return;

		isDropping = true;
		dropTimer = time;

		if (Mathf.IsZeroApprox(time))
			ProcessDrop();
	}

	private void ProcessDrop()
	{
		if (isShattered || !isDropping)
			return;

		dropTimer = Mathf.MoveToward(dropTimer, 0, PhysicsManager.physicsDelta);
		if (!Mathf.IsZeroApprox(dropTimer))
			return;

		Drop();
	}

	private void Drop()
	{
		if (isShattered)
			return;

		isShattered = true;
		animator.Play("shatter");
	}
}