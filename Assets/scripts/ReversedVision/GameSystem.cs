using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// ゲームのセーブや終了等のシステム面に関するクラスです．
/// このクラスを継承してもよいでしょう．（動作確認できていません．）
/// 既存のクラスは何らかの想定外のエラーが起こる可能性を考慮し，継承していません．
/// </summary>
public class GameSystem : MonoBehaviour
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
    private GameObject postprocess;

    /// <summary>
    /// ゲームを一時停止します．
    /// </summary>
    void PauseGame(GameObject postprocess)
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
    /// <param name="save_identifier"> セーブするファイルの頭につける識別用の名前 </param>
    /// <param name="folder"> セーブ先の ResultData 内のフォルダー名 </param>
    void SaveAndQuitGame(string save_identifier = "experiment", string folder = "any")
    {
        if (timeLog.Count > 100)
        {
            // アプリケーションが終了する際に、結果を日時付きのCSVに出力
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = save_identifier + "_results_" + timestamp + ".csv";
            string folderName = Path.Combine("ResultData", folder);
            string filePath;
#if UNITY_EDITOR
            filePath = Path.Combine(Application.dataPath, folderName, fileName);
#else
            filePath = Path.Combine(Application.persistentDataPath, fileName);
#endif

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                //HMDの軌跡を記録
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

    /// <summary>
    /// アプリケーション終了時に呼ばれる関数です．
    /// </summary>
    /// <remarks>
    /// ここでの中身は SaveAndQuitGame 関数とほぼ同じです．
    /// しかしながら，アプリ化して HMD に入れた場合は呼ばれないことが多く使えません．
    /// </remarks>
    void OnApplicationQuit()
    {
        if (timeLog.Count > 100)
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
                //HMDの軌跡を記録
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
    }

    /// <summary>
    /// 0°～360° を -180°～180° に写像します．全体のシフトではなく， 180～360 が -180～0 になります．
    /// </summary>
    /// <param name="value">角度の値[deg]</param>
    /// <returns>-180°～180°に変換した角度</returns>
    float CenterRotValue(float value)
    {
        if (value > 180)
        {
            value = value - 360;
        }
        return value;
    }
}
