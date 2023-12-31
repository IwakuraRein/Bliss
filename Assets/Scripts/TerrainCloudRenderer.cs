using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bliss
{
    public class TerrainCloudPass : ScriptableRenderPass
    {
        internal ProfilingSampler pofilingSamplerTerrain = new ProfilingSampler("Terrain");
        internal ProfilingSampler pofilingSamplerCloud = new ProfilingSampler("Cloud");
        internal Material terrainMat;
        internal Material cloudMat;

        internal Blitter blitter = new Blitter();
        public bool enableInSceneViewPort = false;
        public bool enableCloud = true;
        public bool enableTerrain = true;
        public bool enableShadow
        {
            get 
            {
                if (terrainMat != null && terrainMat.IsKeywordEnabled("_RAY_MARCH_SHADOW")) return true;
                return false;
            }
            set
            {
                if (terrainMat != null)
                {
                    if (value) terrainMat.EnableKeyword("_RAY_MARCH_SHADOW");
                    else terrainMat.DisableKeyword("_RAY_MARCH_SHADOW");
                }
            }
        }
        public TerrainCloudPass(Material terrainMat, Material cloudMat, RenderPassEvent injectionPoint)
        {
            this.terrainMat = terrainMat;
            this.cloudMat = cloudMat;
            renderPassEvent = injectionPoint;
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            if ((!enableInSceneViewPort || cameraData.camera.cameraType != CameraType.SceneView) &&
                (cameraData.camera.cameraType != CameraType.Game && cameraData.camera.cameraType != CameraType.Preview))
            {
                return;
            }

            if (cloudMat != null && enableCloud)
            {
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, pofilingSamplerCloud))
                {
                    blitter.Blit(cmd, cloudMat);
                }
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            if (terrainMat != null && enableTerrain)
            {
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, pofilingSamplerTerrain))
                {
                    //m_Material.SetFloat("_RayMarchMaxDistance", m_Settings.m_RayMarchMaxDistance);
                    //m_Material.SetFloat("_RayMarchStep", m_Settings.m_RayMarchStepMultiplier);
                    //m_Material.SetColor("_HighlightColor", m_Settings.m_HighlightColor);
                    //m_Material.SetColor("_ShadowColor", m_Settings.m_ShadowColor);
                    //var mrt = new RenderTargetIdentifier[]
                    //{
                    //    "_CameraColorTexture",
                    //    "_CameraNormalsTexture",
                    //};
                    //var depthRT = new RenderTargetIdentifier("_CameraDepthTexture");
                    //cmd.SetRenderTarget("_CameraColorTexture", "_CameraDepthTexture");
                    blitter.Blit(cmd, terrainMat);
                }
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }
        }
        public void Dispose()
        {

        }
    }
    [ExecuteInEditMode]
    public class TerrainCloudRenderer : MonoBehaviour
    {
        [SerializeField]
        bool enableInScene = false;
        [SerializeField]
        bool enableShadow = false;
        [SerializeField]
        bool enableCloud = true;
        [SerializeField]
        bool enableTerrain = true;
        [SerializeField]
        internal RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingOpaques;
        [SerializeField]
        internal Material terrainMaterial;
        [SerializeField]
        internal Material cloudMaterial;

        TerrainCloudPass renderPass;

        public bool EnableCloud
        {
            get => enableCloud; set => enableCloud = value;
        }
        public bool EnableTerrain
        {
            get => enableTerrain; set => enableTerrain = value;
        }

        void OnEnable()
        {
            renderPass = new TerrainCloudPass(terrainMaterial, cloudMaterial, injectionPoint);
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            renderPass.enableShadow = enableShadow;
            renderPass.enableCloud = enableCloud;
            renderPass.enableTerrain = enableTerrain;
            renderPass.enableInSceneViewPort = enableInScene;
            cam.GetUniversalAdditionalCameraData()
                .scriptableRenderer.EnqueuePass(renderPass);
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            renderPass.Dispose();
        }
    }
}