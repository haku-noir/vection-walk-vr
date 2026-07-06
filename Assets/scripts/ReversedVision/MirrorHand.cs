using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 前額面に対して対称な位置に手を表示するクラス
/// </summary>
public class MirrorHand : MonoBehaviour
{
    /// <summary>
    /// トラッキングした頭部
    /// </summary>
    [Tooltip("トラッキングした頭部")]
    [SerializeField] private GameObject head;
    /// <summary>
    /// トラッキングした左手
    /// </summary>
    [Tooltip("トラッキングした左手")]
    [SerializeField] public GameObject Lhand;
    /// <summary>
    /// トラッキングした右手
    /// </summary>
    [Tooltip("トラッキングした右手")]
    [SerializeField] public GameObject RHand;
    /// <summary>
    /// 左手の見た目
    /// </summary>
    [Tooltip("左手の見た目")]
    [SerializeField] private GameObject LHandPrefab;
    /// <summary>
    /// 右手の見た目
    /// </summary>
    [Tooltip("右手の見た目")]
    [SerializeField] private GameObject RhandPrefab;
    /// <summary>
    /// 前後対称な位置の左手
    /// </summary>
    /// <remarks>
    /// 元の左手とは別でオブジェクトを用意して表示する
    /// </remarks>
    [Tooltip("前後対称な位置の左手")]
    [System.NonSerialized] public GameObject mirrorLhand;
    /// <summary>
    /// 前後対称な位置の右手
    /// </summary>
    [Tooltip("前後対称な位置の右手")]
    [System.NonSerialized] public GameObject mirrorRhand;
    private GameObject body;

    /// <summary>
    /// 左手の見た目（別の関数で使用，多分上と統合できる）
    /// </summary>
    [Tooltip("左手の見た目")]
    [SerializeField] public GameObject LHandAppearance;
    /// <summary>
    /// 右手の見た目（別の関数で使用，多分上と統合できる）
    /// </summary>
    [Tooltip("右手の見た目")]
    [SerializeField] public GameObject RHandAppearance;

    /// <summary>
    /// 左手の当たり判定
    /// </summary>
    [Tooltip("左手の当たり判定")]
    [System.NonSerialized] public SphereCollider LhandCollider;
    /// <summary>
    /// 右手の当たり判定
    /// </summary>
    [Tooltip("右手の当たり判定")]
    [System.NonSerialized] public SphereCollider RHandCollider;

    /// <summary>
    /// 対称な位置に表示するか否か
    /// </summary>
    [Tooltip("対称な位置に表示するか否か")]
    public bool reverseAgainstHead = false;
    /// <summary>
    /// 対称面は目（HMD）よりどれだけ後ろか
    /// </summary>
    [Tooltip("対称面は目（HMD）よりどれだけ後ろか")]
    [Range(-0.5f, 0.5f)] public float PlaneOfSymmetryFromEye = -0.15f;


    private void Awake()
    {
        mirrorLhand = setMirrorObject(LHandPrefab);
        mirrorRhand = setMirrorObject(RhandPrefab);

        LhandCollider = Lhand.GetComponent<SphereCollider>();
        RHandCollider = RHand.GetComponent<SphereCollider>();
    }

    void Start()
    {
        body = new GameObject("body");
    }

    void Update()
    {
        Vector3 headPos = head.transform.position;
        Quaternion headRot = head.transform.rotation;
        if (reverseAgainstHead)
        {
            body.transform.position = headPos;
            body.transform.rotation = headRot;
        }
        else
        {
            body.transform.position = new Vector3(headPos.x, 0, headPos.z);
            body.transform.localEulerAngles = new Vector3(0, headRot.eulerAngles.y, 0);
        }
        body.transform.position += PlaneOfSymmetryFromEye * body.transform.forward;
        Vector3 LhandPos = Lhand.transform.position;
        Vector3 RhandPos = RHand.transform.position;
        Quaternion LhandRot = Lhand.transform.rotation;
        Quaternion RhandRot = RHand.transform.rotation;

        // 体を基準としたローカル座標系に置き換える
        Vector3 LhandPosDif = body.transform.InverseTransformPoint(LhandPos); //体から見た腕の位置
        Vector3 mirrorLhandLocalPos = new Vector3(LhandPosDif.x, LhandPosDif.y, -LhandPosDif.z); //体からみた座標系で前後反転した位置
        Vector3 mirrorLhandPos = body.transform.TransformPoint(mirrorLhandLocalPos); // ワールド座標系に戻す
        Vector3 RhandPosDif = body.transform.InverseTransformPoint(RhandPos);
        Vector3 mirrorRhandLocalPos = new Vector3(RhandPosDif.x, RhandPosDif.y, -RhandPosDif.z);
        Vector3 mirrorRhandPos = body.transform.TransformPoint(mirrorRhandLocalPos);

        Quaternion LhandRotDif = Quaternion.Inverse(body.transform.rotation) * LhandRot; //体から見た腕の向き
        Quaternion mirrorLhandLocalRot = Quaternion.Euler(-LhandRotDif.eulerAngles.x, -LhandRotDif.eulerAngles.y, LhandRotDif.eulerAngles.z);
        Quaternion mirrorLhandRot = body.transform.rotation * mirrorLhandLocalRot; //掛け算は姿勢クォータニオン（ローカル回転）が後ろ
        Quaternion RhandRotDif = Quaternion.Inverse(body.transform.rotation) * RhandRot;
        Quaternion mirrorRhandLocalRot = Quaternion.Euler(-RhandRotDif.eulerAngles.x, -RhandRotDif.eulerAngles.y, RhandRotDif.eulerAngles.z);
        Quaternion mirrorRhandRot = body.transform.rotation * mirrorRhandLocalRot;

        mirrorLhand.transform.position = mirrorLhandPos;
        mirrorRhand.transform.position = mirrorRhandPos;
        mirrorLhand.transform.rotation = mirrorLhandRot;
        mirrorRhand.transform.rotation = mirrorRhandRot;

        //LhandCollider.enabled = false;
        //RHandCollider.enabled = false;
    }

    /// <summary>
    /// 対称な手を生成する関数
    /// </summary>
    /// <param name="prefab"> 見た目のプレハブ </param>
    /// <returns> 対称な手のゲームオブジェクト </returns>
    GameObject setMirrorObject(GameObject prefab)
    {
        GameObject mirror_object = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        mirror_object.transform.localScale = new Vector3(1, 1, -1);
        Rigidbody rb = mirror_object.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        SphereCollider col = mirror_object.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0, 0, -0.03f);
        col.radius = 0.07f;

        return mirror_object;
    }

}
