using Godot;
using Godot.Collections;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Object that shatters when destroyed. All pieces must be a child of this object.
	/// </summary>
	public partial class DestructableObject : Node3D
	{
		private Tween tween;

		[Export(PropertyHint.Layers3dPhysics)]
		public uint collisionLayer;
		[Export(PropertyHint.Layers3dPhysics)]
		public uint collisionMask;

		private bool isShattered;
		[Signal]
		public delegate void ShatteredEventHandler();

		private readonly Array<RigidBody3D> _pieces = new Array<RigidBody3D>();
		private readonly Array<MeshInstance3D> _meshes = new Array<MeshInstance3D>();
		private readonly Array<Transform3D> _spawnTransforms = new Array<Transform3D>();
		private const float EXPLOSION_FORCE = 100f;

		public override void _Ready()
		{
			for (int i = 0; i < GetChildCount(); i++)
			{
				RigidBody3D rigidbody = GetChildOrNull<RigidBody3D>(i);
				if (rigidbody == null) //Pieces must be a rigidbody
					continue;

				rigidbody.CollisionLayer = collisionLayer;
				rigidbody.CollisionMask = collisionMask;

				_pieces.Add(rigidbody);
				_spawnTransforms.Add(rigidbody.Transform);

				MeshInstance3D mesh = rigidbody.GetChildOrNull<MeshInstance3D>(0); //NOTE mesh must be the FIRST child of the rigidbody.
				if (mesh != null)
					_meshes.Add(mesh);
			}

			Respawn();
		}

		public void Respawn()
		{
			if (tween != null)
				tween.Kill();

			isShattered = false;

			//Respawn Pieces
			for (int i = 0; i < _pieces.Count; i++)
			{
				_pieces[i].SetDeferred("transform", _spawnTransforms[i]);
				_pieces[i].Freeze = true;
				_pieces[i].LinearVelocity = _pieces[i].AngularVelocity = Vector3.Zero; //Reset velocity
			}

			for (int i = 0; i < _meshes.Count; i++)
			{
				_meshes[i].Transparency = 0f; //Reset fade
				_meshes[i].CastShadow = GeometryInstance3D.ShadowCastingSetting.On; //Reset Shadows
			}
		}

		public void Shatter() //Call this from a signal
		{
			if (isShattered) return;

			for (int i = 0; i < _pieces.Count; i++)
			{
				_pieces[i].Freeze = false;
				_pieces[i].AddExplosionForce(GlobalPosition, EXPLOSION_FORCE);
			}

			tween = CreateTween().SetParallel(true);
			for (int i = 0; i < _meshes.Count; i++)
			{
				tween.TweenProperty(_meshes[i], "transparency", 1f, 1f);
				_meshes[i].CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; //Particles don't cast shadows when shattering
			}

			isShattered = true;
			EmitSignal(SignalName.Shattered);
		}

		public override void _ExitTree() //Prevent memory leakage
		{
			for (int i = 0; i < _pieces.Count; i++)
			{
				if (_pieces[i].GetParent() != this)
					_pieces[i].QueueFree();
			}

			_pieces.Clear();
			_meshes.Clear();
			_spawnTransforms.Clear();
		}
	}
}
