using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// 再生走：TrajectoryRecorder が保存した軌跡 CSV を読み込み，
/// ゴーストカメラ（収録映像を再レンダリングするカメラ）を軌跡どおりに動かすクラス．
/// 位置は Vector3.Lerp，回転は Quaternion.Slerp で時刻補間する．
/// </summary>
public class TrajectoryPlayer : MonoBehaviour
{
    /// <summary>
    /// 収録軌跡から再生する成分の選択
    /// </summary>
    public enum PlaybackComponents
    {
        /// <summary>位置・回転とも収録軌跡を再生する（従来の動作）</summary>
        PositionAndRotation,
        /// <summary>位置のみ収録軌跡を再生し，回転は現在の HMD の回転を使う</summary>
        PositionOnly,
        /// <summary>回転のみ収録軌跡を再生し，位置は現在の HMD の位置を使う</summary>
        RotationOnly,
    }

    /// <summary>
    /// 軌跡どおりに動かすゴーストカメラ（PlaybackEye RenderTexture に描画するカメラ）
    /// </summary>
    [Tooltip("軌跡どおりに動かすゴーストカメラ")]
    public Transform ghostCamera;

    /// <summary>
    /// 収録軌跡から再生する成分．PositionOnly では回転は現在の HMD を，
    /// RotationOnly では位置は現在の HMD を参照する．
    /// </summary>
    [Tooltip("収録軌跡から再生する成分（残りの成分は現在のHMDを参照する）")]
    public PlaybackComponents playbackComponents = PlaybackComponents.PositionAndRotation;

    /// <summary>
    /// 現在の HMD の頭部（CenterEyeAnchor）．PositionOnly / RotationOnly のときに
    /// 収録しない側の成分の参照元になる．未設定なら FollowingExperimentManager が自動設定する．
    /// </summary>
    [Tooltip("現在のHMDの頭部（PositionOnly/RotationOnly時の参照元）")]
    public Transform headAnchor;

    /// <summary>
    /// 読み込む軌跡ファイル名（空欄なら保存先フォルダ内の最新の trajectory_*.csv を自動選択）
    /// </summary>
    [Tooltip("読み込む軌跡ファイル名（空欄なら最新の trajectory_*.csv を自動選択）")]
    public string fileName = "";

    // 読み込んだ軌跡データ
    private readonly List<float> times = new List<float>(8192);
    private readonly List<Vector3> positions = new List<Vector3>(8192);
    private readonly List<Quaternion> rotations = new List<Quaternion>(8192);

    private float playTime;   // 再生開始からの経過時間
    private int index;        // 現在の再生位置（times[index] <= playTime < times[index+1]）

    /// <summary>軌跡が読み込み済みか</summary>
    public bool IsLoaded { get { return times.Count >= 2; } }
    /// <summary>再生中か</summary>
    public bool IsPlaying { get; private set; }
    /// <summary>軌跡の最後まで再生し終えたか</summary>
    public bool IsFinished { get; private set; }
    /// <summary>軌跡の全長[s]</summary>
    public float Duration { get { return IsLoaded ? times[times.Count - 1] : 0f; } }
    /// <summary>現在の再生時刻[s]</summary>
    public float CurrentTime { get { return playTime; } }
    /// <summary>現在の収録位置（ロガーが追従誤差の計算に使う）</summary>
    public Vector3 CurrentPosition { get; private set; }
    /// <summary>現在の収録回転</summary>
    public Quaternion CurrentRotation { get; private set; }
    /// <summary>収録開始時点の頭部位置（開始地点合わせに使う）</summary>
    public Vector3 StartPosition { get { return IsLoaded ? positions[0] : Vector3.zero; } }
    /// <summary>収録開始時点の頭部回転（開始地点合わせに使う）</summary>
    public Quaternion StartRotation { get { return IsLoaded ? rotations[0] : Quaternion.identity; } }

    /// <summary>
    /// 軌跡 CSV を読み込む（既に読み込み済みなら何もしない）
    /// </summary>
    /// <returns>読み込みに成功したか</returns>
    public bool EnsureLoaded()
    {
        if (IsLoaded) return true;
        return Load();
    }

    /// <summary>
    /// 軌跡 CSV を読み込む
    /// </summary>
    public bool Load()
    {
        string path = ResolveFilePath();
        if (path == null)
        {
            Debug.LogWarning("[TrajectoryPlayer] 軌跡ファイルが見つかりません: " + FollowingPaths.DataDir);
            return false;
        }

        times.Clear();
        positions.Clear();
        rotations.Clear();

        var inv = CultureInfo.InvariantCulture;
        foreach (string line in File.ReadLines(path))
        {
            // ヘッダ行・空行はスキップ
            if (line.Length == 0 || line.StartsWith("time")) continue;

            string[] c = line.Split(',');
            if (c.Length < 8) continue;

            times.Add(float.Parse(c[0], inv));
            positions.Add(new Vector3(float.Parse(c[1], inv), float.Parse(c[2], inv), float.Parse(c[3], inv)));
            rotations.Add(new Quaternion(float.Parse(c[4], inv), float.Parse(c[5], inv), float.Parse(c[6], inv), float.Parse(c[7], inv)));
        }

        if (!IsLoaded)
        {
            Debug.LogWarning("[TrajectoryPlayer] 軌跡データが不正です: " + path);
            return false;
        }

        Debug.Log("[TrajectoryPlayer] 読み込み完了: " + Path.GetFileName(path)
            + " (" + times.Count + "サンプル, " + Duration.ToString("F1") + "s)");
        return true;
    }

    /// <summary>
    /// 再生を最初から開始する（Load 済みであること）
    /// </summary>
    public void StartPlayback()
    {
        if (!IsLoaded)
        {
            Debug.LogWarning("[TrajectoryPlayer] 軌跡が読み込まれていません");
            return;
        }
        playTime = 0f;
        index = 0;
        IsPlaying = true;
        IsFinished = false;
        ApplySample(); // 開始位置に即座に配置
        Debug.Log("[TrajectoryPlayer] 再生開始");
    }

    /// <summary>
    /// 再生を停止する
    /// </summary>
    public void StopPlayback()
    {
        IsPlaying = false;
    }

    private void Update()
    {
        if (!IsPlaying) return;

        // Time.deltaTime は timeScale の影響を受けるため，一時停止（timeScale=0）中は
        // 再生時刻も自動的に止まる（実験全体のポーズ機構と整合させるための設計）
        playTime += Time.deltaTime;
        ApplySample();

        if (playTime >= Duration)
        {
            IsFinished = true;
            IsPlaying = false;
            Debug.Log("[TrajectoryPlayer] 再生終了");
        }
    }

    /// <summary>
    /// 現在の再生時刻に対応する位置・回転を補間してゴーストカメラに適用する
    /// </summary>
    /// <remarks>
    /// CurrentPosition / CurrentRotation には常に「収録軌跡の値」が入る
    /// （playbackComponents に関わらず，ロガーは収録値との比較を記録できる）．
    /// ゴーストカメラへの適用時のみ，収録しない側の成分を現在の HMD で置き換える．
    /// </remarks>
    private void ApplySample()
    {
        // 再生位置のインデックスを進める
        while (index < times.Count - 2 && times[index + 1] <= playTime)
        {
            index++;
        }

        int next = Mathf.Min(index + 1, times.Count - 1);
        float segment = times[next] - times[index];
        float t = segment > 0f ? Mathf.Clamp01((playTime - times[index]) / segment) : 0f;

        CurrentPosition = Vector3.Lerp(positions[index], positions[next], t);
        CurrentRotation = Quaternion.Slerp(rotations[index], rotations[next], t);

        if (ghostCamera != null)
        {
            Vector3 ghostPos = CurrentPosition;
            Quaternion ghostRot = CurrentRotation;

            // 収録しない側の成分は現在の HMD（CenterEyeAnchor）を参照する
            if (headAnchor != null)
            {
                if (playbackComponents == PlaybackComponents.PositionOnly)
                {
                    ghostRot = headAnchor.rotation;   // 回転はライブ
                }
                else if (playbackComponents == PlaybackComponents.RotationOnly)
                {
                    ghostPos = headAnchor.position;   // 位置はライブ
                }
            }
            else if (playbackComponents != PlaybackComponents.PositionAndRotation)
            {
                Debug.LogWarning("[TrajectoryPlayer] headAnchor が未設定のため PositionAndRotation として再生します");
            }

            ghostCamera.SetPositionAndRotation(ghostPos, ghostRot);
        }
    }

    /// <summary>
    /// 読み込むファイルのパスを決定する（fileName 空欄なら最新ファイル）
    /// </summary>
    private string ResolveFilePath()
    {
        string dir = FollowingPaths.DataDir;

        if (!string.IsNullOrEmpty(fileName))
        {
            string path = Path.Combine(dir, fileName);
            return File.Exists(path) ? path : null;
        }

        // ファイル名にタイムスタンプが入っているため，名前順ソートの末尾が最新
        string[] files = Directory.GetFiles(dir, "trajectory_*.csv");
        if (files.Length == 0) return null;
        System.Array.Sort(files);
        return files[files.Length - 1];
    }
}
