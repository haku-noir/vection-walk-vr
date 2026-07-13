using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// 実験で保存した CSV を読み込み，そのとき HMD に表示していた映像を
/// 通常のカメラ（Game ビュー）で再現するクラス．HMD の接続は不要．
///
/// 対応ファイル（ファイル名の先頭で自動判別）:
/// - trajectory_*.csv        : 収録走の頭部視点をそのまま再生
/// - following_results_*.csv : 実験走の記録を再生（表示モードを選択可能）
///
/// 操作方法（キーボード）:
/// - Space : 再生 / 一時停止
/// - R     : 最初から再生し直す
/// - ← / → : 5秒 巻き戻し / 早送り
/// </summary>
public class ReplayPlayer : MonoBehaviour
{
    /// <summary>
    /// following_results を再生するときの表示モード
    /// </summary>
    public enum DisplayMode
    {
        /// <summary>実験時と同じ時分割切替を再現する（source列に従いライブ/収録を切替）</summary>
        AsExperienced,
        /// <summary>被験者が実際に移動した頭部（ライブ）の視点のみを表示する</summary>
        LiveOnly,
        /// <summary>提示された収録映像側（ゴーストカメラ）の視点のみを表示する</summary>
        PlayedOnly,
    }

    /// <summary>
    /// 再生映像を映すカメラ（Game ビューに表示される）
    /// </summary>
    [Tooltip("再生映像を映すカメラ")]
    public Camera replayCamera;

    /// <summary>
    /// 読み込む CSV ファイル名（空欄なら保存先フォルダ内で最も新しい
    /// trajectory_*.csv / following_results_*.csv を自動選択）
    /// </summary>
    [Tooltip("読み込むCSVファイル名（空欄なら最新ファイルを自動選択）")]
    public string fileName = "";

    /// <summary>
    /// following_results 再生時の表示モード（trajectory 再生時は無視される）
    /// </summary>
    [Tooltip("following_results再生時の表示モード（trajectoryでは無視）")]
    public DisplayMode displayMode = DisplayMode.AsExperienced;

    /// <summary>再生速度（1 = 実時間）</summary>
    [Tooltip("再生速度（1 = 実時間）")]
    [Range(0.1f, 4f)] public float playbackSpeed = 1f;

    /// <summary>最後まで再生したら最初に戻って繰り返すか</summary>
    [Tooltip("最後まで再生したら最初に戻って繰り返すか")]
    public bool loop = false;

    // ---- 読み込んだデータ ----
    private readonly List<float> times = new List<float>(8192);
    private readonly List<Vector3> livePositions = new List<Vector3>(8192);   // trajectory の場合はここに軌跡が入る
    private readonly List<Quaternion> liveRotations = new List<Quaternion>(8192);
    private readonly List<Vector3> recPositions = new List<Vector3>(8192);    // following のみ
    private readonly List<Quaternion> recRotations = new List<Quaternion>(8192);
    private readonly List<int> sources = new List<int>(8192);                 // following のみ（0=ライブ, 1=収録）

    private bool isFollowingFile;   // following_results か（false なら trajectory）
    private string loadedFileName = "";
    private float replayTime;
    private int index;
    private bool playing;

    private bool IsLoaded { get { return times.Count >= 2; } }
    private float Duration { get { return IsLoaded ? times[times.Count - 1] : 0f; } }

    private void Start()
    {
        Load();
    }

    private void Update()
    {
        // ---- キー操作 ----
        if (Input.GetKeyDown(KeyCode.Space)) playing = !playing;
        if (Input.GetKeyDown(KeyCode.R)) { replayTime = 0f; index = 0; playing = true; }
        if (Input.GetKeyDown(KeyCode.LeftArrow)) Seek(replayTime - 5f);
        if (Input.GetKeyDown(KeyCode.RightArrow)) Seek(replayTime + 5f);

        if (!IsLoaded || !playing) return;

        replayTime += Time.deltaTime * playbackSpeed;
        if (replayTime >= Duration)
        {
            if (loop)
            {
                replayTime = 0f;
                index = 0;
            }
            else
            {
                replayTime = Duration;
                playing = false;
            }
        }
        ApplyPose();
    }

    /// <summary>
    /// 再生位置を指定時刻へ移動する（巻き戻し対応のためインデックスを先頭から探し直す）
    /// </summary>
    private void Seek(float t)
    {
        replayTime = Mathf.Clamp(t, 0f, Duration);
        index = 0;
        ApplyPose();
    }

    /// <summary>
    /// 現在の再生時刻に対応する姿勢を補間してカメラに適用する
    /// </summary>
    private void ApplyPose()
    {
        while (index < times.Count - 2 && times[index + 1] <= replayTime)
        {
            index++;
        }
        int next = Mathf.Min(index + 1, times.Count - 1);
        float segment = times[next] - times[index];
        float t = segment > 0f ? Mathf.Clamp01((replayTime - times[index]) / segment) : 0f;

        // 表示すべき視点（ライブ/収録）を決める
        bool useRec = false;
        if (isFollowingFile)
        {
            switch (displayMode)
            {
                case DisplayMode.AsExperienced: useRec = sources[index] == 1; break;
                case DisplayMode.LiveOnly: useRec = false; break;
                case DisplayMode.PlayedOnly: useRec = true; break;
            }
        }

        List<Vector3> pos = useRec ? recPositions : livePositions;
        List<Quaternion> rot = useRec ? recRotations : liveRotations;

        if (replayCamera != null)
        {
            replayCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(pos[index], pos[next], t),
                Quaternion.Slerp(rot[index], rot[next], t));
        }
        CurrentSourceIsRec = useRec;
    }

    /// <summary>今表示している視点が収録側か（HUD表示用）</summary>
    public bool CurrentSourceIsRec { get; private set; }

    /// <summary>
    /// CSV を読み込む．ファイル名の先頭（trajectory / following_results）で形式を自動判別する．
    /// </summary>
    public bool Load()
    {
        string path = ResolveFilePath();
        if (path == null)
        {
            Debug.LogWarning("[ReplayPlayer] 再生できるCSVが見つかりません: " + FollowingPaths.DataDir);
            return false;
        }

        times.Clear();
        livePositions.Clear();
        liveRotations.Clear();
        recPositions.Clear();
        recRotations.Clear();
        sources.Clear();

        loadedFileName = Path.GetFileName(path);
        isFollowingFile = loadedFileName.StartsWith("following_results");
        var inv = CultureInfo.InvariantCulture;

        // ヘッダから列位置を特定する（列の追加・並び替えに強くするため）
        Dictionary<string, int> col = null;
        foreach (string line in File.ReadLines(path))
        {
            if (line.Length == 0) continue;
            string[] c = line.Split(',');
            if (col == null)
            {
                col = new Dictionary<string, int>();
                for (int i = 0; i < c.Length; i++) col[c[i].Trim()] = i;
                continue;
            }

            times.Add(float.Parse(c[col["time"]], inv));
            if (isFollowingFile)
            {
                sources.Add(int.Parse(c[col["source"]], inv));
                livePositions.Add(new Vector3(
                    float.Parse(c[col["livePosX"]], inv), float.Parse(c[col["livePosY"]], inv), float.Parse(c[col["livePosZ"]], inv)));
                liveRotations.Add(Quaternion.Euler(
                    float.Parse(c[col["liveRotX"]], inv), float.Parse(c[col["liveRotY"]], inv), float.Parse(c[col["liveRotZ"]], inv)));
                recPositions.Add(new Vector3(
                    float.Parse(c[col["recPosX"]], inv), float.Parse(c[col["recPosY"]], inv), float.Parse(c[col["recPosZ"]], inv)));
                recRotations.Add(Quaternion.Euler(
                    float.Parse(c[col["recRotX"]], inv), float.Parse(c[col["recRotY"]], inv), float.Parse(c[col["recRotZ"]], inv)));
            }
            else
            {
                livePositions.Add(new Vector3(
                    float.Parse(c[col["posX"]], inv), float.Parse(c[col["posY"]], inv), float.Parse(c[col["posZ"]], inv)));
                liveRotations.Add(new Quaternion(
                    float.Parse(c[col["qX"]], inv), float.Parse(c[col["qY"]], inv), float.Parse(c[col["qZ"]], inv), float.Parse(c[col["qW"]], inv)));
            }
        }

        if (!IsLoaded)
        {
            Debug.LogWarning("[ReplayPlayer] CSVの内容が不正です: " + path);
            return false;
        }

        replayTime = 0f;
        index = 0;
        playing = true;
        ApplyPose();
        Debug.Log("[ReplayPlayer] 読み込み完了: " + loadedFileName
            + " (" + (isFollowingFile ? "following" : "trajectory") + ", "
            + times.Count + "サンプル, " + Duration.ToString("F1") + "s)");
        return true;
    }

    /// <summary>
    /// 読み込むファイルのパスを決定する（fileName 空欄なら更新日時が最も新しいCSV）
    /// </summary>
    private string ResolveFilePath()
    {
        string dir = FollowingPaths.DataDir;

        if (!string.IsNullOrEmpty(fileName))
        {
            string path = Path.Combine(dir, fileName);
            return File.Exists(path) ? path : null;
        }

        string newest = null;
        System.DateTime newestTime = System.DateTime.MinValue;
        foreach (string pattern in new[] { "trajectory_*.csv", "following_results_*.csv" })
        {
            foreach (string f in Directory.GetFiles(dir, pattern))
            {
                System.DateTime w = File.GetLastWriteTime(f);
                if (w > newestTime)
                {
                    newestTime = w;
                    newest = f;
                }
            }
        }
        return newest;
    }

    /// <summary>
    /// 画面左上に再生状態と操作方法を表示する（ビルドでも表示される）
    /// </summary>
    private void OnGUI()
    {
        if (!IsLoaded)
        {
            GUI.Label(new Rect(10, 10, 800, 20), "CSVが読み込まれていません（Inspector の File Name とデータフォルダを確認）");
            return;
        }

        // 表示中の視点をバナーで示す（収録=オレンジ / ライブ=青）
        string sourceLabel;
        Color bannerColor;
        if (!isFollowingFile)
        {
            sourceLabel = "収録走の再生: " + loadedFileName;
            bannerColor = new Color(0.16f, 0.47f, 0.84f, 0.85f);
        }
        else if (CurrentSourceIsRec)
        {
            sourceLabel = "収録映像（提示側） | " + displayMode + " | " + loadedFileName;
            bannerColor = new Color(0.92f, 0.41f, 0.20f, 0.85f);
        }
        else
        {
            sourceLabel = "ライブ（被験者の移動） | " + displayMode + " | " + loadedFileName;
            bannerColor = new Color(0.16f, 0.47f, 0.84f, 0.85f);
        }

        Color prev = GUI.color;
        GUI.color = bannerColor;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, 26), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(10, 4, Screen.width - 20, 20), sourceLabel);
        GUI.color = prev;

        GUI.Label(new Rect(10, 32, 800, 20),
            (playing ? "再生中" : "一時停止") + "  " + replayTime.ToString("F1") + " / " + Duration.ToString("F1") + " s"
            + "   速度 x" + playbackSpeed.ToString("F1"));
        GUI.Label(new Rect(10, 52, 800, 20), "Space: 再生/停止   R: 最初から   ←/→: ±5秒   1/2/3: 環境密度");
    }
}
