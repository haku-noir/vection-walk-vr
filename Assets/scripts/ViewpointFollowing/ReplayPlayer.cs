using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 実験で保存した CSV を読み込み，そのとき記録された視点を通常のカメラ（Game ビュー）で
/// 再現するクラス．HMD の接続は不要．
///
/// 従来は「実際に表示されていた映像」を再現するだけだったが，
/// ライブ姿勢と収録姿勢の両方を2台のカメラで同時に再レンダリングするようにしたため，
/// <b>収録後にパラメータを変えて表示を作り直せる</b>:
/// - 切替周波数の変更（実験時と違う周波数で再度ライブ⇔収録を切り替える）
/// - 4ストローク合成の追加（実験時と同じ FourStrokeCompositor をそのまま流用）
///
/// 対応ファイル（ファイル名の先頭で自動判別）:
/// - trajectory_*.csv        : 収録走の頭部視点をそのまま再生（ライブのみ）
/// - following_results_*.csv : 実験走の記録を再生（下記の表示モードを選択可能）
///
/// 操作方法（キーボード）:
/// - Space : 再生 / 一時停止
/// - R     : 最初から再生し直す
/// - ← / → : 5秒 巻き戻し / 早送り
/// - M     : 表示モード切替（AsExperienced → LiveOnly → PlayedOnly → Reswitch）
/// - ↑ / ↓ : 切替周波数 ±0.5Hz（Reswitch で有効）
/// - 4     : 4ストローク合成の ON/OFF（Reswitch で有効）
/// - V     : 4ストロークの極性切替（Enhance → Reversal → Zero）
/// - 1/2/3 : 環境密度（EnvironmentSwitcher）
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
        /// <summary>収録後にパラメータを変えて再合成する（切替周波数の変更・4ストロークの追加）</summary>
        Reswitch,
    }

    [Header("再生カメラ（シーンビルダーが自動設定）")]
    /// <summary>ライブ姿勢（被験者の頭部）を再現し，ライブ映像テクスチャへ描画するカメラ</summary>
    [Tooltip("ライブ姿勢を再現するカメラ（→ ライブ映像テクスチャ）")]
    public Camera liveCamera;

    /// <summary>収録姿勢（提示された収録映像側）を再現し，収録映像テクスチャへ描画するカメラ</summary>
    [Tooltip("収録姿勢を再現するカメラ（→ 収録映像テクスチャ）")]
    public Camera ghostCamera;

    [Header("映像出力（シーンビルダーが自動設定）")]
    /// <summary>合成・切替を担う ViewSwitcher（実験シーンと同じものを流用）</summary>
    [Tooltip("表示の切替・4ストローク合成を担う ViewSwitcher")]
    public ViewSwitcher viewSwitcher;

    /// <summary>視野として表示している UI（RawImage）</summary>
    [Tooltip("視野として表示しているUI（RawImage）")]
    public RawImage rawImage;

    /// <summary>ライブ映像テクスチャ（liveCamera のターゲット）</summary>
    [Tooltip("ライブ映像テクスチャ（liveCamera のターゲット）")]
    public Texture liveTexture;

    /// <summary>収録映像テクスチャ（ghostCamera のターゲット）</summary>
    [Tooltip("収録映像テクスチャ（ghostCamera のターゲット）")]
    public Texture playbackTexture;

    [Header("再生設定")]
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
    private DisplayMode lastDisplayMode;

    private bool IsLoaded { get { return times.Count >= 2; } }
    private float Duration { get { return IsLoaded ? times[times.Count - 1] : 0f; } }

    private void Start()
    {
        Load();
    }

    private void Update()
    {
        // ---- 再生キー ----
        if (Input.GetKeyDown(KeyCode.Space)) playing = !playing;
        if (Input.GetKeyDown(KeyCode.R)) { replayTime = 0f; index = 0; playing = true; }
        if (Input.GetKeyDown(KeyCode.LeftArrow)) Seek(replayTime - 5f);
        if (Input.GetKeyDown(KeyCode.RightArrow)) Seek(replayTime + 5f);

        // ---- 表示パラメータのキー（following のみ有効） ----
        HandleDisplayKeys();

        if (!IsLoaded) return;

        // Inspector から displayMode を変えられた場合も追随する
        if (displayMode != lastDisplayMode)
        {
            lastDisplayMode = displayMode;
            ConfigureDisplay();
        }

        if (playing)
        {
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

        // カメラ姿勢に依らず表示テクスチャは毎フレーム反映する
        // （AsExperienced は source 列に沿ってライブ/収録が切り替わるため）
        ApplyDisplay();
    }

    /// <summary>
    /// 表示モード・切替周波数・4ストロークの調整キーを処理する（following 再生時のみ）
    /// </summary>
    private void HandleDisplayKeys()
    {
        if (!isFollowingFile) return;

        // 表示モード巡回（M）
        if (Input.GetKeyDown(KeyCode.M))
        {
            displayMode = NextDisplayMode(displayMode);
            lastDisplayMode = displayMode;
            ConfigureDisplay();
            Debug.Log("[ReplayPlayer] 表示モード: " + displayMode);
        }

        if (viewSwitcher == null) return;

        // 切替周波数 ±0.5Hz（↑/↓。Reswitch で反映される）
        if (Input.GetKeyDown(KeyCode.UpArrow))
            viewSwitcher.switchFrequency = Mathf.Clamp(viewSwitcher.switchFrequency + 0.5f, 0.1f, 10f);
        if (Input.GetKeyDown(KeyCode.DownArrow))
            viewSwitcher.switchFrequency = Mathf.Clamp(viewSwitcher.switchFrequency - 0.5f, 0.1f, 10f);

        // 4ストローク合成 ON/OFF（4。Reswitch で反映される）
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
        {
            viewSwitcher.fourStrokeEnabled = !viewSwitcher.fourStrokeEnabled;
            Debug.Log("[ReplayPlayer] 4ストローク: " + (viewSwitcher.fourStrokeEnabled ? "ON" : "OFF"));
        }

        // 4ストロークの極性巡回（V: Enhance → Reversal → Zero）
        if (viewSwitcher.fourStroke != null && Input.GetKeyDown(KeyCode.V))
        {
            viewSwitcher.fourStroke.polarity = NextPolarity(viewSwitcher.fourStroke.polarity);
            Debug.Log("[ReplayPlayer] 4ストローク極性: " + viewSwitcher.fourStroke.polarity);
        }
    }

    /// <summary>表示モードの巡回順</summary>
    private static DisplayMode NextDisplayMode(DisplayMode m)
    {
        switch (m)
        {
            case DisplayMode.AsExperienced: return DisplayMode.LiveOnly;
            case DisplayMode.LiveOnly: return DisplayMode.PlayedOnly;
            case DisplayMode.PlayedOnly: return DisplayMode.Reswitch;
            default: return DisplayMode.AsExperienced;
        }
    }

    /// <summary>極性の巡回順（実験シーンと同じ Enhance → Reversal → Zero）</summary>
    private static FourStrokeCompositor.Polarity NextPolarity(FourStrokeCompositor.Polarity p)
    {
        switch (p)
        {
            case FourStrokeCompositor.Polarity.Enhance: return FourStrokeCompositor.Polarity.Reversal;
            case FourStrokeCompositor.Polarity.Reversal: return FourStrokeCompositor.Polarity.Zero;
            default: return FourStrokeCompositor.Polarity.Enhance;
        }
    }

    /// <summary>
    /// 表示モードに応じて ViewSwitcher の有効・モードを設定する
    /// （AsExperienced と trajectory は ReplayPlayer 自身がテクスチャを差し替える）
    /// </summary>
    private void ConfigureDisplay()
    {
        // 収録映像側カメラは following のときだけ動かす（trajectory では収録姿勢が無い）
        if (ghostCamera != null) ghostCamera.gameObject.SetActive(isFollowingFile);

        if (!isFollowingFile) { DisableSwitcher(); return; }

        switch (displayMode)
        {
            case DisplayMode.AsExperienced: DisableSwitcher(); break;
            case DisplayMode.LiveOnly: EnableSwitcher(ViewSwitcher.SourceMode.LiveOnly); break;
            case DisplayMode.PlayedOnly: EnableSwitcher(ViewSwitcher.SourceMode.PlaybackOnly); break;
            case DisplayMode.Reswitch: EnableSwitcher(ViewSwitcher.SourceMode.Alternate); break;
        }
    }

    /// <summary>ViewSwitcher に表示を委ねる（切替・4ストロークは ViewSwitcher が担当）</summary>
    private void EnableSwitcher(ViewSwitcher.SourceMode mode)
    {
        if (viewSwitcher == null) { if (rawImage != null) rawImage.texture = liveTexture; return; }
        viewSwitcher.enabled = true;
        viewSwitcher.mode = mode;
        viewSwitcher.ResetPhase(); // 再有効化時にもテクスチャを確実に適用する
    }

    /// <summary>ViewSwitcher を止め，ReplayPlayer 自身がテクスチャを差し替える</summary>
    private void DisableSwitcher()
    {
        if (viewSwitcher == null) return;
        viewSwitcher.enabled = false;
        if (viewSwitcher.fourStroke != null) viewSwitcher.fourStroke.enabled = false; // 無駄な合成を止める
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
    /// 現在の再生時刻に対応する姿勢を補間して各カメラに適用する．
    /// following ではライブ姿勢を liveCamera，収録姿勢を ghostCamera の両方へ同時に適用し，
    /// どちらを表示するか（合成するか）は表示側（ViewSwitcher / ApplyDisplay）で決める．
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

        if (liveCamera != null)
        {
            liveCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(livePositions[index], livePositions[next], t),
                Quaternion.Slerp(liveRotations[index], liveRotations[next], t));
        }

        if (isFollowingFile && ghostCamera != null)
        {
            ghostCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(recPositions[index], recPositions[next], t),
                Quaternion.Slerp(recRotations[index], recRotations[next], t));
        }
    }

    /// <summary>
    /// 現在のモードに応じて RawImage に映すテクスチャを決める．
    /// ViewSwitcher が担当するモード（LiveOnly/PlayedOnly/Reswitch）では表示ソースの読み取りのみ行う．
    /// </summary>
    private void ApplyDisplay()
    {
        if (rawImage == null) return;

        if (!isFollowingFile)
        {
            rawImage.texture = liveTexture;
            rawImage.color = Color.white;
            CurrentSourceIsRec = false;
            return;
        }

        if (displayMode == DisplayMode.AsExperienced)
        {
            bool useRec = index < sources.Count && sources[index] == 1;
            rawImage.texture = useRec ? playbackTexture : liveTexture;
            rawImage.color = Color.white;
            CurrentSourceIsRec = useRec;
        }
        else
        {
            // ViewSwitcher が rawImage.texture を管理する．バナー表示用にソースだけ読み取る
            CurrentSourceIsRec = viewSwitcher != null && viewSwitcher.CurrentSource == 1;
        }
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
        lastDisplayMode = displayMode;
        ConfigureDisplay(); // ファイル種別に応じて表示経路を初期化する
        ApplyPose();
        ApplyDisplay();
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
        Color live = new Color(0.16f, 0.47f, 0.84f, 0.85f);
        Color rec = new Color(0.92f, 0.41f, 0.20f, 0.85f);
        if (!isFollowingFile)
        {
            sourceLabel = "収録走の再生: " + loadedFileName;
            bannerColor = live;
        }
        else if (CurrentSourceIsRec)
        {
            sourceLabel = "収録映像（提示側） | " + displayMode + " | " + loadedFileName;
            bannerColor = rec;
        }
        else
        {
            sourceLabel = "ライブ（被験者の移動） | " + displayMode + " | " + loadedFileName;
            bannerColor = live;
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

        // following はパラメータ操作の状態を表示する
        if (isFollowingFile && viewSwitcher != null)
        {
            string fs = viewSwitcher.fourStrokeEnabled
                ? "ON (" + (viewSwitcher.fourStroke != null ? viewSwitcher.fourStroke.polarity.ToString() : "?") + ")"
                : "OFF";
            GUI.Label(new Rect(10, 52, 800, 20),
                "モード: " + displayMode + "   切替周波数: " + viewSwitcher.switchFrequency.ToString("F1") + " Hz"
                + "   4ストローク: " + fs + (displayMode == DisplayMode.Reswitch ? "" : "（Reswitchで反映）"));
            GUI.Label(new Rect(10, 72, 900, 20),
                "Space:再生/停止  R:最初から  ←/→:±5秒  M:表示モード  ↑↓:周波数  4:4ストローク  V:極性  1/2/3:環境密度");
        }
        else
        {
            GUI.Label(new Rect(10, 52, 800, 20), "Space: 再生/停止   R: 最初から   ←/→: ±5秒   1/2/3: 環境密度");
        }
    }
}
