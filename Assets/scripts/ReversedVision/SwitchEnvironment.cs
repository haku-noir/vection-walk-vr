using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// シーンを切り替えるためのクラス
/// </summary>
public class SwitchEnvironment : MonoBehaviour
{
    private void Update()
    {
        if (OVRInput.GetDown(OVRInput.RawButton.LIndexTrigger))
        {
            ChangeScene();
        }
    }

    /// <summary>
    /// シーンを切り替えるための関数
    /// </summary>
    public void ChangeScene()
    {
        if (SceneManager.GetActiveScene().name == "reverse")
            SceneManager.LoadScene("the last revelation");
        if (SceneManager.GetActiveScene().name == "the last revelation")
            SceneManager.LoadScene("reverse");
    }
}