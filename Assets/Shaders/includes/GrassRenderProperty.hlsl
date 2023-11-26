#ifndef GRASS_RENDER_PROPERTY
#define GRASS_RENDER_PROPERTY


struct GrassRenderProperty {
    float4 v0_world; // xyz: v0's world position; w: grass height multiplier
    float4 v1andv2_local; // x: v1.y over grass height; yw: v2's xz coordinates uniformed by grass width; z:  v2's y coordinate over grass height
    float4 right;
    float4 color;
    float4 innerForce;
};
float _GrassHeight;
float _GrassWidth;
//float3 _ScaleOverride;
//static const float3 GRASS_UP = float3(0, 1, 0);

#endif