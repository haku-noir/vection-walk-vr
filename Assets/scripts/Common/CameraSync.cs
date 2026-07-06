using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// このスクリプトをアタッチしたオブジェクトを別のオブジェクト (主にCenterEyeAnchor) に同期して動かすクラス
/// </summary>
public class CameraSync : MonoBehaviour
{
    [SerializeField] private Transform _centerEyeAnchor;

    private Vector3 _originPos;
    private Vector3 _originRot;
    private Vector3 rotEuler;
    private Vector3 position_offset;
    private Vector3 euler_offset;

    public bool fix_position = false;
    public bool fix_rotation = false;
    public bool initial_sync = true;

    void Awake()
    {
        if (initial_sync)
        {
            transform.position = _centerEyeAnchor.transform.position;
            transform.rotation = _centerEyeAnchor.transform.rotation;
        }
    }

    private void Start()
    {
        _originPos = transform.position;
        _originRot = transform.eulerAngles;
    }

    void Update()
    {
        if (fix_position)
        {
            _originPos = transform.position;
        }
        else
        {
            Vector3 Posdif = _centerEyeAnchor.position - _originPos;
            transform.position = _originPos + Posdif;
        }

        if (fix_rotation)
        {
            _originRot = transform.eulerAngles;
        }
        else
        {
            Vector3 Rotdif = _centerEyeAnchor.eulerAngles - _originRot;
            rotEuler = _originRot + Rotdif;
            transform.eulerAngles = rotEuler;
        }
            
    }
}
