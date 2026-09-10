using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背景・箱・ガイドを<b>独立した3チャンネル</b>として合成する合成器（09 仕様 §2.2 / §3.7）．
///
/// 入力を受け取り，チャンネルごとに選んだ提示条件で合成して RawImage に出す:
///
/// | 入力 | 中身 | 撮るカメラ・作り方 |
/// |---|---|---|
/// | BG_Live | 環境（ライブ視点） | CenterEyeCapture / LiveReplayCamera |
/// | BG_Ghost | 環境（収録視点） | GhostCamera / GhostReplayCamera |
/// | Box_Other | Cone_Other 全体（近断面＋遠断面＋稜線，透明背景） | LiveBoxCam |
/// | Box_Self | Cone_Self のみ（透明背景） | GhostBoxCam |
/// | Guide_NearNow | 近断面のみの「今」 | LiveBoxNearCam |
/// | Guide_NearDelayed | 近断面のみの「数百ms前」 | LiveBoxNearCam を
///   <see cref="DelayedFrameBuffer"/> でリングバッファに複製した過去フレーム |
/// | Guide_FarNow | 遠断面のみの「今」 | LiveBoxFarCam |
/// | Guide_FarDelayed | 遠断面のみの「数百ms前」 | LiveBoxFarCam を
///   <see cref="DelayedFrameBuffer"/> でリングバッファに複製した過去フレーム |
/// | Guide_RidgeNow | 稜線のみの「今」 | LiveBoxRidgeCam |
/// | Guide_RidgeDelayed | 稜線のみの「数百ms前」 | LiveBoxRidgeCam を
///   <see cref="DelayedFrameBuffer"/> でリングバッファに複製した過去フレーム |
///
/// | チャンネル | 選択肢 |
/// |---|---|
/// | 背景 | Live固定 / Ghost固定 / 矩形波交替(f_bg) / 4ストローク(f_bg, 極性) |
/// | 箱 | Off / Other固定 / Self固定 / 矩形波交替(f_box) / 4ストローク(f_box, 極性) |
/// | ガイド | Off / 今固定 / 数百ms前固定 / 矩形波交替(f_guide) / 4ストローク(f_guide, 極性) |
///
/// 周波数・極性はチャンネルごとに独立．デューティ比は 50% 固定．
/// **箱チャンネル**（Cone_Other⇔Cone_Self，相互のワブル用）と**ガイドチャンネル**
/// （近断面・遠断面それぞれの今⇔数百ms前，4ストローク歩行シーンと同じ「今 vs 過去の自分」の
/// 仕組みを箱に適用したもの）は互いに独立しており，同時に有効化しても一方が他方の
/// テクスチャ・位相を書き換えることはない．
///
/// ガイドチャンネルの<b>提示条件（モード・極性・周波数）は近断面・遠断面・稜線で共有</b>する
/// （位相がずれると3者の4ストロークが噛み合わず，かえって見えにくくなるため）．
/// 一方，<see cref="guideNearEnabled"/> / <see cref="guideFarEnabled"/> /
/// <see cref="guideRidgeEnabled"/> で<b>どの部分にガイド効果を掛けるかは独立に ON/OFF</b>
/// できる（09 §3.7 拡張）．稜線そのものの表示/非表示は既存の
/// <see cref="ConeGuide.drawRidges"/> で別途 ON/OFF できる（ガイド効果とは独立）．
///
/// 「数百ms前」の<b>遅延量（フレーム数）も部位ごとに独立設定できる</b>
/// （<see cref="guideNearDelayFrames"/> / <see cref="guideFarDelayFrames"/> /
/// <see cref="guideRidgeDelayFrames"/>）．<see cref="guideDelayLocked"/> を ON にすると
/// 3部位すべてを <see cref="guideDelayFrames"/> の1つの値に固定できる（既定は ON）．
/// この合成器が各 <see cref="DelayedFrameBuffer.delayFrames"/> の唯一の書き込み元になる．
///
/// 両眼立体視化（10 §2.7 拡張）に伴い，<see cref="mirrorFrom"/> を設定すると，
/// 提示条件（モード・極性・周波数・輝度変調量・Enabledフラグ・遅延フレーム数）を
/// 同期元から毎フレーム<b>引く</b>（<see cref="ConeGuide.mirrorFrom"/> と同じパターン）．
/// 左目用・右目用の2インスタンスのうち片方をマスターとし，もう片方が
/// <see cref="mirrorFrom"/> でマスターを参照することで，両目の4ストローク・ワブルの
/// 位相が噛み合わなくなることを防ぐ．入力テクスチャ・カメラ・出力先など目ごとに
/// 固有の参照は同期しない．
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

    /// <summary>
    /// Cone_Other の近断面のみを描いた透明背景の RenderTexture（09 §3.7 拡張）．
    /// ガイドチャンネルの「今」入力（近断面側）．未設定なら近断面のガイドは出ない．
    /// </summary>
    [Tooltip("Cone_Other 近断面のみ（透明背景）= Guide_NearNow RT")]
    public Texture guideNearNowTexture;

    /// <summary>
    /// Cone_Other の遠断面のみを描いた透明背景の RenderTexture（09 §3.7 拡張）．
    /// ガイドチャンネルの「今」入力（遠断面側）．未設定なら遠断面のガイドは出ない．
    /// </summary>
    [Tooltip("Cone_Other 遠断面のみ（透明背景）= Guide_FarNow RT")]
    public Texture guideFarNowTexture;

    /// <summary>
    /// Cone_Other の稜線のみを描いた透明背景の RenderTexture（09 §3.7 拡張）．
    /// ガイドチャンネルの「今」入力（稜線側）．未設定なら稜線のガイドは出ない．
    /// </summary>
    [Tooltip("Cone_Other 稜線のみ（透明背景）= Guide_RidgeNow RT")]
    public Texture guideRidgeNowTexture;

    [Header("出力・連携先")]
    /// <summary>合成結果を表示する UI（CenterRawImage / ReplayRawImage）</summary>
    [Tooltip("合成結果を表示するRawImage")]
    public RawImage rawImage;

    /// <summary>
    /// Game画面確認用のミラー出力先（任意．未設定なら何もしない）．
    ///
    /// 両眼立体視化（10 仕様）以降，合成結果の本来の表示先である
    /// <see cref="rawImage"/> は <c>LeftRawImage</c>/<c>RightRawImage</c> であり，
    /// これらは <c>LeftCanvas</c>/<c>RightCanvas</c>（Screen Space - Camera，描画カメラは
    /// <c>LeftEyeAnchor</c>/<c>RightEyeAnchor</c>）に載る．これらのカメラは
    /// <c>stereoTargetEye = Left/Right</c> でXR/HMD経路へ流れるため，
    /// <b>デスクトップのGame画面には出てこない</b>．
    ///
    /// 一方，再生確認シーン（ViewpointFollowingReplay.unity）は「HMD不要でGameビューで
    /// 確認できる」ことが存在意義で，そのGame画面は <c>ReplayRawImage</c>
    /// （ReplayCanvas，Screen Space - Overlay）が担っていた．しかし
    /// <see cref="OnEnable"/> が表示を引き取る際に <see cref="viewSwitcher"/> を
    /// 無効化するため，両眼化後は <c>ReplayRawImage</c> を更新するものが誰もいなくなり，
    /// 錐ガイドONの瞬間にGame画面が更新の止まった古いテクスチャのままになっていた．
    ///
    /// このフィールドに <c>ReplayRawImage</c> を指定すると，<see cref="rawImage"/> と
    /// 同じ合成結果をそこにも渡す（左目用コンポジタにだけ設定する運用）．
    /// HMDでは両眼立体視，Game画面では左目の映像をモニタ，という状態になる．
    /// 実験シーンでは未設定のままでよい．
    /// </summary>
    [Tooltip("Game画面確認用のミラー出力先（任意。再生確認シーンのReplayRawImage を左目用にだけ設定する）")]
    public RawImage monitorRawImage;

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

    /// <summary>
    /// Guide_NearNow を撮っている専用カメラ．非アクティブのときは古いフレームが
    /// 残るため，近断面ガイドの重みを 0 にする．
    /// </summary>
    [Tooltip("Guide_NearNow を撮る専用カメラ（非アクティブ時に古いフレームを出さないために参照する）")]
    public Camera guideNearCamera;

    /// <summary>
    /// Guide_FarNow を撮っている専用カメラ．非アクティブのときは古いフレームが
    /// 残るため，遠断面ガイドの重みを 0 にする．
    /// </summary>
    [Tooltip("Guide_FarNow を撮る専用カメラ（非アクティブ時に古いフレームを出さないために参照する）")]
    public Camera guideFarCamera;

    /// <summary>
    /// Guide_RidgeNow を撮っている専用カメラ．非アクティブのときは古いフレームが
    /// 残るため，稜線ガイドの重みを 0 にする．
    /// </summary>
    [Tooltip("Guide_RidgeNow を撮る専用カメラ（非アクティブ時に古いフレームを出さないために参照する）")]
    public Camera guideRidgeCamera;

    /// <summary>
    /// ガイドチャンネル（近断面）用の遅延バッファ．<see cref="guideNearNowTexture"/> を
    /// 一定レートでリングバッファへ複製し，数百ms前の「近断面」を
    /// <see cref="DelayedFrameBuffer.DelayedTexture"/> として取り出す
    /// （4ストローク歩行シーンの仕組みをそのまま流用．09 §3.7）．
    /// </summary>
    [Tooltip("ガイドチャンネル（近断面）用の遅延バッファ")]
    public DelayedFrameBuffer guideNearDelayBuffer;

    /// <summary>
    /// ガイドチャンネル（遠断面）用の遅延バッファ．<see cref="guideFarNowTexture"/> を
    /// 一定レートでリングバッファへ複製し，数百ms前の「遠断面」を取り出す（09 §3.7 拡張）．
    /// </summary>
    [Tooltip("ガイドチャンネル（遠断面）用の遅延バッファ")]
    public DelayedFrameBuffer guideFarDelayBuffer;

    /// <summary>
    /// ガイドチャンネル（稜線）用の遅延バッファ．<see cref="guideRidgeNowTexture"/> を
    /// 一定レートでリングバッファへ複製し，数百ms前の「稜線」を取り出す（09 §3.7 拡張）．
    /// </summary>
    [Tooltip("ガイドチャンネル（稜線）用の遅延バッファ")]
    public DelayedFrameBuffer guideRidgeDelayBuffer;

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
    /// <summary>
    /// 箱の提示条件．
    ///
    /// 既定値は <see cref="BoxMode.OtherFixed"/>（相手基準の錐を常時表示）．
    /// ベースライン条件である <see cref="BoxMode.Off"/> を既定にしていたころは，
    /// ConeGuideSceneUpgrader で新規作成した ChannelCompositor が箱を一切描画せず
    /// 「配線は正しいのに Game 画面に錐が出ない」と誤解される原因になっていたため，
    /// 配線直後にそのまま錐が見える値を既定にしている．
    /// ベースライン条件で実験する場合は Inspector で Off に戻すこと．
    /// </summary>
    [Tooltip("箱の提示条件（既定は OtherFixed＝錐が見える状態。ベースラインは Off）")]
    public BoxMode boxMode = BoxMode.OtherFixed;

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

    [Header("ガイドチャンネル（近断面・遠断面: 今⇔数百ms前, 拡張 09 §3.7）")]
    /// <summary>
    /// ガイドチャンネルの提示条件．<see cref="BoxMode"/> をそのまま流用する
    /// （Other=「今」 / Self=「数百ms前」）．近断面・遠断面で共有する（位相がずれると
    /// 噛み合わなくなるため）．既存の「箱チャンネル」（Cone_Other⇔Cone_Self）とは
    /// 完全に独立しており，同時に併用しても互いのテクスチャ・位相を書き換えない．
    /// 既定は Off（明示的に有効化するまでガイド効果は掛からない）．
    /// </summary>
    [Tooltip("ガイドチャンネルの提示条件（Other=今 / Self=数百ms前）。近断面・遠断面で共有、箱チャンネルとは独立")]
    public BoxMode guideMode = BoxMode.Off;

    /// <summary>
    /// 近断面にガイド効果（今⇔数百ms前）を掛けるか．既定 ON（従来からの挙動を維持）．
    /// </summary>
    [Tooltip("近断面にガイド効果を掛けるか（既定ON）")]
    public bool guideNearEnabled = true;

    /// <summary>
    /// 遠断面にガイド効果（今⇔数百ms前）を掛けるか．既定 OFF（新規追加の能力のため，
    /// 明示的に有効化しない限り従来と同じ見えのままにする）．
    /// </summary>
    [Tooltip("遠断面にガイド効果を掛けるか（既定OFF、新規拡張）")]
    public bool guideFarEnabled = false;

    /// <summary>
    /// 稜線にガイド効果（今⇔数百ms前）を掛けるか．既定 OFF（新規追加の能力のため，
    /// 明示的に有効化しない限り従来と同じ見えのままにする）．
    /// 稜線の表示/非表示自体は <see cref="ConeGuide.drawRidges"/> で別途制御する．
    /// </summary>
    [Tooltip("稜線にガイド効果を掛けるか（既定OFF、新規拡張）")]
    public bool guideRidgeEnabled = false;

    /// <summary>
    /// ON: 近断面・遠断面・稜線の「数百ms前」の遅延量をすべて <see cref="guideDelayFrames"/>
    /// に固定する（既定）．OFF: <see cref="guideNearDelayFrames"/> /
    /// <see cref="guideFarDelayFrames"/> / <see cref="guideRidgeDelayFrames"/> で
    /// 部位ごとに個別設定できる．いずれの場合も実体は
    /// <see cref="guideNearDelayBuffer"/> 等の <c>DelayedFrameBuffer.delayFrames</c> に
    /// 毎フレーム反映される（この合成器が唯一の書き込み元になる）．
    /// </summary>
    [Tooltip("ON: 近断面/遠断面/稜線の遅延量をすべて Guide Delay Frames に固定。OFF: 部位ごとに個別設定")]
    public bool guideDelayLocked = true;

    /// <summary>固定時（<see cref="guideDelayLocked"/> = ON）に3部位共通で使う遅延フレーム数</summary>
    [Tooltip("固定時に使う共通の遅延フレーム数（8 @30fps ≈267ms）")]
    [Range(0, 29)] public int guideDelayFrames = 8;

    /// <summary>近断面の遅延フレーム数（<see cref="guideDelayLocked"/> = OFF のときのみ有効）</summary>
    [Tooltip("近断面の遅延フレーム数（固定OFF時のみ有効）")]
    [Range(0, 29)] public int guideNearDelayFrames = 8;

    /// <summary>遠断面の遅延フレーム数（<see cref="guideDelayLocked"/> = OFF のときのみ有効）</summary>
    [Tooltip("遠断面の遅延フレーム数（固定OFF時のみ有効）")]
    [Range(0, 29)] public int guideFarDelayFrames = 8;

    /// <summary>稜線の遅延フレーム数（<see cref="guideDelayLocked"/> = OFF のときのみ有効）</summary>
    [Tooltip("稜線の遅延フレーム数（固定OFF時のみ有効）")]
    [Range(0, 29)] public int guideRidgeDelayFrames = 8;

    /// <summary>
    /// ガイドチャンネルの4ストローク極性（guideMode = FourStroke のときのみ有効）．
    /// 4ストローク歩行シーン（<see cref="FourStrokeCompositor.Polarity"/>）と同じ意味づけ:
    /// Enhance＝過去→現在の順で提示し実運動と同方向の信号を加算（加速感），
    /// Reversal＝現在→過去の順で逆向きの信号（抵抗・逆行感），
    /// Zero＝運動信号なし（輝度反転フリッカーのみの統制条件）．
    /// </summary>
    [Tooltip("ガイドチャンネルの4ストローク極性（Enhance=加速感/Reversal=抵抗感/Zero=統制）")]
    public FourStrokeCompositor.Polarity guidePolarity = FourStrokeCompositor.Polarity.Zero;

    /// <summary>f_guide を f_bg に同期させるか（既定ON。箱チャンネルと同じ流儀）</summary>
    [Tooltip("f_guide を f_bg に同期させるか（既定ON）")]
    public bool syncGuideFreqToBg = true;

    /// <summary>ガイドチャンネルの切替周波数 f_guide[Hz]（syncGuideFreqToBg = OFF のときのみ有効）</summary>
    [Tooltip("ガイドチャンネルの切替周波数 f_guide[Hz]（同期OFF時のみ有効）")]
    [Range(0.1f, 10f)] public float guideFrequency = 1f;

    /// <summary>
    /// ガイドチャンネルの輝度変調量 Δ．箱チャンネルと同じ式 final += guideMask × sign × Δ
    /// で既存の合成結果に<b>加算</b>される（箱チャンネルの結果には影響しない）．
    /// </summary>
    [Tooltip("ガイドチャンネルの輝度変調量 Δ（0-1輝度、既定0.35）")]
    [Range(0f, 1f)] public float guideDelta = 0.35f;

    [Header("両眼共有（10 仕様 §2.7 拡張）")]
    /// <summary>
    /// 提示条件（モード・極性・周波数・輝度変調量・Enabledフラグ・遅延フレーム数）を
    /// <b>この ChannelCompositor からコピーする</b>（同期元）．None なら同期しない．
    ///
    /// 両眼立体視化に伴い，左目用・右目用の2つの ChannelCompositor は同じ提示条件で
    /// なければならない（片目だけ4ストロークの位相がずれると条件が破綻する）．
    /// <see cref="ConeGuide.mirrorFrom"/> と同じ「引く」パターンで，2つのコンポーネントの
    /// 更新順に依存せず同じフレームで値が揃う．入力テクスチャ・カメラ・出力先RawImage・
    /// ViewSwitcher参照など目ごとに固有の参照はコピーしない．
    /// </summary>
    [Tooltip("提示条件の同期元（None なら同期しない）。左右の ChannelCompositor は同じ提示条件である必要がある")]
    public ChannelCompositor mirrorFrom;

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

    /// <summary>ガイドチャンネルで今どちらが支配的か（0 = 近い箱の今, 1 = 近い箱の数百ms前）</summary>
    public int GuideDominantSource { get; private set; }

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

    /// <summary>実際に使っている f_guide[Hz]</summary>
    public float GuideFrequency
    {
        get { return syncGuideFreqToBg ? BgFrequency : guideFrequency; }
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

    // 背景用・箱用・ガイド用の位相は完全に独立して進む
    private readonly ChannelPhase bgPhase = new ChannelPhase();
    private readonly ChannelPhase boxPhase = new ChannelPhase();
    private readonly ChannelPhase guidePhase = new ChannelPhase();

    private Material material;
    private bool switcherWasEnabled;

    // 別色モードと箱4ストロークの排他処理（仕様 §2.3 / §3.1）の状態
    private bool dualColorWarned;      // 警告ログを1回だけ出すためのフラグ
    private bool dualColorAutoDisabled; // HUD に「自動無効化した」旨を出すためのフラグ

    /// <summary>mirrorFrom の相互参照警告を1回だけ出すためのフラグ（10 §2.7 拡張）</summary>
    private bool mirrorCycleWarned;

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
    /// 全チャンネルの提示位相をリセットする（試行開始時に呼ぶ）．
    /// 背景はライブ提示から，箱は Other 提示から，ガイドは近い箱の「今」提示から始まる．
    /// </summary>
    public void ResetPhase()
    {
        bgPhase.Reset(WaveformOf(bgMode));
        boxPhase.Reset(WaveformOf(boxMode));
        guidePhase.Reset(WaveformOf(guideMode));
        CurrentSource = (bgMode == BackgroundMode.GhostFixed) ? 1 : 0;
        BoxDominantSource = (boxMode == BoxMode.SelfFixed) ? 1 : 0;
        GuideDominantSource = (guideMode == BoxMode.SelfFixed) ? 1 : 0;

        // 箱の姿勢フィルタ（LPF）も揃えてリセットし，開始直後の過渡応答を出さない
        if (coneOther != null) coneOther.ResetPose();
        if (coneSelf != null) coneSelf.ResetPose();
    }

    /// <summary>
    /// 同期元の ChannelCompositor から提示条件（モード・極性・周波数・輝度変調量・
    /// Enabledフラグ・遅延フレーム数）をコピーする（10 §2.7 拡張）．
    ///
    /// 各 <see cref="ChannelPhase"/>（<see cref="bgPhase"/> 等）の内部状態（位相の現在値）は
    /// 同期しないが，周波数・極性・波形が揃っていれば両インスタンスとも同じ
    /// <see cref="Time.deltaTime"/> で同時に進むため，自然に位相が揃う
    /// （両インスタンスが同じフレームで有効化・<see cref="ResetPhase"/> されている前提）．
    /// </summary>
    private void SyncFromMirrorSource()
    {
        ChannelCompositor source = mirrorFrom;
        if (source == null || source == this) return;

        // 相互参照は互いに上書きし合って発散するので同期しない
        if (source.mirrorFrom == this)
        {
            if (!mirrorCycleWarned)
            {
                Debug.LogWarning("[ChannelCompositor] " + name + " と " + source.name
                    + " が互いを同期元にしています。どちらか一方の Mirror From を None にしてください。", this);
                mirrorCycleWarned = true;
            }
            return;
        }
        mirrorCycleWarned = false;

        bgMode = source.bgMode;
        bgPolarity = source.bgPolarity;
        bgGrayscale = source.bgGrayscale;
        bgFrequencyFallback = source.bgFrequencyFallback;

        boxMode = source.boxMode;
        boxPolarity = source.boxPolarity;
        syncBoxFreqToBg = source.syncBoxFreqToBg;
        boxFrequency = source.boxFrequency;
        boxDelta = source.boxDelta;

        guideMode = source.guideMode;
        guideNearEnabled = source.guideNearEnabled;
        guideFarEnabled = source.guideFarEnabled;
        guideRidgeEnabled = source.guideRidgeEnabled;
        guideDelayLocked = source.guideDelayLocked;
        guideDelayFrames = source.guideDelayFrames;
        guideNearDelayFrames = source.guideNearDelayFrames;
        guideFarDelayFrames = source.guideFarDelayFrames;
        guideRidgeDelayFrames = source.guideRidgeDelayFrames;
        guidePolarity = source.guidePolarity;
        syncGuideFreqToBg = source.syncGuideFreqToBg;
        guideFrequency = source.guideFrequency;
        guideDelta = source.guideDelta;
    }

    private void LateUpdate()
    {
        if (bgLiveTexture == null || bgGhostTexture == null) return;
        if (!EnsureResources()) return;

        // 同期元があれば先に提示条件を引く（10 §2.7 拡張。左右の ChannelCompositor を揃える）
        SyncFromMirrorSource();

        // 表示の担当を確実にこちらへ寄せる（ReplayPlayer など他所が ViewSwitcher を
        // 有効化し直しても，この合成器が有効な間は合成結果を出す）
        if (viewSwitcher != null && viewSwitcher.enabled) viewSwitcher.enabled = false;

        // 箱の4ストロークは輝度変調方式なので別色モードとは両立しない（仕様 §2.3）
        EnforceDualColorExclusivity();

        // ガイドの「数百ms前」の遅延量を DelayedFrameBuffer 側へ反映する（09 §3.7 拡張）
        ApplyGuideDelayFrames();

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

        // ---------- ガイドチャンネル（近断面・遠断面それぞれの今 ⇔ 数百ms前, 09 §3.7） ----------
        // 箱チャンネルとは完全に独立（別の ChannelPhase・別のテクスチャ・別の合成項）。
        // 4ストローク歩行シーンと同じ「今 vs 過去の自分」の仕組みを箱に適用したもの。
        // 提示条件（モード・極性・周波数）は近断面・遠断面で共有し，どちらに掛けるかは
        // guideNearEnabled / guideFarEnabled で独立に ON/OFF する
        float guideWeightNow, guideWeightDelayed, guideSign;
        switch (guideMode)
        {
            case BoxMode.Off:
                guideWeightNow = 0f; guideWeightDelayed = 0f; guideSign = 1f;
                break;
            case BoxMode.OtherFixed:
                guideWeightNow = 1f; guideWeightDelayed = 0f; guideSign = 1f;
                GuideDominantSource = 0;
                break;
            case BoxMode.SelfFixed:
                guideWeightNow = 0f; guideWeightDelayed = 1f; guideSign = 1f;
                GuideDominantSource = 1;
                break;
            default: // SquareAlternate / FourStroke
                guidePhase.Advance(dt, GuideFrequency, WaveformOf(guideMode), guidePolarity);
                guideWeightNow = guidePhase.WeightA;
                guideWeightDelayed = guidePhase.WeightB;
                guideSign = guidePhase.Inverted ? -1f : 1f;
                GuideDominantSource = guidePhase.DominantSource;
                break;
        }

        // 近断面・遠断面それぞれの専用カメラが止まっている間はその入力の RT が
        // 更新されないため，古いフレームのまま静止してしまう。その断面の重みを 0 にする
        float guideNearWeightNow = guideNearEnabled ? guideWeightNow : 0f;
        float guideNearWeightDelayed = guideNearEnabled ? guideWeightDelayed : 0f;
        if (guideNearCamera != null && !guideNearCamera.isActiveAndEnabled)
        {
            guideNearWeightNow = 0f;
            guideNearWeightDelayed = 0f;
        }

        float guideFarWeightNow = guideFarEnabled ? guideWeightNow : 0f;
        float guideFarWeightDelayed = guideFarEnabled ? guideWeightDelayed : 0f;
        if (guideFarCamera != null && !guideFarCamera.isActiveAndEnabled)
        {
            guideFarWeightNow = 0f;
            guideFarWeightDelayed = 0f;
        }

        float guideRidgeWeightNow = guideRidgeEnabled ? guideWeightNow : 0f;
        float guideRidgeWeightDelayed = guideRidgeEnabled ? guideWeightDelayed : 0f;
        if (guideRidgeCamera != null && !guideRidgeCamera.isActiveAndEnabled)
        {
            guideRidgeWeightNow = 0f;
            guideRidgeWeightDelayed = 0f;
        }

        Texture guideNearDelayedTexture = guideNearDelayBuffer != null ? guideNearDelayBuffer.DelayedTexture : null;
        Texture guideFarDelayedTexture = guideFarDelayBuffer != null ? guideFarDelayBuffer.DelayedTexture : null;
        Texture guideRidgeDelayedTexture = guideRidgeDelayBuffer != null ? guideRidgeDelayBuffer.DelayedTexture : null;

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
        material.SetTexture("_GuideNearNow", guideNearNowTexture != null ? guideNearNowTexture : Texture2D.blackTexture);
        material.SetTexture("_GuideNearDelayed", guideNearDelayedTexture != null ? guideNearDelayedTexture : Texture2D.blackTexture);
        material.SetTexture("_GuideFarNow", guideFarNowTexture != null ? guideFarNowTexture : Texture2D.blackTexture);
        material.SetTexture("_GuideFarDelayed", guideFarDelayedTexture != null ? guideFarDelayedTexture : Texture2D.blackTexture);
        material.SetTexture("_GuideRidgeNow", guideRidgeNowTexture != null ? guideRidgeNowTexture : Texture2D.blackTexture);
        material.SetTexture("_GuideRidgeDelayed", guideRidgeDelayedTexture != null ? guideRidgeDelayedTexture : Texture2D.blackTexture);
        material.SetFloat("_GuideWeightNearNow", guideNearWeightNow);
        material.SetFloat("_GuideWeightNearDelayed", guideNearWeightDelayed);
        material.SetFloat("_GuideWeightFarNow", guideFarWeightNow);
        material.SetFloat("_GuideWeightFarDelayed", guideFarWeightDelayed);
        material.SetFloat("_GuideWeightRidgeNow", guideRidgeWeightNow);
        material.SetFloat("_GuideWeightRidgeDelayed", guideRidgeWeightDelayed);
        material.SetFloat("_GuideSign", guideSign);
        material.SetFloat("_GuideDelta", guideDelta);
        Graphics.Blit(null, OutputTexture, material);

        if (rawImage != null)
        {
            rawImage.texture = OutputTexture;
            rawImage.color = Color.white; // 合成結果に着色しない
        }

        // Game画面確認用のミラー出力（再生確認シーンの ReplayRawImage 等）．
        // 同じ OutputTexture を参照させるだけなので追加の描画コストは無い
        if (monitorRawImage != null)
        {
            monitorRawImage.texture = OutputTexture;
            monitorRawImage.color = Color.white;
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

    /// <summary>
    /// 近断面/遠断面/稜線の「数百ms前」の遅延量を，Inspector の設定に従って
    /// 各 <see cref="DelayedFrameBuffer"/> に反映する（09 §3.7 拡張）．
    ///
    /// <see cref="guideDelayLocked"/> が ON なら3部位すべてに <see cref="guideDelayFrames"/>
    /// を，OFF なら部位ごとの個別値を書き込む。この合成器が
    /// <c>DelayedFrameBuffer.delayFrames</c> の唯一の書き込み元になる（Inspector で
    /// 各 DelayedFrameBuffer を直接編集しても，次のフレームでここから上書きされる）。
    /// </summary>
    private void ApplyGuideDelayFrames()
    {
        int nearFrames = guideDelayLocked ? guideDelayFrames : guideNearDelayFrames;
        int farFrames = guideDelayLocked ? guideDelayFrames : guideFarDelayFrames;
        int ridgeFrames = guideDelayLocked ? guideDelayFrames : guideRidgeDelayFrames;

        if (guideNearDelayBuffer != null) guideNearDelayBuffer.delayFrames = nearFrames;
        if (guideFarDelayBuffer != null) guideFarDelayBuffer.delayFrames = farFrames;
        if (guideRidgeDelayBuffer != null) guideRidgeDelayBuffer.delayFrames = ridgeFrames;
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
        string guide = guideMode.ToString()
            + (guideMode == BoxMode.FourStroke ? " " + guidePolarity : "")
            + (guideMode == BoxMode.SquareAlternate || guideMode == BoxMode.FourStroke
                ? " " + GuideFrequency.ToString("F1") + "Hz" : "");
        GUI.Label(new Rect(Screen.width - 430, 10, 420, 20), "背景: " + bg);
        GUI.Label(new Rect(Screen.width - 430, 30, 420, 20),
            "箱: " + box + (DualColorActive ? "（別色モード）" : ""));
        GUI.Label(new Rect(Screen.width - 430, 110, 420, 20),
            "ガイド(今⇔数百ms前): " + guide
            + " [近:" + (guideNearEnabled ? "ON" : "OFF")
            + " 遠:" + (guideFarEnabled ? "ON" : "OFF")
            + " 稜線:" + (guideRidgeEnabled ? "ON" : "OFF") + "]");
        GUI.Label(new Rect(Screen.width - 430, 130, 420, 20),
            "ガイド遅延: " + (guideDelayLocked
                ? "固定 " + DelayMsLabel(guideNearDelayBuffer)
                : "近" + DelayMsLabel(guideNearDelayBuffer)
                    + " 遠" + DelayMsLabel(guideFarDelayBuffer)
                    + " 稜線" + DelayMsLabel(guideRidgeDelayBuffer)));

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

    /// <summary>HUD 表示用: DelayedFrameBuffer の現在の遅延時間を "(267ms)" の形で返す</summary>
    private static string DelayMsLabel(DelayedFrameBuffer buffer)
    {
        return buffer != null ? "(" + (buffer.DelaySeconds * 1000f).ToString("F0") + "ms)" : "(-)";
    }
#endif
}
