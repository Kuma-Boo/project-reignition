shader_type canvas_item;

// Originally created by nobuyuki
uniform sampler2D blend_texture;
uniform sampler2D screen_texture : hint_screen_texture;
uniform float influence : hint_range(0, 1) = 1.0;

void fragment() {
	vec4 background_color;
	vec4 color = texture(TEXTURE, UV);
	vec4 blendColor;
	vec4 output = vec4(1,1,1,1);
	
	background_color = texture(screen_texture, SCREEN_UV);
	
	output.a = color.a;
	
	blendColor = texture(blend_texture, vec2(background_color.r, color.r));
	output.r = blendColor.r;
	blendColor = texture(blend_texture, vec2(background_color.g, color.g));
	output.g = blendColor.g;
	blendColor = texture(blend_texture, vec2(background_color.b, color.b));
	output.b = blendColor.b;

	output.rgb = mix(color.rgb, output.rgb, influence);
	COLOR.rgb = output.rgb;
	COLOR.a *= output.a;
}