using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{

    private Animator _animator;

    void Start()
    {
        _animator = GetComponent<Animator>();
    }

    void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S) || OVRInput.Get(OVRInput.RawButton.LThumbstickUp) || OVRInput.Get(OVRInput.RawButton.LThumbstickDown))
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger) || OVRInput.Get(OVRInput.Button.SecondaryIndexTrigger))
                _animator.SetBool("isRun", true);
            else
                _animator.SetBool("isWalk", true);
        }
        else
        {
            _animator.SetBool("isRun", false);
            _animator.SetBool("isWalk", false);
        }

        if (Input.GetKey(KeyCode.V))
            _animator.SetBool("isJump", true);
        else
            _animator.SetBool("isJump", false);
    }
}