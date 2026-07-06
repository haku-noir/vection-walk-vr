using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 反転時のメニュー画面の管理用クラス．
/// </summary>
public class MenuForReversion : MonoBehaviour
{
    private GameObject camerarig;
    private OVRCameraRig rigscript;
    private bool rig_change_flag = false; // UIは必ずCenterEyeAnchorで見させるのでその管理用

    [SerializeField] SetReversion _setreversion;

    /// <summary>
    /// メニュー用UI
    /// </summary>
    [Tooltip("メニュー用UI")]
    [SerializeField] GameObject menuPanel;
    public bool open_menu = false;
    private bool menu_flag = false;
    /// <summary>
    /// 右手から伸びる光線
    /// </summary>
    [Tooltip("右手から伸びる光線")]
    [SerializeField] LineRenderer lineRenderer;
    /// <summary>
    /// HMD に出力する映像を撮影するカメラ
    /// </summary>
    /// <remarks>
    /// CenterEyeAnchor
    /// </remarks>
    [Tooltip("HMD に出力する映像を撮影するカメラ")]
    [SerializeField] Camera seeingCamera;
    //[SerializeField] Camera captureCamera;

    // ここは今使っていない？
    /// <summary>
    /// 左目視野用UI（非対応）
    /// </summary>
    [Tooltip("左目視野用UI（非対応）")]
    public GameObject leftcan;
    /// <summary>
    /// 両目共通視野用UI
    /// </summary>
    [Tooltip("両目共通視野用UI")]
    public GameObject rightcan;
    /// <summary>
    /// 右眼視野用UI（非対応）
    /// </summary>
    [Tooltip("右目視野用UI（非対応）")]
    public GameObject centcan;

    /// <summary>
    /// メニューを上手くクリックできないときは，周りの世界を丸ごとここに入れてメニュー表示時に消してやるといいかもしれない．
    /// </summary>
    [Tooltip("メニューを上手くクリックできないときは，周りの世界を丸ごとここに入れてメニュー表示時に消してやるといいかもしれない．")]
    [SerializeField] GameObject obstacles;

    // Start is called before the first frame update
    void Start()
    {
        camerarig = GameObject.Find("OVRCameraRig");
        rigscript = camerarig.GetComponent<OVRCameraRig>();
        menuPanel.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if ((OVRInput.GetDown(OVRInput.RawButton.B) || Input.GetKeyDown(KeyCode.P) || open_menu == true) && menu_flag == false)
        {
            openMenu();
        }
        else if ((OVRInput.GetDown(OVRInput.RawButton.B) || Input.GetKeyDown(KeyCode.P) || open_menu == false) && menu_flag == true)
        {
            closeMenu();
        }
    }
    
    public void openMenu()
    {
        if (open_menu == false)
            open_menu = true;
        menu_flag = true;

        menuPanel.SetActive(true);
        lineRenderer.enabled = menuPanel.activeSelf;
        //captureCamera.enabled = false;
        seeingCamera.cullingMask |= (1 << 13);
        Debug.Log("Menu Opened");

        // 必ずCenterEyeAnchorで見る
        if (rigscript.usePerEyeCameras)
        {
            rigscript.usePerEyeCameras = false;
            rig_change_flag = true;
        }

        //centcan.SetActive(false);
        //leftcan.SetActive(false);
        //rightcan.SetActive(false);
        switchObstacle(obstacles);
    }

    public void closeMenu()
    {
        if (open_menu == true)
            open_menu = false;
        menu_flag = false;

        menuPanel.SetActive(false);
        lineRenderer.enabled = menuPanel.activeSelf;
        //captureCamera.enabled = true;
        seeingCamera.cullingMask &= ~(1 << 13);
        Debug.Log("Menu Closed");

        if (rig_change_flag)
        {
            rigscript.usePerEyeCameras = true;
            rig_change_flag = false;
        }

        //centcan.SetActive(true);
        //leftcan.SetActive(true);
        //rightcan.SetActive(true);
        switchObstacle(obstacles);
    }

    /// <summary>
    /// アクティブ状態を切り替える
    /// </summary>
    private void switchObstacle(GameObject obstacle)
    {
        obstacle.SetActive(obstacle.activeSelf ? false : true);
    }
}
