shader_type spatial;

global uniform float time;
global uniform float secondary_environment_fx_intensity;

uniform sampler2D base : source_color;
uniform sampler2D detail : source_color;
uniform sampler2D lightning_base : source_color;
uniform sampler2D lightning_detail : source_color;
uniform float lightning_intensity = 2.0;

const float scroll_speed = 0.3;

void fragment() {
	vec2 scroll_amount = vec2(scroll_speed * time, 0);
	vec4 base_col = texture(base, UV + scroll_amount) + texture(detail, UV2 + scroll_amount);
	vec4 lightning_col = texture(lightning_base, UV + scroll_amount) + texture(lightning_detail, UV2 + scroll_amount);
	vec4 col = mix(base_col, lightning_col, lightning_intensity * secondary_environment_fx_intensity);
	col *= COLOR;
	ALBEDO = col.rgb;
	ALPHA = clamp(col.a, 0.0, 1.0);
}
