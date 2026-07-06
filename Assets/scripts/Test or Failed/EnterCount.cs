using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnterCount : MonoBehaviour
{
    [SerializeField] private Walking _walking;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            //_walking.CountUp();
        }
    }
}
