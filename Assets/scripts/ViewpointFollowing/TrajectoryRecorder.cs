using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// 収録走：HMD（CenterEyeAnchor）の頭部軌跡を記録して CSV に保存するクラス．
/// FixedUpdate（50Hz, Fixed Timestep = 0.02s）でワールド座標の位置と回転を記録する．
/// 回転は再生時の補間で破綻しないよう四元数で保存する（オイラー角は解析用に併記）．
/// </summary>
/// <remarks>
/// 記録座標はワールド座標系．視点リセット（Oculusボタン長押し）で TrackingSpace の
/// 原点が変わるため，収録時と実験時で同じ立ち位置・向きで視点リセットする手順を守ること．
/// </remarks>
public class TrajectoryRecorder : MonoBehaviour
{
    /// <summary>
    /// 記録対象の頭部（CenterEyeAnchor）
    /// </summary>
    [Tooltip("記録対象の頭部（CenterEyeAnchor）")]
    public Transform headAnchor;

    private readonly List<float> timeLog = new List<float>(8192);
    private readonly List<Vector3> posLog = new List<Vector3>(8192);
    private readonly List<Quaternion> rotLog = new List<Quaternion>(8192);

    private float startTime;

    /// <summary>記録中か否か</summary>
    public bool IsRecording { get; private set; }

    /// <summary>現在の記録サンプル数</summary>
    public int SampleCount { get { return timeLog.Count; } }

    /// <summary>
    /// 記録を開始する（それまでのログは破棄される）
    /// </summary>
    public void StartRecording()
    {
        timeLog.Clear();
        posLog.Clear();
        rotLog.Clear();
        startTime = Time.time;
        IsRecording = true;
        Debug.Log("[TrajectoryRecorder] 記録開始");
    }

    /// <summary>
    /// 記録を停止する（ログは保持されるので，このあと SaveToCsv() で保存できる）
    /// </summary>
    public void StopRecording()
    {
        IsRecording = false;
        Debug.Log("[TrajectoryRecorder] 記録停止 サンプル数: " + timeLog.Count);
    }

    private void FixedUpdate()
    {
        // Time.timeScale = 0（一時停止中）のとき FixedUpdate は呼ばれないため，
        // 停止中に余計なサンプルが混ざることはない
        if (!IsRecording) return;

        timeLog.Add(Time.time - startTime);
        posLog.Add(headAnchor.position);       // ワールド座標
        rotLog.Add(headAnchor.rotation);       // ワールド回転（四元数）
    }

    /// <summary>
    /// 記録した軌跡を trajectory_日時.csv として保存する
    /// </summary>
    /// <returns>保存したファイルのパス（サンプル数不足で保存しなかった場合は null）</returns>
    public string SaveToCsv()
    {
        if (timeLog.Count < 100)
        {
            Debug.LogWarning("[TrajectoryRecorder] サンプル数が少なすぎるため保存しません (" + timeLog.Count + ")");
            return null;
        }

        string path = Path.Combine(FollowingPaths.DataDir, "trajectory_" + FollowingPaths.Timestamp() + ".csv");
        var inv = CultureInfo.InvariantCulture; // 小数点はロケール非依存の "." で統一

        using (StreamWriter writer = new StreamWriter(path))
        {
            writer.WriteLine("time,posX,posY,posZ,qX,qY,qZ,qW,eulerX,eulerY,eulerZ");
            for (int i = 0; i < timeLog.Count; i++)
            {
                Vector3 e = rotLog[i].eulerAngles;
                writer.WriteLine(string.Join(",",
                    timeLog[i].ToString("F4", inv),
                    posLog[i].x.ToString("F6", inv), posLog[i].y.ToString("F6", inv), posLog[i].z.ToString("F6", inv),
                    rotLog[i].x.ToString("F6", inv), rotLog[i].y.ToString("F6", inv), rotLog[i].z.ToString("F6", inv), rotLog[i].w.ToString("F6", inv),
                    CenterAngle(e.x).ToString("F4", inv), CenterAngle(e.y).ToString("F4", inv), CenterAngle(e.z).ToString("F4", inv)));
            }
        }

        Debug.Log("[TrajectoryRecorder] 保存しました: " + path);
        return path;
    }

    /// <summary>
    /// 0°〜360° を -180°〜180° に変換する（既存実験のログ形式と合わせるため）
    /// </summary>
    private static float CenterAngle(float value)
    {
        return value > 180f ? value - 360f : value;
    }
}
