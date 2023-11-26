using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShowSliderValue : MonoBehaviour
{
    [SerializeField]
    Slider slider;
    [SerializeField]
    Text text;
    // Start is called before the first frame update
    void Start()
    {
        if (text == null)
            text = GetComponent<Text>();
        if (slider == null)
            slider = transform.parent.GetComponent<Slider>();
    }

    // Update is called once per frame
    void Update()
    {
        text.text = (slider.value).ToString();
    }
}
