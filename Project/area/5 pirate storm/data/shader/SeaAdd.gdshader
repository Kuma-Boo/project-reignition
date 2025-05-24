shader_type spatial;
render_mode unshaded, blend_add;

global uniform float time;

uniform sampler2D base : source_color;
uniform sampler2D detail : source_color;

const float scroll_speed = 0.3;

void fragment() {
	vec2 scroll_amount = vec2(scroll_speed * time, -scroll_speed * time);
	vec4 col = texture(base, UV + scroll_amount);
	col += texture(detail, UV2 + scroll_amount);
	col *= COLOR;
	ALBEDO = col.rgb;
}
