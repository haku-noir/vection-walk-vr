using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// 実験走：ライブの頭部姿勢と，実際に提示された収録映像カメラ（ゴーストカメラ）の姿勢を
/// 毎フレーム記録し，追従誤差解析の元データとなる CSV を保存するクラス．
/// FixedUpdate（50Hz）で記録する．
/// </summary>
/// <remarks>
/// recPos / recRot 列には再生成分の条件（PositionOnly / RotationOnly）による
/// 置き換え後の「実際に再生された姿勢」が入る．
/// </remarks>
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

    /// <summary>
    /// 四角錐ガイドの2チャンネル合成器（09 仕様）．有効なときは表示ソースと
    /// 提示条件をこちらから取る．未設定・無効なら従来どおり switcher を参照する．
    /// </summary>
    [Tooltip("錐ガイドの2チャンネル合成器（無効なら従来どおり ViewSwitcher を参照）")]
    public ChannelCompositor compositor;

    /// <summary>
    /// 実験管理クラス（初期オフセット量の取得元）
    /// </summary>
    [Tooltip("実験管理クラス（初期オフセット量の取得元）")]
    public FollowingExperimentManager manager;

    private readonly List<float> timeLog = new List<float>(8192);
    private readonly List<int> sourceLog = new List<int>(8192);        // 0=ライブ表示中, 1=収録表示中
    private readonly List<Vector3> livePosLog = new List<Vector3>(8192);
    private readonly List<Vector3> liveEulerLog = new List<Vector3>(8192);
    private readonly List<Vector3> recPosLog = new List<Vector3>(8192);
    private readonly List<Vector3> recEulerLog = new List<Vector3>(8192);

    private float startTime;

    // ---- 試行開始時に確定させる提示条件（試行中は変わらない．キー操作は停止中のみ有効） ----
    private bool coneActive;          // 錐ガイド経路で提示していたか
    private string boxModeTag = "Off";
    private string boxPolarityTag = "-";
    private float fBox;
    private Vector3 startOffset;      // x=横[m], y=ヨー[deg], z=前後[m]

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

        // 提示条件は試行中に変わらない（条件変更キーは停止中のみ有効）ので開始時に確定させる
        coneActive = compositor != null && compositor.enabled;
        boxModeTag = coneActive ? compositor.boxMode.ToString() : "Off";
        boxPolarityTag = (coneActive && compositor.boxMode == ChannelCompositor.BoxMode.FourStroke)
            ? compositor.boxPolarity.ToString() : "-";
        fBox = coneActive ? compositor.BoxFrequency : 0f;
        startOffset = manager != null ? manager.AppliedOffset : Vector3.zero;

        IsLogging = true;
        Debug.Log("[FollowingLogger] 記録開始（箱: " + boxModeTag + ", f_box: " + fBox.ToString("F1")
            + "Hz, 初期オフセット: 横" + startOffset.x.ToString("F2") + "m 前後"
            + startOffset.z.ToString("F2") + "m ヨー" + startOffset.y.ToString("F1") + "°）");
    }

    /// <summary>
    /// 今表示しているソース（0 = ライブ, 1 = 収録）．
    /// 錐ガイド経路が有効なときは背景チャンネル基準の値を使う（意味づけは従来と同じ）．
    /// </summary>
    private int CurrentSource()
    {
        if (compositor != null && compositor.enabled) return compositor.CurrentSource;
        return switcher != null ? switcher.CurrentSource : 0;
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
        sourceLog.Add(CurrentSource());
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
        // 実験条件（切替周波数・再生成分・4ストローク）をファイル名に埋め込む
        // 例: following_results_2.0Hz_PositionOnly_20260706_193000.csv
        //     following_results_3.0Hz_PositionAndRotation_4stEnhance_20260706_193000.csv
        string freqTag = switcher != null ? switcher.switchFrequency.ToString("F1", inv) + "Hz" : "unknown";
        string componentsTag = player != null ? player.playbackComponents.ToString() : "unknown";
        // 箱チャンネルのタグ（錐ガイド未使用なら空＝従来のファイル名と完全に同じ）
        string boxTag = BoxFileTag();
        // 背景の4ストローク極性タグ（錐ガイドの有無で取得元が変わるだけで書式は従来どおり）
        string fourStrokeTag = BackgroundFourStrokeTag();
        string path = Path.Combine(FollowingPaths.DataDir,
            "following_results_" + freqTag + "_" + componentsTag + boxTag + fourStrokeTag
            + "_" + FollowingPaths.Timestamp() + ".csv");

        using (StreamWriter writer = new StreamWriter(path))
        {
            // 既存17列の順序は変えない（既存の解析・Replay を壊さないため）．新規列は末尾に追加する
            writer.WriteLine("time,source,freq," +
                "livePosX,livePosY,livePosZ,liveRotX,liveRotY,liveRotZ," +
                "recPosX,recPosY,recPosZ,recRotX,recRotY,recRotZ," +
                "errXZ,err3D," +
                "boxMode,fBox,boxPolarity,offsetLat,offsetFwd,offsetYaw," +
                "errLat,errFwd,errYaw");
            float freq = switcher != null ? switcher.switchFrequency : 0f;

            // 試行を通じて一定の条件列（開始時に確定させた値）
            string boxCols = boxModeTag + "," + fBox.ToString("F2", inv) + "," + boxPolarityTag + ","
                + startOffset.x.ToString("F3", inv) + "," + startOffset.z.ToString("F3", inv) + ","
                + startOffset.y.ToString("F2", inv);

            for (int i = 0; i < timeLog.Count; i++)
            {
                Vector3 diff = livePosLog[i] - recPosLog[i];
                // errXZ: 水平面内の位置誤差（歩行の追従度の主指標）
                float errXZ = new Vector2(diff.x, diff.z).magnitude;
                // err3D: 3次元の位置誤差
                float err3D = diff.magnitude;

                // 誤差の成分分解（仕様 09 §6）: コース進行方向 +Z を基準に，
                // 横 = X 成分，前後 = Z 成分，ヨー = 収録に対するライブの符号つき角度差．
                // 前後多義性が効いていれば errFwd にだけ大きな誤差や符号反転が残るはず
                float errLat = diff.x;
                float errFwd = diff.z;
                float errYaw = Mathf.DeltaAngle(recEulerLog[i].y, liveEulerLog[i].y);

                writer.WriteLine(string.Join(",",
                    timeLog[i].ToString("F4", inv),
                    sourceLog[i].ToString(inv),
                    freq.ToString("F2", inv),
                    livePosLog[i].x.ToString("F6", inv), livePosLog[i].y.ToString("F6", inv), livePosLog[i].z.ToString("F6", inv),
                    CenterAngle(liveEulerLog[i].x).ToString("F4", inv), CenterAngle(liveEulerLog[i].y).ToString("F4", inv), CenterAngle(liveEulerLog[i].z).ToString("F4", inv),
                    recPosLog[i].x.ToString("F6", inv), recPosLog[i].y.ToString("F6", inv), recPosLog[i].z.ToString("F6", inv),
                    CenterAngle(recEulerLog[i].x).ToString("F4", inv), CenterAngle(recEulerLog[i].y).ToString("F4", inv), CenterAngle(recEulerLog[i].z).ToString("F4", inv),
                    errXZ.ToString("F6", inv),
                    err3D.ToString("F6", inv),
                    boxCols,
                    errLat.ToString("F6", inv),
                    errFwd.ToString("F6", inv),
                    errYaw.ToString("F4", inv)));
            }
        }

        Debug.Log("[FollowingLogger] 保存しました: " + path);
        return path;
    }

    /// <summary>
    /// ファイル名に入れる箱チャンネルのタグを返す．
    /// 錐ガイドを使わなかった試行では空文字になり，従来のファイル名と完全に同じになる．
    /// 例: "_boxOther" / "_boxSelf" / "_boxBoth" / "_boxBoth4stEnhance"
    /// </summary>
    private string BoxFileTag()
    {
        if (!coneActive) return "";
        switch (boxModeTag)
        {
            case "OtherFixed": return "_boxOther";
            case "SelfFixed": return "_boxSelf";
            case "SquareAlternate": return "_boxBoth";
            case "FourStroke": return "_boxBoth4st" + boxPolarityTag;
            default: return ""; // Off
        }
    }

    /// <summary>
    /// ファイル名に入れる背景の4ストロークタグ（従来と同じ "_4st極性" の書式）を返す．
    /// 錐ガイド経路では背景チャンネルの条件から，従来経路では ViewSwitcher から取る．
    /// </summary>
    private string BackgroundFourStrokeTag()
    {
        if (coneActive)
        {
            return compositor.bgMode == ChannelCompositor.BackgroundMode.FourStroke
                ? "_4st" + compositor.bgPolarity : "";
        }
        return (switcher != null && switcher.fourStrokeEnabled && switcher.fourStroke != null)
            ? "_4st" + switcher.fourStroke.polarity : "";
    }

    /// <summary>
    /// 0°〜360° を -180°〜180° に変換する（既存実験のログ形式と合わせるため）
    /// </summary>
    private static float CenterAngle(float value)
    {
        return value > 180f ? value - 360f : value;
    }
}
