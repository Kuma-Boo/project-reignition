using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Object that shatters when destroyed. All pieces must be a child of this object.
	/// </summary>
	public partial class DestructableObject : Node3D
	{
		[Export]
		public NodePath originalMesh; //Unbroken mesh. Leave empty to always show the pieces
		private Node3D _originalMesh;
		[Export]
		public NodePath brokenMesh; //Useful if you have a model that represents the broken object
		private Node3D _brokenMesh;
		[Export]
		public NodePath collider;
		private CollisionShape3D _collider;
		[Export]
		public Array<Material> overrideMaterials;
		private readonly Array<Material> materialList = new Array<Material>();

		private Tween tween;

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
		public delegate void ShatteredEventHandler();

		private StageSettings.SpawnData spawnData;
		private readonly Array<RigidBody3D> _pieces = new Array<RigidBody3D>();
		private readonly Array<Transform3D> _piecesSpawnTransforms = new Array<Transform3D>();
		private const float EXPLOSION_FORCE = 10f;

		public override void _Ready()
		{
			spawnData = new StageSettings.SpawnData(GetParent(), Transform);
			StageSettings.instance.RegisterRespawnableObject(this);

			if (originalMesh != null)
				_originalMesh = GetNodeOrNull<Node3D>(originalMesh);

			if (brokenMesh != null)
				_brokenMesh = GetNodeOrNull<Node3D>(brokenMesh);

			if (collider != null)
				_collider = GetNodeOrNull<CollisionShape3D>(collider);

			//Clone materials so multiple destructable objects of the same type can animate independantly
			for (int i = 0; i < overrideMaterials.Count; i++)
				materialList.Add(overrideMaterials[i].Duplicate() as Material);

			for (int i = 0; i < GetChildCount(); i++)
			{
				RigidBody3D rigidbody = GetChildOrNull<RigidBody3D>(i);
				if (rigidbody == null) //Pieces must be a rigidbody
					continue;

				_pieces.Add(rigidbody);
				_piecesSpawnTransforms.Add(rigidbody.Transform);

				if (materialList.Count != 0) //Assign override material
				{
					MeshInstance3D mesh = _pieces[i].GetChildOrNull<MeshInstance3D>(0); //NOTE mesh must be the FIRST child of the rigidbody.
					if (mesh != null)
					{
						for (int j = 0; j < materialList.Count; j++)
						{
							if (j >= mesh.GetSurfaceOverrideMaterialCount()) break;
							mesh.SetSurfaceOverrideMaterial(j, materialList[j]);
						}
					}
				}
			}

			Respawn();
		}

		public void Respawn()
		{
			if (!IsInsideTree() && GetParent() != spawnData.parentNode)
				spawnData.parentNode.AddChild(this);
			Transform = spawnData.spawnTransform;

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

		public void Despawn()
		{
			if (!IsInsideTree() || !isShattered) return; //Just in case the player was respawned while the object was still shattering
			GetParent().CallDeferred("remove_child", this);
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
				_pieces[i].AddExplosionForce(GlobalPosition, EXPLOSION_FORCE);
				_pieces[i].Sleeping = false;
			}

			tween = CreateTween().SetParallel(true);
			for (int i = 0; i < materialList.Count; i++)
			{
				materialList[i].Set("albedo_color", Colors.White);
				tween.TweenProperty(materialList[i], "albedo_color", Colors.Transparent, 1f);
			}
			tween.TweenCallback(new Callable(this, MethodName.Despawn)).SetDelay(3f); //Despawn this object

			isShattered = true;
			EmitSignal(SignalName.Shattered);
		}

		public override void _ExitTree()
		{
			//Prevent memory leakage
			for (int i = 0; i < _pieces.Count; i++)
			{
				if (_pieces[i].GetParent() != this)
					_pieces[i].QueueFree();
			}

			for (int i = 0; i < overrideMaterials.Count; i++)
				materialList[i].Dispose();

			_pieces.Clear();
			_piecesSpawnTransforms.Clear();
			materialList.Clear();
			overrideMaterials.Clear();
		}

		public void OnBodyEntered(PhysicsBody3D body)
		{
			if (body.IsInGroup("crusher"))
				Shatter();
		}

		private void OnEntered(Area3D a)
		{
			if (shatterType == ShatterType.OnSignal) return; //Don't process

			if (a.IsInGroup("player"))
			{
				if (CharacterController.instance.IsAttacking)
				{
					//if(health <= 0)
					//Shatter(a.GlobalPosition);
				}
			}
			else
				Shatter(); //Must be an enemy or something
		}
	}
}
