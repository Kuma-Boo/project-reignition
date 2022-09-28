shader_type spatial;
render_mode unshaded, specular_disabled;
uniform sampler2D albedo : source_color;
uniform sampler2D displacement : source_color;
uniform vec2 scrollSpeed;
uniform vec2 displacementScrollSpeed;
uniform vec2 displacementScale = vec2(1.0);

void vertex() {
	UV = UV + scrollSpeed * TIME;
	UV2 = UV2 * displacementScale + displacementScrollSpeed * TIME;
}

void fragment() {
	vec2 displacementUV = UV2;
	vec4 displacementCol = texture(displacement, displacementUV);
	
	vec2 base_uv = UV + displacementCol.rg * displacementCol.a;
	vec4 albedo_tex = texture(albedo, base_uv);
	albedo_tex *= COLOR;
	ALBEDO = albedo_tex.rgb;
}
