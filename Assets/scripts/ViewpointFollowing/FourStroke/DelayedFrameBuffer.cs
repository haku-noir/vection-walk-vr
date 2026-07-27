using UnityEngine;

/// <summary>
/// ライブ映像（CenterEye RenderTexture）を一定サンプリングレートでリングバッファへ複製し，
/// 数フレーム前の「過去の自分の映像」を取り出せるようにするクラス．
/// 4ストローク歩行シーンで FourStrokeCompositor の過去フレーム D の供給源になる．
///
/// 元実装（winmain.cpp）の std::deque&lt;cv::Mat&gt; フレームバッファに相当:
/// 既定は 8 フレーム @30fps ≈ 267ms 前の映像．
/// </summary>
public class DelayedFrameBuffer : MonoBehaviour
{
    /// <summary>複製元のライブ映像（CenterEye RenderTexture）</summary>
    [Tooltip("複製元のライブ映像（CenterEye RenderTexture）")]
    public RenderTexture sourceTexture;

    /// <summary>遅延フレーム数（captureFps 換算．8 @30fps ≈ 267ms 前）</summary>
    [Tooltip("遅延フレーム数（8 @30fps ≈ 267ms前。0なら遅延なし）")]
    [Range(0, 29)] public int delayFrames = 8;

    /// <summary>バッファへの取り込みレート[fps]（元実装の約30fps 取り込みに合わせる）</summary>
    [Tooltip("バッファへの取り込みレート[fps]")]
    [Range(1f, 72f)] public float captureFps = 30f;

    /// <summary>
    /// 遅延した過去映像．バッファが充填されるまで（開始直後や遅延量変更直後）は
    /// ライブ映像をそのまま返す（元実装のフォールバックと同じ挙動）．
    /// </summary>
    public Texture DelayedTexture { get; private set; }

    /// <summary>現在の遅延時間[s]（HUD 表示用）</summary>
    public float DelaySeconds { get { return delayFrames / Mathf.Max(captureFps, 1f); } }

    private RenderTexture[] ring;   // 容量 = delayFrames + 1（必要最小限のみ確保）
    private int writeIndex;
    private int filled;             // これまでに書き込んだ枚数（充填判定用）
    private float accum;            // 取り込みタイマー
    private int allocatedDelay = -1;

    private void Update()
    {
        if (sourceTexture == null) return;

        // 遅延なしはバッファを介さずライブをそのまま返す
        if (delayFrames <= 0)
        {
            DelayedTexture = sourceTexture;
            return;
        }

        EnsureRing();

        // 一時停止（timeScale=0）中は deltaTime=0 なので取り込みも自動的に止まる
        accum += Time.deltaTime;
        float interval = 1f / Mathf.Max(captureFps, 1f);
        if (accum >= interval)
        {
            accum %= interval; // 処理落ち時に取り込みが連続爆発しないよう剰余で戻す
            writeIndex = (writeIndex + 1) % ring.Length;
            Graphics.Blit(sourceTexture, ring[writeIndex]);
            if (filled < ring.Length) filled++;
        }

        // リングが一杯なら「次に上書きされる最古のフレーム」= delayFrames 前の映像
        DelayedTexture = filled >= ring.Length
            ? ring[(writeIndex + 1) % ring.Length]
            : (Texture)sourceTexture;
    }

    /// <summary>
    /// リングバッファを用意する（遅延量や入力サイズが変わったら作り直す）
    /// </summary>
    private void EnsureRing()
    {
        bool sizeChanged = ring != null && ring.Length > 0 && ring[0] != null
            && (ring[0].width != sourceTexture.width || ring[0].height != sourceTexture.height);

        if (ring != null && allocatedDelay == delayFrames && !sizeChanged) return;

        ReleaseRing();
        int capacity = delayFrames + 1;
        ring = new RenderTexture[capacity];
        for (int i = 0; i < capacity; i++)
        {
            ring[i] = new RenderTexture(sourceTexture.width, sourceTexture.height, 0)
            {
                name = "DelayedFrame_" + i,
            };
            ring[i].Create();
        }
        allocatedDelay = delayFrames;
        writeIndex = 0;
        filled = 0;
        accum = 0f;
        DelayedTexture = sourceTexture; // 充填されるまではライブを返す
    }

    private void ReleaseRing()
    {
        if (ring == null) return;
        foreach (RenderTexture rt in ring)
        {
            if (rt != null)
            {
                rt.Release();
                Object.Destroy(rt);
            }
        }
        ring = null;
    }

    private void OnDestroy()
    {
        ReleaseRing();
    }
}
