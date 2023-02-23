shader_type spatial;
render_mode unshaded, specular_disabled;

global uniform float time;

uniform sampler2D albedo : source_color;
uniform sampler2D displacement : hint_default_black;
uniform vec2 scrollSpeed;
uniform vec2 displacementScrollSpeed;
uniform vec2 displacementScale = vec2(1.0);

void vertex() {
	UV = UV + scrollSpeed * time;
	UV2 = UV2 * displacementScale + displacementScrollSpeed * time;
}

void fragment() {
	vec2 displacementUV = UV2;
	vec4 displacementCol = texture(displacement, displacementUV);
	
	vec2 base_uv = UV + displacementCol.rg * displacementCol.a;
	vec4 albedo_tex = texture(albedo, base_uv);
	albedo_tex *= COLOR;
	ALBEDO = albedo_tex.rgb;
}
