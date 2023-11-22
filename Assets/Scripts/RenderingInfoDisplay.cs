using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Bliss;

public class RenderingInfoDisplay : MonoBehaviour
{
    [SerializeField]
    GrassRenderer grassRenderer;
    [SerializeField]
    Text text;
    [SerializeField]
    float UpdateInterval = 0.5f; // Update the FPS text every 0.5 seconds
    float timeLeft;
    float deltaTime = 0f;
    int frames = 0;

    int FPS = 0;
    int GrassNum = 0;
    private void Reset()
    {
        text = GetComponent<Text>();
    }

    private void Start()
    {
        if (text == null)
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
            FPS = (int)Mathf.Ceil(1.0f / deltaTime) * frames;
            timeLeft = UpdateInterval;
            deltaTime = 0f;
            frames = 0;
        }

        GrassNum = grassRenderer.DrawNum;

        text.text = $"FPS: {FPS}\nInstance Number: {GrassNum}";
    }
}
