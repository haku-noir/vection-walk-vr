using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BGM ‚ð—¬‚·ƒNƒ‰ƒX
/// </summary>
public class Alerm : MonoBehaviour
{
    public AudioClip sound1;
    public AudioSource audioSource;

    void Start()
    {
        //Component‚ðŽæ“¾
        audioSource = GetComponent<AudioSource>();
        sound1 = audioSource.clip;
    }

    public void PlaySound()
    {
        //audioSource.PlayOneShot(sound1);
        audioSource.Play();
        //_alerm.audioSource.loop = true;
    }
}
