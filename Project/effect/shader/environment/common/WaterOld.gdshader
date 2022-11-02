shader_type spatial;
render_mode shadows_disabled;

uniform vec2 primarySpeed;
uniform vec2 primaryScale = vec2(1.0);
uniform vec2 secondarySpeed;
uniform vec2 secondaryScale = vec2(1.0);
uniform vec4 waterColor : source_color;
uniform sampler2D primaryNoiseTex : source_color;
uniform sampler2D secondaryNoiseTex : source_color;

vec2 waterScroll(vec2 spd, vec2 scl, vec2 uv)
{
	uv += spd * TIME;
	uv *= scl;
	return uv;
}

void fragment()
{
	ALBEDO = waterColor.rgb * COLOR.rgb;
	ALPHA = waterColor.a * COLOR.a;
	
	vec3 scroll = texture(primaryNoiseTex, waterScroll(primarySpeed, primaryScale, UV)).rgb;
	scroll += texture(secondaryNoiseTex, waterScroll(secondarySpeed, secondaryScale, UV)).rgb;
	
	NORMAL_MAP = scroll;
	ROUGHNESS = 0.0;
	SPECULAR = 1.0;
}
