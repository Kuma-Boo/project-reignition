using Godot;
using Project.Core;
using Project.CustomNodes;

namespace Project.Gameplay;

/// <summary> Controls the king's bones in Skeleton Dome. </summary>
public partial class Bone : Node3D
{
	[Signal] public delegate void RespawnedEventHandler();
	[Signal] public delegate void CollectedEventHandler();
	[Signal] public delegate void CollectionFinishedEventHandler();
	[Signal] public delegate void JumpFinishedEventHandler();
	[Signal] public delegate void ObjectiveFinishedEventHandler();

	[Export] private Area3D area;
	[Export] private Node3D root;
	[Export] private AudioStreamPlayer3D sfx;
	[Export] private GroupGpuParticles3D warpParticleGroup;

	private StageSettings Stage => StageSettings.Instance;
	private PlayerController Player => StageSettings.Player;
	private Tween tween;
	private SpawnData spawnData;

	public override void _Ready()
	{
		spawnData = new(GetParent(), Transform);
		Stage.Respawned += Respawn;
		Respawn();
	}

	private void Respawn()
	{
		if (tween?.IsValid() == true)
			tween.Kill();

		spawnData.Respawn(this);
		root.Transform = Transform3D.Identity;
		Visible = true;

		area.SetDeferred("monitoring", true);
		area.SetDeferred("monitorable", true);

		warpParticleGroup.StopGroup();
		warpParticleGroup.Visible = false;
		sfx.VolumeLinear = 1f;
		sfx.Play();

		ProcessMode = ProcessModeEnum.Inherit;
		EmitSignal(SignalName.Respawned);
	}

	private void StartTeleport()
	{
		warpParticleGroup.GetParent().RemoveChild(warpParticleGroup);
		Stage.AddChild(warpParticleGroup);

		RaycastHit groundHit = Player.CastRay(Player.GlobalPosition, Vector3.Down * 20f, Runtime.Instance.environmentMask);
		warpParticleGroup.GlobalPosition = groundHit.point + Vector3.Up * 0.5f;
		warpParticleGroup.ResetPhysicsInterpolation();
		warpParticleGroup.RestartGroup();
		warpParticleGroup.Visible = true;

		LaunchSettings settings = LaunchSettings.Create(Player.GlobalPosition, groundHit.point, 3f);
		settings.AllowDamage = false;
		Player.StartLauncher(settings);

		Player.Animator.StartSpin(2f);
		Player.Effect.StartSpinFX();

		Player.Connect(PlayerController.SignalName.LaunchFinished, Callable.From(() => OnJumpFinished()), (uint)ConnectFlags.Deferred + (uint)ConnectFlags.OneShot);
	}

	private void OnJumpFinished()
	{
		Despawn();
		EmitSignal(SignalName.JumpFinished);
	}

	private void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		CallDeferred(MethodName.Collect);
	}

	private void Collect()
	{
		Stage.IncrementObjective();
		EmitSignal(SignalName.Collected);

		area.SetDeferred("monitoring", false);
		area.SetDeferred("monitorable", false);

		Vector3 offset = (Player.CenterPosition - GlobalPosition) * GlobalBasis.Inverse();
		root.Position = offset;

		Transform3D t = GlobalTransform;
		GetParent().RemoveChild(this);
		Player.AddChild(this);
		t.Origin = Player.CenterPosition;
		GlobalTransform = t;
		ResetPhysicsInterpolation();

		tween = CreateTween().SetParallel(true);
		tween.TweenProperty(this, "rotation", Rotation + Vector3.Up * Mathf.Tau, 1f).FromCurrent();
		tween.TweenProperty(root, "position", root.Position + Vector3.Forward * 5f, 1f).FromCurrent();
		tween.TweenProperty(this, "scale", Vector3.One * 0.001f, 1f).FromCurrent();
		tween.TweenProperty(sfx, "volume_linear", 0f, 1f).FromCurrent();

		if (Stage.CurrentObjectiveCount == Stage.Data.MissionObjectiveCount)
		{
			EmitSignal(SignalName.ObjectiveFinished);
			StartTeleport();
			return;
		}

		tween.TweenCallback(Callable.From(() => EmitSignal(SignalName.CollectionFinished))).SetDelay(1f);
		tween.TweenCallback(Callable.From(() => Despawn())).SetDelay(2f);
		if (Player.IsHomingAttacking)
			Player.StartBounce();
	}

	private void Despawn()
	{
		sfx.Stop();
		spawnData.Respawn(this);
		Visible = false;
		ProcessMode = ProcessModeEnum.Disabled;
	}
}
