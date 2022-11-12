using Godot;
using Project.Core;

namespace Project.Gameplay
{
	[Tool]
	public partial class Crusher : Node3D
	{
		[Export(PropertyHint.Range, "0, 10")]
		private int height;
		[Export]
		private float travelLength;
		[Export]
		private float waitLength; //How long to wait between movements
		[Export]
		private float fallenWaitLengthOverride = -1; //How long to wait when on the floor. -1 to ignore.
		[Export]
		private float currentTime; //Set this from the editor to change where the initial timer is
		[Export]
		private bool isWaiting;
		[Export]
		private bool isFalling;
		[Export]
		private Curve travelCurve;

		[Export]
		private Node3D light;
		[Export]
		private NodePath top;
		private Node3D _top;

		public override void _Ready()
		{
			if (Engine.IsEditorHint()) return;

			_top = GetNode<Node3D>(top);
			_top.Position = Vector3.Up * height; //Start at the top
		}

		public override void _PhysicsProcess(double _)
		{
			if (Engine.IsEditorHint())
			{
				_top = GetNodeOrNull<Node3D>(top);
				if (_top != null)
					_top.Position = Vector3.Up * height;

				return;
			}

			if (Mathf.IsZeroApprox(travelLength)) return; //Static

			light.Visible = isFalling;
			if (isWaiting)
			{
				currentTime += PhysicsManager.physicsDelta;
				if ((fallenWaitLengthOverride > 0 && isFalling && currentTime >= fallenWaitLengthOverride) || currentTime >= waitLength)
				{
					isWaiting = false;
					isFalling = !isFalling;
					currentTime = isFalling ? travelLength : 0;
				}
			}
			else
			{
				if (isFalling)
					currentTime = Mathf.MoveToward(currentTime, 0, PhysicsManager.physicsDelta);
				else
					currentTime = Mathf.MoveToward(currentTime, travelLength, PhysicsManager.physicsDelta);

				float t = currentTime / travelLength;
				if (travelCurve == null)
				{
					if (t > .5f)
						t = Mathf.SmoothStep(0, 1, t);
				}
				else
					t = travelCurve.Sample(t);

				_top.Position = Vector3.Up * t * height;

				if (Mathf.IsZeroApprox(currentTime) || Mathf.IsEqualApprox(currentTime, travelLength))
				{
					if ((fallenWaitLengthOverride > 0 && isFalling) || !Mathf.IsZeroApprox(waitLength))
					{
						currentTime = 0;
						isWaiting = true;
					}
					else
						isFalling = !isFalling;
				}
			}
		}
	}
}
