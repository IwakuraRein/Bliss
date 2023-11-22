#ifndef GRASS_RENDER_PROPERTY
#define GRASS_RENDER_PROPERTY


struct GrassRenderProperty {
    float4 v0;
    float4 v1andv2; // x: v1.y over grass height; yw: v2's xz coordinates uniformed by grass width; z:  v2's y coordinate over grass height
    float4 right;
    float4 color;
};
float _GrassHeight;
float _GrassWidth;
//float3 _ScaleOverride;
static const float3 GRASS_UP = float3(0, 1, 0);

#endif