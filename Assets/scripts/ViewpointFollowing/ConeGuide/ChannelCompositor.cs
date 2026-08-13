using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背景と箱を<b>独立した2チャンネル</b>として合成する合成器（09 仕様 §2.2）．
///
/// 4つの入力を受け取り，チャンネルごとに選んだ提示条件で合成して RawImage に出す:
///
/// | 入力 | 中身 | 撮るカメラ |
/// |---|---|---|
/// | BG_Live | 環境（ライブ視点） | CenterEyeCapture / LiveReplayCamera |
/// | BG_Ghost | 環境（収録視点） | GhostCamera / GhostReplayCamera |
/// | Box_Other | Cone_Other のみ（透明背景） | LiveBoxCam |
/// | Box_Self | Cone_Self のみ（透明背景） | GhostBoxCam |
///
/// | チャンネル | 選択肢 |
/// |---|---|
/// | 背景 | Live固定 / Ghost固定 / 矩形波交替(f_bg) / 4ストローク(f_bg, 極性) |
/// | 箱 | Off / Other固定 / Self固定 / 矩形波交替(f_box) / 4ストローク(f_box, 極性) |
///
/// 周波数・極性はチャンネルごとに独立．デューティ比は 50% 固定．
/// </summary>
/// <remarks>
/// - <b>既存の ViewSwitcher の単一テクスチャ経路は変更していない</b>．この合成器が有効な間だけ
///   ViewSwitcher を無効化して表示を引き取り，無効化すると ViewSwitcher の従来動作に戻る．
/// - 背景の切替周波数 f_bg は ViewSwitcher.switchFrequency を参照する（周波数の持ち主を1つに保つため）．
/// - 位相は Time.deltaTime で進むため，timeScale=0 の一時停止中は自動的に止まる（unscaledTime は使わない）．
/// - 合成は LateUpdate の Graphics.Blit（GPU 1パス）．
/// </remarks>
public class ChannelCompositor : MonoBehaviour
{
    /// <summary>背景チャンネルの提示条件</summary>
    public enum BackgroundMode
    {
        /// <summary>ライブ映像に固定</summary>
        LiveFixed,
        /// <summary>収録映像に固定</summary>
        GhostFixed,
        /// <summary>ライブ⇔収録を f_bg で矩形波交替（従来の本実験条件）</summary>
        SquareAlternate,
        /// <summary>ライブと収録を f_bg で4ストローク合成</summary>
        FourStroke,
    }

    /// <summary>箱チャンネルの提示条件</summary>
    public enum BoxMode
    {
        /// <summary>箱を出さない（ベースライン）</summary>
        Off,
        /// <summary>Cone_Other のみ（視覚運動極性は正常）</summary>
        OtherFixed,
        /// <summary>Cone_Self のみ（視覚運動極性は反転）</summary>
        SelfFixed,
        /// <summary>Other⇔Self を f_box で矩形波交替（時分割によるワブル）</summary>
        SquareAlternate,
        /// <summary>Other と Self を f_box で4ストローク合成（極性を一義化）</summary>
        FourStroke,
    }

    [Header("入力テクスチャ（シーンアップグレーダが自動設定）")]
    /// <summary>背景（ライブ視点）＝ CenterEye RenderTexture</summary>
    [Tooltip("背景（ライブ視点）= CenterEye RT")]
    public Texture bgLiveTexture;

    /// <summary>背景（収録視点）＝ PlaybackEye RenderTexture</summary>
    [Tooltip("背景（収録視点）= PlaybackEye RT")]
    public Texture bgGhostTexture;

    /// <summary>Cone_Other のみを描いた透明背景の RenderTexture</summary>
    [Tooltip("Cone_Other のみ（透明背景・アルファ付き）= Box_Other RT")]
    public Texture boxOtherTexture;

    /// <summary>Cone_Self のみを描いた透明背景の RenderTexture</summary>
    [Tooltip("Cone_Self のみ（透明背景・アルファ付き）= Box_Self RT")]
    public Texture boxSelfTexture;

    [Header("出力・連携先")]
    /// <summary>合成結果を表示する UI（CenterRawImage / ReplayRawImage）</summary>
    [Tooltip("合成結果を表示するRawImage")]
    public RawImage rawImage;

    /// <summary>
    /// 背景の切替周波数 f_bg の取得元．この合成器が有効な間は ViewSwitcher を無効化して
    /// 表示を引き取る（従来の単一テクスチャ経路は壊さない）．
    /// </summary>
    [Tooltip("f_bg の取得元。有効な間は ViewSwitcher を無効化して表示を引き取る")]
    public ViewSwitcher viewSwitcher;

    /// <summary>別色モードの検出に使う（Cone_Other 側）</summary>
    [Tooltip("Cone_Other（別色モードの検出に使う）")]
    public ConeGuide coneOther;

    /// <summary>別色モードの検出に使う（Cone_Self 側）</summary>
    [Tooltip("Cone_Self（別色モードの検出に使う）")]
    public ConeGuide coneSelf;

    /// <summary>
    /// Box_Other を撮っている箱カメラ．非アクティブのときは Box_Other RT に
    /// 古いフレームが残るため，箱の重みを 0 にして静止した箱が出るのを防ぐ．
    /// </summary>
    [Tooltip("Box_Other を撮る箱カメラ（非アクティブ時に古いフレームを出さないために参照する）")]
    public Camera liveBoxCamera;

    /// <summary>
    /// Box_Self を撮っている箱カメラ．GhostCamera は Record モードなどで
    /// 無効化されるため，その間は箱の重みを 0 にする．
    /// </summary>
    [Tooltip("Box_Self を撮る箱カメラ（Recordモード等で無効化されるため参照する）")]
    public Camera ghostBoxCamera;

    [Header("背景チャンネル")]
    /// <summary>背景の提示条件</summary>
    [Tooltip("背景の提示条件")]
    public BackgroundMode bgMode = BackgroundMode.SquareAlternate;

    /// <summary>背景の4ストローク極性（bgMode = FourStroke のときのみ有効）</summary>
    [Tooltip("背景の4ストローク極性")]
    public FourStrokeCompositor.Polarity bgPolarity = FourStrokeCompositor.Polarity.Zero;

    /// <summary>
    /// 背景の4ストローク時にグレースケール化するか（原典と同じく既定 ON）．
    /// 矩形波交替・固定表示ではカラーのまま提示される．
    /// </summary>
    [Tooltip("背景の4ストローク時にグレースケール化するか（既定ON）")]
    public bool bgGrayscale = true;

    /// <summary>
    /// ViewSwitcher が未設定のときに使う f_bg[Hz]．
    /// 通常は ViewSwitcher.switchFrequency が使われるため触る必要はない．
    /// </summary>
    [Tooltip("ViewSwitcher未設定時のみ使う f_bg[Hz]")]
    [Range(0.1f, 10f)] public float bgFrequencyFallback = 1f;

    [Header("箱チャンネル")]
    /// <summary>箱の提示条件</summary>
    [Tooltip("箱の提示条件")]
    public BoxMode boxMode = BoxMode.Off;

    /// <summary>箱の4ストローク極性（boxMode = FourStroke のときのみ有効）</summary>
    [Tooltip("箱の4ストローク極性")]
    public FourStrokeCompositor.Polarity boxPolarity = FourStrokeCompositor.Polarity.Zero;

    /// <summary>f_box を f_bg に同期させるか（仕様 §3.3 の既定）</summary>
    [Tooltip("f_box を f_bg に同期させるか（既定ON）")]
    public bool syncBoxFreqToBg = true;

    /// <summary>箱の切替周波数 f_box[Hz]（syncBoxFreqToBg = OFF のときのみ有効）</summary>
    [Tooltip("箱の切替周波数 f_box[Hz]（同期OFF時のみ有効）")]
    [Range(0.1f, 10f)] public float boxFrequency = 1f;

    /// <summary>
    /// 箱の輝度変調量 Δ（仕様 §2.3，既定 0.35）．
    /// final = BG + boxMask × sign × Δ
    /// </summary>
    [Tooltip("箱の輝度変調量 Δ（0-1輝度、既定0.35）")]
    [Range(0f, 1f)] public float boxDelta = 0.35f;

    [Header("シェーダ（未設定なら自動検索）")]
    /// <summary>合成シェーダ（Hidden/ChannelComposite）</summary>
    [Tooltip("合成シェーダ（Hidden/ChannelComposite。未設定なら自動検索）")]
    public Shader shader;

    /// <summary>合成結果の出力先</summary>
    public RenderTexture OutputTexture { get; private set; }

    /// <summary>
    /// 今表示しているソース（0 = ライブ, 1 = 収録）．<b>背景チャンネル基準</b>で，
    /// 既存 ViewSwitcher.CurrentSource と同じ意味づけ．ロガーが記録に使う．
    /// </summary>
    public int CurrentSource { get; private set; }

    /// <summary>箱チャンネルで今どちらが支配的か（0 = Other, 1 = Self）</summary>
    public int BoxDominantSource { get; private set; }

    /// <summary>実際に使っている f_bg[Hz]</summary>
    public float BgFrequency
    {
        get { return viewSwitcher != null ? viewSwitcher.switchFrequency : bgFrequencyFallback; }
    }

    /// <summary>実際に使っている f_box[Hz]</summary>
    public float BoxFrequency
    {
        get { return syncBoxFreqToBg ? BgFrequency : boxFrequency; }
    }

    /// <summary>別色モードが有効か（どちらかの錐が別色モードなら true）</summary>
    public bool DualColorActive
    {
        get
        {
            return (coneOther != null && coneOther.dualColorMode)
                || (coneSelf != null && coneSelf.dualColorMode);
        }
    }

    // 背景用・箱用の位相は完全に独立して進む
    private readonly ChannelPhase bgPhase = new ChannelPhase();
    private readonly ChannelPhase boxPhase = new ChannelPhase();

    private Material material;
    private bool switcherWasEnabled;

    // 別色モードと箱4ストロークの排他処理（仕様 §2.3 / §3.1）の状態
    private bool dualColorWarned;      // 警告ログを1回だけ出すためのフラグ
    private bool dualColorAutoDisabled; // HUD に「自動無効化した」旨を出すためのフラグ

    /// <summary>
    /// 箱の4ストロークと排他だったため別色モードを自動無効化したか（HUD 表示用）
    /// </summary>
    public bool DualColorAutoDisabled { get { return dualColorAutoDisabled; } }

    private void OnEnable()
    {
        // 表示を引き取る（ViewSwitcher の単一テクスチャ経路は壊さず，止めるだけ）
        if (viewSwitcher != null)
        {
            switcherWasEnabled = viewSwitcher.enabled;
            viewSwitcher.enabled = false;
            if (viewSwitcher.fourStroke != null) viewSwitcher.fourStroke.enabled = false;
        }
        ResetPhase();
    }

    private void OnDisable()
    {
        // 従来の経路へ戻す
        if (viewSwitcher != null)
        {
            viewSwitcher.enabled = switcherWasEnabled;
            if (switcherWasEnabled) viewSwitcher.ResetPhase();
        }
    }

    private void OnDestroy()
    {
        ReleaseOutput();
        if (material != null) Object.Destroy(material);
    }

    /// <summary>
    /// 両チャンネルの提示位相をリセットする（試行開始時に呼ぶ）．
    /// 背景はライブ提示から，箱は Other 提示から始まる．
    /// </summary>
    public void ResetPhase()
    {
        bgPhase.Reset(WaveformOf(bgMode));
        boxPhase.Reset(WaveformOf(boxMode));
        CurrentSource = (bgMode == BackgroundMode.GhostFixed) ? 1 : 0;
        BoxDominantSource = (boxMode == BoxMode.SelfFixed) ? 1 : 0;

        // 箱の姿勢フィルタ（LPF）も揃えてリセットし，開始直後の過渡応答を出さない
        if (coneOther != null) coneOther.ResetPose();
        if (coneSelf != null) coneSelf.ResetPose();
    }

    private void LateUpdate()
    {
        if (bgLiveTexture == null || bgGhostTexture == null) return;
        if (!EnsureResources()) return;

        // 表示の担当を確実にこちらへ寄せる（ReplayPlayer など他所が ViewSwitcher を
        // 有効化し直しても，この合成器が有効な間は合成結果を出す）
        if (viewSwitcher != null && viewSwitcher.enabled) viewSwitcher.enabled = false;

        // 箱の4ストロークは輝度変調方式なので別色モードとは両立しない（仕様 §2.3）
        EnforceDualColorExclusivity();

        float dt = Time.deltaTime; // 一時停止中は 0 → 変調も止まる

        // ---------- 背景チャンネル ----------
        float bgWeightLive, bgWeightGhost, bgInvert;
        bool bgGray = false;
        switch (bgMode)
        {
            case BackgroundMode.LiveFixed:
                bgWeightLive = 1f; bgWeightGhost = 0f; bgInvert = 1f;
                CurrentSource = 0;
                break;
            case BackgroundMode.GhostFixed:
                bgWeightLive = 0f; bgWeightGhost = 1f; bgInvert = 1f;
                CurrentSource = 1;
                break;
            default: // SquareAlternate / FourStroke
                bgPhase.Advance(dt, BgFrequency, WaveformOf(bgMode), bgPolarity);
                bgWeightLive = bgPhase.WeightA;
                bgWeightGhost = bgPhase.WeightB;
                bgInvert = bgPhase.Inverted ? -1f : 1f;
                bgGray = bgMode == BackgroundMode.FourStroke && bgGrayscale;
                CurrentSource = bgPhase.DominantSource;
                break;
        }

        // ---------- 箱チャンネル ----------
        float boxWeightOther, boxWeightSelf, boxSign;
        switch (boxMode)
        {
            case BoxMode.Off:
                boxWeightOther = 0f; boxWeightSelf = 0f; boxSign = 1f;
                break;
            case BoxMode.OtherFixed:
                boxWeightOther = 1f; boxWeightSelf = 0f; boxSign = 1f;
                BoxDominantSource = 0;
                break;
            case BoxMode.SelfFixed:
                boxWeightOther = 0f; boxWeightSelf = 1f; boxSign = 1f;
                BoxDominantSource = 1;
                break;
            default: // SquareAlternate / FourStroke
                boxPhase.Advance(dt, BoxFrequency, WaveformOf(boxMode), boxPolarity);
                boxWeightOther = boxPhase.WeightA;
                boxWeightSelf = boxPhase.WeightB;
                // 輝度変調の符号．4ストロークの反転位相でのみ −1 になる（仕様 §2.3）
                boxSign = boxPhase.Inverted ? -1f : 1f;
                BoxDominantSource = boxPhase.DominantSource;
                break;
        }

        // 箱カメラが止まっている間は RT に古いフレームが残るので，その入力は出さない
        // （Record モードでは GhostCamera ごと GhostBoxCam が無効化される）
        if (liveBoxCamera != null && !liveBoxCamera.isActiveAndEnabled) boxWeightOther = 0f;
        if (ghostBoxCamera != null && !ghostBoxCamera.isActiveAndEnabled) boxWeightSelf = 0f;

        // ---------- 合成 ----------
        material.SetTexture("_BgLive", bgLiveTexture);
        material.SetTexture("_BgGhost", bgGhostTexture);
        material.SetTexture("_BoxOther", boxOtherTexture != null ? boxOtherTexture : Texture2D.blackTexture);
        material.SetTexture("_BoxSelf", boxSelfTexture != null ? boxSelfTexture : Texture2D.blackTexture);
        material.SetFloat("_BgWeightLive", bgWeightLive);
        material.SetFloat("_BgWeightGhost", bgWeightGhost);
        material.SetFloat("_BgInvert", bgInvert);
        material.SetFloat("_BgGrayscale", bgGray ? 1f : 0f);
        material.SetFloat("_BoxWeightOther", boxWeightOther);
        material.SetFloat("_BoxWeightSelf", boxWeightSelf);
        material.SetFloat("_BoxSign", boxSign);
        material.SetFloat("_BoxDelta", boxDelta);
        material.SetFloat("_BoxColorBlend", DualColorActive ? 1f : 0f);
        Graphics.Blit(null, OutputTexture, material);

        if (rawImage != null)
        {
            rawImage.texture = OutputTexture;
            rawImage.color = Color.white; // 合成結果に着色しない
        }
    }

    /// <summary>
    /// 箱の4ストロークと別色モードの排他を強制する（仕様 §2.3 / §3.1）．
    ///
    /// 箱の4ストロークは色情報を輝度に潰す<b>輝度変調方式</b>で実現しているため，
    /// 近＝シアン／遠＝マゼンタの別色モードとは両立しない．
    /// この組合せが選ばれたら<b>別色モードを自動的に無効化</b>し，警告と HUD 表示を出す．
    /// </summary>
    /// <remarks>
    /// 奥行き手がかりは稜線オクルージョン（形状による手がかり，仕様 §1.6）が担うので，
    /// 別色モードを切っても全条件で同じ奥行き手がかりが提供される．
    /// </remarks>
    private void EnforceDualColorExclusivity()
    {
        if (boxMode != BoxMode.FourStroke)
        {
            // 4ストロークを抜けたら状態を戻す（再び選ばれたらもう一度警告する）
            dualColorWarned = false;
            dualColorAutoDisabled = false;
            return;
        }
        if (!DualColorActive) return;

        if (coneOther != null) coneOther.dualColorMode = false;
        if (coneSelf != null) coneSelf.dualColorMode = false;
        dualColorAutoDisabled = true;

        if (!dualColorWarned)
        {
            Debug.LogWarning("[ChannelCompositor] 箱の4ストロークは輝度変調方式（仕様 §2.3）のため"
                + "別色モードと排他です。別色モードを自動的に無効化しました。"
                + "\n奥行き手がかりは稜線オクルージョン（§1.6）が担うため、単色でも前後の多義性は解消されます。");
            dualColorWarned = true;
        }
    }

    /// <summary>提示条件から波形を決める</summary>
    private static ChannelPhase.Waveform WaveformOf(BackgroundMode mode)
    {
        return mode == BackgroundMode.FourStroke
            ? ChannelPhase.Waveform.FourStroke : ChannelPhase.Waveform.Square;
    }

    /// <summary>提示条件から波形を決める</summary>
    private static ChannelPhase.Waveform WaveformOf(BoxMode mode)
    {
        return mode == BoxMode.FourStroke
            ? ChannelPhase.Waveform.FourStroke : ChannelPhase.Waveform.Square;
    }

    /// <summary>
    /// マテリアルと出力 RenderTexture を用意する（入力サイズが変わったら作り直す）
    /// </summary>
    private bool EnsureResources()
    {
        if (material == null)
        {
            if (shader == null) shader = Shader.Find("Hidden/ChannelComposite");
            if (shader == null)
            {
                Debug.LogError("[ChannelCompositor] シェーダ Hidden/ChannelComposite が見つかりません");
                enabled = false;
                return false;
            }
            material = new Material(shader);
        }

        if (OutputTexture == null
            || OutputTexture.width != bgLiveTexture.width
            || OutputTexture.height != bgLiveTexture.height)
        {
            ReleaseOutput();
            OutputTexture = new RenderTexture(bgLiveTexture.width, bgLiveTexture.height, 0)
            {
                name = "ChannelCompositeOutput",
            };
            OutputTexture.Create();
        }
        return true;
    }

    private void ReleaseOutput()
    {
        if (OutputTexture != null)
        {
            OutputTexture.Release();
            Object.Destroy(OutputTexture);
            OutputTexture = null;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// エディタ実行時のみ，現在のチャンネル条件を画面右上に表示する（実験者用）
    /// </summary>
    private void OnGUI()
    {
        string bg = bgMode.ToString()
            + (bgMode == BackgroundMode.FourStroke ? " " + bgPolarity : "")
            + (bgMode == BackgroundMode.SquareAlternate || bgMode == BackgroundMode.FourStroke
                ? " " + BgFrequency.ToString("F1") + "Hz" : "");
        string box = boxMode.ToString()
            + (boxMode == BoxMode.FourStroke ? " " + boxPolarity : "")
            + (boxMode == BoxMode.SquareAlternate || boxMode == BoxMode.FourStroke
                ? " " + BoxFrequency.ToString("F1") + "Hz" : "");
        GUI.Label(new Rect(Screen.width - 430, 10, 420, 20), "背景: " + bg);
        GUI.Label(new Rect(Screen.width - 430, 30, 420, 20),
            "箱: " + box + (DualColorActive ? "（別色モード）" : ""));

        // 別色モードを自動無効化した場合は目立つように出す（条件の取り違えを防ぐ）
        if (dualColorAutoDisabled)
        {
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.85f, 0.3f);
            GUI.Label(new Rect(Screen.width - 430, 70, 420, 40),
                "別色モードを自動無効化しました\n（箱の4ストロークは輝度変調方式のため排他）");
            GUI.color = prev;
        }

        // 箱の姿勢処理（仕様 §3.2）。2つの錐は同じ設定で運用する前提なので Other 側を代表に出す
        ConePoseFilter filter = coneOther != null ? coneOther.poseFilter : null;
        if (filter != null)
        {
            GUI.Label(new Rect(Screen.width - 430, 50, 420, 20),
                "箱の姿勢: ヨー=" + filter.yawMode + " ピッチ=" + filter.pitchMode
                + " ロール=" + filter.rollMode + (filter.enabled ? "" : "（無効）"));
        }
    }
#endif
}
