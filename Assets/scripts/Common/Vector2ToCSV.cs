using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;

public class Vector2ToCSV : MonoBehaviour
{
    // Vector2型のリスト
    public List<Vector2> vector2List = new List<Vector2>(50);
    public List<Vector2> vector2ListSum = new List<Vector2>(50);
    public int seed = 100;

    void Start()
    {
        UnityEngine.Random.InitState(seed);
        // サンプルデータをリストに追加（必要に応じて削除）
        for (int i = 0; i < 50; i++)
        {
            vector2List.Add(new Vector2(UnityEngine.Random.Range(-30f, 30f), UnityEngine.Random.Range(-30f, 30f)));
            if (i == 0)
            {
                vector2ListSum.Add(new Vector2(CenterRotValue(vector2List[i].x), CenterRotValue(vector2List[i].y)));
            }
            else
            {
                vector2ListSum.Add(new Vector2(CenterRotValue(vector2ListSum[i - 1].x + vector2List[i].x), CenterRotValue(vector2ListSum[i - 1].y + vector2List[i].y)));
            }
        }

        // CSVファイルとして出力
        SaveToCSV(vector2List, seed + "Vector2Data.csv");
    }

    void SaveToCSV(List<Vector2> vector2List, string fileName)
    {
        // StringBuilderを使ってCSV形式の文字列を作成
        StringBuilder csvContent = new StringBuilder();
        csvContent.AppendLine("X,Xsum,Y,Ysum");

        for ( int i = 0;i < vector2List.Count;i++)
        {
            csvContent.AppendLine($"{vector2List[i].x},{vector2ListSum[i].x},{vector2List[i].y},{vector2ListSum[i].y}");
        }

        // ファイルパスを設定
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string folderName = Path.Combine("ResultData", "vrsj");
        string filePath = Path.Combine(Application.dataPath, folderName, fileName);

        // ファイルに書き込み
        File.WriteAllText(filePath, csvContent.ToString());

        Debug.Log($"CSV file saved to: {filePath}");
    }

    float CenterRotValue(float value)
    {
        if (value > 180)
        {
            value = value - 360;
        }
        return value;
    }
}
