using Godot;
using Godot.Collections;

namespace Project.Gameplay
{
	/// <summary>
	/// Static stage settings. Unlike LevelSettings, this data gets shared between all levels that use the same Static area.
	/// </summary>
	public partial class StageSettings : Node3D
	{
		public static StageSettings instance;

		[Export]
		public Node3D pathParent;
		/// <summary> List of all level paths contained for this level. </summary>
		private readonly Array<Path3D> pathList = new Array<Path3D>();

		/// <summary>
		/// Returns the path the player is currently the closest to.
		/// Allows placing the player anywhere in the editor without needing to manually assign paths.
		/// </summary>
		public Path3D CalculateStartingPath(Vector3 globalPosition)
		{
			int closestPathIndex = -1;
			float closestDistanceSquared = Mathf.Inf;

			for (int i = 0; i < pathList.Count; i++)
			{
				Vector3 closestPoint = pathList[i].Curve.GetClosestPoint(globalPosition - pathList[i].GlobalPosition);
				closestPoint += pathList[i].GlobalPosition;
				float dstSquared = globalPosition.DistanceSquaredTo(closestPoint);

				if (dstSquared < closestDistanceSquared)
				{
					closestPathIndex = i;
					closestDistanceSquared = dstSquared;
				}
			}

			if (closestPathIndex == -1)
				return null;

			return pathList[closestPathIndex];
		}

		[Export]
		public Node3D completionDemoNode;
		/// <summary> Camera demo that gets enabled after the level is cleared. </summary>
		[Export]
		public AnimationPlayer completionDemoAnimator;

		[Export]
		public SFXLibraryResource dialogLibrary;

		[Export]
		/// <summary> Reference to active area's WorldEnvironment node. </summary>
		public WorldEnvironment environment;

		public override void _EnterTree()
		{
			instance = this; //Always override previous instance

			for (int i = 0; i < pathParent.GetChildCount(); i++)
			{
				Path3D path = pathParent.GetChildOrNull<Path3D>(i);
				if (path != null)
					pathList.Add(path);
			}
		}

		public bool StartCompletionDemo()
		{
			if (completionDemoNode == null || completionDemoAnimator == null) return false;

			OnCameraDemoAdvance();

			completionDemoAnimator.Connect(AnimationPlayer.SignalName.AnimationFinished, new Callable(this, MethodName.OnCameraDemoAdvance));
			completionDemoAnimator.Play("demo1");
			return true;
		}

		/// <summary> Completion demo advanced, play a crossfade </summary>
		public void OnCameraDemoAdvance() => CharacterController.instance.Camera.StartCrossfade();
	}
}