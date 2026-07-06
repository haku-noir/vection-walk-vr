using OVRTouchSample;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

/// <summary>
/// HMD のトラッキングの様子を見るためのクラス
/// </summary>
public class LogRecording : MonoBehaviour
{
    private float start_time;
    private List<float> timeLog = new List<float>(4096);
    private List<float> realTimeLog = new List<float>(4096);
    private List<Vector3> positionLog = new List<Vector3>(4096);
    private List<Vector3> rotationLog = new List<Vector3>(4096);

    private List<Vector3> headPosLog = new List<Vector3>(4096);
    private List<Vector3> LhandPosLog = new List<Vector3>(4096);
    private List<Vector3> LhandRotLog = new List<Vector3>(4096);
    private List<Vector3> RhandPosLog = new List<Vector3>(4096);
    private List<Vector3> RhandRotLog = new List<Vector3>(4096);

    [SerializeField] GameObject head;
    [SerializeField] GameObject Lhand;
    [SerializeField] GameObject Rhand;

    public GameObject canvas;
    public GameObject backImage;
    public Text headPosText;
    public Text headRotText;
    public Text LhandPosText;
    public Text RhandPosText;
    public Text PauseText;
    public GameObject floor;

    float timeLimit = 3600000;
    public bool showLog = true;
    public bool whiteBackground = true;

    // Start is called before the first frame update
    void Start()
    {
        start_time = Time.time;
        Debug.Log(PauseText.text);
        PauseText.enabled = false;

        //head.transform.rotation = Quaternion.Euler(0, 0, 30) * Quaternion.Euler(20, 0, 0) * head.transform.rotation;
    }

    // Update is called once per frame
    void Update()
    {
        if (OVRInput.GetDown(OVRInput.RawButton.B) || Input.GetKeyDown(KeyCode.O))
        {
            if (Time.timeScale == 0)
            {
                PauseGame();
                timeLog.Clear();
                realTimeLog.Clear();
                positionLog.Clear();
                rotationLog.Clear();
                headPosLog.Clear();
                LhandPosLog.Clear();
                LhandRotLog.Clear();
                RhandPosLog.Clear();
                RhandRotLog.Clear();

                start_time = Time.time;
            }
            else
            {
                start_time = -1f;
                PauseGame();
            }
        }

        if (Time.timeScale == 0 && OVRInput.GetDown(OVRInput.RawButton.RHandTrigger))
        {
            SaveAndQuitGame();
        }

        canvas.SetActive(showLog ? true : false);
        backImage.SetActive(whiteBackground ? true : false);
        floor.SetActive((!canvas.activeSelf || !backImage.activeSelf) ? true : false);

        if (Time.time - start_time > timeLimit)
        {
            PauseGame();
            SaveAndQuitGame();
            showLog = true;
            whiteBackground = true;
        }
    }

    private void FixedUpdate()
    {

        System.DateTime now = System.DateTime.Now;
        long milliseconds = now.Ticks / System.TimeSpan.TicksPerMillisecond;

        timeLog.Add(Time.time - start_time);
        realTimeLog.Add(milliseconds);
        positionLog.Add(head.transform.localPosition);
        rotationLog.Add(head.transform.localEulerAngles);
        headPosLog.Add(head.transform.position);
        LhandPosLog.Add(Lhand.transform.position);
        LhandRotLog.Add(Lhand.transform.localEulerAngles);
        RhandPosLog.Add(Rhand.transform.position);
        RhandRotLog.Add(Rhand.transform.localEulerAngles);

        headPosText.text = head.transform.position.ToString();
        //headPosText.text = head.transform.eulerAngles.ToString();
        headRotText.text = head.transform.localEulerAngles.ToString();
        LhandPosText.text = Lhand.transform.position.ToString();
        RhandPosText.text = Rhand.transform.position.ToString();

        Debug.Log("quat: " + head.transform.rotation);
        Debug.Log("x: " + head.transform.right);
        Debug.Log("y: " + head.transform.up);
        Debug.Log("z: " + head.transform.forward);

    }

    void PauseGame()
    {
        if (Time.timeScale == 0)
        {
            Time.timeScale = 1;
            PauseText.text = "-Pause-";
            PauseText.enabled = false;
        }
        else
        {
            Time.timeScale = 0;
            PauseText.enabled = true;
        }
    }

    void SaveAndQuitGame()
    {
        if (realTimeLog.Count > 500)
        {
            // アプリケーションが終了する際に、結果を日時付きのCSVに出力
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = @"TrackingAccuracyLog" + timestamp + ".csv";
            string folderName = Path.Combine("ResultData");
            string filePath;
#if UNITY_EDITOR
            filePath = Path.Combine(Application.dataPath, folderName, fileName);
#else
            filePath = Path.Combine(Application.persistentDataPath, fileName);
#endif

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                //HMDの軌跡を記録
                writer.WriteLine("time,realtime,posX,posY,posZ,rotX,rotY,rotZ,headPosX,headPosY,headPosZ,LhandPosX,LhandPosY,LhandPosZ,LhandRotX,LhandRotY,LhandRotZ,RhandPosX,RhandPosY,RhandPosZ,RhandRotX,RhandRotY,RhandRotZ");
                for (int i = 0; i < timeLog.Count; i++)
                {
                    writer.WriteLine(timeLog[i] + "," + realTimeLog[i] + "," + 
                        positionLog[i].x + "," + positionLog[i].y + "," + positionLog[i].z + "," +
                        CenterRotValue(rotationLog[i].x) + "," + CenterRotValue(rotationLog[i].y) + "," + CenterRotValue(rotationLog[i].z) + "," +
                        headPosLog[i].x + "," + headPosLog[i].y + "," + headPosLog[i].z + "," +
                        LhandPosLog[i].x + "," + LhandPosLog[i].y + "," + LhandPosLog[i].z + "," +
                        CenterRotValue(LhandRotLog[i].x) + "," + CenterRotValue(LhandRotLog[i].y) + "," + CenterRotValue(LhandRotLog[i].z) + "," +
                        RhandPosLog[i].x + "," + RhandPosLog[i].y + "," + RhandPosLog[i].z + "," +
                        CenterRotValue(RhandRotLog[i].x) + "," + CenterRotValue(RhandRotLog[i].y) + "," + (RhandRotLog[i].z) );
                }
                timeLog.Clear();
                realTimeLog.Clear();
                positionLog.Clear();
                rotationLog.Clear();
                headPosLog.Clear();
                LhandPosLog.Clear();
                LhandRotLog.Clear();
                RhandPosLog.Clear();
                RhandRotLog.Clear();
            }

            Debug.Log("Results saved to: " + filePath);
            PauseText.text = "Saved";
        }
        else
        {
            Debug.Log("Quit game.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;   // UnityEditorの実行を停止する処理
#else
            Application.Quit();                                // ゲームを終了する処理
#endif
        }
    }

    float CenterRotValue(float value)
    {
        if (value > 180)
        {
            value = value - 360;
        }
        return value;
    }
}
