using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 歩行実験に関する汎用クラス
/// </summary>
/// <remarks>
/// Walkingクラスとの違いは，こちらの方が機能を少なくすることで汎用性を高めています．
/// </remarks>
public class WalkingCourse : MonoBehaviour
{
    private List<float> interactionTimes = new List<float>(128);
    private List<float> timeLog = new List<float>(2048);
    private List<Vector3> positionLog = new List<Vector3>(2048);
    private List<Vector3> rotationLog = new List<Vector3>(2048);
    private List<Vector3> forwardLog = new List<Vector3>(2048);
    private List<int> phaseLog = new List<int>(2048);
    private List<int> missLog = new List<int>(2048);

    private float start_time;
    [SerializeField] GameObject head;
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

    //[SerializeField] private Alerm _alerm;

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
            phaseLog.Add(phase_num);
            missLog.Add(miss_count);
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
                // ログ等を初期化
                interactionTimes.Clear();
                timeLog.Clear();
                positionLog.Clear();
                rotationLog.Clear();
                //headPosLog.Clear();
                phaseLog.Clear();
                missLog.Clear();
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

        
    }

    //private void NoticeMissing()
    //{
    //    _alerm.PlaySound();
    //}

    // 今は使っていない
    public void CountUp()
    {
        phase_num++;
        print("count");
    }

    /// <summary>
    /// ゲームを一時停止します．
    /// </summary>
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
            //SaveAndQuitGame();
        }
    }

    /// <summary>
    /// セーブしてゲームを終了します．
    /// </summary>
    void SaveAndQuitGame()
    {
        if (phase_num > -1)
        {
            // アプリケーションが終了する際に、結果を日時付きのCSVに出力
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = @"walk_results_" + timestamp + ".csv";
            string folderName = Path.Combine("ResultData", "walk");
            string filePath;
#if UNITY_EDITOR
            filePath = Path.Combine(Application.dataPath, folderName, fileName);
#else
            filePath = Path.Combine(Application.persistentDataPath, fileName);
#endif

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                //HMDの軌跡を記録（追加するデータは Walking クラスの方が多い．こちらは最小限．）
                writer.WriteLine("time,phase,posX,posY,posZ,rotX,rotY,rotZ");
                for (int i = 0; i < rotationLog.Count; i++)
                {
                    writer.WriteLine(timeLog[i] + "," + phaseLog[i] + "," + positionLog[i].x + "," + positionLog[i].y + "," + positionLog[i].z + "," +
                                     CenterRotValue(rotationLog[i].x) + "," + CenterRotValue(rotationLog[i].y) + "," + CenterRotValue(rotationLog[i].z));
                }
                timeLog.Clear();
                positionLog.Clear();
                rotationLog.Clear();
                forwardLog.Clear();
                phaseLog.Clear();
                missLog.Clear();
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
    //                    writer.WriteLine(timeLog[i] + "," + phaseLog[i] + "," + positionLog[i].x + "," + positionLog[i].y + "," + positionLog[i].z + "," +
    //                                     CenterRotValue(rotationLog[i].x) + "," + CenterRotValue(rotationLog[i].y) + "," + CenterRotValue(rotationLog[i].z));
    //                }
    //                timeLog.Clear();
    //                positionLog.Clear();
    //                rotationLog.Clear();
    //                phaseLog.Clear();
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

