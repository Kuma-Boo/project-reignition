shader_type canvas_item;

global uniform float time;

uniform sampler2D distortion : source_color, repeat_enable;
uniform float intensity = 128.0;
uniform vec2 scroll_speed = vec2(1, 1);
uniform vec2 scale = vec2(1, 1);

uniform float bias;

void fragment()
{
	vec2 uv = UV * scale + scroll_speed * time;
	vec4 warp = texture(distortion, uv);
	
	uv = SCREEN_UV + (vec2((warp.g * intensity) + bias, 0) * SCREEN_PIXEL_SIZE.x);
	
	vec4 col = texture(SCREEN_TEXTURE, uv);
	COLOR = col;
}