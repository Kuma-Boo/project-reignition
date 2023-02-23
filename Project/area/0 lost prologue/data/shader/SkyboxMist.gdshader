shader_type spatial;
render_mode unshaded, blend_add;

global uniform float time;

uniform sampler2D albedo : source_color;
uniform float brightness = 1.0;
uniform float scrollBias;
uniform vec2 scrollSpeed;

void vertex() {
	float vScroll = sin(time) + time * scrollBias;
	UV += vec2(scrollSpeed.x * time, vScroll * scrollSpeed.y);
}

void fragment() {
	vec4 albedo_tex = texture(albedo, UV);
	ALBEDO = albedo_tex.rgb * brightness;
}