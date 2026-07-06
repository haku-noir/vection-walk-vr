using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// 実験走：ライブの頭部位置と収録軌跡上の対応位置を毎フレーム記録し，
/// 追従誤差解析の元データとなる CSV を保存するクラス．
/// FixedUpdate（50Hz）で記録する．
/// </summary>
public class FollowingLogger : MonoBehaviour
{
    /// <summary>
    /// ライブの頭部（CenterEyeAnchor）
    /// </summary>
    [Tooltip("ライブの頭部（CenterEyeAnchor）")]
    public Transform headAnchor;

    /// <summary>
    /// 収録軌跡の再生クラス（現在の収録位置の取得元）
    /// </summary>
    [Tooltip("収録軌跡の再生クラス")]
    public TrajectoryPlayer player;

    /// <summary>
    /// 映像切替クラス（現在の表示ソースと周波数の取得元）
    /// </summary>
    [Tooltip("映像切替クラス")]
    public ViewSwitcher switcher;

    private readonly List<float> timeLog = new List<float>(8192);
    private readonly List<int> sourceLog = new List<int>(8192);        // 0=ライブ表示中, 1=収録表示中
    private readonly List<Vector3> livePosLog = new List<Vector3>(8192);
    private readonly List<Vector3> liveEulerLog = new List<Vector3>(8192);
    private readonly List<Vector3> recPosLog = new List<Vector3>(8192);
    private readonly List<Vector3> recEulerLog = new List<Vector3>(8192);

    private float startTime;

    /// <summary>記録中か否か</summary>
    public bool IsLogging { get; private set; }

    /// <summary>現在の記録サンプル数</summary>
    public int SampleCount { get { return timeLog.Count; } }

    /// <summary>
    /// 記録を開始する（それまでのログは破棄される）
    /// </summary>
    public void StartLogging()
    {
        timeLog.Clear();
        sourceLog.Clear();
        livePosLog.Clear();
        liveEulerLog.Clear();
        recPosLog.Clear();
        recEulerLog.Clear();
        startTime = Time.time;
        IsLogging = true;
        Debug.Log("[FollowingLogger] 記録開始");
    }

    /// <summary>
    /// 記録を停止する（ログは保持されるので，このあと SaveToCsv() で保存できる）
    /// </summary>
    public void StopLogging()
    {
        IsLogging = false;
        Debug.Log("[FollowingLogger] 記録停止 サンプル数: " + timeLog.Count);
    }

    private void FixedUpdate()
    {
        if (!IsLogging) return;

        timeLog.Add(Time.time - startTime);
        sourceLog.Add(switcher != null ? switcher.CurrentSource : 0);
        livePosLog.Add(headAnchor.position);
        liveEulerLog.Add(headAnchor.eulerAngles);
        recPosLog.Add(player != null ? player.CurrentPosition : Vector3.zero);
        recEulerLog.Add(player != null ? player.CurrentRotation.eulerAngles : Vector3.zero);
    }

    /// <summary>
    /// 記録した追従データを following_results_周波数_日時.csv として保存する
    /// </summary>
    /// <returns>保存したファイルのパス（サンプル数不足で保存しなかった場合は null）</returns>
    public string SaveToCsv()
    {
        if (timeLog.Count < 100)
        {
            Debug.LogWarning("[FollowingLogger] サンプル数が少なすぎるため保存しません (" + timeLog.Count + ")");
            return null;
        }

        var inv = CultureInfo.InvariantCulture;
        // 切替周波数をファイル名に埋め込む（例: following_results_2.0Hz_20260706_193000.csv）
        string freqTag = switcher != null ? switcher.switchFrequency.ToString("F1", inv) + "Hz" : "unknown";
        string path = Path.Combine(FollowingPaths.DataDir,
            "following_results_" + freqTag + "_" + FollowingPaths.Timestamp() + ".csv");

        using (StreamWriter writer = new StreamWriter(path))
        {
            writer.WriteLine("time,source,freq," +
                "livePosX,livePosY,livePosZ,liveRotX,liveRotY,liveRotZ," +
                "recPosX,recPosY,recPosZ,recRotX,recRotY,recRotZ," +
                "errXZ,err3D");
            float freq = switcher != null ? switcher.switchFrequency : 0f;

            for (int i = 0; i < timeLog.Count; i++)
            {
                Vector3 diff = livePosLog[i] - recPosLog[i];
                // errXZ: 水平面内の位置誤差（歩行の追従度の主指標）
                float errXZ = new Vector2(diff.x, diff.z).magnitude;
                // err3D: 3次元の位置誤差
                float err3D = diff.magnitude;

                writer.WriteLine(string.Join(",",
                    timeLog[i].ToString("F4", inv),
                    sourceLog[i].ToString(inv),
                    freq.ToString("F2", inv),
                    livePosLog[i].x.ToString("F6", inv), livePosLog[i].y.ToString("F6", inv), livePosLog[i].z.ToString("F6", inv),
                    CenterAngle(liveEulerLog[i].x).ToString("F4", inv), CenterAngle(liveEulerLog[i].y).ToString("F4", inv), CenterAngle(liveEulerLog[i].z).ToString("F4", inv),
                    recPosLog[i].x.ToString("F6", inv), recPosLog[i].y.ToString("F6", inv), recPosLog[i].z.ToString("F6", inv),
                    CenterAngle(recEulerLog[i].x).ToString("F4", inv), CenterAngle(recEulerLog[i].y).ToString("F4", inv), CenterAngle(recEulerLog[i].z).ToString("F4", inv),
                    errXZ.ToString("F6", inv),
                    err3D.ToString("F6", inv)));
            }
        }

        Debug.Log("[FollowingLogger] 保存しました: " + path);
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
