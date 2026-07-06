using System.IO;
using UnityEngine;

/// <summary>
/// 視点追従実験のデータ保存先パスを一元管理する静的クラス．
/// エディタ実行時は Assets/ResultData/following/ に，
/// Quest 実機ビルドでは Application.persistentDataPath/following/ に保存する．
/// </summary>
public static class FollowingPaths
{
    /// <summary>
    /// データ保存先フォルダの絶対パス（無ければ自動作成する）
    /// </summary>
    public static string DataDir
    {
        get
        {
#if UNITY_EDITOR
            string dir = Path.Combine(Application.dataPath, "ResultData", "following");
#else
            string dir = Path.Combine(Application.persistentDataPath, "following");
#endif
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>
    /// ファイル名用のタイムスタンプ（例: 20260706_193000）
    /// </summary>
    public static string Timestamp()
    {
        return System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
    }
}
