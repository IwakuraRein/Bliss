#ifndef CLOUD_HLSL
#define CLOUD_HLSL

/// <summary>
/// reference: https://www.shadertoy.com/view/4ttSWf
/// </summary>
/// 
#include "RayMarchProperties.hlsl"


static const float3x3 _m3 = float3x3(0.00, 0.80, 0.60,
    -0.80, 0.36, -0.48,
    -0.60, -0.48, 0.64);
static const float3x3 _m3i = float3x3(0.00, -0.80, -0.60,
    0.80, 0.36, -0.48,
    0.60, -0.48, 0.64);

float4 _cloud_noise(float3 x)
{
    float3 p = floor(x);
    float3 w = frac(x);
#if 1
    float3 u = w * w * w * (w * (w * 6.0 - 15.0) + 10.0);
    float3 du = 30.0 * w * w * (w * (w - 2.0) + 1.0);
#else
    float3 u = w * w * (3.0 - 2.0 * w);
    float3 du = 6.0 * w * (1.0 - w);
#endif

    float n = p.x + 317.0 * p.y + 157.0 * p.z;

    float a = _rayMarchHash(n + 0.0);
    float b = _rayMarchHash(n + 1.0);
    float c = _rayMarchHash(n + 317.0);
    float d = _rayMarchHash(n + 318.0);
    float e = _rayMarchHash(n + 157.0);
    float f = _rayMarchHash(n + 158.0);
    float g = _rayMarchHash(n + 474.0);
    float h = _rayMarchHash(n + 475.0);

    float k0 = a;
    float k1 = b - a;
    float k2 = c - a;
    float k3 = e - a;
    float k4 = a - b - c + d;
    float k5 = a - c - e + g;
    float k6 = a - b - e + f;
    float k7 = -a + b + c - d + e - f - g + h;

    return float4(-1.0 + 2.0 * (k0 + k1 * u.x + k2 * u.y + k3 * u.z + k4 * u.x * u.y + k5 * u.y * u.z + k6 * u.z * u.x + k7 * u.x * u.y * u.z),
        2.0 * du * float3(k1 + k4 * u.y + k6 * u.z + k7 * u.y * u.z,
            k2 + k5 * u.z + k4 * u.x + k7 * u.z * u.x,
            k3 + k6 * u.x + k5 * u.y + k7 * u.x * u.y));
}
float4 _fbmd_8(float3 x)
{
    float f = 2.0;
    float s = 0.65;
    float a = 0.0;
    float b = 0.5;
    float3 d = 0;
    float3x3  m = float3x3(1.0, 0.0, 0.0,
        0.0, 1.0, 0.0,
        0.0, 0.0, 1.0);
    for (int i = 0; i < 8; i++)
    {
        float4 n = _cloud_noise(x);
        a += b * n.x;          // accumulate values		
        if (i < 4)
            d += b * mul(m, n.yzw);      // accumulate derivatives
        b *= s;
        x = f * mul(_m3, x);
        m = f * mul(_m3i, m);
    }
    return float4(a, d);
}

float4 _cloudsFbm(float3 pos)
{
    return _fbmd_8(pos * 0.0015 + float3(2.0, 1.1, 1.0) + 0.02 * float3(_Time.y, 0.5 * _Time.y, -0.15 * _Time.y));
}

float4 cloudsMap(float3 pos, out float nnd)
{
    float d = abs(pos.y - 900.0) - 40.0;
    float3 gra = float3(0.0, sign(pos.y - 900.0), 0.0);

    float4 n = _cloudsFbm(pos);
    d += 400.0 * n.x * (0.7 + 0.3 * gra.y);

    if (d > 0.0) return float4(-d, 0.0, 0.0, 0.0);

    nnd = -d;
    d = min(-d / 100.0, 0.25);

    //gra += 0.1*n.yzw *  (0.7+0.3*gra.y);

    return float4(d, gra);
}

float CloudsShadow(float3 ro, float3 rd)
{
    float t = (900.0 - ro.y) / rd.y;
    if (t < 0.0) return 1.0;
    float3 pos = ro + rd * t;
    return _cloudsFbm(pos).x;
}

float4 RayMarchClouds(float3 color, float3 kSunDir, float3 ro, float3 rd, float step, float tmin, float tmax, inout float resT)
{
    float4 sum = float4(0.0, 0.0, 0.0, 0.0);

    // bounding volume!!
    float tl = (600.0 - ro.y) / rd.y;
    float th = (1200.0 - ro.y) / rd.y;
    if (tl > 0.0) tmin = max(tmin, tl); else return sum;
    if (th > 0.0) tmax = min(tmax, th);

    float t = tmin;
    //t += 1.0*hash1(gl_FragCoord.xy);
    float lastT = -1.0;
    float thickness = 0.0;
    for (int i = 0; i < MAX_MARCH_CLOUD_STEPS; i++)
    {
        float3  pos = ro + t * rd;
        float nnd;
        float4  denGra = cloudsMap(pos, nnd);
        float den = denGra.x;
        float dt = max(0.2, 0.011 * t);
        //dt *= hash1(px+float(i));
        if (den > 0.001)
        {
            float kk;
            cloudsMap(pos + kSunDir * 70.0, kk);
            float sha = 1.0 - smoothstep(-200.0, 200.0, kk); sha *= 1.5;

            float3 nor = normalize(denGra.yzw);
            float dif = clamp(0.4 + 0.6 * dot(nor, kSunDir), 0.0, 1.0) * sha;
            float fre = clamp(1.0 + dot(nor, rd), 0.0, 1.0) * sha;
            float occ = 0.2 + 0.7 * max(1.0 - kk / 200.0, 0.0) + 0.1 * (1.0 - den);
            // lighting
            float3 lin = float3(0.0, 0.0, 0.0);
            lin += float3(0.70, 0.80, 1.00) * 1.0 * (0.5 + 0.5 * nor.y) * occ;
            lin += float3(0.10, 0.40, 0.20) * 1.0 * (0.5 - 0.5 * nor.y) * occ;
            lin += float3(1.00, 0.95, 0.85) * 3.0 * dif * occ + 0.1;

            // color
            float3 col = float3(0.6,0.6,0.6);

            col *= lin;

            //col = fog(col, t);

            // front to back blending    
            float alp = clamp(den * 0.5 * 0.125 * dt, 0.0, 1.0);
            col.rgb *= alp;
            sum = sum + float4(col, alp) * (1.0 - sum.a);

            thickness += dt * den;
            if (lastT < 0.0) lastT = t;
        }
        else
        {
            dt = abs(den) + step;

        }
        t += dt;
        if (sum.a > 0.995 || t > tmax) break;
    }

    //resT = min(resT, (150.0-ro.y)/rd.y );
    if (lastT > 0.0) resT = min(resT, lastT);
    //if( lastT>0.0 ) resT = mix( resT, lastT, sum.w );

    sum.xyz += max(0.0, 1.0 - 0.0125 * thickness) * color * 0.3 * pow(clamp(dot(kSunDir, rd), 0.0, 1.0), 32.0);

    return clamp(sum, 0.0, 1.0);
}

#endif