using Godot;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary> Flower Majin that spits out seeds to attack the player. </summary>
	public partial class FlowerMajin : Enemy
	{
		[ExportGroup("Enemy Settings")]
		[Export]
		private bool attackInstantly; //Skip passive phase when activated?
		[Export]
		private float passiveLength; //How long to remain passive
		[Export]
		private float preAttackLength; //How long to wait before firing seeds after turning aggressive
		[Export]
		private float postAttackLength; //How long to wait after firing seeds before turning passive

		[ExportGroup("Animation Settings")]
		[Export]
		private bool isOpen; //Can the flower be damaged?
		[Export]
		private bool isInHitstun; //Is this enemy being clobbered by a homing attack?
		[Export]
		private PackedScene seed;
		private int seedIndex;
		private const int MAX_SEED_COUNT = 3;
		private readonly Seed[] seedPool = new Seed[MAX_SEED_COUNT]; //Only three seeds can be spawned at a time.

		protected override void SetUp()
		{
			LevelSettings.instance.ConnectUnloadSignal(this);

			for (int i = 0; i < MAX_SEED_COUNT; i++)
			{
				seedPool[i] = seed.Instantiate<Seed>();
			}

			base.SetUp();
		}

		public override void Unload()
		{
			for (int i = 0; i < seedPool.Length; i++) //Clear memory
				seedPool[i].QueueFree();

			base.Unload();
		}

		protected override void Spawn()
		{
			if (attackInstantly && currentState == States.Passive) //Skip passive phase
				stateTimer = passiveLength;

			base.Spawn();
		}

		public override void Respawn()
		{
			base.Respawn();

			animationPlayer.Play("spawn");
			animationPlayer.Seek(0.0, true);

			stateTimer = 0;
			currentState = States.Passive;

			rotationVelocity = 0;

			seedIndex = 0;
			for (int i = 0; i < MAX_SEED_COUNT; i++)
			{
				if (seedPool[i].IsInsideTree())
					seedPool[i].GetParent().CallDeferred(MethodName.RemoveChild, seedPool[i]);
			}
		}

		protected override void UpdateInteraction()
		{
			if (!isOpen)
			{
				if (Character.ActionState == CharacterController.ActionStates.JumpDash)
				{
					Character.Lockon.StartBounce();
					Character.Camera.LockonTarget = this;
				}
			}
			else
				base.UpdateInteraction();
		}

		public override void TakeDamage()
		{
			base.TakeDamage();

			if (IsDefeated)
			{
				animationPlayer.Play("defeat");
				return;
			}

			animationPlayer.Play("hit");
			if (currentState == States.Attack)
				currentState = States.PostAttack;
		}

		protected override void UpdateEnemy()
		{
			if (IsDefeated) return;

			if (IsInRange || currentState != States.Passive)
			{
				UpdateRotation();
				UpdateState();
			}
		}

		private void UpdateRotation()
		{
			if (isOpen)
			{
				//Rotate towards the player
				TrackPlayer();

				//Update movement
			}

			root.Rotation = new Vector3(root.Rotation.X, currentRotation, root.Rotation.Z);
		}

		private float stateTimer;
		private States currentState;
		private enum States
		{
			Passive,
			PreAttack,
			Attack,
			PostAttack
		}
		private void UpdateState()
		{
			if (isInHitstun || currentState == States.Attack) return;

			stateTimer += PhysicsManager.physicsDelta;
			switch (currentState)
			{
				case States.Passive:
					if (stateTimer >= passiveLength)
					{
						animationPlayer.Play("show");

						stateTimer = 0;
						currentState = States.PreAttack;
					}
					break;
				case States.PreAttack:
					if (stateTimer >= preAttackLength)
					{
						if (IsInRange)
						{
							animationPlayer.Play("attack");
							currentState = States.Attack;
						}
						else //Player is out of range
						{
							animationPlayer.Play("hide");
							currentState = States.Passive;
						}

						stateTimer = 0;
					}
					break;
				case States.PostAttack:
					if (stateTimer >= postAttackLength)
					{
						animationPlayer.Play("hide");
						currentState = States.Passive;
						stateTimer = 0;
					}
					break;
			}
		}

		public void IncrementAttack()
		{
			if (seedIndex >= MAX_SEED_COUNT)
			{
				seedIndex = 0;
				stateTimer = 0;
				currentState = States.PostAttack;
			}
			else
			{
				animationPlayer.Play("attack");
				animationPlayer.Seek(0.0, true);
			}
		}

		public void FireAttack()
		{
			if (!seedPool[seedIndex].IsInsideTree()) //Add to tree
				GetTree().Root.AddChild(seedPool[seedIndex]);

			Vector3 shotDirection = hurtbox.GlobalPosition - Character.CenterPosition;
			shotDirection -= Vector3.Up * .4f; //Aim slightly higher so seeds actually hit the player instead of the ground
			seedPool[seedIndex].LookAtFromPosition(hurtbox.GlobalPosition, hurtbox.GlobalPosition + shotDirection, Vector3.Up);
			seedPool[seedIndex].Spawn();

			seedIndex++; //Increment counter
		}
	}
}
