using UnityEngine;
using UnityEngine.Profiling;
/// <summary>
/// For debugging: FPS Counter
/// デバッグ用: FPS カウンタ
/// </summary>
public class FPSCounter : MonoBehaviour
{
    /// <summary>
    /// Reflect measurement results every 'EveryCalcurationTime' seconds.
    /// EveryCalcurationTime 秒ごとに計測結果を反映する
    /// </summary>
    [SerializeField, Range(0.1f, 1.0f)]
    float EveryCalcurationTime = 0.5f;

    /// <summary>
    /// FPS value
    /// </summary>
    public float Fps
    {
        get; private set;
    }

    int frameCount;
    float prevTime;

    void Start()
    {
        Application.targetFrameRate = 120;
        frameCount = 0;
        prevTime = 0.0f;
        Fps = 0.0f;
    }
    void Update()
    {
        frameCount++;
        float time = Time.realtimeSinceStartup - prevTime;

        // n秒ごとに計測
        if (time >= EveryCalcurationTime)
        {
            Fps = frameCount / time;

            frameCount = 0;
            prevTime = Time.realtimeSinceStartup;
            Debug.Log(Fps);
        }


    }
}