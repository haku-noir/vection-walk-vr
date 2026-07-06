using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine.UIElements;

/// <summary>
/// 周囲のランダムな位置に現れる円柱にタッチする実験を管理するクラス
/// </summary>
/// <remarks>
/// CheckpointManager クラスから操作可能
/// </remarks>
public class CylinderInteraction : MonoBehaviour
{
    /// <summary>
    /// 球の生成領域の座標値の最大値
    /// </summary>
    [Tooltip("球の生成領域の座標値の最大値")]
    public Vector3 max_coordinate = new Vector3(-50, 0, -50);
    /// <summary>
    /// 球の生成領域の座標値の最小値
    /// </summary>
    [Tooltip("球の生成領域の座標値の最小値")]
    public Vector3 min_coordinate = new Vector3(-500, 0, -400);
    /// <summary>
    /// 円柱の高さ
    /// </summary>
    [Tooltip("円柱の高さ")]
    public float height = 100;
    /// <summary>
    /// 円柱の半径
    /// </summary>
    [Tooltip("円柱の半径")]
    public float radius = 2.2f;

    /// <summary>
    /// 円柱に触れたときのエフェクト
    /// </summary>
    [Tooltip("円柱に触れたときのエフェクト")]
    public ParticleSystem explosionEffect;

    [System.NonSerialized] public float start_time; 
    private List<float> interactionTimes = new List<float>();

    // 何に使っていたのか不明
    // [SerializeField] CheckpointManager manager;

    void Start()
    {
        start_time = Time.time;
        Vector3 initPos = GetRandomPosition();
        GenerateCylinder(initPos, height);
    }

    /// <summary>
    /// ランダムな位置ベクトルを取得
    /// </summary>
    /// <returns> 位置ベクトル </returns>
    Vector3 GetRandomPosition()
    {
        Vector3 pos = new Vector3(UnityEngine.Random.Range(min_coordinate.x, max_coordinate.x), height / 2, UnityEngine.Random.Range(min_coordinate.z, max_coordinate.z));
        Vector3 pos_on_floor = new Vector3(pos.x, -8, pos.z);

        Collider[] colliders = new Collider[8];
        int count = Physics.OverlapSphereNonAlloc(pos_on_floor, 2.5f, colliders);

        for (int i = 0; i < count; i++)
        {
            if (colliders[i].CompareTag("Barrier"))
            {
                Debug.Log("avoid" + pos_on_floor);
                pos = new Vector3(UnityEngine.Random.Range(min_coordinate.x, max_coordinate.x), height / 2, UnityEngine.Random.Range(min_coordinate.z, max_coordinate.z)); ;
                pos_on_floor = new Vector3(pos.x, -8, pos.z);
                count = Physics.OverlapSphereNonAlloc(pos_on_floor, 2f, colliders);
                i = -1;
            }
        }
        return pos;
    }

    /// <summary>
    /// 円柱を生成
    /// </summary>
    /// <param name="position"> 円柱の生成位置 </param>
    /// <param name="height"> 円柱の高さ </param>
    public void GenerateCylinder(Vector3 position, float height)
    {
        // 円柱の生成
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.tag = "CheckPoint";

        // 高さを設定
        cylinder.transform.localScale = new Vector3(radius, height, radius);

        // 位置を設定
        Collider[] colliders = new Collider[12];


        int count = Physics.OverlapSphereNonAlloc(position, 0.9f * radius, colliders);
        while (true)
        {
            for (int i = 0; i < count; i++)
            {
                if (colliders[i].CompareTag("Barrier"))
                {
                    position = GetRandomPosition();
                    count = Physics.OverlapSphereNonAlloc(position, 0.9f * radius, colliders);
                    i = -1;
                }
            }
            break;
        }
        cylinder.transform.position = position;

        // 色を指定
        cylinder.GetComponent<Renderer>().material.color = Color.red;

        // 当たり判定を削除しトリガーとして利用
        cylinder.GetComponent<Collider>().isTrigger = true;
    }

    /// <summary>
    /// 円柱に触れたときの処理
    /// </summary>
    /// <param name="other"> 接触対象 </param>
    void OnTriggerEnter(Collider other)
    {
        Debug.Log("checkpoint");
        // 衝突したオブジェクトがCheckPointであるかチェック
        if (other.CompareTag("CheckPoint"))
        {
            // 接触時の経過時間を記録
            float elapsedTime = Time.time - start_time;
            interactionTimes.Add(elapsedTime);
            if (interactionTimes.Count % 10 == 0 && explosionEffect != null)
            {
                ParticleSystem explosion = Instantiate(explosionEffect, transform.position, Quaternion.identity);
                explosion.Play();
            }

            // 円柱との接触が検出された場合の処理
            HandleCylinderInteraction(other.gameObject);
        }
    }

    /// <summary>
    /// 円柱をワープさせる
    /// </summary>
    /// <param name="cylinder"> ワープ対象の円柱 </param>
    void HandleCylinderInteraction(GameObject cylinder)
    {
        // 新しい位置を計算して元の円柱の位置を変更する
        Vector3 newPos = GetRandomPosition();
        cylinder.transform.position = newPos;
    }

    /// <summary>
    /// アプリケーション終了時に呼ばれる関数です．
    /// </summary>
    /// <remarks>
    /// ここでの中身は SaveAndQuitGame 関数とほぼ同じです．
    /// しかしながら，アプリ化して HMD に入れた場合は呼ばれないことが多く使えません．
    /// </remarks>
    void OnApplicationQuit()
    {
        if (interactionTimes.Count > 7)
        {
            // アプリケーションが終了する際に、結果を日時付きのCSVに出力
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = "interaction_results_" + timestamp + ".csv";
            string folderName = "ResultData";
            string filePath = Path.Combine(Application.dataPath, folderName, fileName);

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                // ヘッダ行を書き込む
                writer.WriteLine("InteractionNumber,ElapsedTime");

                // データ行を書き込む
                for (int i = 0; i < interactionTimes.Count; i++)
                {
                    writer.WriteLine((i + 1) + "," + interactionTimes[i]);
                }
            }

            Debug.Log("Results saved to: " + filePath);
        }
    }
}

