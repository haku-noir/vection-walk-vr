using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// キャラクターをキーボードやコントローラで操作するためのクラス
/// </summary>
public class PlayerInput : MonoBehaviour
{
    //public float Angle = 90f;

    public float DefaultSpeed = 1f;
    public float DashSpeed = 2f;
    public float SlowSpeed = 0.5f;

    public float jumpPower = 50;

    private Rigidbody rb;

    //public bool cameraExchange_notImplemented = false;

    private Vector3 angular;
    public float maxRotationSpeed = 1.6f;
    private Vector3 velocity;

    float SpeedScale
    {
        get
        {
            return 5f * (IsPressTrigger ? DashSpeed : IsPressGrip ? SlowSpeed : DefaultSpeed);
        }
    }

    bool IsPressTrigger
    {
        get
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || OVRInput.Get(OVRInput.RawButton.A);
        }
    }
    bool IsPressGrip
    {
        get
        {
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.LeftAlt) || OVRInput.Get(OVRInput.Button.SecondaryHandTrigger);
        }
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        angular = Vector3.zero;
        velocity = Vector3.zero;
}

    void Update()
    {
        //rotate
        float horizontalRotationInput = ((Input.GetKey(KeyCode.A) || OVRInput.Get(OVRInput.RawButton.RThumbstickLeft)) ? -1f : 0f)
                                                + ((Input.GetKey(KeyCode.D) || OVRInput.Get(OVRInput.RawButton.RThumbstickRight)) ? 1f : 0f);

        if (Mathf.Abs(horizontalRotationInput) > 0)
        {
            angular += Vector3.up * horizontalRotationInput * Mathf.PI * Time.deltaTime;
            angular = Vector3.ClampMagnitude(angular, maxRotationSpeed);
        }
        else if (angular.magnitude < 2)
        {
            angular = Vector3.zero;
        }
        else
        {
            angular -= angular.normalized * 1f * Mathf.PI * Time.deltaTime; // なんかおかしい（減衰が強い）
        }
        transform.eulerAngles += Mathf.Rad2Deg * angular * Time.deltaTime;


        // move
        Vector2 vectorL = Vector2.zero;
        vectorL += OVRInput.Get(OVRInput.RawAxis2D.LThumbstick);
        float forwardMovementInput = Input.GetKey(KeyCode.W) ? 1f : Input.GetKey(KeyCode.S) ? -1f : 0f;
        Vector3 inputVector = transform.TransformDirection( new Vector3(vectorL.x, 0, vectorL.y + forwardMovementInput));
        if (inputVector.magnitude > 0 && velocity.magnitude <= SpeedScale)
        {
            //velocity += inputVector * SpeedScale * Time.deltaTime;
            //velocity = Vector3.ClampMagnitude(velocity, SpeedScale);
            velocity = inputVector * SpeedScale;
        }
        else if (velocity.magnitude < 0.1f)
        {
            velocity = Vector3.zero;
        }
        else
        {
            velocity -= velocity.normalized * 20f * Time.deltaTime;
        }
        //rb.velocity = velocity;
        //Debug.Log(velocity + ":" + Time.deltaTime);
        transform.position += velocity * Time.deltaTime;

        //// Down rotate
        //if (OVRInput.Get(OVRInput.RawButton.RThumbstickUp))
        //{
        //    transform.Rotate(Angle * Time.deltaTime, 0, 0);
        //}
        //// Up rotate
        //if (OVRInput.Get(OVRInput.RawButton.RThumbstickDown))
        //{
        //    transform.Rotate(-Angle * Time.deltaTime, 0, 0);
        //}
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

