shader_type canvas_item;

global uniform float time;
global uniform float environment_fx_intensity;

uniform sampler2D distortion : source_color, repeat_enable;
uniform vec2 scroll_speed = vec2(1, 1);
uniform vec2 scale = vec2(1, 1);

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;

const float bias = -28.0;
const float intensity = 0.01;

void fragment()
{
	vec2 uv = (UV + scroll_speed * time) * scale;
	vec4 warp = texture(distortion, uv);
	uv = vec2(0.5 - warp.g, 0) * intensity * environment_fx_intensity;
	uv = SCREEN_UV + uv;
	
	vec4 col = texture(SCREEN_TEXTURE, uv);
	COLOR = col;
}