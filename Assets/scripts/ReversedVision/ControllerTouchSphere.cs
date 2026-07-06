using Oculus.Interaction;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 周囲のランダムな位置に現れる球にタッチする実験を管理するクラス
/// </summary>
public class ControllerTouchSphere : MonoBehaviour
{
    /// <summary>
    /// 球の生成領域の座標値の最大値
    /// </summary>
    [Tooltip("球の生成領域の座標値の最大値")]
    public Vector3 max_coordinate = new Vector3(2f, 1.5f, 2f);
    /// <summary>
    /// 球の生成領域の座標値の最小値
    /// </summary>
    [Tooltip("球の生成領域の座標値の最小値")]
    public Vector3 min_coordinate = new Vector3(-2f, 0.5f, 0f);
    /// <summary>
    /// 地面の目に対する相対高さ
    /// </summary>
    [Tooltip("地面の目に対する相対高さ")]
    public float floorLevel = -1.5f;
    /// <summary>
    /// 生成する球の半径
    /// </summary>
    [Tooltip("生成する球の半径")]
    public float radius = 0.2f;
    /// <summary>
    /// 生成する球の色
    /// </summary>
    [Tooltip("生成する球の色")]
    public Color sphereColor = new Color(255f / 255f, 150f / 255f, 0f);
    /// <summary>
    /// 球用のマテリアル
    /// </summary>
    private Material sphereMat;

    [System.NonSerialized] public float start_time;
    private List<float> interactionTimes = new List<float>(128);
    private List<float> timeLog = new List<float>(2048);
    private List<Vector3> positionLog = new List<Vector3>(2048);
    private List<Vector3> rotationLog = new List<Vector3>(2048);

    private List<Vector3> headPosLog = new List<Vector3>(2048);
    private List<Vector3> LhandPosLog = new List<Vector3>(2048);
    private List<Vector3> RhandPosLog = new List<Vector3>(2048);

    private List<int> missTimes = new List<int>(128);
    private int missCount = 0;

    [SerializeField] GameObject head;
    [SerializeField] GameObject Lhand;
    [SerializeField] GameObject Rhand;

    private bool hasGenerated;

    private float triggerTime = 0;
    private GameObject viewpoint_standard;
    private GameObject postprocess;

    [System.NonSerialized] public OVRInput.Controller touchedController = OVRInput.Controller.RTouch;

    [SerializeField] MirrorHand mirrorhand_script;

    private void Awake()
    {
        postprocess = GameObject.Find("PostProcessVolume");
    }

    void Start()
    {

        int seed = 121;
        UnityEngine.Random.InitState(seed);

        triggerTime = 0;
        viewpoint_standard = GameObject.Find("standard");
        postprocess.SetActive(false);

        start_time = Time.time;
        Vector3 initPos = GetRandomPosition();
        GenerateSphere(initPos, radius);
        hasGenerated = true;

        mirrorhand_script.mirrorLhand.AddComponent<NondominantHandTouch>().script = GameObject.FindWithTag("GameSystem").GetComponent<ControllerTouchSphere>();
        mirrorhand_script.mirrorRhand.AddComponent<NondominantHandTouch>().script = GameObject.FindWithTag("GameSystem").GetComponent<ControllerTouchSphere>();

//#if UNITY_ANDROID
//        // 新しくcsvファイルを作成して、{}の中の要素分csvに追記をする(Androidの処理)
//        string FilePath = @"/SaveData.csv";
//        StreamWriter sw = new StreamWriter(Application.persistentDataPath + FilePath, false, Encoding.GetEncoding("utf-8"));
//        sw.WriteLine("InteractionNumber,ElapsedTime,SplitTime,missCount");
//        sw.Close();
//#endif
    }

    void Update()
    {
        if (OVRInput.GetDown(OVRInput.RawButton.B) || Input.GetKeyDown(KeyCode.O))
        {
            if (!hasGenerated)
            {
                PauseGame();
                interactionTimes.Clear();
                timeLog.Clear();
                positionLog.Clear();
                rotationLog.Clear();
                headPosLog.Clear();
                LhandPosLog.Clear();
                RhandPosLog.Clear();
                missTimes.Clear();

                start_time = Time.time;
                triggerTime = 0;
                missCount = 0;
                hasGenerated = true;
                Vector3 initPos = GetRandomPosition();
                GenerateSphere(initPos, radius);
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
                headPosLog.Add(head.transform.position);
                LhandPosLog.Add(Lhand.transform.position);
                RhandPosLog.Add(Rhand.transform.position);
                triggerTime = 0;
                //Debug.Log("head moving" + head.transform.localPosition);
            } else if (triggerTime > 0.1f)
            {
                Debug.Log("not tracking");
            }
        }

        // 任意のカウント
        if (Time.timeScale == 1 && (Input.GetKeyDown(KeyCode.Q) || OVRInput.GetDown(OVRInput.RawButton.RHandTrigger)))
        {
            missCount++;
            Debug.Log("miss" + missCount);
        }

        // ゲーム終了
        if (Time.timeScale == 0 && OVRInput.GetDown(OVRInput.RawButton.RHandTrigger))
        {
            SaveAndQuitGame();
        }
    }

    /// <summary>
    /// ランダムな座標を取得
    /// </summary>
    /// <param name="prev"> 前の位置 </param>
    /// <returns> 前の位置および原点から50cm以上離れた座標 </returns>
    Vector3 GetRandomPosition(Vector3 prev = default(Vector3))
    {
        Vector3 pos = Vector3.zero;
        var posXZ = Vector3.zero;
        var player_pos = Vector3.zero;
        float dif, dist;

        do
        {
            pos = new Vector3(UnityEngine.Random.Range(min_coordinate.x, max_coordinate.x), floorLevel + UnityEngine.Random.Range(min_coordinate.y, max_coordinate.y), UnityEngine.Random.Range(min_coordinate.z, max_coordinate.z));
            dif = Vector3.Distance(pos, prev);
            posXZ = new Vector3(pos.x, head.transform.position.y, pos.z);
            player_pos = new Vector3(head.transform.position.x, head.transform.position.y, head.transform.position.z) - 0.15f * head.transform.forward;
            dist = Vector3.Distance(posXZ, player_pos);

            Debug.Log("next");
        }
        while (dif < 0.5f || dist < 0.5f); // 原点及び前の球の位置から離れた位置

        Debug.Log(pos + "; head: " + player_pos);
        return pos;
    }

    /// <summary>
    /// 指定位置に指定半径の球を生成する
    /// </summary>
    /// <param name="position"> 生成位置 </param>
    /// <param name="radius"> 球の半径 </param>
    public void GenerateSphere(Vector3 position, float radius)
    {
        // 円柱の生成
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.tag = "CheckPoint";
        sphere.transform.localScale = new Vector3(radius * 2, radius * 2, radius * 2); //scale=diameter
        sphere.transform.position = position;
        //Rigidbody rb = sphere.AddComponent<Rigidbody>();
        //rb.useGravity = false;
        //rb.isKinematic = true;
        //rb.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
        sphere.GetComponent<Collider>().isTrigger = true;

        // 色を指定
        sphereMat = sphere.GetComponent<Renderer>().material;
        sphereMat.color = sphereColor;
    }

    /// <summary>
    /// 球に接触した際の動作
    /// </summary>
    /// <param name="other"> 接触対象 </param>
    public void OnTriggerEnter(Collider other)
    {
        //Debug.Log("checkpoint");
        // 衝突したオブジェクトがChackPointであるかチェック
        if (other.gameObject.CompareTag("CheckPoint"))
        {
            // 接触時の経過時間を記録
            float elapsedTime = Time.time - start_time;
            interactionTimes.Add(elapsedTime);
            missTimes.Add(missCount);
            //VibrateController(touchedController);
            Debug.Log(interactionTimes.Count + "個目 : " + elapsedTime + "s 経過 : 空振り" +  missCount + "回");

            other.gameObject.transform.position = GetRandomPosition(other.gameObject.transform.position);

            if (interactionTimes.Count % 100 == 0)
            {
                sphereMat.color = Color.magenta;
            }
            else if (interactionTimes.Count % 10 == 0)
            {
                sphereMat.color = Color.red; //sphereMat=other.GetComponent<Renderer>().material
            }
            else
            {
                sphereMat.color = sphereColor;
            }


            missCount = 0;
        }
    }

    /// <summary>
    /// コントローラーを振動させる（動作不安定）
    /// </summary>
    /// <param name="duration"> 振動時間 </param>
    /// <param name="frequency"> 振動周波数 </param>
    /// <param name="amplitude"> 振動強度 </param>
    /// <param name="controller"> 振動させるコントローラ </param>
    /// <returns></returns>
    public void VibrateController(OVRInput.Controller controller = OVRInput.Controller.Active, float duration = 0.2f, float frequency = 1f, float amplitude = 0.5f)
    {
        Debug.Log("vibrate");
        StartCoroutine(Vibrate(duration, frequency, amplitude, controller));
    }

    /// <summary>
    /// コントローラーを振動させる（動作不安定）
    /// </summary>
    /// <param name="duration"> 振動時間 </param>
    /// <param name="frequency"> 振動周波数 </param>
    /// <param name="amplitude"> 振動強度 </param>
    /// <param name="controller"> 振動させるコントローラ </param>
    /// <returns></returns>
    IEnumerator Vibrate(float duration, float frequency, float amplitude, OVRInput.Controller controller)
    {
        //コントローラーを振動させる
        OVRInput.SetControllerVibration(frequency, amplitude, controller);

        //指定された時間待つ
        yield return new WaitForSeconds(duration);

        //コントローラーの振動を止める
        OVRInput.SetControllerVibration(0, 0, controller);
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
    /// セーブしてゲームを終了します．
    /// </summary>
    void SaveAndQuitGame()
    {
        if (interactionTimes.Count > 7)
        {
            // アプリケーションが終了する際に、結果を日時付きのCSVに出力
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = @"touch_results_" + timestamp + ".csv";
            string folderName = Path.Combine("ResultData", "touch");
            string filePath;
#if UNITY_EDITOR
            filePath = Path.Combine(Application.dataPath, folderName, fileName);
#else
            filePath = Path.Combine(Application.persistentDataPath, fileName);
#endif

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("InteractionNumber,ElapsedTime,SplitTime,missCount");
                writer.WriteLine((0) + "," + interactionTimes[0] + ", ," + missTimes[0]);
                for (int i = 1; i < interactionTimes.Count; i++)
                {
                    writer.WriteLine(i + "," + interactionTimes[i] + "," + (interactionTimes[i] - interactionTimes[i - 1]) + "," + missTimes[i]);
                }
                interactionTimes.Clear();
                missTimes.Clear();

                writer.WriteLine("");

                //HMDの軌跡を記録
                writer.WriteLine("time,posX,posY,posZ,rotX,rotY,rotZ");
                for (int i = 0; i < rotationLog.Count; i++)
                {
                    writer.WriteLine(timeLog[i] + "," + positionLog[i].x + "," + positionLog[i].y + "," + positionLog[i].z + "," + 
                                     CenterRotValue(rotationLog[i].x) + "," + CenterRotValue(rotationLog[i].y) + "," + CenterRotValue(rotationLog[i].z));
                }
                timeLog.Clear();
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
            string fileName = @"touch_results_" + timestamp + ".csv";
            string folderName = Path.Combine("ResultData", "touch");
            string filePath;
#if UNITY_EDITOR
            filePath = Path.Combine(Application.dataPath, folderName, fileName);
#else
            filePath = Path.Combine(Application.persistentDataPath, fileName);
#endif

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("InteractionNumber,ElapsedTime,SplitTime,missCount");
                writer.WriteLine((0) + "," + interactionTimes[0] + ", ," + missTimes[0]);
                for (int i = 1; i < interactionTimes.Count; i++)
                {
                    writer.WriteLine(i + "," + interactionTimes[i] + "," + (interactionTimes[i] - interactionTimes[i - 1]) + "," + missTimes[i]);
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
