@tool
extends MeshInstance3D

#Custom meshes are "somewhat" supported. Move the Planar Reflector for floating mirrors
const SHOW_NODES_IN_EDITOR = true

enum FitMode {
	FIT_AREA, # Fits reflection checked the whole area
	FIT_VIEW # Fits reflection in view.
}

# Exported variables
## The resolution of the reflection.
var resolution := 512 :
	get:
		return resolution
	set(mod_value):
		resolution = max(mod_value, 1)
## How much normal maps distort the reflection.
var perturb_scale := 0.7
## How much geometry beyond the plane will be rendered.
## Can be used along with perturb scale to make sure there're no seams in the reflection.
var clip_bias := 0.1
## Whether to render the sky in the reflection.
## Disabling this allows you to mix planar reflection, with other sources of reflections,
## such as reflection probes.
var render_sky := true :
	get:
		return render_sky
	set(mod_value):
		render_sky = mod_value
		if reflect_viewport:
			reflect_viewport.transparent_bg = not render_sky
## What geometry gets rendered into the reflection.
var cull_mask := 0xfffff :
	get:
		return cull_mask
	set(mod_value):
		cull_mask = mod_value
		if reflect_camera:
			reflect_camera.cull_mask = cull_mask
## Custom environment to render the reflection with.
var environment : Environment :
	get:
		return environment
	set(mod_value):
		environment = mod_value
		if reflect_camera:
			reflect_camera.environment = environment

var position_offset : Vector2 :
	get:
		return position_offset
	set(mod_value):
		position_offset = mod_value

# Internal variables
var plugin : EditorPlugin

var reflect_mesh : MeshInstance3D
var reflect_viewport : SubViewport
var reflect_texture : ViewportTexture
var viewport_rect := Rect2(0, 0, 1, 1)

var main_cam : Camera3D
var reflect_camera : Camera3D

func _set(p : StringName, value) -> bool:
	var property = (p as String)
	match property:
		"mesh":
			mesh = value
			reflect_mesh.mesh = mesh
		"material_override":
			if material_override and material_override != value:
				ReflectMaterialManager.remove_material(material_override, self)

			material_override = value
			RenderingServer.instance_geometry_set_material_override(get_instance(), preload("discard.material").get_rid())
			reflect_mesh.material_override = ReflectMaterialManager.add_material(value, self)
		"cast_shadow":
			cast_shadow = value
			reflect_mesh.cast_shadow = value
		"layers":
			layers = value
			reflect_mesh.layers = value
		_:
			if property.begins_with("material/"):
				property.erase(0, "material/".length())
				
				var index : int = property.to_int()
				var material = get_surface_override_material(index)

				if material and material != value:
					ReflectMaterialManager.remove_material(material, self)

				super.set_surface_override_material(index, value)
				reflect_mesh.set_surface_override_material(index, ReflectMaterialManager.add_material(value, self))
			else:
				return false
	return true

func _get_property_list() -> Array:
	var props := []

	props += [{"name": "PlanarReflector", "type": TYPE_NIL, "usage": PROPERTY_USAGE_CATEGORY}]
	props += [{"name": "environment", "type": TYPE_OBJECT, "hint": PROPERTY_HINT_RESOURCE_TYPE, "hint_string": "Environment"}]
	props += [{"name": "resolution", "type": TYPE_INT}]
	props += [{"name": "fit_mode", "type": TYPE_INT, "hint": PROPERTY_HINT_ENUM, "hint_string": "Fit Area3D, Fit View"}]
	props += [{"name": "perturb_scale", "type": TYPE_FLOAT}]
	props += [{"name": "clip_bias", "type": TYPE_FLOAT, "hint": PROPERTY_HINT_RANGE, "hint_string": "0, 1, 0.01, or_greater"}]
	props += [{"name": "position_offset", "type": TYPE_VECTOR2}]
	props += [{"name": "render_sky", "type": TYPE_BOOL}]
	props += [{"name": "cull_mask", "type": TYPE_INT, "hint": PROPERTY_HINT_LAYERS_3D_RENDER}]

	return props

func _ready() -> void:
	if Engine.is_editor_hint():
		plugin = get_node("/root").get_child(0).get_node("PlanarReflectionPlugin")

	if SHOW_NODES_IN_EDITOR:
		for node in get_children():
			node.queue_free()

	# Create mirror surface
	reflect_mesh = MeshInstance3D.new()
	reflect_mesh.layers = layers
	reflect_mesh.cast_shadow = cast_shadow
	reflect_mesh.mesh = mesh
	add_child(reflect_mesh)
	
	if not mesh:
		var new_mesh : PlaneMesh = PlaneMesh.new()
		new_mesh.orientation = PlaneMesh.FACE_Z
		self.mesh = new_mesh

	# Create reflection viewport
	reflect_viewport = SubViewport.new()
	reflect_viewport.transparent_bg = not render_sky
	reflect_viewport.size_2d_override_stretch = true
	add_child(reflect_viewport)

	# Add a mirror camera
	reflect_camera = Camera3D.new()
	reflect_camera.cull_mask = cull_mask
	reflect_camera.environment = environment
	reflect_camera.name = "reflect_cam"
	reflect_camera.keep_aspect = Camera3D.KEEP_HEIGHT
	reflect_camera.current = true
	reflect_viewport.add_child(reflect_camera)

	# Create reflection texture
	reflect_texture = reflect_viewport.get_texture()
	if not Engine.is_editor_hint():
		reflect_texture.viewport_path = "/root/" + (get_node("/root").get_path_to(reflect_viewport) as String)

	self.material_override = material_override
	for mat in get_surface_override_material_count():
		set_surface_override_material(mat, get_surface_override_material(mat))

	if SHOW_NODES_IN_EDITOR:
		for i in get_children():
			i.owner = get_tree().edited_scene_root

	update_viewport()

func _process(delta : float) -> void:
	if not reflect_camera or not reflect_viewport or not get_extents().length():
		return

	if not is_visible_in_tree(): #Don't process hidden reflectors
		reflect_camera.current = false
		return

	reflect_camera.current = true
	update_viewport()

	# Get main camera and viewport
	var main_viewport : Viewport
	if Engine.is_editor_hint():
		main_cam = plugin.editor_camera
		if not main_cam:
			return
		main_viewport = main_cam.get_parent()
	else:
		main_viewport = get_viewport()
		main_cam = main_viewport.get_camera_3d()
	
	# Compute reflection plane and its global transform  (origin in the middle,
	#  X and Y axis properly aligned with the viewport, -Z is the mirror's forward direction)
	var reflection_transform := (global_transform * Transform3D().rotated(Vector3.RIGHT, PI))
	var plane_origin := reflection_transform.origin
	var plane_normal := reflection_transform.basis.z.normalized()
	var reflection_plane := Plane(plane_normal, plane_origin.dot(plane_normal))

	# Main camera position
	var cam_pos := main_cam.global_position

	# Calculate the area the viewport texture will fit into.
	var rect : Rect2 = Rect2(-get_extents() / 2.0, get_extents())
	rect.position += position_offset
	viewport_rect = rect

	var rect_center := rect.position + rect.size / 2.0
	reflection_transform.origin += reflection_transform.basis.x * rect_center.x
	reflection_transform.origin += reflection_transform.basis.y * rect_center.y

	# The projected point of main camera's position onto the reflection plane
	var proj_pos := reflection_plane.project(cam_pos)

	# Main camera position reflected over the mirror's plane
	var mirrored_pos := cam_pos + (proj_pos - cam_pos) * 2.0

	# Compute mirror camera transform
	# - origin at the mirrored position
	# - looking perpedicularly into the reflection plane (this way the near clip plane will be
	#      parallel to the reflection plane)
	var t := Transform3D(Basis(), mirrored_pos)
	t = t.looking_at(proj_pos, reflection_transform.basis.y.normalized())
	reflect_camera.set_global_transform(t)

	# Compute the tilting offset for the frustum (the X and Y coordinates of the mirrored camera position
	# when expressed in the reflection plane coordinate system)
	var offset = cam_pos * reflection_transform
	offset = Vector2(offset.x, offset.y)

	# Set mirror camera frustum
	# - size 	-> mirror's width (camera is set to KEEP_WIDTH)
	# - offset 	-> previously computed tilting offset
	# - z_near 	-> distance between the mirror camera and the reflection plane (this ensures we won't
	#               be reflecting anything behind the mirror)
	# - z_far	-> large arbitrary value (render distance limit form th mirror camera position)
	var z_near := proj_pos.distance_to(cam_pos)
	var clip_factor = (z_near - clip_bias) / z_near
	if rect.size.y * clip_factor > 0:
		reflect_camera.set_frustum(rect.size.y * clip_factor, -offset * clip_factor, z_near * clip_factor, main_cam.far)

func update_viewport() -> void:
	reflect_viewport.transparent_bg = not render_sky
	var new_size : Vector2 = Vector2(get_extents().aspect(), 1.0) * resolution
	if new_size.x > new_size.y:
		new_size = new_size / new_size.x * resolution
	
	new_size = new_size.floor()
	if new_size != (reflect_viewport.size as Vector2):
		reflect_viewport.size = new_size

func get_extents() -> Vector2:
	if mesh:
		return Vector2(mesh.get_aabb().size.x, mesh.get_aabb().size.y)
	else:
		return Vector2()

# Scale rect2 relative to its center
static func scale_rect2(rect : Rect2, scale : Vector2) -> Rect2:
	var center = rect.position + rect.size / 2.0;
	rect.position -= center
	rect.size *= scale
	rect.position *= scale
	rect.position += center

	return rect
