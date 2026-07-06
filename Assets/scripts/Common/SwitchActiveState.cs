using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwitchActiveState : MonoBehaviour
{
    public GameObject targetObject;
    public bool activeState;
    void Update()
    {
        targetObject.SetActive(activeState);
    }
}
