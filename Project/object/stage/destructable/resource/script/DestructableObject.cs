using Godot;
using System.Collections.Generic;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Object that shatters when destroyed. All pieces must be a child of pieceRoot.
	/// </summary>
	public partial class DestructableObject : Node3D
	{
		private Tween tweener;

		[Export]
		private float explosionStrength;
		[Export]
		private float pieceMass;
		[Export]
		/// <summary> Stop the player when being shattered? </summary>
		private bool stopPlayerOnShatter;

		[Export]
		/// <summary> Don't collide with the environment? </summary>
		private bool disableEnvironmentCollision;
		[Export]
		/// <summary> Don't automatically respawn this object. Call Respawn() manually instead. </summary>
		private bool disableRespawn;

		[Export]
		/// <summary> Unshattered model. </summary>
		private Node3D root;
		[Export]
		/// <summary> Parent node of all the pieces. </summary>
		private Node3D pieceRoot;
		[Export]
		protected AnimationPlayer animator;
		[Export(PropertyHint.Flags, "PlayerCollision,ObjectCollision,AttackSkill,HomingAttack,SpeedBreak")]
		private int shatterFlags;
		private enum ShatterFlags
		{
			Signal = 0, //Don't listen to area collider
			PlayerCollision = 1, //Break immediately when touching
			ObjectCollision = 2, //Should objects be able to shatter this?
			AttackSkill = 4, //Break when player is using an attack skill
			HomingAttack = 8, //Break when player is homing attacking. Must be enabled even if AttackSkill is active.
			SpeedBreak = 16, //Break when speedbreak is active. Must be enabled even if AttackSkill is active.
		}

		private ShatterFlags FlagSetting => (ShatterFlags)shatterFlags;
		private CharacterController Character => CharacterController.instance;

		protected bool isShattered;
		protected bool isInteractingWithPlayer;
		[Signal]
		public delegate void ShatteredEventHandler();

		private readonly List<Piece> pieces = new List<Piece>();

		private class Piece
		{
			public RigidBody3D rigidbody;
			public MeshInstance3D mesh;
			public CollisionShape3D collider;
			public Vector3 scale;
			public Vector3 position; //Local transform to spawn with
		}

		public override void _Ready()
		{
			for (int i = 0; i < pieceRoot.GetChildCount(); i++)
			{
				RigidBody3D rigidbody = pieceRoot.GetChildOrNull<RigidBody3D>(i);
				if (rigidbody == null) //Pieces must be a rigidbody
					continue;

				MeshInstance3D mesh = rigidbody.GetChildOrNull<MeshInstance3D>(0); //mesh must be the FIRST child of the rigidbody.
				CollisionShape3D collider = rigidbody.GetChildOrNull<CollisionShape3D>(1); //collider must be the SECOND child of the rigidbody.

				rigidbody.Mass = pieceMass;
				rigidbody.CollisionLayer = Core.Runtime.Instance.particleCollisionLayer;
				rigidbody.CollisionMask = Core.Runtime.Instance.particleCollisionMask;

				if (disableEnvironmentCollision)
					rigidbody.CollisionMask &= ~Core.Runtime.Instance.environmentMask;

				pieces.Add(new Piece()
				{
					rigidbody = rigidbody,
					mesh = mesh,
					collider = collider,
					scale = pieceRoot.GlobalTransform.Basis.Scale,
					position = rigidbody.Position
				});
			}

			Respawn();

			if (!disableRespawn)
				LevelSettings.instance.ConnectRespawnSignal(this);

			LevelSettings.instance.ConnectUnloadSignal(this);
		}

		public override void _PhysicsProcess(double _)
		{
			/*
			if (isShattered && pieceRoot.Visible)
			{
				for (int i = 0; i < pieces.Count; i++) //Use this to visualize piece tradjectory
					Core.Debug.DrawRay(pieces[i].rigidbody.GlobalPosition, pieces[i].rigidbody.LinearVelocity * Core.PhysicsManager.physicsDelta, Colors.Red);
			}
			*/

			if (isShattered || !isInteractingWithPlayer) return;

			ProcessPlayerCollision();
		}

		public virtual void Respawn()
		{
			isShattered = false;

			if (tweener != null)
				tweener.Kill();

			//Reset Pieces
			for (int i = 0; i < pieces.Count; i++)
			{
				pieces[i].rigidbody.Freeze = true;
				pieces[i].rigidbody.LinearVelocity = pieces[i].rigidbody.AngularVelocity = Vector3.Zero; //Reset velocity
				pieces[i].mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.On; //Reset Shadows
			}


			//Enable root
			root.ProcessMode = ProcessModeEnum.Inherit;

			if (root is RigidBody3D)
			{
				RigidBody3D rb = root as RigidBody3D;
				rb.LinearVelocity = rb.AngularVelocity = Vector3.Zero;
			}

			//Wait an extra physics frame for rigidbody to freeze to allow updating transforms
			GetTree().CreateTimer(Core.PhysicsManager.physicsDelta, true, true).Connect(SceneTreeTimer.SignalName.Timeout,
			new Callable(this, MethodName.ResetNodeTransforms));
		}

		private void ResetNodeTransforms()
		{
			//Start with the piece parent disabled
			pieceRoot.Visible = false;
			pieceRoot.ProcessMode = ProcessModeEnum.Disabled;

			//Reset animator
			if (animator != null && animator.HasAnimation("RESET"))
				animator.Play("RESET");

			for (int i = 0; i < pieces.Count; i++)
			{
				//Update mesh/collider transforms, due to rigidbody resetting scales
				pieces[i].mesh.Transform = Transform3D.Identity.ScaledLocal(pieces[i].scale);
				pieces[i].collider.Transform = Transform3D.Identity.ScaledLocal(pieces[i].scale);

				pieces[i].rigidbody.GlobalTransform = Transform3D.Identity; //Reset location, rotation and scale
				pieces[i].rigidbody.Position = pieces[i].position; //Use local position
				pieces[i].rigidbody.GlobalRotation = pieceRoot.GlobalRotation; //Sync rotation

				pieces[i].mesh.Transparency = 0f; //Reset fade
			}

			root.Transform = Transform3D.Identity;
		}

		public virtual void Despawn()
		{
			pieceRoot.Visible = true;
			root.ProcessMode = ProcessModeEnum.Disabled;

			pieceRoot.Visible = true;
			pieceRoot.ProcessMode = ProcessModeEnum.Disabled;
		}

		public virtual void Shatter() //Call this from a signal
		{
			if (isShattered) return;

			//Play particle effects, sfx, etc
			if (animator != null && animator.HasAnimation("shatter"))
				animator.Play("shatter");

			pieceRoot.Visible = true; //Make sure piece root is visible
			pieceRoot.ProcessMode = ProcessModeEnum.Inherit;

			Vector3 shatterPoint = root.GlobalPosition;
			float shatterStrength = explosionStrength;
			if (isInteractingWithPlayer && !Character.Skills.IsSpeedBreakActive) //Directional shatter
			{
				//Kill character's speed
				if (Character.IsOnGround && stopPlayerOnShatter)
					Character.MoveSpeed = 0f;

				shatterPoint = Character.CenterPosition; //Shatter from player

				if (Character.ActionState != CharacterController.ActionStates.JumpDash)
					shatterStrength *= Mathf.Clamp(Character.GroundSettings.GetSpeedRatio(Character.MoveSpeed), .5f, 1f);
			}

			tweener = CreateTween().SetParallel(true);
			for (int i = 0; i < pieces.Count; i++)
			{
				pieces[i].rigidbody.Freeze = false;
				pieces[i].rigidbody.AddExplosionForce(shatterPoint, shatterStrength);
				pieces[i].mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; //Particles don't cast shadows when shattering
				tweener.TweenProperty(pieces[i].mesh, "transparency", 1f, 1f).From(0f);
			}

			isShattered = true;
			EmitSignal(SignalName.Shattered);
		}

		//Prevent memory leakage
		public void Unload() => pieces.Clear();

		public void OnEntered(Area3D a)
		{
			if (isShattered) return;

			if (!a.IsInGroup("player") && !a.IsInGroup("stackable"))
			{
				if (FlagSetting.HasFlag(ShatterFlags.ObjectCollision))
					Shatter();

				return;
			}

			isInteractingWithPlayer = true;
		}

		public void OnExited(Area3D a)
		{
			if (a.IsInGroup("player"))
				isInteractingWithPlayer = false;
		}

		private void ProcessPlayerCollision()
		{
			if (FlagSetting.HasFlag(ShatterFlags.PlayerCollision))
				Shatter();
			else if (FlagSetting.HasFlag(ShatterFlags.AttackSkill) && Character.Skills.IsAttacking)
				Shatter();
			else if (FlagSetting.HasFlag(ShatterFlags.HomingAttack) && Character.Lockon.IsHomingAttacking)
			{
				Shatter();
				Character.Lockon.StartBounce();
			}
			else if (FlagSetting.HasFlag(ShatterFlags.SpeedBreak) && Character.Skills.IsSpeedBreakActive)
				Shatter();
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
				ProcessPlayerCollision(); //Check whether we shattered

				if (!isShattered) //Attempt to play animator's "push" animation
				{
					if (animator != null && animator.HasAnimation("push"))
					{
						//Prevent objects from getting "stuck" on the player
						RigidBody3D rb = root as RigidBody3D;
						float pushPower = Mathf.Clamp(Character.MoveSpeed, 10.0f, 20.0f);
						Vector3 launchPosition = (rb.GlobalPosition + rb.CenterOfMass) - Character.GlobalPosition;
						rb.ApplyImpulse(launchPosition * pushPower);
						animator.Play("push");
					}

					Character.MoveSpeed *= 0.4f; //Kill character's speed
				}
			}
		}
	}
}
