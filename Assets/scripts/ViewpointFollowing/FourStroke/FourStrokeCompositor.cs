using UnityEngine;

/// <summary>
/// 4ストローク見かけの運動（4-stroke apparent motion）の合成器．
/// 「現在フレーム C」と「過去/収録フレーム D」の2枚のテクスチャを，
/// 半周期ごとの輝度反転＋台形波クロスフェードで合成して OutputTexture に出力する．
///
/// 元実装（docs/fourstroke/color4st/winmain.cpp の FOURSTROKE_MODE）の C# 移植:
/// - 位相 = 2πf·t（Time.deltaTime 加算のため timeScale=0 の一時停止中は進まない）
/// - 半周期正規化 t' = (位相 mod π)/π に対する台形波
///   αC = 0 (t'&lt;0.4) / 線形遷移 (0.4〜0.6) / 1 (t'&gt;0.6)，αD = 1-αC
/// - 位相 π〜2π では C/D とも輝度反転（1周期の提示系列 = D→C→D̄→C̄）
///
/// 視点追従シーンでは C=ライブ映像・D=収録映像，
/// 4ストローク歩行シーンでは C=ライブ映像・D=数百ms前の自分の映像を入力する．
/// </summary>
public class FourStrokeCompositor : MonoBehaviour
{
    /// <summary>
    /// 極性スイッチ（知覚される運動信号の向き）
    /// </summary>
    public enum Polarity
    {
        /// <summary>過去→現在の順で提示し，実運動と同方向の信号を加算する（加速感）</summary>
        Enhance = 1,
        /// <summary>αC=1 に固定し現在フレームのみ提示する（反転フリッカーのみの統制条件）</summary>
        Zero = 0,
        /// <summary>現在→過去の順で提示し，実運動と逆向きの信号を与える（抵抗・逆行感）</summary>
        Reversal = -1,
    }

    /// <summary>
    /// 合成シェーダ（Hidden/FourStroke）．シーンビルダーが配線する．
    /// 未設定の場合は Shader.Find で自動検索する（エディタ実行では常に見つかる）．
    /// </summary>
    [Tooltip("合成シェーダ（Hidden/FourStroke。未設定なら自動検索）")]
    public Shader shader;

    /// <summary>現在フレーム C のテクスチャ（ライブ映像 = CenterEye）</summary>
    [Tooltip("現在フレームC（ライブ映像）")]
    public Texture currentTexture;

    /// <summary>過去フレーム D のテクスチャ（遅延映像 または 収録映像）</summary>
    [Tooltip("過去フレームD（遅延映像または収録映像）")]
    public Texture delayedTexture;

    /// <summary>
    /// 変調周波数[Hz]（1周期 = D→C→D̄→C̄ の4ストローク1巡）．
    /// 視点追従シーンでは ViewSwitcher が switchFrequency を毎フレーム上書きする．
    /// </summary>
    [Tooltip("変調周波数[Hz]（1周期=4ストローク1巡）")]
    [Range(0.02f, 10f)] public float frequency = 3f;

    /// <summary>極性（Enhance=増強 / Zero=統制 / Reversal=逆転）</summary>
    [Tooltip("極性（Enhance=増強 / Zero=統制 / Reversal=逆転）")]
    public Polarity polarity = Polarity.Enhance;

    /// <summary>
    /// 両入力をグレースケール化するか（反転操作を輝度軸上で対称にするため既定ON．元実装と同じ）
    /// </summary>
    [Tooltip("両入力をグレースケール化するか（元実装と同じく既定ON）")]
    public bool grayscale = true;

    /// <summary>合成結果の出力先（RawImage 等に設定して表示する）</summary>
    public RenderTexture OutputTexture { get; private set; }

    /// <summary>現在の合成比 αC（1=現在フレームのみ, 0=過去フレームのみ）</summary>
    public float AlphaC { get; private set; }

    /// <summary>今どちらのフレームが支配的か（0=現在(ライブ), 1=過去/収録）．ロガーが記録に使う．</summary>
    public int DominantSource { get; private set; }

    /// <summary>現在の半周期が反転提示か（HUD 表示用）</summary>
    public bool IsInverted { get; private set; }

    private Material material;
    private float phase; // 変調位相[rad]（0〜2πでラップ）

    /// <summary>
    /// リセット後の初期位相．t'=0.6（αC=1・非反転＝現在フレームの静止提示）から始めることで，
    /// 既存実験の「必ずライブ映像から提示を始める」慣習を維持する．
    /// </summary>
    private const float StartPhase = 0.6f * Mathf.PI;

    private void Awake()
    {
        ResetPhase();
    }

    /// <summary>
    /// 変調位相をリセットする（試行開始時に呼ぶ．現在フレームの提示から始まる）
    /// </summary>
    public void ResetPhase()
    {
        phase = StartPhase;
    }

    private void LateUpdate()
    {
        if (currentTexture == null || delayedTexture == null) return;
        if (!EnsureResources()) return;

        // 一時停止（timeScale=0）中は deltaTime=0 なので変調も自動的に止まる
        phase += 2f * Mathf.PI * frequency * Time.deltaTime;
        phase = Mathf.Repeat(phase, 2f * Mathf.PI);

        // --- 台形波 αC（滞在40%・遷移20%・滞在40%）を半周期ごとに計算 ---
        float halfT = Mathf.Repeat(phase, Mathf.PI) / Mathf.PI; // 半周期内の局所時間 0〜1
        float alphaC;
        if (halfT < 0.4f) alphaC = 0f;
        else if (halfT < 0.6f) alphaC = (halfT - 0.4f) * 5f;
        else alphaC = 1f;

        // --- 極性による上書き（元実装 §4.4 と同じ．反転はそのまま継続する） ---
        if (polarity == Polarity.Zero) alphaC = 1f;             // 現在のみ（錯視オフ）
        else if (polarity == Polarity.Reversal) alphaC = 1f - alphaC; // 提示順を反転

        float alphaD = 1f - alphaC;

        // --- 位相後半（π〜2π）は C/D とも輝度反転 ---
        bool invert = phase >= Mathf.PI;
        float inv = invert ? -1f : 1f;

        AlphaC = alphaC;
        DominantSource = alphaC >= 0.5f ? 0 : 1;
        IsInverted = invert;

        // --- シェーダで合成して出力 ---
        material.SetTexture("_CurrentTex", currentTexture);
        material.SetTexture("_DelayedTex", delayedTexture);
        material.SetFloat("_AlphaC", alphaC);
        material.SetFloat("_AlphaD", alphaD);
        material.SetFloat("_InvertC", inv);
        material.SetFloat("_InvertD", inv);
        material.SetFloat("_Grayscale", grayscale ? 1f : 0f);
        Graphics.Blit(null, OutputTexture, material);
    }

    /// <summary>
    /// マテリアルと出力 RenderTexture を用意する（入力サイズが変わったら作り直す）
    /// </summary>
    private bool EnsureResources()
    {
        if (material == null)
        {
            if (shader == null) shader = Shader.Find("Hidden/FourStroke");
            if (shader == null)
            {
                Debug.LogError("[FourStrokeCompositor] シェーダ Hidden/FourStroke が見つかりません");
                enabled = false;
                return false;
            }
            material = new Material(shader);
        }

        if (OutputTexture == null
            || OutputTexture.width != currentTexture.width
            || OutputTexture.height != currentTexture.height)
        {
            ReleaseOutput();
            OutputTexture = new RenderTexture(currentTexture.width, currentTexture.height, 0)
            {
                name = "FourStrokeOutput",
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

    private void OnDestroy()
    {
        ReleaseOutput();
        if (material != null) Object.Destroy(material);
    }
}
