using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MenuController : MonoBehaviour
{
    [SerializeField]
    private GameObject canvasObj;

    [SerializeField]
    private LineRenderer lineRenderer;

    [SerializeField] GameObject obstacle;

    void Start()
    {
        canvasObj.SetActive(lineRenderer.enabled);
    }

    void Update()
    {
        if (OVRInput.GetDown(OVRInput.RawButton.B) || Input.GetKeyDown(KeyCode.P))
        {
            SwitchActivateCanvas();
        }

    }

    /// <summary>
    /// Canvas ‚Ì•\Ž¦/”ñ•\Ž¦
    /// </summary>
    private void SwitchActivateCanvas()
    {
        canvasObj.SetActive(canvasObj.activeSelf ? false : true);
        lineRenderer.enabled = canvasObj.activeSelf;
        obstacle.SetActive(obstacle.activeSelf ? false : true);
    }
}