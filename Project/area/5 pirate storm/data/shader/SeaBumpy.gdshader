shader_type spatial;
render_mode unshaded;

global uniform float time;

uniform sampler2D base : source_color;
uniform sampler2D detail : source_color;
uniform sampler2D height_texture : source_color;
uniform float height_strength = 1.0;
uniform float height_scroll_speed = 0.3;

const float scroll_speed = 0.3;

void vertex() {
	float height = texture(height_texture, UV + vec2(height_scroll_speed * time, 0)).r;
	VERTEX.y += height * height_strength;
}

void fragment() {
	vec2 scroll_amount = vec2(scroll_speed * time, 0);
	vec4 col = texture(base, UV + scroll_amount);
	col += texture(detail, UV2 + scroll_amount);
	col *= COLOR;
	ALBEDO = col.rgb;
}
