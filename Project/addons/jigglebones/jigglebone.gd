tool
extends Spatial

enum Axis {
	X_Plus, Y_Plus, Z_Plus, X_Minus, Y_Minus, Z_Minus
}

export (String) var bone_name
export(float, 0.1, 100, 0.1) var stiffness = 1
export(float, 0, 100, 0.1) var damping = 0
export var use_gravity = false
export var gravity = Vector3(0, -9.81, 0)
export(Axis) var forward_axis = Axis.Z_Minus

# Previous position
var prev_pos = Vector3()

# Rest length of the distance constraint
var rest_length = 1

func jiggleprint(text):
	print("[Jigglebones] Error: " + text)
	
func get_bone_forward_local():
	match forward_axis:
		Axis.X_Plus: return Vector3(1,0,0) 
		Axis.Y_Plus: return Vector3(0,1,0) 
		Axis.Z_Plus: return Vector3(0,0,1)
		Axis.X_Minus: return Vector3(-1,0,0)
		Axis.Y_Minus: return Vector3(0,-1,0) 
		Axis.Z_Minus: return Vector3(0,0,-1) 

func _ready():
	set_as_toplevel(true)  # Ignore parent transformation
	prev_pos = global_transform.origin

func _process(delta):
	
	var skeleton = get_parent()
	
	if !(skeleton is Skeleton):
		jiggleprint("Jigglebone must be a direct child of a Skeleton node")
		return
	
	if !bone_name:
		jiggleprint("Please enter a bone name")
		return
	
	var bone_id = skeleton.find_bone(bone_name)
	
	if bone_id == -1:
		jiggleprint("Unknown bone %s - please enter a valid bone name" % bone_name)
		return
		
	var bone_id_parent = skeleton.get_bone_parent(bone_id)
	
	# Note:
	# Local space = local to the bone
	# Object space = local to the skeleton (confusingly called "global" in get_bone_global_pose)
	# World space = global
	
	# See https://godotengine.org/qa/7631/armature-differences-between-bones-custom_pose-transform
	
	var bone_transf_obj = skeleton.get_bone_global_pose(bone_id) # Object space bone pose
	var bone_transf_world = skeleton.global_transform * bone_transf_obj
	
	var bone_transf_rest_local = skeleton.get_bone_rest(bone_id)
	var bone_transf_rest_obj = skeleton.get_bone_global_pose(bone_id_parent) * bone_transf_rest_local 
	var bone_transf_rest_world = skeleton.global_transform * bone_transf_rest_obj
	
	############### Integrate velocity (Verlet integration) ##############	
	
	# If not using gravity, apply force in the direction of the bone (so it always wants to point "forward")
	var grav = bone_transf_rest_world.basis.xform(Vector3(0, 0, -1)).normalized() * 9.81
	var vel = (global_transform.origin - prev_pos) / delta
	
	if use_gravity:
		grav = gravity
		
	grav *= stiffness
	vel += grav 
	vel -= vel * damping * delta  # Damping
	
	prev_pos = global_transform.origin
	global_transform.origin = global_transform.origin + vel * delta
	
	############### Solve distance constraint ##############
	
	var goal_pos = skeleton.to_global(skeleton.get_bone_global_pose(bone_id).origin)
	var new_pos_clamped = goal_pos + (global_transform.origin - goal_pos).normalized() * rest_length
	global_transform.origin = new_pos_clamped 
	
	############## Rotate the bone to point to this object #############

	var diff_vec_local = bone_transf_world.affine_inverse().xform(global_transform.origin).normalized() 
	
	var bone_forward_local = get_bone_forward_local()

	# The axis+angle to rotate on, in local-to-bone space
	var bone_rotate_axis = bone_forward_local.cross(diff_vec_local)
	var bone_rotate_angle = acos(bone_forward_local.dot(diff_vec_local))
	
	if bone_rotate_axis.length() < 1e-3:
		return  # Already aligned, no need to rotate
	
	bone_rotate_axis = bone_rotate_axis.normalized()

	# Bring the axis to object space, WITHOUT translation (so only the BASIS is used) since vectors shouldn't be translated
	var bone_rotate_axis_obj = bone_transf_obj.basis.xform(bone_rotate_axis).normalized()
	var bone_new_transf_obj = Transform(bone_transf_obj.basis.rotated(bone_rotate_axis_obj, bone_rotate_angle), bone_transf_obj.origin)  

	if is_nan(bone_new_transf_obj[0][0]):
		bone_new_transf_obj = Transform()  # Corrupted somehow

	skeleton.set_bone_global_pose(bone_id, bone_new_transf_obj) 
	
	# Orient this object to the jigglebone
	global_transform.basis = (skeleton.global_transform * skeleton.get_bone_global_pose(bone_id)).basis
	

