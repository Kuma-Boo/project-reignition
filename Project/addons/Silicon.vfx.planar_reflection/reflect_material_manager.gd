tool
extends Node

const DEFAULT_SPATIAL_CODE := """
shader_type spatial;
render_mode blend_mix,depth_draw_opaque,cull_back,diffuse_burley,specular_schlick_ggx;
uniform vec4 albedo : hint_color;
uniform sampler2D texture_albedo : hint_albedo;
uniform float specular;
uniform float metallic;
uniform float roughness : hint_range(0,1);
uniform float point_size : hint_range(0,128);
uniform vec3 uv1_scale;
uniform vec3 uv1_offset;
uniform vec3 uv2_scale;
uniform vec3 uv2_offset;

void vertex() {
	UV=UV*uv1_scale.xy+uv1_offset.xy;
}

void fragment() {
	vec2 base_uv = UV;
	vec4 albedo_tex = texture(texture_albedo,base_uv);
	ALBEDO = albedo.rgb * albedo_tex.rgb;
	METALLIC = metallic;
	ROUGHNESS = roughness;
	SPECULAR = specular;
}
"""

var materials := []
var reflectors := []
var mat_ref_counts := []

func _enter_tree() -> void:
	if not VisualServer.is_connected("frame_pre_draw", self,"_pre_draw"):
		VisualServer.connect("frame_pre_draw", self,"_pre_draw")

# Update the materials before the frame is rendered.
func _pre_draw() -> void:
	for index in materials.size():
		update_material(index)

func add_material(material : Material, planar) -> ShaderMaterial:
	if not material:
		return null

	var idx := materials.find(material)

	if idx == -1:
		materials.append(material)
		mat_ref_counts.append(1)

		var reflector := ShaderMaterial.new()
		reflectors.append({planar: reflector})
		update_reflector(idx, planar, material)
		return reflector
	else:
		mat_ref_counts[idx] += 1
		if reflectors[idx].has(planar):
			return reflectors[idx][planar]
		else:
			var reflector := ShaderMaterial.new()
			reflectors[idx][planar] = reflector
			update_reflector(idx, planar, material)
			return reflector

func remove_material(material : Material, planar) -> void:
	var idx := materials.find(material)
	if idx != -1:
		if mat_ref_counts[idx] <= 1:
			materials.remove(idx)
			reflectors.remove(idx)
			mat_ref_counts.remove(idx)
		else:
			mat_ref_counts[idx] -= 1
			reflectors[idx].erase(planar)

func update_material(index : int) -> void:
	var material = materials[index]
	var variables := read_material_properties(material)
	for planar in reflectors[index]:
		if not weakref(planar).get_ref():
			# Remove material if planar reflector no longer exists.
			remove_material(material, planar)
		else:
			update_reflector(index, planar, material, variables)

func update_reflector(index : int, planar, material : Material, variables := []) -> void:
	var reflector = reflectors[index][planar]
	if variables.empty():
		variables = read_material_properties(material)

	var shader_params := []
	var update_required := false
	for variable in variables:
		var property = variable[1]
		var name = property.name
		if material is SpatialMaterial:
			if property.type in [TYPE_INT, TYPE_BOOL] and not name.ends_with("_texture_channel"):
				var meta_list = reflector.get_meta_list()
				if not name in meta_list or reflector.get_meta(name) != material.get(name):
					update_required = true
				reflector.set_meta(name, material.get(name))
			else:
				var value = material.get(name)
				# texture channels, although are integers, actually are vec4 in the shader.
				if name.ends_with("_texture_channel"):
					value = [
						Plane(1, 0, 0, 0),
						Plane(0, 1, 0, 0),
						Plane(0, 0, 1, 0),
						Plane(0, 0, 0, 1),
						Plane(0.333, 0.333, 0.333, 0)]\
					[material.get(name)]
				shader_params.append([variable[0], value])
		else:
			# Check for change in shader code
			var prev_code = reflector.get_meta("prev_code")
			if not prev_code or prev_code != VisualServer.shader_get_code(material.shader.get_rid()):
				update_required = true
			reflector.set_meta("prev_code", VisualServer.shader_get_code(material.shader.get_rid()))
			shader_params.append([variable[0], material.get(name)])

	if update_required:
		reflector.shader = convert_material(material)

	for variable in shader_params:
		reflector.set_shader_param(variable[0], variable[1])

	# Pass planar reflector parameters
	var rect : Rect2 = planar.viewport_rect
	reflector.set_shader_param("_pr_viewport_rect", Plane(
			rect.position.x, rect.position.y, rect.size.x, rect.size.y
	))
	reflector.set_shader_param("_pr_viewport", planar.reflect_texture)
	reflector.set_shader_param("_pr_perturb_scale", planar.perturb_scale)

# Returns an array of [shader name, property of material]
func read_material_properties(material : Material) -> Array:
	var property_list := material.get_property_list()

	var list := []
	var reading_vars := false
	for property in property_list:
		# Start looking for properties after encountering this.
		if property.name == "Material":
			reading_vars = true
			continue

		# Ignore groups, scripts and next_pass
		if not reading_vars or \
				property.usage & (PROPERTY_USAGE_GROUP | PROPERTY_USAGE_CATEGORY) or \
				property.name in ["script", "next_pass"]:
			continue

		var shader_name : String = property.name

		# Not all properties match with their shader parameter.
		# We'll need to convert some of them.
		if material is SpatialMaterial:
			match shader_name:
				"params_grow_amount":
					shader_name = "grow"
				"params_alpha_scissor_threshold":
					shader_name = "alpha_scissor_threshold"
				"albedo_color":
					shader_name = "albedo"
				"metallic_specular":
					shader_name = "specular"
				"anisotropy":
					shader_name = "anisotropy_ratio"
				"anisotropy_flowmap":
					shader_name = "texture_flowmap"
				"subsurf_scatter_strength":
					shader_name = "subsurface_scattering_strength"
				"refraction_scale":
					shader_name = "refraction"
				"uv1_triplanar_sharpness":
					shader_name = "uv1_blend_sharpness"
				"uv2_triplanar_sharpness":
					shader_name = "uv2_blend_sharpness"
				"distance_fade_min_distance":
					shader_name = "distance_fade_min"
				"distance_fade_max_distance":
					shader_name = "distance_fade_max"

			if shader_name.ends_with("_texture"):
				shader_name = "texture_" + shader_name.rstrip("_texture")
			elif shader_name.begins_with("detail_") and property.type == TYPE_OBJECT:
				shader_name = "texture_" + shader_name
		else:
			if shader_name.begins_with("shader_param/"):
				shader_name.erase(0, "shader_param/".length())
		list.append([shader_name, property])
	return list

static func convert_material(material : Material) -> Shader:
	var base := preload("base_reflection.shader").code
	var code := VisualServer.shader_get_code(
			VisualServer.material_get_shader(material.get_rid())
	)

	# When a material is first created, it does not immediately have shader code.
	# This makes sure that it will initially work.
	if code.empty():
		if material is SpatialMaterial:
			code = DEFAULT_SPATIAL_CODE
		else:
			var default := Shader.new()
			default.code = base
			return default

	# Get the uniforms and functions from the base shader
	var uni_func_start := base.find("//PR__UNIFORMS_AND_FUNCTIONS__PR//")
	var uni_func_length := base.find("//PR__UNIFORMS_AND_FUNCTIONS__PR//", uni_func_start + 1) - uni_func_start
	var uni_funcs = base.substr(uni_func_start, uni_func_length)

	code = code.insert(code.find(";") + 1, "\n" + uni_funcs)

	# Get the fragment code from the base shader
	var frag_start := base.find("//PR__FRAGMENT_CODE__PR//")
	var frag_length := base.find("//PR__FRAGMENT_CODE__PR//", frag_start + 1) - frag_start
	var frag_code = base.substr(frag_start, frag_length)

	# The shader may or may not have a fragment shader
	var regex := RegEx.new()
	regex.compile("void\\s+fragment\\s*\\(\\s*\\)\\s*{")
	var has_fragment := regex.search(code)
	if has_fragment:
		var end := has_fragment.get_end() - 1
		var frag_end := find_closing_bracket(code, end)
		code = code.insert(frag_end, frag_code)
	else:
		code += "void fragment() {" + frag_code + "}"

	var shader := Shader.new()
	shader.code = code
	return shader

static func find_closing_bracket(string : String, open_bracket_idx : int) -> int:
	var bracket_count := 1
	var open_bracket := string.substr(open_bracket_idx, 1)
	var close_bracket := "}" if open_bracket == "{" else ")" if open_bracket == "(" else "]"
	var index := open_bracket_idx

	while index < string.length():
		var open_index = string.find(open_bracket, index+1)
		var close_index = string.find(close_bracket, index+1)

		if close_index != -1 and (open_index == -1 or close_index < open_index):
			index = close_index
			bracket_count -= 1
		elif open_index != -1 and (close_index == -1 or open_index < close_index):
			index = open_index
			bracket_count += 1
		else:
			return -1

		if bracket_count <= 0:
			return index

	return -1
