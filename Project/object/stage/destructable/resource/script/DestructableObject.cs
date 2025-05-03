using Godot;
using System.Collections.Generic;

namespace Project.Gameplay.Objects;

/// <summary>
/// Object that shatters when destroyed. All pieces must be a child of pieceRoot.
/// </summary>
public partial class DestructableObject : Node3D
{
	[Signal]
	public delegate void ShatteredEventHandler();

	private Tween tweener;

	[Export]
	private float pieceMass;
	[Export(PropertyHint.Range, "-128,128")]
	private float gravityScale = 1;
	[Export]
	public bool shakeScreenOnShatter = true;
	[Export]
	/// <summary> Stop the player when being shattered? </summary>
	private bool stopPlayerOnShatter;
	[Export]
	private bool damagePlayer;

	[Export]
	/// <summary> Don't collide with the environment? </summary>
	private bool disableEnvironmentCollision = true;
	[Export]
	/// <summary> Don't automatically respawn this object. Call Respawn() manually instead. </summary>
	private bool disableRespawn;

	[Export(PropertyHint.Flags, "PlayerCollision,ObjectCollision,AttackSkill,JumpDash,SpeedBreak")]
	private int shatterFlags;
	private enum ShatterFlags
	{
		Signal = 0, // Don't listen to area collider
		PlayerCollision = 1, // Break immediately when touching
		ObjectCollision = 2, // Should objects be able to shatter this?
		AttackSkill = 4, // Break when player is using an attack skill
		JumpDash = 8, // Break when player is jumpdashing/homing attacking. Must be enabled even if AttackSkill is active.
		SpeedBreak = 16, // Break when speedbreak is active. Must be enabled even if AttackSkill is active.
	}
	[Export(PropertyHint.Range, "0,1,0.1")]
	private float bounceStrength;
	[Export]
	private BounceState.SnapMode snapMode = BounceState.SnapMode.SnappingEnabled;
	private const float ShatterStrength = 10.0f;

	private ShatterFlags FlagSetting => (ShatterFlags)shatterFlags;
	private PlayerController Player => StageSettings.Player;

	protected bool isShattered;
	protected bool isInteractingWithPlayer;

	/// <summary> Unshattered model. </summary>
	[Export] protected Node3D root;
	/// <summary> Parent node of all the pieces. </summary>
	[Export] protected Node3D pieceRoot;
	[Export] protected AnimationPlayer animator;
	private readonly List<Piece> pieces = [];
	private class Piece
	{
		public RigidBody3D rigidbody;
		public MeshInstance3D mesh;
		public CollisionShape3D collider;
		public Vector3 scale;
		public Vector3 position; // Local transform to spawn with
	}

	public override void _Ready() => SetUp();

	public override void _PhysicsProcess(double _) => ProcessObject();

	protected virtual void SetUp()
	{
		for (int i = 0; i < pieceRoot.GetChildCount(); i++)
		{
			RigidBody3D rigidbody = pieceRoot.GetChildOrNull<RigidBody3D>(i);
			if (rigidbody == null) // Pieces must be a rigidbody
				continue;

			MeshInstance3D mesh = rigidbody.GetChildOrNull<MeshInstance3D>(0); // mesh must be the FIRST child of the rigidbody.
			CollisionShape3D collider = rigidbody.GetChildOrNull<CollisionShape3D>(1); // collider must be the SECOND child of the rigidbody.

			rigidbody.GravityScale = gravityScale;
			rigidbody.Mass = pieceMass;
			rigidbody.CollisionLayer = Core.Runtime.Instance.particleCollisionLayer;
			rigidbody.CollisionMask = Core.Runtime.Instance.particleCollisionMask;

			if (disableEnvironmentCollision)
				rigidbody.CollisionMask &= ~Core.Runtime.Instance.environmentMask;

			Piece piece = new()
			{
				rigidbody = rigidbody,
				mesh = mesh,
				collider = collider,
				scale = pieceRoot.GlobalTransform.Basis.Scale,
				position = rigidbody.Position
			};
			pieces.Add(piece);

			// Force piece shaders to compile
			piece.mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
			piece.mesh.Transparency = 0.01f;
		}

		Respawn();

		if (!disableRespawn)
			StageSettings.Instance.Respawned += Respawn;

		StageSettings.Instance.Unloaded += Unload;
	}

	protected virtual void ProcessObject()
	{
		if (!isInteractingWithPlayer) return;
		ProcessPlayerCollision();
	}

	public virtual void Respawn()
	{
		isShattered = false;
		isInteractingWithPlayer = false;

		tweener?.Kill();

		// Reset Pieces
		for (int i = 0; i < pieces.Count; i++)
		{
			pieces[i].rigidbody.Freeze = true;
			pieces[i].rigidbody.LinearVelocity = pieces[i].rigidbody.AngularVelocity = Vector3.Zero; // Reset velocity
			pieces[i].mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.On; // Reset Shadows
		}

		// Enable root
		root.ProcessMode = ProcessModeEnum.Inherit;

		if (root is RigidBody3D)
		{
			RigidBody3D rb = root as RigidBody3D;
			rb.LinearVelocity = rb.AngularVelocity = Vector3.Zero;
			rb.GravityScale = gravityScale;
		}

		// Wait an extra physics frame for rigidbody to freeze to allow updating transforms
		GetTree().CreateTimer(Core.PhysicsManager.physicsDelta, true, true).Connect(SceneTreeTimer.SignalName.Timeout,
		new Callable(this, MethodName.ResetNodeTransforms));
	}

	private void ResetNodeTransforms()
	{
		// Start with the piece parent disabled
		pieceRoot.Visible = false;
		pieceRoot.ProcessMode = ProcessModeEnum.Disabled;

		// Reset animator
		if (animator != null)
		{
			if (animator.HasAnimation("RESET"))
				animator.Play("RESET");

			// Attempt to queue autoplay animation
			if (!string.IsNullOrEmpty(animator.Autoplay))
				animator.Queue(animator.Autoplay);
		}

		for (int i = 0; i < pieces.Count; i++)
		{
			// Update mesh/collider transforms, due to rigidbody resetting scales
			pieces[i].mesh.Transform = Transform3D.Identity.ScaledLocal(pieces[i].scale);
			pieces[i].collider.Transform = Transform3D.Identity.ScaledLocal(pieces[i].scale);

			pieces[i].rigidbody.GlobalTransform = Transform3D.Identity; // Reset location, rotation and scale
			pieces[i].rigidbody.Position = pieces[i].position; // Use local position
			pieces[i].rigidbody.GlobalRotation = pieceRoot.GlobalRotation; // Sync rotation

			pieces[i].mesh.Transparency = 0f; // Reset fade
		}

		root.Transform = Transform3D.Identity;
	}

	public virtual void Despawn()
	{
		root.Visible = pieceRoot.Visible = false;
		root.ProcessMode = pieceRoot.ProcessMode = ProcessModeEnum.Disabled;
	}

	public virtual void Shatter() // Call this from a signal
	{
		if (isShattered) return;

		// Play particle effects, sfx, etc
		if (animator?.HasAnimation("shatter") == true)
			animator.Play("shatter");

		if (shakeScreenOnShatter)
			Player.Camera.StartMediumCameraShake(root.GlobalPosition);

		pieceRoot.Visible = true; // Make sure piece root is visible
		pieceRoot.ProcessMode = ProcessModeEnum.Inherit;
		pieceRoot.GlobalTransform = root.GlobalTransform; // Copy root transform

		Vector3 shatterPoint = root.GlobalPosition;
		float shatterStrength = ShatterStrength;
		if (isInteractingWithPlayer && !Player.Skills.IsSpeedBreakActive) // Directional shatter
		{
			// Kill character's speed
			if (Player.IsOnGround && stopPlayerOnShatter)
				Player.MoveSpeed = 0f;

			shatterPoint = Player.CenterPosition; // Shatter from player

			if (!Player.IsJumpDashOrHomingAttack)
				shatterStrength *= Mathf.Clamp(Player.Stats.GroundSettings.GetSpeedRatio(Player.MoveSpeed), .5f, 1f);
		}

		tweener = CreateTween().SetParallel(true);
		for (int i = 0; i < pieces.Count; i++)
		{
			pieces[i].rigidbody.Freeze = false;
			pieces[i].rigidbody.AddExplosionForce(shatterPoint, shatterStrength);
			pieces[i].mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; // Particles don't cast shadows when shattering
			tweener.TweenProperty(pieces[i].mesh, "transparency", 1f, 1f).From(0.01f);
		}

		isShattered = true;
		EmitSignal(SignalName.Shattered);
	}

	// Prevent memory leakage
	public void Unload() => pieces.Clear();

	public void OnEntered(Area3D a)
	{
		if (isShattered) return;

		if (!a.IsInGroup("player") && !a.IsInGroup("player detection"))
		{
			if (!a.IsInGroup("ignore destructable") && FlagSetting.HasFlag(ShatterFlags.ObjectCollision))
				Shatter();

			return;
		}

		if (!a.IsInGroup("player"))
			return;

		isInteractingWithPlayer = true;
		ProcessPlayerCollision();
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player"))
			return;

		isInteractingWithPlayer = false;
	}

	protected virtual void ProcessPlayerCollision()
	{
		if (isShattered)
			return;

		// Prioritize Jump Dash
		if (FlagSetting.HasFlag(ShatterFlags.JumpDash) && Player.IsJumpDashOrHomingAttack)
		{
			Shatter();
			if (!Mathf.IsZeroApprox(bounceStrength))
				Player.StartBounce(snapMode, bounceStrength);

			return;
		}

		if (FlagSetting.HasFlag(ShatterFlags.PlayerCollision))
		{
			Shatter();
			return;
		}

		if (FlagSetting.HasFlag(ShatterFlags.AttackSkill) && Player.AttackState != PlayerController.AttackStates.None)
		{
			Shatter();

			if (Player.IsSpinJump)
				Player.StartSpinJumpBounce();

			return;
		}

		if (FlagSetting.HasFlag(ShatterFlags.SpeedBreak) && Player.Skills.IsSpeedBreakActive)
		{
			Shatter();
			return;
		}

		if (!damagePlayer)
			return;

		Player.StartKnockback();
	}

	public void OnBodyEntered(Node3D b)
	{
		if (isShattered) return;

		if (FlagSetting.HasFlag(ShatterFlags.ObjectCollision))
		{
			if (b.IsInGroup("crusher") || b.IsInGroup("enemy"))
				Shatter();
		}

		if (b.IsInGroup("player"))
		{
			ProcessPlayerCollision(); // Check whether we shattered

			if (!isShattered) // Attempt to play animator's "push" animation
			{
				if (animator?.HasAnimation("push") == true)
				{
					// Prevent objects from getting "stuck" on the player
					RigidBody3D rb = root as RigidBody3D;
					float pushPower = Mathf.Clamp(Player.MoveSpeed, 10.0f, 20.0f);
					Vector3 launchPosition = (rb.GlobalPosition + rb.CenterOfMass) - Player.GlobalPosition;
					rb.ApplyImpulse(launchPosition * pushPower);
					animator.Play("push");
				}

				Player.MoveSpeed *= 0.4f; // Kill character's speed
			}
		}
	}
}
