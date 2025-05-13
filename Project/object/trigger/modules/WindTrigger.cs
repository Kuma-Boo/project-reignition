using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers;

public partial class WindTrigger : StageTriggerModule
{
	[Export(PropertyHint.NodePathValidTypes, "GPUParticles3D")] private NodePath windFX;
	private GpuParticles3D _windFX;
	[Export] private float windStrength = 15.0f;

	private bool isActive;
	private float influence;

	private readonly float BlendRatio = 2.0f;

	public override void _Ready()
	{
		_windFX = GetNodeOrNull<GpuParticles3D>(windFX);
		DisableProcessing();
	}

	public override void _PhysicsProcess(double _)
	{
		influence = Mathf.MoveToward(influence, isActive && !IsAgainstReverseWall() ? 1f : 0f, BlendRatio * PhysicsManager.physicsDelta);
		if (Mathf.IsZeroApprox(influence) && !isActive)
			DisableProcessing();

		if (!Player.Skills.IsSpeedBreakActive)
			Player.ExternalVelocity += this.Forward() * windStrength * influence;

		GlobalPosition = Player.PathFollower.GlobalPosition;
	}

	/// <summary> Checks whether the player is pushing against a wall. Used to avoid softlocks. </summary>
	private bool IsAgainstReverseWall()
	{
		Vector3 castVector = this.Forward() * (Player.CollisionSize.X + windStrength * PhysicsManager.physicsDelta);
		RaycastHit hit = this.CastRay(Player.GlobalPosition, castVector);
		DebugManager.DrawRay(Player.GlobalPosition, castVector, hit ? Colors.Red : Colors.White);
		return hit && hit.collidedObject.IsInGroup("wall");
	}

	public override void Activate()
	{
		Visible = true;
		ProcessMode = ProcessModeEnum.Inherit;

		isActive = true;
		if (IsInstanceValid(_windFX))
			_windFX.Restart();
	}

	public override void Deactivate()
	{
		isActive = false;
		if (IsInstanceValid(_windFX))
			_windFX.Emitting = false;
	}

	public override void Respawn() => Deactivate();

	private void DisableProcessing()
	{
		Visible = false;
		ProcessMode = ProcessModeEnum.Disabled;
	}
}
