using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FPSDisplay : MonoBehaviour
{
    [SerializeField]
    Text fpsText;
    [SerializeField]
    float UpdateInterval = 0.5f; // Update the FPS text every 0.5 seconds
    float timeLeft;
    float deltaTime = 0f;
    int frames = 0;
    private void Reset()
    {
        fpsText = GetComponent<Text>();
    }

    private void Start()
    {
        if (fpsText == null)
        {
            Debug.LogError("Please assign the Text component to display FPS!");
            enabled = false;
            return;
        }
        timeLeft = UpdateInterval;
    }

    private void Update()
    {
        deltaTime += Time.unscaledDeltaTime;
        timeLeft -= Time.unscaledDeltaTime;
        frames ++;

        if (timeLeft <= 0)
        {
            fpsText.text = $"FPS: {Mathf.Ceil(1.0f / deltaTime) * frames}";
            timeLeft = UpdateInterval;
            deltaTime = 0f;
            frames = 0;
        }
    }
}
