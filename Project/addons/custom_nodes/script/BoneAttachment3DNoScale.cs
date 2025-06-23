using Godot;

namespace Project.CustomNodes;

/// <summary>
/// BoneAttachment3D, but without the scale. Use this whenever a Physics node needs to follow a bone.
/// </summary>
[Tool]
public partial class BoneAttachment3DNoScale : Node3D
{
	[Export(PropertyHint.NodeType, "Skeleton3D")] private NodePath skeleton;
	private Skeleton3D _skeleton;
	private Node3D parentNode;
	[Export] private bool prioritizeBoneName = true;
	[Export] private StringName boneName;
	[Export(PropertyHint.Range, "0,0,1, or_greater")] private int boneIndex;

	[ExportToolButton("Update Linked Data")]
	public Callable RefreshResourceGroup => Callable.From(UpdateLinkedData);

	public override void _EnterTree()
	{
		if (skeleton.IsEmpty)
			return;

		UpdateLinkedData();
	}

	public override void _ExitTree()
	{
		if (_skeleton == null)
			return;

		try
		{
			_skeleton.SkeletonUpdated -= OnSkeletonUpdate;
		}
		catch
		{
			return;
		}
	}

	private void UpdateLinkedData()
	{
		parentNode = GetParentNode3D();
		Skeleton3D targetSkeleton = GetNode<Skeleton3D>(skeleton);
		if (_skeleton != targetSkeleton)
		{
			if (_skeleton != null)
				_skeleton.SkeletonUpdated -= OnSkeletonUpdate;

			_skeleton = targetSkeleton;
			_skeleton.SkeletonUpdated += OnSkeletonUpdate;
		}

		if (prioritizeBoneName)
		{
			// Linear search for the bone
			for (int i = 0; i < _skeleton.GetBoneCount(); i++)
			{
				if (_skeleton.GetBoneName(i) != boneName)
					continue;

				boneIndex = i;
				break;
			}
		}
		else
		{
			boneName = _skeleton.GetBoneName(boneIndex);
		}
	}

	private void OnSkeletonUpdate()
	{
		Transform3D transform = _skeleton.GetBoneGlobalPose(boneIndex);
		if (parentNode != null)
		{
			transform.Origin += parentNode.GlobalPosition;
			transform.Basis = parentNode.GlobalBasis * transform.Basis;
			transform = transform.Orthonormalized();
		}

		GlobalPosition = transform.Origin;
		GlobalRotation = transform.Basis.GetEuler();
	}
}