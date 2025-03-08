using Godot;
using Godot.Collections;

namespace Project.Gameplay.Objects;

[Tool]
public partial class Ivy : Launcher
{
	[Export]
	public bool Regenerate
	{
		get => false;
		set
		{
			if (value)
				Initialize();
		}
	}

	[ExportGroup("Settings")]
	[Export(PropertyHint.Range, "2, 50")] public int length;
	[Export]
	private float maxRotation;
	private float IndividualRotation => maxRotation / length;

	[Export] public bool IsSleeping { get; private set; }
	[Export(PropertyHint.Range, "-1,1")]
	public float LaunchRatio
	{
		get => launchRatio;
		private set => launchRatio = value;
	}

	[Export] public bool IsSwingingForward { get; private set; }

	[ExportGroup("Components")]
	[Export] private PackedScene ivyScene;
	private Node3D linkRoot;
	private Array<Node3D> ivyLinks = [];
	[Export(PropertyHint.NodePathValidTypes, "AnimationMixer")]
	private NodePath animator;
	private AnimationMixer _animator;

	[Signal] public delegate void IvyStartedEventHandler();

	public override float GetLaunchRatio()
	{
		if (IsSleeping)
			return 0;

		if (IsSwingingForward)
			return Mathf.Clamp(LaunchRatio + 1, 0f, 1f);

		if (LaunchRatio <= 0)
			return 0;

		return LaunchRatio;
	}

	protected override void SetUp()
	{
		Initialize();

		if (Engine.IsEditorHint())
			return;

		IsSleeping = true;
		_animator = GetNode<AnimationMixer>(animator);
		_animator.Active = true;

		// Adjust swing speed based on length (longer ivys swing slower)
		float swingSpeed = Mathf.Clamp(8f / length, 0.2f, 1f);
		_animator.Set(SwingSpeedParameter, swingSpeed);
	}

	public override void _PhysicsProcess(double _)
	{
		if (IsSleeping)
			return;

		SetRotation();

		if (Engine.IsEditorHint())
			return;

		UpdateSwing();
		CallDeferred(MethodName.UpdateAreaPosition);
	}

	protected override void LaunchAnimation()
	{
		Player.Effect.StartSpinFX();
		Player.Animator.StartSpin(3.0f);
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection") || Engine.IsEditorHint())
			return;

		GD.Print(Player.ActiveLauncher);
		if (Player.ActiveLauncher == this)
			return;

		Player.StartIvy(this);
		EmitSignal(SignalName.IvyStarted);
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player detection") || Engine.IsEditorHint())
			return;
	}

	public float TargetSwingStrength { get; private set; }
	private float currentSwingStrength;
	private float swingStrengthVelocity;
	private readonly float SwingStrengthSmoothing = 0.3f;
	/// <summary> Adds some force from the player. </summary>
	public void AddForce(float amount)
	{
		TargetSwingStrength = Mathf.Clamp(TargetSwingStrength + amount, 0f, 1f);

		if (IsSleeping && !Mathf.IsZeroApprox(TargetSwingStrength))
		{
			_animator.Set(SwingSeekParameter, 0f);
			IsSleeping = false;
		}

		_animator.Set(SwingStrengthParameter, currentSwingStrength);
	}

	private readonly StringName SwingSeekParameter = "parameters/swing_seek/seek_request";
	private readonly StringName SwingSpeedParameter = "parameters/swing_speed/scale";
	private readonly StringName SwingStrengthParameter = "parameters/swing_strength/blend_position";
	private void UpdateSwing()
	{
		currentSwingStrength = ExtensionMethods.SmoothDamp(currentSwingStrength, TargetSwingStrength, ref swingStrengthVelocity, SwingStrengthSmoothing);

		if (TargetSwingStrength <= currentSwingStrength && currentSwingStrength < 0.01f)
		{
			IsSleeping = true;
			currentSwingStrength = 0;
		}

		_animator.Set(SwingStrengthParameter, currentSwingStrength);
	}

	#region Setup
	public void SetRotation()
	{
		float rotation = IndividualRotation * launchRatio;

		for (int i = 0; i < ivyLinks.Count; i++)
			ivyLinks[i].RotationDegrees = Vector3.Left * rotation;

		UpdateAreaPosition();
	}

	/// <summary> Moves the area trigger to the last link's position. </summary>
	private void UpdateAreaPosition()
	{
		launchPoint.GlobalTransform = ivyLinks[ivyLinks.Count - 1].GlobalTransform;
		launchPoint.GlobalPosition -= ivyLinks[ivyLinks.Count - 1].Up() * .5f;
	}

	private void Initialize()
	{
		if (launchPoint == null)
		{
			GD.PushError("Ivy launch Point could not be found.");
			return;
		}

		// Resize ivy to the proper length
		UpdateIvyLength();
		UpdateAreaPosition();
	}

	private void UpdateIvyLength()
	{
		if (ivyLinks.Count > length)
		{
			// Since every ivy link is parented, we only need to delete one.
			ivyLinks[length].QueueFree();
			ivyLinks.Resize(length);
			return;
		}

		if (ivyScene == null)
		{
			GD.PushError("Ivy Scene could not be found.");
			return;
		}

		if (ivyLinks.Count == 0)
		{
			linkRoot = ivyScene.Instantiate<Node3D>();
			AddChild(linkRoot);
			ivyLinks.Add(linkRoot);
		}

		// Add ivy individually as needed
		while (ivyLinks.Count < length)
		{
			Node3D linkNode = ivyScene.Instantiate<Node3D>();
			ivyLinks[ivyLinks.Count - 1].AddChild(linkNode); // Add as a child so rotations carry over
			linkNode.Position = Vector3.Down;
			ivyLinks.Add(linkNode);
		}
	}
	#endregion
}
