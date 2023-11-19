using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bliss
{
    public class CameraMovement : MonoBehaviour
    {
        const float rayCastDelta = 0.05f;
        [SerializeField]
        float moveSpeed = 5.0f;
        [SerializeField]
        float heightAboveGround = 2.0f;

        void Update()
        {
            float horizontalInput = Input.GetAxis("Horizontal");
            float verticalInput = Input.GetAxis("Vertical");

            Vector3 moveDirection = new Vector3(horizontalInput, 0.0f, verticalInput) * moveSpeed * Time.deltaTime;
            transform.Translate(moveDirection, Space.Self);

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