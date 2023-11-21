#ifndef GRASS_RENDER_PROPERTY
#define GRASS_RENDER_PROPERTY


struct GrassRenderProperty {
    float4x4 objectToWorldMat;
    float4x4 objectToWorldRotateMat;
    float4 color;
};

#endif