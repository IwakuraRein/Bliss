using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bliss
{
    public class TerrainPass : ScriptableRenderPass
    {
        internal ProfilingSampler pofilingSampler = new ProfilingSampler("Terrain");
        internal Material material;

        internal Blitter blitter = new Blitter();
        internal bool enableInSceneViewPort = false;
        public TerrainPass(Material material, RenderPassEvent injectionPoint)
        {
            this.material = material;
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

            if (material == null)
                return;
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, pofilingSampler))
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
                blitter.Blit(cmd, material);
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }
    }
    [ExecuteInEditMode]
    public class TerrainRenderer : MonoBehaviour
    {
        [SerializeField]
        RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingOpaques;
        [SerializeField]
        Material material;
        [SerializeField]
        bool enableInScene = false;

        TerrainPass terrainPass;

        void OnEnable()
        {
            terrainPass = new TerrainPass(material, injectionPoint);
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            terrainPass.enableInSceneViewPort = enableInScene;
            cam.GetUniversalAdditionalCameraData()
                .scriptableRenderer.EnqueuePass(terrainPass);
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            //terrainPass.Dispose();
        }
    }
}