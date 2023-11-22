#ifndef GRASS_RENDER_PROPERTY
#define GRASS_RENDER_PROPERTY


struct GrassRenderProperty {
    float4 v0;
    float4 v1;
    float4 v2;
    float4 right;
    float4 color;
};
float _GrassHeight;
float _GrassWidth;
//float3 _ScaleOverride;
//const float3 GRASS_UP = float3(0, 1, 0);

#endif