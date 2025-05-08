shader_type spatial;
render_mode specular_disabled;

uniform sampler2D texture_albedo : source_color,filter_linear_mipmap,repeat_enable;
uniform sampler2D texture_emission : source_color, hint_default_black,filter_linear_mipmap,repeat_enable;
uniform vec4 emission : source_color;
uniform float emission_energy;

global uniform float time;

varying vec3 pos;

void vertex() {
	pos = VERTEX;
}


void fragment() {
	vec2 base_uv = pos.xz * vec2(0.5);
	vec4 albedo_tex = texture(texture_albedo, base_uv);
	ALBEDO = albedo_tex.rgb * COLOR.rgb;
	vec3 emission_tex = texture(texture_emission, base_uv).rgb;
	EMISSION = (emission.rgb * emission_tex) * (emission_energy + sin(time * PI) * 2.0);
}
