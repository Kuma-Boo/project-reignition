shader_type spatial;
render_mode blend_add;
uniform sampler2D albedo : source_color;
uniform float brightness = 1.0;
uniform float scrollBias;
uniform vec2 scrollSpeed;

void vertex() {
	float vScroll = sin(TIME) + TIME * scrollBias;
	UV += vec2(scrollSpeed.x * TIME, vScroll * scrollSpeed.y);
}

void fragment() {
	vec4 albedo_tex = texture(albedo, UV);
	ALBEDO = albedo_tex.rgb;
	EMISSION = albedo_tex.rgb * brightness;
}