using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Object that shatters when destroyed
	/// </summary>
	public class DestructableObject : RespawnableObject
	{
		[Export]
		public NodePath originalMesh; //Unbroken mesh
		private Spatial _originalMesh;
		[Export]
		public NodePath pieceParent;
		private Spatial _pieceParent;
		[Export]
		public NodePath collider;
		private CollisionShape _collider;

		private RigidBody rb;
		private bool IsRigidbody => rb != null;

		[Export]
		public Material overrideMaterial;
		private bool UseOverrideMaterial => overrideMaterial != null;

		private readonly Array<RigidBody> _pieces = new Array<RigidBody>();
		private readonly Array<Transform> _piecesOriginTransforms = new Array<Transform>();

		[Export]
		public ShatterType shatterType;
		public enum ShatterType
		{
			Disabled,
			OnTouch,
			OnAttack
		}
		[Export]
		public int maxHealth; //How much health does this object have?
		private int health;
		
		[Signal]
		public delegate void Shattered();

		[Export]
		public float explosionForce;

		private bool wasShattered;
		protected override bool IsRespawnable() => true;

		protected override void SetUp()
		{
			_originalMesh = GetNode<Spatial>(originalMesh);
			_pieceParent = GetNode<Spatial>(pieceParent);
			_collider = GetNode<CollisionShape>(collider);

			if (UseOverrideMaterial)
				overrideMaterial = overrideMaterial.Duplicate() as Material;

			for (int i = 0; i < _pieceParent.GetChildCount(); i++)
			{
				RigidBody rigidbody = _pieceParent.GetChildOrNull<RigidBody>(i);
				if (rigidbody == null)
					continue;

				_pieces.Add(rigidbody);
				_piecesOriginTransforms.Add(rigidbody.Transform);

				//Initialize materials
				if (!UseOverrideMaterial) continue;

				MeshInstance mesh = _pieces[i].GetChildOrNull<MeshInstance>(0); //Note mesh must be the FIRST child of the rigidbody.
				if (mesh == null) continue;

				mesh.MaterialOverride = overrideMaterial;
			}

			if (((Node)this) is RigidBody)
				rb = ((Node)this) as RigidBody;

			health = maxHealth;
			base.SetUp();
		}

		public override void Respawn()
		{
			base.Respawn();

			health = maxHealth;
			wasShattered = false;
			_originalMesh.Visible = true;

			if (IsRigidbody)
			{
				rb.AngularVelocity = rb.LinearVelocity = Vector3.Zero;
				rb.GravityScale = 1f;
				rb.Sleeping = true;

				foreach (Node e in rb.GetCollisionExceptions())
					rb.RemoveCollisionExceptionWith(e);
			}

			for (int i = 0; i < _pieces.Count; i++)
			{
				_pieces[i].Transform = _piecesOriginTransforms[i];
				_pieces[i].LinearVelocity = _pieces[i].AngularVelocity = Vector3.Zero;
				_pieces[i].Sleeping = true;
			}

			DisablePieces();
		}

		private void DisablePieces()
		{
			//Disable the pieces
			if (!_pieceParent.IsInsideTree()) return;
			_pieceParent.GetParent().RemoveChild(_pieceParent);
		}

		public virtual void Shatter(Vector3 fromPoint)
		{
			if (wasShattered) return;

			wasShattered = true;
			_collider.Disabled = true;
			_originalMesh.Visible = false;

			GetParent().AddChild(_pieceParent);
			_pieceParent.GlobalTransform = GlobalTransform;
			_pieceParent.Visible = true;
			for (int i = 0; i < _pieces.Count; i++)
			{
				_pieces[i].AddExplosionForce(fromPoint, explosionForce);
				_pieces[i].Sleeping = false;
			}

			SceneTreeTween tween = CreateTween();
			if (UseOverrideMaterial)
			{
				overrideMaterial.Set("albedo_color", Colors.White);
				tween.TweenProperty(overrideMaterial, "albedo_color", Colors.Transparent, 2f).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
			}
			tween.TweenCallback(this, nameof(DisablePieces)).SetDelay(3f);

			EmitSignal(nameof(Shattered));
		}

		public override void _ExitTree()
		{
			//Avoid memory leak
			_pieceParent.QueueFree();
		}

		private void OnEntered(Area a)
		{
			if(a.IsInGroup("player"))
			{
				if (CharacterController.instance.IsAttacking)
					health--;
			}
			
			if(health <= 0)
				Shatter(a.GlobalTranslation);
		}
	}
}
