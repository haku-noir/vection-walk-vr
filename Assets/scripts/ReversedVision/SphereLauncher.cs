using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// 球を遠方からプレイヤー側に斜方投射するクラス
/// </summary>
public class SphereLauncher : MonoBehaviour
{
    /// <summary>
    /// 発射間隔[s]
    /// </summary>
    [Tooltip("発射間隔[s]")]
    public float launchInterval = 5.0f;
    /// <summary>
    /// 平均到達時間[s]
    /// </summary>
    [Tooltip("平均到達時間[s]")]
    public float flyingTimeAvg = 2.0f;
    /// <summary>
    /// 発射地点までの奥行き[m]
    /// </summary>
    [Tooltip("発射地点までの奥行き[m]")]
    public float distance = 10;
    /// <summary>
    /// 発射される球の半径[m]
    /// </summary>
    [Tooltip("発射される球の半径[m]")]
    public float radius = 0.5f;
    [Tooltip("球の色")]
    public Color sphereColor = new Color(255f / 255f, 150f / 255f, 0f);

    [Tooltip("重力加速度の大きさ")]
    public float gravity = 9.8f;

    /// <summary>
    /// 発射される球の情報
    /// </summary>
    private struct Projectiles
    {
        public GameObject obj;
        public Rigidbody rb;
        /// <summary>
        /// 今地面より上にいる（着地していない）か否か
        /// </summary>
        public bool flying;
    }
    private List<Projectiles> projectiles = new List<Projectiles>(4);
    /// <summary>
    /// 発射数上限
    /// </summary>
    private int projectileNum = 4;
    /// <summary>
    /// 管理用フラグ
    /// </summary>
    private bool touched = false;

    [System.NonSerialized] public float start_time;
    /// <summary>
    /// タッチできたか否かを保存
    /// </summary>
    private List<float> Results = new List<float>(128);
    private List<float> timeLog = new List<float>(2048);
    private List<Vector3> positionLog = new List<Vector3>(2048);
    private List<Vector3> rotationLog = new List<Vector3>(2048);

    private List<Vector3> headPosLog = new List<Vector3>(2048);
    private List<Vector3> LhandPosLog = new List<Vector3>(2048);
    private List<Vector3> RhandPosLog = new List<Vector3>(2048);

    [SerializeField] GameObject head;
    [SerializeField] GameObject Lhand;
    [SerializeField] GameObject Rhand;
    /// <summary>
    /// ログの保存に関わる変数
    /// </summary>
    private float triggerTime = 0;
    /// <summary>
    /// 画面に色を付けるため
    /// </summary>
    private GameObject postprocess;

    private bool start_flag = false;

    void Start()
    {
        int seed = 123;
        UnityEngine.Random.InitState(seed);

        postprocess = GameObject.Find("PostProcessVolume");
        postprocess.SetActive(false);

        initializeProjectiles();

    }

    private void Update()
    {
        int ratio = (int)Mathf.Ceil((flyingTimeAvg - 0.5f) / launchInterval);
        if (ratio > projectileNum)
        {
            projectileNum = ratio;
            initializeProjectiles();
        }

        for (int i=0; i<projectileNum; i++)
        {
            if (projectiles[i].flying == true && (projectiles[i].obj.transform.position.y < -0.2f || projectiles[i].obj.transform.position.z > 11.5f))
            {
                Projectiles projectile = projectiles[i];
                projectile.rb.velocity = Vector3.zero;
                projectile.rb.useGravity = false;
                projectile.flying = false;
                projectiles[i] = projectile;
                if (!touched)
                {
                    Results.Add(0);
                }
                else
                {
                    touched = false;
                    Results.Add(1);
                }
            }
        }

        if (OVRInput.GetDown(OVRInput.RawButton.B) || Input.GetKeyDown(KeyCode.O))
        {
            if (Time.timeScale == 0)
            {
                PauseGame();
                Results.Clear();
                timeLog.Clear();
                positionLog.Clear();
                rotationLog.Clear();
                headPosLog.Clear();
                LhandPosLog.Clear();
                RhandPosLog.Clear();

                start_time = Time.time;
                triggerTime = 0;

                if(start_flag == false)
                {
                    start_flag = true;
                    // 5秒ごとにLaunchProjectileメソッドを呼び出す
                    StartCoroutine(invokeLauncher());
                }
            }
            else
            {
                start_time = -1f;
                PauseGame();
            }
        }

        // 0.1sおきにデータを記録
        if (Time.timeScale == 1 && head != null)
        {
            triggerTime += Time.deltaTime;
            if (triggerTime > 0.1f && start_time >= 0)
            {
                timeLog.Add(Time.time - start_time);
                positionLog.Add(head.transform.localPosition);
                rotationLog.Add(head.transform.localEulerAngles);
                headPosLog.Add(head.transform.position);
                LhandPosLog.Add(Lhand.transform.position);
                RhandPosLog.Add(Rhand.transform.position);
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
    }

    /// <summary>
    /// 発射物（球）を初期化します．
    /// </summary>
    void initializeProjectiles()
    {
        projectiles.Clear();
        //projectileNum = (int)Mathf.Ceil(launchInterval / (flyingTimeAvg - 0.5f));
        for (int i = 0; i < projectileNum; i++)
        {
            Projectiles projectileSet;
            GameObject projectile;

            projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.tag = "CheckPoint";
            projectile.transform.localScale = 2 * radius * Vector3.one;
            projectile.GetComponent<Renderer>().material.color = sphereColor;
            projectile.GetComponent<Collider>().isTrigger = true;

            Rigidbody rigidbody = projectile.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            projectileSet.obj = projectile;
            projectileSet.rb = rigidbody;
            projectileSet.flying = false;
            projectiles.Add(projectileSet);
        }
    }

    /// <summary>
    /// 一定間隔で発射します．
    /// </summary>
    IEnumerator invokeLauncher()
    {
        LaunchProjectile();
        Debug.Log("launched0");
        yield return new WaitForSeconds(launchInterval);
        Debug.Log("wait");
        StartCoroutine(invokeLauncher());
        Debug.Log("next");
    }

    /// <summary>
    /// オブジェクトを発射します．
    /// </summary>
    void  LaunchProjectile()
    {
        // ランダムに発射地点及び目的地点の座標値を生成
        float launcher_x = UnityEngine.Random.Range(-8f, 8f);
        float launcher_y = UnityEngine.Random.Range(1f, 2f);
        float target_x = UnityEngine.Random.Range(-1f, 1f);
        float target_y = UnityEngine.Random.Range(0.7f, 1.7f);
        float flyingTime = UnityEngine.Random.Range(flyingTimeAvg - 0.3f, flyingTimeAvg + 0.3f);

        float gravityValue = gravity;
        if (gravity == 9.8)
        {
            // UnityのPhysics設定から重力のy成分を取得
            gravityValue = Mathf.Abs(Physics.gravity.y);
        }
        else
        {
            Physics.gravity = new Vector3(0, -gravity, 0);
        }

        // 初速度ベクトルを計算
        Vector3 initialVelocity = CalculateInitialVelocity(target_x - launcher_x, target_y - launcher_y, distance, flyingTime, gravityValue);

        // 発射地点を計算
        Vector3 launchPoint = new Vector3(launcher_x, launcher_y, 0);
        Debug.Log(launchPoint);

        // 発射中のオブジェクトの数が上限に達していなければ発射
        for (int i = 0; i < projectileNum; i++)
        {
            if (projectiles[i].flying == false)
            {
                Projectiles projectile = projectiles[i];
                projectile.obj.transform.position = launchPoint;
                projectile.rb.velocity = initialVelocity;
                projectile.rb.useGravity = true;
                projectile.flying = true;
                if (Results.Count % 10 == 9)
                {
                    // 定期的に色を変えて何回目かの目安にする
                    projectile.obj.GetComponent<Renderer>().material.color = Color.red;
                }
                else
                {
                    projectile.obj.GetComponent<Renderer>().material.color = sphereColor;
                }

                projectiles[i] = projectile;
                break;
            }
        }
    }

    /// <summary>
    /// 初速度ベクトルを求める．
    /// </summary>
    /// <param name="dif_x">発射位置と到達位置のx座標の差</param>
    /// <param name="dif_y">発射位置と到達位置のy座標の差</param>
    /// <param name="d">距離（奥行き）</param>
    /// <param name="t">到達までの時間</param>
    /// <param name="gravity">重力加速度の大きさ</param>
    /// <returns>初速度ベクトル</returns>
    Vector3 CalculateInitialVelocity(float dif_x, float dif_y, float d, float t, float gravity = 9.8f)
    {
        float vz = d / t;
        float vx = vz * dif_x / d;
        float vy = vz / d * (dif_y + (Mathf.Pow(d, 2) * gravity) / (2 * Mathf.Pow(vz, 2)));

        return new Vector3(vx, vy, vz);
    }

    public void OnTriggerEnter(Collider other)
    {
        touched = true;
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
        }
    }

    /// <summary>
    /// セーブしてゲームを終了します．
    /// </summary>
    void SaveAndQuitGame()
    {
        if (Results.Sum() > 7)
        {
            // アプリケーションが終了する際に、結果を日時付きのCSVに出力
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = @"catch_results_" + timestamp + ".csv";
            string folderName = Path.Combine("ResultData", "catch");
            string filePath;
#if UNITY_EDITOR
            filePath = Path.Combine(Application.dataPath, folderName, fileName);
#else
            filePath = Path.Combine(Application.persistentDataPath, fileName);
#endif

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("Interval," + launchInterval);
                writer.WriteLine("FlyingTime," + flyingTimeAvg);
                writer.WriteLine("Distance," + distance);
                writer.WriteLine("Radius," + radius);
                writer.WriteLine("Gravity," + gravity);
                writer.WriteLine("InteractionNumber,touched");
                for (int i = 0; i < Results.Count; i++)
                {
                    writer.WriteLine(i + "," + Results[i]);
                }
                Results.Clear();

                writer.WriteLine("");

                //HMDの軌跡を記録
                writer.WriteLine("time,posX,posY,posZ,rotX,rotY,rotZ,headPosX,headPosY,headPosZ,LhandPosX,LhandPosY,LhandPosZ,RhandPosX,RhandPosY,RhandPosZ");
                for (int i = 0; i < rotationLog.Count; i++)
                {
                    writer.WriteLine(timeLog[i] + "," + 
                        positionLog[i].x + "," + positionLog[i].y + "," + positionLog[i].z + "," + 
                        rotationLog[i].x + "," + rotationLog[i].y + "," + rotationLog[i].z + "," +
                        headPosLog[i].x + "," + headPosLog[i].y + "," + headPosLog[i].z + "," +
                        LhandPosLog[i].x + "," + LhandPosLog[i].y + "," + LhandPosLog[i].z + "," + 
                        RhandPosLog[i].x + "," + RhandPosLog[i].y + "," + RhandPosLog[i].z);
                }
                timeLog.Clear();
                positionLog.Clear();
                rotationLog.Clear();
                headPosLog.Clear();
                LhandPosLog.Clear();
                RhandPosLog.Clear();
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

    //// <summary>
    /// アプリケーション終了時に呼ばれる関数です．
    /// </summary>
    /// <remarks>
    /// ここでの中身は SaveAndQuitGame 関数とほぼ同じです．
    /// しかしながら，アプリ化して HMD に入れた場合は呼ばれないことが多く使えません．
    /// </remarks>
    void OnApplicationQuit()
    {
        if (Results.Sum() > 7)
        {
            // アプリケーションが終了する際に、結果を日時付きのCSVに出力
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = @"catch_results_" + timestamp + ".csv";
            string folderName = Path.Combine("ResultData", "catch");
            string filePath;
#if UNITY_EDITOR
            filePath = Path.Combine(Application.dataPath, folderName, fileName);
#else
            filePath = Path.Combine(Application.persistentDataPath, fileName);
#endif

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("InteractionNumber,ElapsedTime,SplitTime,missCount");
                for (int i = 0; i < Results.Count; i++)
                {
                    writer.WriteLine(i + "," + Results[i]);
                }

                writer.WriteLine("");

                //HMDの軌跡を記録
                writer.WriteLine("time,posX,posY,posZ,rotX,rotY,rotZ,headPosX,headPosY,headPosZ,LhandPosX,LhandPosY,LhandPosZ,RhandPosX,RhandPosY,RhandPosZ");
                for (int i = 0; i < rotationLog.Count; i++)
                {
                    writer.WriteLine(timeLog[i] + "," +
                        positionLog[i].x + "," + positionLog[i].y + "," + positionLog[i].z + "," +
                        rotationLog[i].x + "," + rotationLog[i].y + "," + rotationLog[i].z + "," +
                        headPosLog[i].x + "," + headPosLog[i].y + "," + headPosLog[i].z + "," +
                        LhandPosLog[i].x + "," + LhandPosLog[i].y + "," + LhandPosLog[i].z + "," +
                        RhandPosLog[i].x + "," + RhandPosLog[i].y + "," + RhandPosLog[i].z);
                }
            }

            Debug.Log("Results saved to: " + filePath);
        }
    }
}
