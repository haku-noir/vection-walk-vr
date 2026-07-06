using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 直進歩行実験用のクラス
/// </summary>
/// <remarks>
/// WalkingCourse クラスとの違いは，こちらの方が機能が多くより詳細なデータが計測可能ですが，直線経路でなければ意味のないものも多いです．
/// </remarks>
public class Walking : MonoBehaviour
{
    private List<float> timeLog = new List<float>(2048);
    private List<Vector3> positionLog = new List<Vector3>(2048);
    private List<Vector3> rotationLog = new List<Vector3>(2048);
    private List<Vector3> forwardLog = new List<Vector3>(2048);
    private List<float> angleLog = new List<float>(2048);
    private List<int> missLog = new List<int>(2048);
    private List<int> outLog = new List<int>(2048);
    private List<int> outframeLog = new List<int>(2048);

    private float start_time;
    [SerializeField] GameObject head;

    [SerializeField] private GameObject line;
    [SerializeField] private GameObject marker1;
    [SerializeField] private GameObject marker2;
    /// <summary>
    /// 画面に色を付けるため
    /// </summary>
    [Tooltip("画面に色を付けるための PostProcess")]
    private GameObject postprocess;

    private bool miss_flag = false;
    private int miss_count = 0;

    private int object_state_num = 1;
    private int phase_num = 0;

    private int fixed_count = 0;

    [SerializeField] private SetReversion _setreversion;
    [SerializeField] private Alerm _alerm;
    [SerializeField] private KeepLooking _keeplooking;

    // Start is called before the first frame update
    void Start()
    {
        start_time = Time.time;
        postprocess = GameObject.Find("PostProcessVolume");
        postprocess.SetActive(false);
        PauseGame();
    }

    private void FixedUpdate()
    {

        if (Time.timeScale == 1)
        {
            //ログを保存
            timeLog.Add(Time.time - start_time);
            //positionLog.Add(head.transform.localPosition);
            positionLog.Add(head.transform.position);
            rotationLog.Add(head.transform.localEulerAngles);
            forwardLog.Add(head.transform.forward);
            //headPosLog.Add(head.transform.position);
            angleLog.Add(_keeplooking.target_angle);
            missLog.Add(miss_count);
            outLog.Add(_keeplooking.out_count);
            outframeLog.Add(_keeplooking.out_frame);
            //print("euler:" + head.transform.localEulerAngles);
            //print("forward:" + head.transform.forward);
        }

        fixed_count = (fixed_count + 1) % 12;
        //print(miss_count);
        //print(Mathf.Abs(head.transform.position.x) > 0.2);
        if (Mathf.Abs(head.transform.position.x) > 0.2)
        {
            if (fixed_count > 10)
            {
                print("miss");
                NoticeMissing();
                if (!miss_flag)
                {
                    miss_count++;
                    miss_flag = true;
                }
            }
        }
        else
        {
            //_alerm.audioSource.loop = false;
            if (miss_flag)
            {
                miss_flag = false;
            }
        }
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
                positionLog.Clear();
                rotationLog.Clear();
                forwardLog.Clear();
                //headPosLog.Clear();
                angleLog.Clear();
                missLog.Clear();
                outLog.Clear();
                outframeLog.Clear();
                phase_num = 0;
                miss_count = 0; 
                miss_flag = false;

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

        // 進行方向表示
        if (OVRInput.GetDown(OVRInput.RawButton.LIndexTrigger))
        {
            if (object_state_num == 0)
            {
                line.SetActive(false);
                marker1.SetActive(true);
                marker2.SetActive(true);
                object_state_num = 1;
            } else if (object_state_num == 1)
            {
                line.SetActive(true);
                marker1.SetActive(false);
                marker2.SetActive(false);
                object_state_num = 2;
            }
            else
            {
                line.SetActive(true);
                marker1.SetActive(true);
                marker2.SetActive(true);
                object_state_num = 0;
            }
        }
        
    }

    private void NoticeMissing()
    {
        _alerm.PlaySound();
    }
    

    void PauseGame()
    {
        if (Time.timeScale == 0)
        {
            Time.timeScale = 1;
            postprocess.SetActive(false);
        }
        else
        {
            Time.timeScale = 0;
            postprocess.SetActive(true);
        }
    }

    // 終了プロセス
    void SaveAndQuitGame()
    {
        if (timeLog.Count > 200)
        {
            // アプリケーションが終了する際に、結果を日時付きのCSVに出力
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string LR = _setreversion.leftright_reversion ? "LR" : "";
            string UD = _setreversion.updown_reversion ? "UD" : "";
            string FB = _setreversion.frontback_reversion ? "FB" : "";
            string fileName = @"walk_results_" + LR + UD + FB + timestamp + ".csv";
            string folderName = Path.Combine("ResultData", "walk");
            string filePath;
#if UNITY_EDITOR
            filePath = Path.Combine(Application.dataPath, folderName, fileName);
#else
            filePath = Path.Combine(Application.persistentDataPath, fileName);
#endif

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                //HMDの軌跡を記録
                writer.WriteLine("time,posX,posY,posZ,rotX,rotY,rotZ,forX,forY,forZ,angle,miss,out,outf");
                for (int i = 0; i < rotationLog.Count; i++)
                {
                    writer.WriteLine(timeLog[i] + "," + positionLog[i].x + "," + positionLog[i].y + "," + positionLog[i].z + "," +
                                     CenterRotValue(rotationLog[i].x) + "," + CenterRotValue(rotationLog[i].y) + "," + CenterRotValue(rotationLog[i].z) + "," +
                                     CenterRotValue(forwardLog[i].x) + "," + CenterRotValue(forwardLog[i].y) + "," + CenterRotValue(forwardLog[i].z) + "," +
                                     +angleLog[i] + "," + missLog[i] + "," + outLog[i] + "," + outframeLog[i]
                                     );
                }
                timeLog.Clear();
                positionLog.Clear();
                rotationLog.Clear();
                forwardLog.Clear();
                angleLog.Clear();
                missLog.Clear();
                outLog.Clear();
                outframeLog.Clear();
            }

            Debug.Log("Results saved to: " + filePath);
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

    //アプリ化すると使えない無能終了処理
//    void OnApplicationQuit()
//    {
//        if (phase_num > 1)
//        {
//            // アプリケーションが終了する際に、結果を日時付きのCSVに出力
//            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
//            string fileName = @"walk_results_" + timestamp + ".csv";
//            string folderName = Path.Combine("ResultData", "walk");
//            string filePath;
//#if UNITY_EDITOR
//            filePath = Path.Combine(Application.dataPath, folderName, fileName);
//#else
//            filePath = Path.Combine(Application.persistentDataPath, fileName);
//#endif

//            using (StreamWriter writer = new StreamWriter(filePath))
//            {
//                //HMDの軌跡を記録
//                writer.WriteLine("time,phase,posX,posY,posZ,rotX,rotY,rotZ");
//                for (int i = 0; i < rotationLog.Count; i++)
//                {
//                    writer.WriteLine(timeLog[i] + "," + angleLog[i] + "," + positionLog[i].x + "," + positionLog[i].y + "," + positionLog[i].z + "," +
//                                     CenterRotValue(rotationLog[i].x) + "," + CenterRotValue(rotationLog[i].y) + "," + CenterRotValue(rotationLog[i].z));
//                }
//                timeLog.Clear();
//                positionLog.Clear();
//                rotationLog.Clear();
//                angleLog.Clear();
//            }

//            Debug.Log("Results saved to: " + filePath);
//        }
//    }

    float CenterRotValue(float value)
    {
        if (value > 180)
        {
            value = value - 360;
        }
        return value;
    }
}

