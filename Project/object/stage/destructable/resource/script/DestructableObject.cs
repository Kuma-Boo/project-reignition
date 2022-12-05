using Godot;
using System.Collections.Generic;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Object that shatters when destroyed. All pieces must be a child of this object.
	/// </summary>
	public partial class DestructableObject : Node3D
	{
		private Tween tween;

		[Export]
		private float explosionStrength;
		[Export]
		private float pieceMass;

		[Export]
		private Node3D root;
		[Export]
		private Node3D pieceRoot; //Parent node of pieces
		[Export]
		protected AnimationPlayer animator;
		[Export(PropertyHint.Layers3dPhysics)]
		private uint collisionLayer;
		[Export(PropertyHint.Layers3dPhysics)]
		private uint collisionMask;
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
			public Transform3D spawnTransform; //Local transform to spawn with
		}

		public override void _Ready()
		{
			for (int i = 0; i < pieceRoot.GetChildCount(); i++)
			{
				RigidBody3D rigidbody = pieceRoot.GetChildOrNull<RigidBody3D>(i);
				if (rigidbody == null) //Pieces must be a rigidbody
					continue;

				MeshInstance3D mesh = rigidbody.GetChildOrNull<MeshInstance3D>(0); //NOTE mesh must be the FIRST child of the rigidbody.

				rigidbody.Mass = pieceMass;
				rigidbody.CollisionLayer = collisionLayer;
				rigidbody.CollisionMask = collisionMask;

				pieces.Add(new Piece()
				{
					rigidbody = rigidbody,
					mesh = mesh,
					spawnTransform = rigidbody.Transform
				});
			}

			Respawn();
		}

		public override void _PhysicsProcess(double _)
		{
			if (isShattered && pieceRoot.IsInsideTree())
			{
				for (int i = 0; i < pieces.Count; i++)
					Core.Debug.DrawRay(pieces[i].rigidbody.GlobalPosition, pieces[i].rigidbody.LinearVelocity * Core.PhysicsManager.physicsDelta, Colors.Red);
			}

			if (isShattered || !isInteractingWithPlayer) return;

			ProcessPlayerCollision();
		}

		public virtual void Respawn()
		{
			//Reset animator
			if (animator != null && animator.HasAnimation("RESET"))
				animator.Play("RESET");

			if (tween != null)
				tween.Kill();

			isShattered = false;

			//Reset Pieces
			for (int i = 0; i < pieces.Count; i++)
			{
				pieces[i].rigidbody.SetDeferred("transform", pieces[i].spawnTransform);
				pieces[i].rigidbody.Freeze = true;
				pieces[i].rigidbody.LinearVelocity = pieces[i].rigidbody.AngularVelocity = Vector3.Zero; //Reset velocity
				pieces[i].mesh.Transparency = 0f; //Reset fade
				pieces[i].mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.On; //Reset Shadows
			}

			if (pieceRoot.IsInsideTree())
				pieceRoot.GetParent().CallDeferred(MethodName.RemoveChild, pieceRoot); //Start with the piece parent despawned

			if (!root.IsInsideTree())
			{
				AddChild(root);
				root.Transform = Transform3D.Identity;
			}
		}

		public virtual void Despawn()
		{
			root.GetParent().CallDeferred(MethodName.RemoveChild, root);
			pieceRoot.GetParent().CallDeferred(MethodName.RemoveChild, pieceRoot);
		}

		public virtual void Shatter() //Call this from a signal
		{
			if (isShattered) return;

			//Play particle effects, sfx, etc
			if (animator != null && animator.HasAnimation("shatter"))
				animator.Play("shatter");

			AddChild(pieceRoot);
			pieceRoot.Visible = true;
			pieceRoot.GlobalTransform = root.GlobalTransform;
			Vector3 shatterPoint = root.GlobalPosition;
			float shatterStrength = explosionStrength;
			if (isInteractingWithPlayer && !Character.Skills.IsSpeedBreakActive) //Directional shatter
			{
				shatterPoint = Character.CenterPosition; //Shatter from player

				if (Character.ActionState != CharacterController.ActionStates.JumpDash)
					shatterStrength *= Mathf.Clamp(Character.GroundSettings.GetSpeedRatio(Character.MoveSpeed), .5f, 1f);
			}

			tween = CreateTween().SetParallel(true);
			for (int i = 0; i < pieces.Count; i++)
			{
				pieces[i].rigidbody.Freeze = false;
				pieces[i].rigidbody.AddExplosionForce(shatterPoint, shatterStrength);
				pieces[i].mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; //Particles don't cast shadows when shattering
				tween.TweenProperty(pieces[i].mesh, "transparency", 1f, 1f);
			}

			isShattered = true;
			EmitSignal(SignalName.Shattered);
		}

		public override void _ExitTree() //Prevent memory leakage
		{
			root.QueueFree();
			pieceRoot.QueueFree();
			pieces.Clear();
		}

		public void OnEntered(Area3D a)
		{
			if (isShattered) return;

			if (!a.IsInGroup("player"))
			{
				if (FlagSetting.IsSet(ShatterFlags.ObjectCollision))
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
			if (FlagSetting.IsSet(ShatterFlags.PlayerCollision))
				Shatter();
			else if (FlagSetting.IsSet(ShatterFlags.AttackSkill) && Character.Skills.IsAttacking)
				Shatter();
			else if (FlagSetting.IsSet(ShatterFlags.HomingAttack) && Character.ActionState == CharacterController.ActionStates.JumpDash)
			{
				if (Character.Lockon.IsHomingAttacking)
					Character.Lockon.StartBounce();
				Shatter();
			}
			else if (FlagSetting.IsSet(ShatterFlags.SpeedBreak) && Character.Skills.IsSpeedBreakActive)
				Shatter();
		}

		public void OnBodyEntered(Node3D b)
		{
			if (isShattered) return;

			if (FlagSetting.IsSet(ShatterFlags.ObjectCollision))
			{
				if (b.IsInGroup("crusher") || b.IsInGroup("enemy"))
					Shatter();
			}

			if (b.IsInGroup("player"))
			{
				ProcessPlayerCollision(); //Check whether we shattered

				if (!isShattered) //Attempt to play animator's "push" animation
				{
					Character.MoveSpeed *= 0.5f; //Kill character's speed

					if (animator != null && animator.HasAnimation("push"))
						animator.Play("push");
				}
			}
		}
	}
}
