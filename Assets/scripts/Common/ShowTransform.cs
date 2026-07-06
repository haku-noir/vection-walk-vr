using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class ShowTransform : MonoBehaviour
{
    [SerializeField] private GameObject head;
    [SerializeField] private Text headPosText;
    [SerializeField] private Text headRotText;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        headPosText.text = head.transform.position.ToString();
        float rotX = head.transform.localEulerAngles.x;
        float rotY = head.transform.localEulerAngles.y;
        float rotZ = head.transform.localEulerAngles.z;
        Vector3 rot = new Vector3(CenterRotValue(rotX), CenterRotValue(rotY), CenterRotValue(rotZ));
        headRotText.text = rot.ToString();
        headRotText.text = CenterRotValue(rotY).ToShortString();
    }

    float CenterRotValue(float value)
    {
        if (value > 180)
        {
            value = value - 360;
        }
        return value;
    }
}
