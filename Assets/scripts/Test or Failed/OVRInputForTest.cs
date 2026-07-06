using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OVRInputForTest : Photon.Pun.MonoBehaviourPun
{
    public float Angle = 90f;

    public float WalkSpeed = 1f;
    public float DashSpeed = 2f;
    public float SlowSpeed = 0.5f;

    public float jumpPower = 50;

    private Rigidbody rb;

    public bool cameraExchange_notImplemented = false;

    //public OVRInput.Controller dominantHand = OVRInput.Controller.LTouch;
    //public OVRInput.Controller otherHand = OVRInput.Controller.RTouch;

    //void Reset()
    //{
    //    Head = GetComponentInChildren<OVRCameraRig>().transform.Find("TrackingSpace/CenterEyeAnchor");
    //}

    float Scale
    {
        get
        {
            return IsPressTrigger ? DashSpeed : IsPressGrip ? SlowSpeed : WalkSpeed;
        }
    }

    bool IsPressTrigger
    {
        get
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
            || OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger) || OVRInput.Get(OVRInput.Button.SecondaryIndexTrigger);
        }
    }
    bool IsPressGrip
    {
        get
        {
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.LeftAlt)
            || OVRInput.Get(OVRInput.Button.PrimaryHandTrigger) || OVRInput.Get(OVRInput.Button.SecondaryHandTrigger);
        }
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        //if (!photonView.IsMine)
        //{
        //    if (!cameraExchange_notImplemented)
        //        foreach(Camera cam in GetComponentsInChildren<Camera>())
        //        Destroy(GetComponentInChildren<Camera>().gameObject);
        //}
    }

    void Update()
    {
        // é©ï™ÇÃëÄçÏÇÃÇ›îΩâf
        //if (!photonView.IsMine)
        //{
        //    return;
        //}

        // move
        Vector2 vectorL = OVRInput.Get(OVRInput.RawAxis2D.LThumbstick);
        transform.position += new Vector3(vectorL.x, 0, vectorL.y) * Time.deltaTime * Scale;

        // Forward move
        if (Input.GetKey(KeyCode.W))
        {
            transform.position += transform.forward * Time.deltaTime * Scale;
        }
        // Back move
        if (Input.GetKey(KeyCode.S))
        {
            transform.position -= transform.forward * Time.deltaTime * Scale;
        }

        // Left rotate
        if (Input.GetKey(KeyCode.A) || OVRInput.Get(OVRInput.RawButton.RThumbstickLeft))
        {
            transform.Rotate(0, -Angle * Time.deltaTime, 0);
        }
        // Right rotate
        if (Input.GetKey(KeyCode.D) || OVRInput.Get(OVRInput.RawButton.RThumbstickRight))
        {
            transform.Rotate(0, Angle * Time.deltaTime, 0);
        }
        // Down rotate
        if (OVRInput.Get(OVRInput.RawButton.RThumbstickUp))
        {
            transform.Rotate(Angle * Time.deltaTime, 0, 0);
        }
        // Up rotate
        if (OVRInput.Get(OVRInput.RawButton.RThumbstickDown))
        {
            transform.Rotate(-Angle * Time.deltaTime, 0, 0);
        }
        // Camera reset
        if (OVRInput.GetDown(OVRInput.RawButton.RThumbstick))
        {
            transform.rotation = Quaternion.identity;
        }

        // Jump (infinity)
        if (Input.GetKeyDown(KeyCode.V))
        {
            //rb.AddForce(transform.up * jumpPower * transform.localScale.y, ForceMode.Impulse);
            //rb.velocity = Vector3.up * 20 * transform.localScale.y;
            float g = Mathf.Abs(Physics.gravity.y);
            rb.velocity = Vector3.up * 0.01f * jumpPower * transform.localScale.y * 2 * Mathf.Pow(g, 3) / (2 * Mathf.Pow(g, 2) - 1);
        }
    }
}

