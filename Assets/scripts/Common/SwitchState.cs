using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwitchState : MonoBehaviour
{
    public GameObject obj1;
    public GameObject obj2;
    // Start is called before the first frame update
    void Start()
    {
        obj1.SetActive(true);
        obj2.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P)){
            bool onflag = obj1.active;
            if (onflag)
            {
                obj1.SetActive(false);
                obj2.SetActive(true);
            }
            else
            {
                obj1.SetActive(true);
                obj2.SetActive(false);
            }
        }
        
    }
}
