using UnityEngine;

/// <summary>
/// 四角錐ガイドの姿勢処理（09 仕様 §3.2）．
/// ヨー／ピッチ／ロールそれぞれに Raw（そのまま）／LowPass（一次LPF）／Zero（ゼロ化）を選べる．
///
/// 既定は<b>ヨー=Raw・ピッチ=LowPass 0.5Hz・ロール=Zero</b>:
/// - ヨーは目標自由度そのものなのでそのまま通す
/// - ピッチは歩行の上下動を除去する
/// - ロールは歩行由来の振動（1〜2Hz）が，操作したいワブル・4ストロークと同帯域で
///   <b>交絡する</b>ため除去する（目標自由度でもない。主指標は errXZ）
/// </summary>
/// <remarks>
/// - 入出力は四元数．内部で成分に分解し，処理後に四元数へ再構成する．
///   Unity の回転順（Y→X→Z）に沿った分解なので，Quaternion.Euler での再構成は厳密に一致する．
///   ここで行うのは成分ごとのフィルタであって「オイラー角の補間」ではない．
/// - LPF は一次 IIR．角度の巻き回り（350°→10°）は Mathf.DeltaAngle で累積するため破綻しない．
/// - 時間は Time.deltaTime（timeScale=0 の停止中は状態を保持したまま止まる）．
/// - 真上／真下を向くとピッチ±90°でジンバルロックするが，歩行課題では想定範囲外．
/// </remarks>
public class ConePoseFilter : MonoBehaviour
{
    /// <summary>各回転成分の処理方法</summary>
    public enum ComponentMode
    {
        /// <summary>そのまま通す</summary>
        Raw,
        /// <summary>一次ローパスフィルタを掛ける</summary>
        LowPass,
        /// <summary>常に 0 にする</summary>
        Zero,
    }

    [Header("成分ごとの処理（仕様 09 §3.2）")]
    /// <summary>ヨー（水平回転）の処理．既定 Raw（目標自由度そのもの）</summary>
    [Tooltip("ヨーの処理（既定 Raw = 目標自由度そのもの）")]
    public ComponentMode yawMode = ComponentMode.Raw;

    /// <summary>ピッチ（上下の向き）の処理．既定 LowPass（歩行の上下動を除去）</summary>
    [Tooltip("ピッチの処理（既定 LowPass = 歩行の上下動を除去）")]
    public ComponentMode pitchMode = ComponentMode.LowPass;

    /// <summary>ロール（首の傾き）の処理．既定 Zero（ワブル・4ストロークとの交絡を避ける）</summary>
    [Tooltip("ロールの処理（既定 Zero = ワブル・4ストロークとの交絡を避ける）")]
    public ComponentMode rollMode = ComponentMode.Zero;

    [Header("ローパスのカットオフ周波数[Hz]")]
    /// <summary>ヨーの LPF カットオフ周波数[Hz]（yawMode = LowPass のときのみ有効）</summary>
    [Tooltip("ヨーのLPFカットオフ[Hz]")]
    [Range(0.05f, 5f)] public float yawCutoffHz = 0.5f;

    /// <summary>ピッチの LPF カットオフ周波数[Hz]（既定 0.5Hz）</summary>
    [Tooltip("ピッチのLPFカットオフ[Hz]（既定0.5Hz）")]
    [Range(0.05f, 5f)] public float pitchCutoffHz = 0.5f;

    /// <summary>ロールの LPF カットオフ周波数[Hz]（rollMode = LowPass のときのみ有効）</summary>
    [Tooltip("ロールのLPFカットオフ[Hz]")]
    [Range(0.05f, 5f)] public float rollCutoffHz = 0.5f;

    // ---- LPF の内部状態（連続角として保持し，巻き回りを避ける） ----
    private float yawState, pitchState, rollState;
    private bool initialized;

    /// <summary>直近に出力した回転（HUD・デバッグ用）</summary>
    public Quaternion LastOutput { get; private set; }

    /// <summary>
    /// フィルタ状態をリセットする（試行開始時に呼ぶ．次回の入力で初期化される）
    /// </summary>
    public void ResetState()
    {
        initialized = false;
    }

    /// <summary>
    /// 入力回転に成分ごとの処理を掛けて返す．ConeGuide が毎フレーム呼ぶ．
    /// </summary>
    /// <param name="input">元の回転（追従対象の視点姿勢）</param>
    /// <returns>処理後の回転</returns>
    public Quaternion Filter(Quaternion input)
    {
        // Unity の回転順（Y→X→Z）に沿った分解．x=ピッチ, y=ヨー, z=ロール
        Vector3 e = input.eulerAngles;
        float pitchIn = e.x;
        float yawIn = e.y;
        float rollIn = e.z;

        if (!initialized)
        {
            // 初回は入力値をそのまま状態にする（開始直後の過渡応答を出さない）
            yawState = yawIn;
            pitchState = pitchIn;
            rollState = rollIn;
            initialized = true;
        }

        // 一時停止（timeScale=0）中は deltaTime=0 なので状態が保持されたまま止まる
        float dt = Time.deltaTime;

        float yaw = Apply(yawMode, yawIn, ref yawState, yawCutoffHz, dt);
        float pitch = Apply(pitchMode, pitchIn, ref pitchState, pitchCutoffHz, dt);
        float roll = Apply(rollMode, rollIn, ref rollState, rollCutoffHz, dt);

        // 分解と同じ順序で四元数へ再構成する
        LastOutput = Quaternion.Euler(pitch, yaw, roll);
        return LastOutput;
    }

    /// <summary>
    /// 1成分に処理を適用する．LowPass の状態は巻き回りを避けるため
    /// Mathf.DeltaAngle による差分の累積で更新する．
    /// </summary>
    private static float Apply(ComponentMode mode, float input, ref float state, float cutoffHz, float dt)
    {
        switch (mode)
        {
            case ComponentMode.Zero:
                state = input; // 状態は追従させておく（モードを戻したときに飛ばないように）
                return 0f;

            case ComponentMode.LowPass:
                if (dt > 0f)
                {
                    // 一次 IIR: α = 1 − exp(−2π·fc·dt)（サンプリング間隔に依存しない）
                    float alpha = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Max(cutoffHz, 0.001f) * dt);
                    state += Mathf.DeltaAngle(state, input) * alpha;
                }
                return state;

            default: // Raw
                state = input;
                return input;
        }
    }
}
