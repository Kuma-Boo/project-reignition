tool
extends MeshInstance

#Custom meshes are "somewhat" supported. Move the Planar Reflector for floating mirrors
const SHOW_NODES_IN_EDITOR = false

enum FitMode {
	FIT_AREA, # Fits reflection on the whole area
	FIT_VIEW # Fits reflection in view.
}

enum UpdateMode {
	Once, #Render once, then switch to never
	Always, #Always render
	Never, #Don't process
}

# Exported variables
## The resolution of the reflection.
var resolution := 512 setget set_resolution
## How the reflection fits in the area.
var fit_mode : int = FitMode.FIT_AREA
## How much normal maps distort the reflection.
var perturb_scale := 0.7
## How much geometry beyond the plane will be rendered.
## Can be used along with perturb scale to make sure there're no seams in the reflection.
var clip_bias := 0.1
## Whether to render the sky in the reflection.
## Disabling this allows you to mix planar reflection, with other sources of reflections,
## such as reflection probes.
var render_sky := true setget set_render_sky
## What geometry gets rendered into the reflection.
var cull_mask := 0xfffff setget set_cull_mask
## Custom environment to render the reflection with.
var environment : Environment setget set_environment

var position_offset : Vector2 setget set_position_offset

# Internal variables
var plugin : EditorPlugin

var reflect_mesh : MeshInstance
var reflect_viewport : Viewport
var reflect_texture : ViewportTexture
var viewport_rect := Rect2(0, 0, 1, 1)

var main_cam : Camera
var reflect_camera : Camera
var render_frequency : int = 1 setget set_render_frequency #how often should the frames be rerendered? (0 to stop rendering)
var render_counter : int

func _set(property : String, value) -> bool:
	match property:
		"mesh":
			set_mesh(value)
		"material_override":
			set_material_override(value)
		"cast_shadow":
			set_cast_shadow(value)
		"layers":
			set_layers(value)
		_:
			if property.begins_with("material/"):
				property.erase(0, "material/".length())
				set_surface_material(int(property), value)
			else:
				return false
	return true

func _get_property_list() -> Array:
	var props := []

	props += [{"name": "PlanarReflector", "type": TYPE_NIL, "usage": PROPERTY_USAGE_CATEGORY}]
	props += [{"name": "environment", "type": TYPE_OBJECT, "hint": PROPERTY_HINT_RESOURCE_TYPE, "hint_string": "Environment"}]
	props += [{"name": "resolution", "type": TYPE_INT}]
	props += [{"name": "fit_mode", "type": TYPE_INT, "hint": PROPERTY_HINT_ENUM, "hint_string": "Fit Area, Fit View"}]
	props += [{"name": "perturb_scale", "type": TYPE_REAL}]
	props += [{"name": "clip_bias", "type": TYPE_REAL, "hint": PROPERTY_HINT_RANGE, "hint_string": "0, 1, 0.01, or_greater"}]
	props += [{"name": "position_offset", "type": TYPE_VECTOR2}]
	props += [{"name": "render_sky", "type": TYPE_BOOL}]
	props += [{"name": "cull_mask", "type": TYPE_INT, "hint": PROPERTY_HINT_LAYERS_3D_RENDER}]
	props += [{"name": "render_frequency", "type": TYPE_INT, "hint": PROPERTY_HINT_RANGE, "hint_string": "0, 256, 1"}]

	return props

func _ready() -> void:
	if Engine.editor_hint:
		plugin = get_node("/root/EditorNode/PlanarReflectionPlugin")

	if SHOW_NODES_IN_EDITOR:
		for node in get_children():
			node.queue_free()

	# Create mirror surface
	reflect_mesh = MeshInstance.new()
	reflect_mesh.layers = layers
	reflect_mesh.cast_shadow = cast_shadow
	reflect_mesh.mesh = mesh
	add_child(reflect_mesh)

	if not mesh:
		self.mesh = QuadMesh.new()

	# Create reflection viewport
	reflect_viewport = Viewport.new()
	reflect_viewport.transparent_bg = not render_sky
	reflect_viewport.keep_3d_linear = true
	reflect_viewport.hdr = true
	reflect_viewport.fxaa = true
	reflect_viewport.render_target_clear_mode = Viewport.CLEAR_MODE_NEVER #Keep previous frames
	#reflect_viewport.shadow_atlas_size = 512 Disable shadows
	add_child(reflect_viewport)

	# Add a mirror camera
	reflect_camera = Camera.new()
	reflect_camera.cull_mask = cull_mask
	reflect_camera.environment = environment
	reflect_camera.name = "reflect_cam"
	reflect_camera.keep_aspect = Camera.KEEP_HEIGHT
	reflect_camera.current = true
	reflect_viewport.add_child(reflect_camera)

	yield(get_tree(), 'idle_frame')
	yield(get_tree(), 'idle_frame')

	# Create reflection texture
	reflect_texture = reflect_viewport.get_texture()
	reflect_texture.set_flags(Texture.FLAG_FILTER)
	if not Engine.is_editor_hint():
		reflect_texture.viewport_path = "/root/" + get_node("/root").get_path_to(reflect_viewport)

	self.material_override = material_override
	for mat in get_surface_material_count():
		set_surface_material(mat, get_surface_material(mat))

	if SHOW_NODES_IN_EDITOR:
		for i in get_children():
			i.owner = get_tree().edited_scene_root

	update_viewport()

func _process(delta : float) -> void:
	if not reflect_camera or not reflect_viewport or not get_extents().length():
		return

	if not is_visible_in_tree() or render_frequency == 0: #Don't process hidden reflectors
		reflect_camera.current = false
		return

	render_counter -= 1
	if render_counter <= 0:
		reflect_camera.current = true
		render_counter = render_frequency
	else: #IDLE
		reflect_camera.current = false
		return

	update_viewport()

	# Get main camera and viewport
	var main_viewport : Viewport
	if Engine.editor_hint:
		main_cam = plugin.editor_camera
		if not main_cam:
			return
		main_viewport = main_cam.get_parent()
	else:
		main_viewport = get_viewport()
		main_cam = main_viewport.get_camera()

	# Compute reflection plane and its global transform  (origin in the middle,
	#  X and Y axis properly aligned with the viewport, -Z is the mirror's forward direction)
	var reflection_transform := (global_transform * Transform().rotated(Vector3.RIGHT, PI))
	var plane_origin := reflection_transform.origin
	var plane_normal := reflection_transform.basis.z.normalized()
	var reflection_plane := Plane(plane_normal, plane_origin.dot(plane_normal))

	# Main camera position
	var cam_pos := main_cam.global_transform.origin

	# Calculate the area the viewport texture will fit into.
	var rect : Rect2
	if fit_mode == FitMode.FIT_VIEW:
		# Area of the plane that's visible
		for corner in [Vector2(0, 0), Vector2(1, 0), Vector2(0, 1), Vector2(1, 1)]:
			var ray := main_cam.project_ray_normal(corner * main_viewport.size)
			var intersection = reflection_plane.intersects_ray(cam_pos, ray)
			if not intersection:
				intersection = reflection_plane.project(cam_pos + ray * main_cam.far)
			intersection = reflection_transform.xform_inv(intersection)
			intersection = Vector2(intersection.x, intersection.y)

			if not rect:
				rect = Rect2(intersection, Vector2())
			else:
				rect = rect.expand(intersection)
		rect = Rect2(-get_extents() / 2.0, get_extents())
		rect.position += position_offset
		rect = rect.clip(rect)

		# Aspect ratio of our extents must also be enforced.
		var aspect = rect.size.aspect()
		if aspect > get_extents().aspect():
			rect = scale_rect2(rect, Vector2(1.0, aspect / get_extents().aspect()))
		else:
			rect = scale_rect2(rect, Vector2(get_extents().aspect() / aspect, 1.0))
	else:
		# Area of the whole plane
		rect = Rect2(-get_extents() / 2.0, get_extents())
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
	var t := Transform(Basis(), mirrored_pos)
	t = t.looking_at(proj_pos, reflection_transform.basis.y.normalized())
	reflect_camera.set_global_transform(t)

	# Compute the tilting offset for the frustum (the X and Y coordinates of the mirrored camera position
	# when expressed in the reflection plane coordinate system)
	var offset = reflection_transform.xform_inv(cam_pos)
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
	if new_size != reflect_viewport.size:
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

# Setters

func set_resolution(value : int) -> void:
	resolution = max(value, 1)

func set_render_sky(value : bool) -> void:
	render_sky = value
	if reflect_viewport:
		reflect_viewport.transparent_bg = not render_sky

func set_cull_mask(value : int) -> void:
	cull_mask = value
	if reflect_camera:
		reflect_camera.cull_mask = cull_mask

func set_environment(value : Environment) -> void:
	environment = value
	if reflect_camera:
		reflect_camera.environment = environment

func set_mesh(value : Mesh) -> void:
	mesh = value
	reflect_mesh.mesh = mesh

func set_material_override(value : Material) -> void:
	if material_override and material_override != value:
		ReflectMaterialManager.remove_material(material_override, self)

	material_override = value
	VisualServer.instance_geometry_set_material_override(get_instance(), preload("discard.material").get_rid())
	reflect_mesh.material_override = ReflectMaterialManager.add_material(value, self)

func set_surface_material(index : int, value : Material) -> void:
	var material = get_surface_material(index)

	if material and material != value:
		ReflectMaterialManager.remove_material(material, self)

	.set_surface_material(index, value)
	reflect_mesh.set_surface_material(index, ReflectMaterialManager.add_material(value, self))

func set_cast_shadow(value : int) -> void:
	cast_shadow = value
	reflect_mesh.cast_shadow = value

func set_layers(value : int) -> void:
	layers = value
	reflect_mesh.layers = value

func set_position_offset(value : Vector2) -> void:
	position_offset = value

func set_render_frequency(value : int) -> void:
	render_frequency = value
	render_counter = 0
