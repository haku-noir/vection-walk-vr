using System.IO;
using System;
using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;
using System.Collections;

/// <summary>
/// ランダムな位置に球を生成するクラス
/// </summary>
public class SphereGenerator : MonoBehaviour
{
    public Camera mainCamera;
    /// <summary>
    /// 原点から生成する球までの距離
    /// </summary>
    [Tooltip("原点から生成する球までの距離")]
    public float radiusOfHemisphere = 5f; // 半球の半径
    /// <summary>
    /// 生成する球の半径
    /// </summary>
    [Tooltip("生成する球の半径")]
    public float radiusOfSphere = 0.5f; // 生成する球の半径
    /// <summary>
    /// 生成する球の色
    /// </summary>
    [Tooltip("生成する球の色")]
    public Color sphere_color = new Color(255f / 255f, 150f / 255f, 0f);

    /// <summary>
    /// 球が既に生成されているか否かの管理用フラグ
    /// </summary>
    [Tooltip("球が既に生成されているか否かの管理用フラグ")]
    private bool hasGenerated = false;

    [System.NonSerialized] public float start_time;
    private List<float> interactionTimes = new List<float>();
    private bool process_flag = false;

    private List<float> timeLog = new List<float>(2048);
    private List<Vector3> positionLog = new List<Vector3>(2048);
    private List<Vector3> rotationLog = new List<Vector3>(2048);

    [SerializeField] GameObject head;
    private float triggerTime = 0;

    /// <summary>
    /// 開始前に視点を合わせるための指標
    /// </summary>
    [Tooltip("開始前に視点を合わせるための指標")]
    private GameObject viewpoint_standard;
    /// <summary>
    /// 画面に色を付けるため
    /// </summary>
    [Tooltip("画面に色を付けるための PostProcess")]
    GameObject postprocess;

    /// <summary>
    /// 上下方向の生成領域[deg]
    /// </summary>
    [Tooltip("上下方向の生成領域[deg]")]
    public Vector2 eulerRangeOfTheta = new Vector2(50f, 90f);
    /// <summary>
    /// 左右方向の生成領域[deg]
    /// </summary>
    [Tooltip("左右方向の生成領域[deg]")]
    public Vector2 eulerRangeOfPhi = new Vector2(60f, 120f);
    public bool FrontBackReversed = false;
    /// <summary>
    /// 上下方向の生成領域[rad]
    /// </summary>
    private Vector2 rangeOfTheta;
    /// <summary>
    /// 左右方向の生成領域[rad]
    /// </summary>
    private Vector2 rangeOfPhi;

    /// <summary>
    /// 生成時の基準地点
    /// </summary>
    [Tooltip("生成時の基準地点")]
    public GameObject standingPoint;
    public float heightOfHemisphere = 0;
    private Vector3 playerPosition;

    public GameObject light;

    private int seed;

    private void Awake()
    {
        postprocess = GameObject.Find("PostProcessVolume");
        if (postprocess == null) print("null!");
        else print("not null");
    }

    private void Start()
    {
        seed = 121;
        UnityEngine.Random.InitState(seed);

        viewpoint_standard = GameObject.Find("standard");
        //print(postprocess);
        postprocess.SetActive(false);

        playerPosition = standingPoint.transform.position;

        // 前後反転時，生成位置やライトの当たり方を前後対称にする
        if (FrontBackReversed)
        {
            rangeOfTheta = Mathf.Deg2Rad * eulerRangeOfTheta;
            rangeOfPhi = Mathf.Deg2Rad * eulerRangeOfPhi + new Vector2(Mathf.PI, Mathf.PI);
            light.transform.position = new Vector3(light.transform.position.x, light.transform.position.y, -light.transform.position.z);
            light.transform.eulerAngles = new Vector3(light.transform.eulerAngles.x, 180, light.transform.eulerAngles.z);
        }
        else
        {
            rangeOfTheta = Mathf.Deg2Rad * eulerRangeOfTheta;
            rangeOfPhi = Mathf.Deg2Rad * eulerRangeOfPhi;
        }

        start_time = Time.time;
        //GenerateSphereOnHemisphere();
        //hasGenerated = true;

        standingPoint.GetComponent<Rigidbody>().useGravity = false;
        standingPoint.GetComponent<CapsuleCollider>().enabled = false;

        PauseGame();
        hasGenerated = false;
    }

    void Update()
    {
        playerPosition = standingPoint.transform.position + heightOfHemisphere * Vector3.up;
        // 度数法を弧度法に変換
        // 前後反転時，生成位置やライトの当たり方を前後対称にする
        if (FrontBackReversed)
        {
            rangeOfTheta = Mathf.Deg2Rad * eulerRangeOfTheta;
            rangeOfPhi = Mathf.Deg2Rad * eulerRangeOfPhi + new Vector2(Mathf.PI, Mathf.PI);
            light.transform.position = new Vector3(light.transform.position.x, light.transform.position.y, -Mathf.Abs(light.transform.position.z));
            light.transform.eulerAngles = new Vector3(light.transform.eulerAngles.x, 180, light.transform.eulerAngles.z);
        }
        else
        {
            rangeOfTheta = Mathf.Deg2Rad * eulerRangeOfTheta;
            rangeOfPhi = Mathf.Deg2Rad * eulerRangeOfPhi;
            light.transform.position = new Vector3(light.transform.position.x, light.transform.position.y, Mathf.Abs(light.transform.position.z));
            light.transform.eulerAngles = new Vector3(light.transform.eulerAngles.x, 0, light.transform.eulerAngles.z);
        }

        if (OVRInput.GetDown(OVRInput.RawButton.B) || Input.GetKeyDown(KeyCode.O))
        {
            if (!hasGenerated)
            {
                PauseGame();
                interactionTimes.Clear();
                timeLog.Clear();
                positionLog.Clear();
                rotationLog.Clear();
                start_time = Time.time;
                hasGenerated = true;
                GenerateSphereOnHemisphere();
            }
            else
            {
                try
                {
                    Destroy(GameObject.Find("Sphere"));
                }
                catch
                {
                    Debug.Log("No Sphere!");
                }
                finally
                {
                    start_time = -1f;
                    hasGenerated = false;
                    PauseGame();
                }
            }
        }

        // 0.1sおきにデータを記録
        if (Time.timeScale == 1 && head != null)
        {
            triggerTime += Time.deltaTime;
            if (hasGenerated && triggerTime > 0.1f && start_time >= 0)
            {
                timeLog.Add(Time.time - start_time);
                positionLog.Add(head.transform.localPosition);
                rotationLog.Add(head.transform.localEulerAngles);
                triggerTime = 0;
                //Debug.Log("head moving" + head.transform.localPosition);
            }
            else if (triggerTime > 0.1f)
            {
                Debug.Log("not tracking");
            }
        }

        // ゲーム終了
        if (Time.timeScale == 0 && OVRInput.GetDown(OVRInput.RawButton.RHandTrigger))
        {
            SaveAndQuitGame();
        }

        if (!process_flag)
            StartCoroutine(MoveSphereOnHemisphere());
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
            viewpoint_standard.SetActive(false);
        }
        else
        {
            Time.timeScale = 0;
            postprocess.SetActive(true);
            viewpoint_standard.SetActive(true);
        }
    }

    /// <summary>
    /// 球を指定領域内のランダムな位置に生成する．
    /// </summary>
    void GenerateSphereOnHemisphere()
    {
        // ランダムな球面座標を生成
        float theta = UnityEngine.Random.Range(rangeOfTheta.x, rangeOfTheta.y);
        float phi = UnityEngine.Random.Range(rangeOfPhi.x, rangeOfPhi.y);

        // 自分を中心とする球面座標を直交座標に変換
        float x = playerPosition.x + radiusOfHemisphere * Mathf.Sin(theta) * Mathf.Cos(phi);
        float y = playerPosition.y + radiusOfHemisphere * Mathf.Cos(theta);
        float z = playerPosition.z + radiusOfHemisphere * Mathf.Sin(theta) * Mathf.Sin(phi);

        // 球を生成
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = new Vector3(x, y, z);
        sphere.transform.localScale = new Vector3(radiusOfSphere * 2, radiusOfSphere * 2, radiusOfSphere * 2);
        sphere.GetComponent<Renderer>().material.color = sphere_color;
        sphere.tag = "CheckPoint";
    }

    /// <summary>
    /// 球を視野中央に捉えたことを検出し，別の位置にワープさせる．
    /// </summary>
    IEnumerator MoveSphereOnHemisphere()
    {
        // カメラの中央に Ray を飛ばす
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        // Rayが何かに当たったら
        if (Physics.Raycast(ray, out hit))
        {
            // 当たったオブジェクトのタグが "CheckPoint" の場合
            if (hit.collider.CompareTag("CheckPoint"))
            {
                process_flag = true;

                float elapsedTime = Time.time - start_time;
                if (interactionTimes.Count == 0)
                    interactionTimes.Add(elapsedTime);
                else if (interactionTimes.Count > 0 && interactionTimes[interactionTimes.Count - 1] - elapsedTime < 0.2f)
                    interactionTimes.Add(elapsedTime);

                MoveExistingSphere(hit.collider.gameObject);      
                
                Debug.Log(interactionTimes.Count + "個目 : " + elapsedTime + "s 経過");
                yield return new WaitForSeconds(0.1f); // 0.1秒以内にもう一度この関数が呼ばれないようにする。位置が変わる前に次のRayの当たり判定が呼ばれることがあるため。
                process_flag= false;
            }
        }
    }

    /// <summary>
    /// 球を別の位置にワープさせる．
    /// </summary>
    /// <param name="sphere"> ワープ対象の球 </param>
    void MoveExistingSphere(GameObject sphere)
    {
        Vector3 newpos = sphere.transform.position;

        while (Vector3.Distance(sphere.transform.position, newpos) < (3 * radiusOfSphere))
        {
            // ランダムな球面座標を生成
            float theta = UnityEngine.Random.Range(rangeOfTheta.x, rangeOfTheta.y);
            float phi = UnityEngine.Random.Range(rangeOfPhi.x, rangeOfPhi.y);

            // 球面座標を直交座標に変換
            float x = playerPosition.x + radiusOfHemisphere * Mathf.Sin(theta) * Mathf.Cos(phi);
            float y = playerPosition.y + radiusOfHemisphere * Mathf.Cos(theta);
            float z = playerPosition.z + radiusOfHemisphere * Mathf.Sin(theta) * Mathf.Sin(phi);

            // 既存の球の座標を変更
            newpos = new Vector3(x, y, z);
        }
        
        sphere.transform.position = newpos;
        //Debug.Log(newpos);

        if (interactionTimes.Count % 10 == 0)
        {
            sphere.GetComponent<Renderer>().material.color = Color.red;
        }
        else
        {
            sphere.GetComponent<Renderer>().material.color = sphere_color;
        }

        // 25回ごとに出現位置の乱数をリセット（つまり，1～25回目と26～50回目の出現位置は同じ）
        if (interactionTimes.Count % 25 == 1)
        {
            UnityEngine.Random.InitState(seed);
        }

    }

    /// <summary>
    /// セーブしてゲームを終了します．
    /// </summary>
    void SaveAndQuitGame()
    {
        if (interactionTimes.Count > 7)
        {
            // アプリケーションが終了する際に、結果を日時付きのCSVに出力
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = "lookaround_results_" + timestamp + ".csv";
            string folderName = Path.Combine("ResultData", "lookaround");
            string filePath;
#if UNITY_EDITOR
            filePath = Path.Combine(Application.dataPath, folderName, fileName);
#else
            filePath = Path.Combine(Application.persistentDataPath, fileName);
#endif

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("InteractionNumber,ElapsedTime,SplitTime,missCount");
                writer.WriteLine((0) + "," + interactionTimes[0]);
                for (int i = 1; i < interactionTimes.Count; i++)
                {
                    writer.WriteLine(i + "," + interactionTimes[i] + "," + (interactionTimes[i] - interactionTimes[i - 1]));
                }
                interactionTimes.Clear();

                writer.WriteLine("");

                //HMDの軌跡を記録
                writer.WriteLine("time,posX,posY,posZ,rotX,rotY,rotZ");
                for (int i = 0; i < rotationLog.Count; i++)
                {
                    writer.WriteLine(timeLog[i] + "," + positionLog[i].x + "," + positionLog[i].y + "," + positionLog[i].z + "," + 
                                     CenterRotValue(rotationLog[i].x) + "," + CenterRotValue(rotationLog[i].y) + "," + CenterRotValue(rotationLog[i].z));
                }
                positionLog.Clear();
                rotationLog.Clear();
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
        if (interactionTimes.Count > 7)
        {
            // アプリケーションが終了する際に、結果を日時付きのCSVに出力
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = "lookaround_results_" + timestamp + ".csv";
            string folderName = Path.Combine("ResultData", "lookaround");
            string filePath;
#if UNITY_EDITOR
            filePath = Path.Combine(Application.dataPath, folderName, fileName);
#else
            filePath = Path.Combine(Application.persistentDataPath, fileName);
#endif

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                // ヘッダ行を書き込む
                writer.WriteLine("InteractionNumber,ElapsedTime,SplitTime");

                // データ行を書き込む
                writer.WriteLine((1) + "," + interactionTimes[0]);
                for (int i = 1; i < interactionTimes.Count; i++)
                {
                    writer.WriteLine(i + "," + interactionTimes[i] + "," + (interactionTimes[i] - interactionTimes[i-1]));
                }

                writer.WriteLine("");

                //HMDの軌跡を記録
                writer.WriteLine("time,posX,posY,posZ,rotX,rotY,rotZ");
                for (int i = 0; i < rotationLog.Count; i++)
                {
                    writer.WriteLine(timeLog[i] + "," + positionLog[i].x + "," + positionLog[i].y + "," + positionLog[i].z + "," + 
                                     CenterRotValue(rotationLog[i].x) + "," + CenterRotValue(rotationLog[i].y) + "," + CenterRotValue(rotationLog[i].z));
                }
            }

            Debug.Log("Results saved to: " + filePath);
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
