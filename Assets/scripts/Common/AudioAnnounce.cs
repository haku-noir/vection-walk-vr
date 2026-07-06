using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 指定オブジェクトのz座標がある値になったときに効果音を鳴らすためのクラス
/// </summary>
public class AudioAnnounce : MonoBehaviour
{
    [SerializeField] private AudioClip audioClip;
    [SerializeField] private AudioSource audioSource;
    /// <summary>
    /// 指定オブジェクト
    /// </summary>
    [Tooltip("指定オブジェクト")]
    [SerializeField] private GameObject obj;
    /// <summary>
    /// 効果音をならすz座標
    /// </summary>
    [Tooltip("効果音をならすz座標")]
    public float target_coordinate = 0;
    private float prev_z = 0;

    // Start is called before the first frame update
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioClip = audioSource.clip;
    }

    // Update is called once per frame
    void Update()
    {
        if (((obj.transform.position.z >= target_coordinate) && (prev_z < target_coordinate)) || ((obj.transform.position.z <= target_coordinate) && (prev_z > target_coordinate)))
            audioSource.PlayOneShot(audioClip);
        prev_z = obj.transform.position.z;
    }
}
