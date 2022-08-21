using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Object that shatters when destroyed. All pieces must be a child of this object.
	/// </summary>
	public class DestructableObject : RespawnableObject
	{
		[Export]
		public NodePath originalMesh; //Unbroken mesh. Leave empty to always show the pieces
		private Spatial _originalMesh;
		[Export]
		public NodePath brokenMesh; //Useful if you have a model that represents the broken object
		private Spatial _brokenMesh;
		[Export]
		public NodePath collider;
		private CollisionShape _collider;
		[Export]
		public Array<Material> overrideMaterials = new Array<Material>();
		private readonly Array<Material> materialList = new Array<Material>();

		private SceneTreeTween tween;

		[Export]
		public ShatterType shatterType;
		public enum ShatterType
		{
			OnSignal,
			OnTouch,
			OnAttack
		}
		private bool isShattered;
		[Signal]
		public delegate void Shattered();

		protected override bool IsRespawnable() => true;

		private readonly Array<RigidBody> _pieces = new Array<RigidBody>();
		private readonly Array<Transform> _piecesSpawnTransforms = new Array<Transform>();
		private const float EXPLOSION_FORCE = 10f;

		protected override void SetUp()
		{
			base.SetUp();

			if (originalMesh != null)
				_originalMesh = GetNodeOrNull<Spatial>(originalMesh);

			if (brokenMesh != null)
				_brokenMesh = GetNodeOrNull<Spatial>(brokenMesh);

			if (collider != null)
				_collider = GetNodeOrNull<CollisionShape>(collider);

			//Clone materials so multiple destructable objects of the same type can animate independantly
			for (int i = 0; i < overrideMaterials.Count; i++)
				materialList.Add(overrideMaterials[i].Duplicate() as Material);

			for (int i = 0; i < GetChildCount(); i++)
			{
				RigidBody rigidbody = GetChildOrNull<RigidBody>(i);
				if (rigidbody == null) //Pieces must be a rigidbody
					continue;

				_pieces.Add(rigidbody);
				_piecesSpawnTransforms.Add(rigidbody.Transform);

				if (materialList.Count != 0) //Assign override material
				{
					MeshInstance mesh = _pieces[i].GetChildOrNull<MeshInstance>(0); //NOTE mesh must be the FIRST child of the rigidbody.
					if (mesh != null)
					{
						for (int j = 0; j < materialList.Count; j++)
						{
							if (j >= mesh.GetSurfaceMaterialCount()) break;
							mesh.SetSurfaceMaterial(j, materialList[j]);
						}
					}
				}
			}

			Respawn();
		}

		public override void Respawn()
		{
			base.Respawn();

			if (tween != null)
				tween.Stop();

			isShattered = false;

			if (_collider != null)
				_collider.Disabled = false;
			if (_originalMesh != null)
				_originalMesh.Visible = true;
			if (_brokenMesh != null)
				_brokenMesh.Visible = false;

			if (_originalMesh != null)
				DisablePieces();
			else
				EnablePieces();
		}

		public override void Despawn()
		{
			if (!isShattered) return; //Just in case the player was respawned while the object was still shattering

			base.Despawn();
		}

		private void DisablePieces()
		{
			for (int i = 0; i < _pieces.Count; i++)
			{
				if (_pieces[i].IsInsideTree())
					RemoveChild(_pieces[i]);
			}
		}

		private void EnablePieces()
		{
			for (int i = 0; i < _pieces.Count; i++)
			{
				if (!_pieces[i].IsInsideTree())
					AddChild(_pieces[i]);

				_pieces[i].Transform = _piecesSpawnTransforms[i];
				_pieces[i].LinearVelocity = _pieces[i].AngularVelocity = Vector3.Zero; //Reset velocity
			}
		}

		public void Shatter()
		{
			if (isShattered) return;

			if (_collider != null)
				_collider.Disabled = true;

			if (_originalMesh != null)
			{
				_originalMesh.Visible = false;
				EnablePieces();
			}

			if (_brokenMesh != null)
				_brokenMesh.Visible = true;

			for (int i = 0; i < _pieces.Count; i++)
			{
				_pieces[i].AddExplosionForce(GlobalTranslation, EXPLOSION_FORCE);
				_pieces[i].Sleeping = false;
			}

			tween = CreateTween().SetParallel(true);
			for (int i = 0; i < materialList.Count; i++)
			{
				materialList[i].Set("albedo_color", Colors.White);
				tween.TweenProperty(materialList[i], "albedo_color", Colors.Transparent, 1f);
			}
			tween.TweenCallback(this, nameof(Despawn)).SetDelay(3f); //Despawn this object

			isShattered = true;
			EmitSignal(nameof(Shattered));
		}

		public override void Unload()
		{
			//Prevent memory leakage
			for (int i = 0; i < _pieces.Count; i++)
			{
				if(_pieces[i].GetParent() != this)
					_pieces[i].QueueFree();
			}

			for (int i = 0; i < overrideMaterials.Count; i++)
				materialList[i].Dispose();

			_pieces.Clear();
			_piecesSpawnTransforms.Clear();
			materialList.Clear();
			overrideMaterials.Clear();
		}

		public void OnBodyEntered(PhysicsBody body)
		{
			if (body.IsInGroup("crusher"))
				Shatter();
		}

		private void OnEntered(Area a)
		{
			if (shatterType == ShatterType.OnSignal) return; //Don't process

			if (a.IsInGroup("player"))
			{
				if (CharacterController.instance.IsAttacking)
				{
					//if(health <= 0)
					//Shatter(a.GlobalTranslation);
				}
			}
			else
				Shatter(); //Must be an enemy or something
		}
	}
}
