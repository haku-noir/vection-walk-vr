using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class test_camera_transform : MonoBehaviour
{
    Matrix4x4 mat1;
    // Start is called before the first frame update
    void Start()
    {
        mat1 = Matrix4x4.identity;
        mat1.m03 = 2;
        mat1.m13 = 3;
        mat1.m23 = 4;
        Camera.main.projectionMatrix = Camera.main.projectionMatrix * mat1;
    }

    // Update is called once per frame
    void Update()
    {
        Camera.main.projectionMatrix = Camera.main.projectionMatrix * mat1;
    }
}
