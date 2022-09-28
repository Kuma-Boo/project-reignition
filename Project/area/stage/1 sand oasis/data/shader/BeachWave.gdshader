shader_type spatial;
render_mode async_visible,blend_add,depth_draw_opaque,cull_back,diffuse_burley,specular_disabled;

uniform sampler2D main_tex : source_color;
uniform sampler2D secondary_tex : source_color;
uniform sampler2D lut : source_color;
uniform vec2 offset;
uniform vec2 secondary_offset;
uniform float tail_fade : hint_range(0, 1);
uniform float main_fade : hint_range(0, 1);
uniform float secondary_alpha : hint_range(0, 1);

void vertex() {
	UV = UV + offset;
}

void fragment() {
	vec4 col = texture(main_tex, UV) * main_fade;
	col += texture(secondary_tex, UV + secondary_offset) * secondary_alpha;
	col = mix(col, col * texture(lut, UV), tail_fade);
	col *= COLOR;
	col = clamp(col, 0, 1);
	ALBEDO = col.rgb;
	ALPHA = col.a;
}
