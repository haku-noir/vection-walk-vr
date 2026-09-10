using UnityEngine;

/// <summary>
/// 1チャンネル分の提示位相を計算する部品（09 仕様 §2.2）．
/// 背景チャンネルと箱チャンネルがそれぞれ独立したインスタンスを持ち，
/// 周波数・極性をチャンネルごとに独立して操作できるようにする．
///
/// 2つの入力（A / B）に対する重みと，輝度反転フラグを返す:
/// - <b>Square</b>: デューティ比 50% の矩形波交替（A→B→A…）．既存 ViewSwitcher と同じ定義
/// - <b>FourStroke</b>: 台形波クロスフェード（滞在40%・遷移20%・滞在40%）＋半周期ごとの反転
///
/// 入力の対応:
/// | チャンネル | A | B |
/// |---|---|---|
/// | 背景 | ライブ映像 | 収録映像 |
/// | 箱   | Cone_Other | Cone_Self |
/// </summary>
/// <remarks>
/// 既存の FourStrokeCompositor には手を触れず，同じ台形波・極性のロジックを
/// ここに持たせている（1インスタンスでは2チャンネルを独立に扱えないため）．
/// 位相は Time.deltaTime で進めるので，timeScale=0 の一時停止中は自動的に止まる．
/// </remarks>
public class ChannelPhase
{
    /// <summary>提示波形</summary>
    public enum Waveform
    {
        /// <summary>デューティ比 50% の矩形波交替</summary>
        Square,
        /// <summary>4ストローク（台形波クロスフェード＋輝度反転）</summary>
        FourStroke,
    }

    /// <summary>
    /// 4ストロークのリセット位相．t'=0.6（重みA=1・非反転＝Aの静止提示）から始めることで，
    /// 「必ずライブ映像から提示を始める」既存の慣習を維持する（FourStrokeCompositor と同じ）．
    /// </summary>
    private const float FourStrokeStartPhase = 0.6f * Mathf.PI;

    private const float TwoPi = 2f * Mathf.PI;

    private float phase;                 // 変調位相[rad]（0〜2πでラップ）
    private Waveform currentWaveform = Waveform.Square;

    /// <summary>入力 A の重み（背景=ライブ / 箱=Cone_Other）</summary>
    public float WeightA { get; private set; }

    /// <summary>入力 B の重み（背景=収録 / 箱=Cone_Self）</summary>
    public float WeightB { get; private set; }

    /// <summary>この半周期が輝度反転側か（4ストローク時のみ true になりうる）</summary>
    public bool Inverted { get; private set; }

    /// <summary>今どちらが支配的か（0 = A, 1 = B）．ロガーが記録に使う</summary>
    public int DominantSource { get; private set; }

    /// <summary>現在の位相[rad]（HUD・デバッグ用）</summary>
    public float Phase { get { return phase; } }

    public ChannelPhase()
    {
        Reset(Waveform.Square);
    }

    /// <summary>
    /// 位相をリセットする（試行開始時に呼ぶ．必ず入力 A の提示から始まる）
    /// </summary>
    public void Reset(Waveform waveform)
    {
        currentWaveform = waveform;
        phase = (waveform == Waveform.FourStroke) ? FourStrokeStartPhase : 0f;
        WeightA = 1f;
        WeightB = 0f;
        Inverted = false;
        DominantSource = 0;
    }

    /// <summary>
    /// 位相を進め，重み・反転フラグを更新する．
    /// </summary>
    /// <param name="deltaTime">経過時間[s]（Time.deltaTime）</param>
    /// <param name="frequency">変調周波数[Hz]（1周期 = A+B の1往復）</param>
    /// <param name="waveform">提示波形</param>
    /// <param name="polarity">4ストロークの極性（Square では無視される）</param>
    public void Advance(float deltaTime, float frequency, Waveform waveform,
        FourStrokeCompositor.Polarity polarity)
    {
        // 波形が切り替わったら位相の意味が変わるので取り直す
        if (waveform != currentWaveform) Reset(waveform);

        phase = Mathf.Repeat(phase + TwoPi * Mathf.Max(frequency, 0.01f) * deltaTime, TwoPi);

        if (waveform == Waveform.Square)
        {
            // 前半（0〜π）= A，後半（π〜2π）= B のデューティ比 50%
            bool secondHalf = phase >= Mathf.PI;
            WeightA = secondHalf ? 0f : 1f;
            WeightB = secondHalf ? 1f : 0f;
            Inverted = false;
            DominantSource = secondHalf ? 1 : 0;
            return;
        }

        // --- 4ストローク: 台形波（滞在40%・遷移20%・滞在40%）を半周期ごとに計算 ---
        float halfT = Mathf.Repeat(phase, Mathf.PI) / Mathf.PI; // 半周期内の局所時間 0〜1
        float weightA;
        if (halfT < 0.4f) weightA = 0f;
        else if (halfT < 0.6f) weightA = (halfT - 0.4f) * 5f;
        else weightA = 1f;

        // 極性による上書き（FourStrokeCompositor と同じ．反転はそのまま継続する）
        if (polarity == FourStrokeCompositor.Polarity.Zero) weightA = 1f;
        else if (polarity == FourStrokeCompositor.Polarity.Reversal) weightA = 1f - weightA;

        WeightA = weightA;
        WeightB = 1f - weightA;
        // 位相後半（π〜2π）は輝度反転（背景は画素の反転，箱は変調符号の反転）
        Inverted = phase >= Mathf.PI;
        DominantSource = weightA >= 0.5f ? 0 : 1;
    }
}
