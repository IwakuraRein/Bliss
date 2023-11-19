#ifndef FBM_HLSL
#define FBM_HLSL

/// <summary>
/// reference: https://gist.github.com/patriciogonzalezvivo/670c22f3966e662d2f83
/// </summary>

float rand(float2 c) {
	return frac(sin(dot(c.xy, float2(12.9898, 78.233))) * 43758.5453);
}

float noise(float2 p, float freq) {
	float unit = /*screenWidth*/ 1000.0 / freq;
	float2 ij = floor(p / unit);
	float2 xy = fmod(p, unit) / unit;
	//xy = 3.*xy*xy-2.*xy*xy*xy;
	xy = .5 * (1. - cos(PI * xy));
	float a = rand((ij + float2(0., 0.)));
	float b = rand((ij + float2(1., 0.)));
	float c = rand((ij + float2(0., 1.)));
	float d = rand((ij + float2(1., 1.)));
	float x1 = lerp(a, b, xy.x);
	float x2 = lerp(c, d, xy.x);
	return lerp(x1, x2, xy.y);
}

float perlinNoise(float2 p, int res) {
	float persistance = .5;
	float n = 0.;
	float normK = 0.;
	float f = 4.;
	float amp = 1.;
	for (int i = 0; i < res; i++) {
		n += amp * noise(p, f);
		f *= 2.;
		normK += amp;
		amp *= persistance;
	}
	float nf = n / normK;
	return nf * nf * nf * nf;
}

#endif