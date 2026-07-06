using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForIntroduction : MonoBehaviour
{
    // Start is called before the first frame update
    int lr_pos;
    int ud_pos;
    int fb_pos;
    int roll;
    int yaw;
    int pitch;
    public float scale = 1;


    void Start()
    {

        lr_pos = 1;
        ud_pos = 0;
        fb_pos = 0;
        roll = 0;
        yaw = 0;
        pitch = 0;
    }

    private void Update()
    {
        if (lr_pos == 1 && transform.localPosition.x >= 1)
        {
            lr_pos = -1;
        }
        if (lr_pos == -1 && transform.localPosition.x < 0)
        {
            lr_pos = 0;
            ud_pos = 1;
        }
        if (ud_pos == 1 && transform.localPosition.y >= 1)
        {
            ud_pos = -1;
        }
        if (ud_pos == -1 && transform.localPosition.y < 0)
        {
            ud_pos = 0;
            fb_pos = 1;
        }
        if (fb_pos == 1 && transform.localPosition.z >= 1)
        {
            fb_pos = -1;
        }
        if (fb_pos == -1 && transform.localPosition.z < 0)
        {
            fb_pos = 0;
            roll = 1;
        }
        if (roll == 1 && transform.localEulerAngles.z >= 45)
        {
            roll = -1;
        }
        if (roll == -1 && transform.localEulerAngles.z < 1) 
        { 
            roll = 0;
            yaw = 1;
        }
        if (yaw == 1 && transform.localEulerAngles.y >= 45)
        {
            yaw = -1;
        }
        if (yaw == -1 && transform.localEulerAngles.y < 1)
        {
            yaw = 0;
            pitch = 1;
        }
        if (pitch == 1 && transform.localEulerAngles.x >= 45)
        {
            pitch = -1;
        }
        if (pitch == -1 && transform.localEulerAngles.x < 1)
        {
            pitch = 0;
            lr_pos = 1;
        }
        Debug.Log($"{lr_pos}, {ud_pos}, {fb_pos}, {roll}, {yaw}, {pitch}");
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        Vector3 pos = transform.localPosition;
        pos.x += lr_pos * 0.005f;
        pos.y += ud_pos * 0.005f;
        pos.z += fb_pos * 0.005f;
        transform.localPosition = scale * pos;

        Vector3 rot = transform.localEulerAngles;
        rot.x += pitch * 0.2f;
        rot.y += yaw * 0.2f;
        rot.z += roll * 0.2f;
        transform.localEulerAngles = scale * rot;
    }
}
