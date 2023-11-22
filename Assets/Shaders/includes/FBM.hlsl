#ifndef FBM_HLSL
#define FBM_HLSL

/// <summary>
/// reference: https://www.shadertoy.com/view/lsf3WH
/// </summary>

float _fbmHash(in int2 p) 
{
	// 2D -> 1D
	int n = p.x * 3 + p.y * 113;

	// 1D hash by Hugo Elias
	n = (n << 13) ^ n;
	n = n * (n * n * 15731 + 789221) + 1376312589;
	return -1.0 + 2.0 * float(n & 0x0fffffff) / float(0x0fffffff);
}
float _fbmNoise(in float2 p)
{
    int2 i = int2(floor(p));
    float2 f = frac(p);

    // quintic interpolant
    float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

    return lerp(lerp(_fbmHash(i + int2(0, 0)),
		_fbmHash(i + int2(1, 0)), u.x),
		lerp(_fbmHash(i + int2(0, 1)),
			_fbmHash(i + int2(1, 1)), u.x), u.y);
}

float perlinNoise(float2 p) {
    float f = 0.0;
    p *= 8.0;
    float2x2 m = float2x2(1.6, 1.2, -1.2, 1.6);
    f = 0.5000 * _fbmNoise(p); p = mul(m, p);
    f += 0.2500 * _fbmNoise(p); p = mul(m, p);
    f += 0.1250 * _fbmNoise(p); p = mul(m, p);
    f += 0.0625 * _fbmNoise(p); p = mul(m, p);
    return f;
}

#endif