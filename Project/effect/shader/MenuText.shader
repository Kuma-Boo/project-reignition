shader_type canvas_item;

uniform vec4 main_color : hint_color;
uniform vec4 highlight_color : hint_color;
uniform float highlight_intensity : hint_range(0, 8);
uniform int blur_sampling : hint_range (0, 10) = 5;
uniform vec2 highlight_blur = vec2(1, 0);

const float WIDTH = 0.04734573810584494679397346954847;

float gaussian(float x) {
    float x_squared = x*x;
    return WIDTH * exp((x * x / float(2 * blur_sampling)) * -1.0);
}

void fragment()
{
	float font_alpha = texture(TEXTURE, UV).a;
	
	float highlight_mask = texture(TEXTURE, UV - vec2(highlight_intensity, 0) * TEXTURE_PIXEL_SIZE).a;
	highlight_mask *= texture(TEXTURE, UV + vec2(highlight_intensity, 0) * TEXTURE_PIXEL_SIZE).a;
	highlight_mask *= texture(TEXTURE, UV - vec2(0, highlight_intensity) * TEXTURE_PIXEL_SIZE).a;
	highlight_mask *= texture(TEXTURE, UV + vec2(0, highlight_intensity) * TEXTURE_PIXEL_SIZE).a;
	highlight_mask = clamp(highlight_mask, 0, 1);
	
	COLOR.rgb = vec3(highlight_mask);
	//COLOR.rgb = mix(main_color, highlight_color, highlight_mask).rgb;
	COLOR.a = font_alpha;
}