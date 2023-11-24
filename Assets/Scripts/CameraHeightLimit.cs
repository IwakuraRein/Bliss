using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bliss
{
    public class CameraHeightLimit : MonoBehaviour
    {
        const float rayCastDelta = 0.05f;
        [SerializeField]
        float heightAboveGround = 2.0f;

        void LateUpdate()
        {
            Vector2 coord = new Vector2(transform.position.x, transform.position.z);
            float height = TerrainData.GetHeight(coord);
            height = Mathf.Max(height, TerrainData.GetHeight(coord + new Vector2(rayCastDelta, 0)));
            height = Mathf.Max(height, TerrainData.GetHeight(coord + new Vector2(rayCastDelta, rayCastDelta)));
            height = Mathf.Max(height, TerrainData.GetHeight(coord + new Vector2(-rayCastDelta, 0)));
            height = Mathf.Max(height, TerrainData.GetHeight(coord + new Vector2(-rayCastDelta, rayCastDelta)));
            transform.position = new Vector3(transform.position.x, height + heightAboveGround, transform.position.z);
        }
    }
}