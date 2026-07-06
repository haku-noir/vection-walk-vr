using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

public class CrosshairAlignment : MonoBehaviour
{
    [SerializeField] private Laser _laser;
    [SerializeField] private SetReversion _setreversion;
    public GameObject crosshairPrefab; // 十字のプレハブ

    [System.NonSerialized] public GameObject crosshairInstance; // 生成された十字のインスタンス
    private float prev_theta = Mathf.PI / 2;
    private float prev_phi = Mathf.PI / 2;
    private float prev_roll = 0;
    private float center_time = 0;
    private float angle_time = 0;
    private List<Vector3> angle_list = new List<Vector3>();

    private int index = 0;

    private float start_time = 0;
    private float elapsed_time = 0;
    private List<float> interactionTimes = new List<float>(50);

    private List<float> timeLog = new List<float>(2048);
    private List<Vector3> positionLog = new List<Vector3>(2048);
    private List<Vector3> rotationLog = new List<Vector3>(2048);
    private List<string> dateTimeLog = new List<string>(2048);

    [SerializeField] GameObject head;
    private LineRenderer lineRenderer;
    private float waiting_time = 0;

    public string playerName = "";
    public string condition = "";

    private List<Vector3> test_list = new List<Vector3>(); // for debug
    private List<Vector3> target_pos_log = new List<Vector3>(50);
    private List<Vector3> target_rot_log = new List<Vector3>(50);

    void Start()
    {
        UnityEngine.Random.InitState(100);
        for (int i = 0; i < 40; i++)
            angle_list.Add(new Vector3(UnityEngine.Random.Range(-30f, 30f) * Mathf.Deg2Rad, UnityEngine.Random.Range(-30f, 30f) * Mathf.Deg2Rad, UnityEngine.Random.Range(-30f, 30f)));

        InitTarget();
        lineRenderer = GameObject.Find("Laser").GetComponent<LineRenderer>();
    }

    private void FixedUpdate()
    {
        timeLog.Add(Time.time - start_time);
        positionLog.Add(head.transform.localPosition);
        rotationLog.Add(head.transform.localEulerAngles);

        string timestamp = DateTime.Now.ToString("yyyy/MM/dd_HH:mm:ss");
        dateTimeLog.Add(timestamp);
    }

    void Update()
    {
        Mirroring();

        //Debug.Log("start" + index);
        //if (index >= 50)
        //{
        //    SaveAndQuitGame();
        //    //SaveToCSV(test_list, "test.csv");
        //    //Debug.Log("pass");

        //}

        if (index > 0)
        {
            // 生成された球の十字がカメラの十字に重なっているかどうかを判定（）
            float dot = Vector3.Dot(transform.forward, crosshairInstance.transform.forward);
            //Debug.Log(dot);
            //Debug.Log("cos: " + Mathf.Cos(Mathf.PI * 30 / 32));

            if (Mathf.Abs(dot) > Mathf.Cos(Mathf.PI * 3 / 180))
            {
                Debug.Log("Center OK");
                center_time += Time.deltaTime;
                //Debug.Log("centertime: " + center_time);
            }
            else
            {
                center_time = 0;
            }

            if (Mathf.Abs(CenterRotValue(transform.eulerAngles.z) + CenterRotValue(crosshairInstance.transform.eulerAngles.z)) 
                - Mathf.PI + Mathf.Floor(Mathf.Abs(CenterRotValue(transform.eulerAngles.z) + CenterRotValue(crosshairInstance.transform.eulerAngles.z)) / Mathf.PI) < 3)
            {
                Debug.Log("Angle OK");
                angle_time += Time.deltaTime;
                //Debug.Log("angletime: " + angle_time);
            }
            else
            {
                angle_time = 0;
            }

            // 0.2s重なったら自動的にワープ
            if (center_time > 0.2f && angle_time > 0.2f)
            {
                interactionTimes.Add(Time.time - start_time);
                Debug.Log(interactionTimes.Count + "個目 : " + (Time.time - start_time) + "s 経過");
                if (index >= angle_list.Count)
                {
                    SaveAndQuitGame();
                }
                else
                {
                    MoveTarget(angle_list[index].x, angle_list[index].y, angle_list[index].z);
                    index++;
                    crosshairInstance.SetActive(false);
                    lineRenderer.enabled = false;
                }
            }

            // テスト用================
            //try
            //{
            //    MoveTarget(angle_list[index].x, angle_list[index].y, angle_list[index].z);
            //    index++;
            //    Debug.Log(index);
            //} 
            //catch
            //{
            //    Debug.Log(index);
            //}

            if (!crosshairInstance.activeSelf)
            {
                waiting_time += Time.deltaTime;
                if (waiting_time > 0.3)
                {
                    crosshairInstance.SetActive(true);
                    lineRenderer.enabled = true;
                    waiting_time = 0;
                }
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.P) || OVRInput.GetDown(OVRInput.RawButton.A))
            {
                crosshairInstance.SetActive(false);
                lineRenderer.enabled = false;
            }
            if (Input.GetKeyDown(KeyCode.O) || OVRInput.GetDown(OVRInput.RawButton.B))
            {
                start_time = Time.time;
                timeLog.Clear();
                positionLog.Clear();
                rotationLog.Clear();

                MoveTarget(angle_list[index].x, angle_list[index].y, angle_list[index].z);

                crosshairInstance.SetActive(true);
                lineRenderer.enabled = true;
                index++;
            }
        }

    }

    // 極座標変換
    Vector3 GetCoordinateFromAngle(float theta, float phi, float radius = 1)
    {
        float x = radius * Mathf.Sin(theta) * Mathf.Cos(phi);
        float z = radius * Mathf.Sin(theta) * Mathf.Sin(phi);
        float y = radius * Mathf.Cos(theta);
        return new Vector3(x, y, z);
    }

    void InitTarget(float theta = 0f, float phi = 0f)
    {
        if (!_setreversion.frontback_reversion)
        {
            crosshairInstance = Instantiate(crosshairPrefab, transform.position + GetCoordinateFromAngle(prev_theta + theta, prev_phi + phi), Quaternion.identity);
        }
        else
        {
            prev_phi = - Mathf.PI / 2;
            crosshairInstance = Instantiate(crosshairPrefab, transform.position + GetCoordinateFromAngle(prev_theta + theta, prev_phi - phi), Quaternion.identity);
        }
        _laser.target = crosshairInstance.transform;

        crosshairInstance.transform.LookAt(transform);
        crosshairInstance.transform.localEulerAngles = new Vector3(crosshairInstance.transform.localEulerAngles.x, crosshairInstance.transform.localEulerAngles.y, 0);
        crosshairInstance.transform.localScale = 0.5f * new Vector3(1, 1, -1);
        prev_theta = prev_theta + theta;
        prev_phi = prev_phi + phi;
    }

    void MoveTarget(float theta = 0f, float phi = 0f, float roll = 0f)
    {
        //Debug.Log("prevtheta: " + prev_theta + "\t prevphi: " + prev_phi);
        //Debug.Log("theta: " + theta + "\t phi: " +  phi);
        if (_setreversion.updown_reversion)
        {
            theta = -theta;
        }
        if (_setreversion.leftright_reversion) 
        {
            phi = -phi; 
        }
        if (_setreversion.frontback_reversion)
        {
            //theta = -theta;
            phi = -phi;
        }
        float next_theta = prev_theta + theta;
        if (Mathf.Abs(next_theta) < Mathf.PI * 50 / 180 || Mathf.Abs(next_theta) > Mathf.PI * 130 / 180) next_theta -= 2 * theta;
        float next_phi = prev_phi + phi;
        if (Mathf.Abs(next_phi) < Mathf.PI * 40 / 180 || Mathf.Abs(next_phi) > Mathf.PI * 140 / 180) next_phi -= 2 * phi;
        Debug.Log(next_phi);
        float next_roll = prev_roll + roll;
        if (Mathf.Abs(next_roll) > 30) next_roll -= 2 * roll;
        Debug.Log(next_roll);
        crosshairInstance.transform.position = transform.position + GetCoordinateFromAngle(next_theta, next_phi);
        crosshairInstance.transform.LookAt(transform);
        crosshairInstance.transform.localEulerAngles = new Vector3(crosshairInstance.transform.localEulerAngles.x, crosshairInstance.transform.localEulerAngles.y, next_roll);
        prev_theta = next_theta;
        prev_phi = next_phi;
        prev_roll = next_roll;
        test_list.Add(new Vector3(next_theta, next_phi, next_roll));
        target_pos_log.Add(crosshairInstance.transform.position);
        target_rot_log.Add(crosshairInstance.transform.localEulerAngles);
    }

    void Mirroring()
    {
        if ((_setreversion.leftright_reversion != _setreversion.leftright_flag) || Input.GetKeyDown(KeyCode.L) || OVRInput.GetDown(OVRInput.RawButton.Y))
        {
            float dif = transform.position.x - crosshairInstance.transform.position.x;
            Debug.Log(dif);
            crosshairInstance.transform.position = new Vector3(crosshairInstance.transform.position.x + 2 * dif, crosshairInstance.transform.position.y, crosshairInstance.transform.position.z);
            Debug.Log(crosshairInstance.transform.position.x + 2 * dif);
            float roll = crosshairInstance.transform.eulerAngles.z;
            crosshairInstance.transform.LookAt(transform);
            crosshairInstance.transform.eulerAngles = new Vector3(crosshairInstance.transform.eulerAngles.x, crosshairInstance.transform.eulerAngles.y, - roll);

        }
        if ((_setreversion.updown_reversion != _setreversion.updown_flag) || Input.GetKeyDown(KeyCode.U) || OVRInput.GetDown(OVRInput.RawButton.X))
        {
            float dif = transform.position.y - crosshairInstance.transform.position.y;
            crosshairInstance.transform.position = new Vector3(crosshairInstance.transform.position.x, crosshairInstance.transform.position.y + 2 * dif, crosshairInstance.transform.position.z);
            float roll = crosshairInstance.transform.eulerAngles.z;
            crosshairInstance.transform.LookAt(transform);
            crosshairInstance.transform.eulerAngles = new Vector3(crosshairInstance.transform.eulerAngles.x, crosshairInstance.transform.eulerAngles.y, 180 - roll);
        }
        if ((_setreversion.frontback_reversion != _setreversion.frontback_flag) || Input.GetKeyDown(KeyCode.F) || OVRInput.GetDown(OVRInput.RawButton.LHandTrigger))
        {
            float dif = transform.position.z - crosshairInstance.transform.position.z;
            crosshairInstance.transform.position = new Vector3(crosshairInstance.transform.position.x, crosshairInstance.transform.position.y, crosshairInstance.transform.position.z + 2 * dif);
            float roll = crosshairInstance.transform.eulerAngles.z;
            crosshairInstance.transform.LookAt(transform);
            crosshairInstance.transform.eulerAngles = new Vector3(crosshairInstance.transform.eulerAngles.x, crosshairInstance.transform.eulerAngles.y, -roll);
            prev_phi = - prev_phi;
            Debug.Log(prev_phi);
        }
    }
    

    void SaveAndQuitGame()
    {
        // アプリケーションが終了する際に、結果を日時付きのCSVに出力
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = playerName + "_" + condition + "_vrsj_" + timestamp + ".csv";
        string folderName = Path.Combine("ResultData", "vrsj");
        string filePath1, filePath2;
#if UNITY_EDITOR
        filePath1 = Path.Combine(Application.dataPath, folderName, "result_" + fileName);
        filePath2 = Path.Combine(Application.dataPath, folderName, "log_" + fileName);
#else
        filePath1 = Path.Combine(Application.persistentDataPath, "result_" + fileName);
        filePath2 = Path.Combine(Application.persistentDataPath, "logt_" + fileName);
#endif

        using (StreamWriter writer = new StreamWriter(filePath1))
        {
            writer.WriteLine("InteractionNumber,ElapsedTime,SplitTime,tposX,tposY,tposZ,trotX,trotY,trotZ,tangX,tangY,tangZ");
            writer.WriteLine((0) + "," + interactionTimes[0] + "," + interactionTimes[0] + "," +
                    CenterRotValue(target_pos_log[0].x) + "," + CenterRotValue(target_pos_log[0].y) + "," + CenterRotValue(target_pos_log[0].z) + "," +
                    CenterRotValue(target_rot_log[0].x) + "," + CenterRotValue(target_rot_log[0].y) + "," + CenterRotValue(target_rot_log[0].z) + "," +
                    CenterRotValue(test_list[0].x) + "," + CenterRotValue(test_list[0].y) + "," + CenterRotValue(test_list[0].z)
                    );
            for (int i = 1; i < interactionTimes.Count; i++)
            {
                writer.WriteLine(i + "," + interactionTimes[i] + "," + (interactionTimes[i] - interactionTimes[i - 1]) + "," +
                CenterRotValue(target_pos_log[i].x) + "," + CenterRotValue(target_pos_log[i].y) + "," + CenterRotValue(target_pos_log[i].z) + "," +
                CenterRotValue(target_rot_log[i].x) + "," + CenterRotValue(target_rot_log[i].y) + "," + CenterRotValue(target_rot_log[i].z) + "," +
                CenterRotValue(test_list[i].x) + "," + CenterRotValue(test_list[i].y) + "," + CenterRotValue(test_list[i].z)
                );
            }
            interactionTimes.Clear();
        }
        using (StreamWriter writer = new StreamWriter(filePath2))
        {
            //HMDの軌跡を記録
            writer.WriteLine("date,time,posX,posY,posZ,rotX,rotY,rotZ");
            for (int i = 0; i < rotationLog.Count; i++)
            {
                writer.WriteLine(
                    dateTimeLog[i] + "," + timeLog[i] + "," + 
                    positionLog[i].x + "," + positionLog[i].y + "," + positionLog[i].z + "," +
                    CenterRotValue(rotationLog[i].x) + "," + CenterRotValue(rotationLog[i].y) + "," + CenterRotValue(rotationLog[i].z)
                    );

            }
            dateTimeLog.Clear();
            timeLog.Clear();
            positionLog.Clear();
            rotationLog.Clear();
            target_pos_log.Clear();
            target_rot_log.Clear();
            test_list.Clear();
        }

        index = 0;
        prev_theta = Mathf.PI / 2;
        if (!_setreversion.frontback_reversion)
            prev_phi = Mathf.PI / 2;
        else
            prev_phi = - Mathf.PI / 2;
        prev_roll = 0;
        center_time = 0;
        angle_time = 0;
        start_time = 0;
        elapsed_time = 0;
        MoveTarget();

        Debug.Log("Results saved to: " + filePath1);
        Debug.Log("Quit game.");

//#if UNITY_EDITOR
//        UnityEditor.EditorApplication.isPlaying = false;   // UnityEditorの実行を停止する処理
//        Debug.Log("もう終了してる");
//#else
//        Application.Quit();                                // ゲームを終了する処理
//#endif
    }

    void SaveToCSV(List<Vector3> vector3List, string fileName)
    {
        // StringBuilderを使ってCSV形式の文字列を作成
        StringBuilder csvContent = new StringBuilder();
        csvContent.AppendLine("X,Y,Z");

        for (int i = 0; i < vector3List.Count; i++)
        {
            csvContent.AppendLine($"{vector3List[i].x},{vector3List[i].y},{vector3List[i].z}");
            Debug.Log(vector3List[i].x);
        }

        // ファイルパスを設定
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string folderName = Path.Combine("ResultData", "vrsj");
        string filePath = Path.Combine(Application.dataPath, folderName, fileName);

        // ファイルに書き込み
        File.WriteAllText(filePath, csvContent.ToString());

        Debug.Log("pass");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;   // UnityEditorの実行を停止する処理
#else
        Application.Quit();                                // ゲームを終了する処理
#endif
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
