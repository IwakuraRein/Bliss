using Bliss;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    private static GameManager _instance;
    public static GameManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType(typeof(GameManager)) as GameManager;
                if (_instance == null)
                {
                    GameObject go = new GameObject("GameManager");
                    go.tag = "GameController";
                    _instance = go.AddComponent<GameManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    int ChunkSize;
    int Chunks;
    float LOD0;
    float LOD1;
    float LOD2;
    [SerializeField]
    Slider ChunkSizeSlider;
    [SerializeField]
    Slider ChunksSlider;
    [SerializeField]
    Slider LOD0Slider;
    [SerializeField]
    Slider LOD1Slider;
    [SerializeField]
    Slider LOD2Slider;
    [SerializeField]
    Slider TimeScaleSlider;

    [SerializeField]
    GrassRenderer grassRenderer;
    [SerializeField]
    TerrainCloudRenderer terrainCloudRenderer;

    bool LODValueChanged = false;
    private void Start()
    {
        ChunkSize = grassRenderer.chunkGrassSize;
        Chunks = grassRenderer.LOD[0] + grassRenderer.LOD[1] + grassRenderer.LOD[2];
        LOD0 = (float)grassRenderer.LOD[0] / Chunks * 100f;
        LOD1 = (float)grassRenderer.LOD[1] / Chunks * 100f;
        LOD2 = (float)grassRenderer.LOD[2] / Chunks * 100f;

        ChunksSlider.value = Chunks;
        ChunkSizeSlider.value = ChunkSize;
        LOD0Slider.value = LOD0;
        LOD1Slider.value = LOD1;
        LOD2Slider.value = LOD2;

        ChunkSizeSlider.onValueChanged.AddListener(delegate {
            ChunkSize = (int)ChunkSizeSlider.value;
        });
        ChunksSlider.onValueChanged.AddListener(delegate {
            Chunks = (int)ChunksSlider.value;
        });
        LOD0Slider.onValueChanged.AddListener(delegate {
            if (!LODValueChanged)
            {
                LOD0 = Mathf.Min(99.999f, Mathf.Max(0.0001f, LOD0Slider.value));
                float newLOD1 = LOD1 / (LOD1 + LOD2 + 0.0001f) * (100f - LOD0);
                float newLOD2 = LOD2 / (LOD1 + LOD2 + 0.0001f) * (100f - LOD0);
                LOD1 = Mathf.Min(99.999f, Mathf.Max(0.0001f, newLOD1));
                LOD2 = Mathf.Min(99.999f, Mathf.Max(0.0001f, newLOD2));
                //LOD1Slider.value = LOD1;
                //LOD2Slider.value = LOD2;
                LODValueChanged = true;
            }
        });
        LOD1Slider.onValueChanged.AddListener(delegate {
            if (!LODValueChanged)
            {
                LOD1 = Mathf.Min(99.999f, Mathf.Max(0.0001f, LOD1Slider.value));
                float newLOD0 = LOD0 / (LOD0 + LOD2 + 0.0001f) * (100f - LOD1);
                float newLOD2 = LOD2 / (LOD0 + LOD2 + 0.0001f) * (100f - LOD1);
                LOD0 = Mathf.Min(99.999f, Mathf.Max(0.0001f, newLOD0));
                LOD2 = Mathf.Min(99.999f, Mathf.Max(0.0001f, newLOD2));
                //LOD0Slider.value = LOD0;
                //LOD2Slider.value = LOD2;
                LODValueChanged = true;
            }
        });
        LOD2Slider.onValueChanged.AddListener(delegate {
            if (!LODValueChanged)
            {
                LOD2 = Mathf.Min(99.999f, Mathf.Max(0.0001f, LOD2Slider.value));
                float newLOD0 = LOD0 / (LOD0 + LOD2 + 0.0001f) * (100f - LOD2);
                float newLOD1 = LOD1 / (LOD0 + LOD1 + 0.0001f) * (100f - LOD2);
                LOD0 = Mathf.Min(99.999f, Mathf.Max(0.0001f, newLOD0));
                LOD1 = Mathf.Min(99.999f, Mathf.Max(0.0001f, newLOD1));
                //LOD0Slider.value = LOD0;
                //LOD1Slider.value = LOD1;
                LODValueChanged = true;
            }
        });

        // real time settings
        TimeScaleSlider.value = grassRenderer.settings.timeScale;
        TimeScaleSlider.onValueChanged.AddListener(delegate {
            grassRenderer.settings.timeScale = TimeScaleSlider.value;
        });
    }
    private void LateUpdate()
    {
        if (LODValueChanged)
        {
            LOD0Slider.value = LOD0;
            LOD1Slider.value = LOD1;
            LOD2Slider.value = LOD2;
            LODValueChanged = false;
        }
    }
    /// <summary>
    /// Settings that need regeneration of the render pass
    /// </summary>
    public void ApplySettings()
    {
        grassRenderer.chunkGrassSize = ChunkSize;
        grassRenderer.LOD[1] = (int)(LOD1 * Chunks / 100f);
        grassRenderer.LOD[2] = (int)(LOD2 * Chunks / 100f);
        grassRenderer.LOD[0] = Chunks - grassRenderer.LOD[1] - grassRenderer.LOD[2];
        grassRenderer.ResetRenderPass();
    }
}
