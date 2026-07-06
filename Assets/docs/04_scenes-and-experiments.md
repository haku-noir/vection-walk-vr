# 04. シーン一覧と実験の実行方法

## シーン一覧（Assets/Scenes/）

引継ぎ資料に記載のあるもの:

| シーン | 内容 |
|---|---|
| `8figure` | 8の字歩行実験 |
| `AroundSphere` | 周囲に生成される球を視野中央に収める／タッチする実験（SelectSphereExperiment で切替） |
| `LogRecording` | トラッキングのログ確認・記録用 ★視点追従の収録機能のベース候補 |
| `MirrorImage` | 回転の挙動の確認用 |
| `OutdoorReversion` | 屋外環境（CartoonLowPolyCityLite）。円柱タッチ実験。コントローラ移動可 |
| `PassThrough` | パススルー映像を Unity 内で見る試行 |
| `RandomDotPixels` | ピクセル単位のランダムドット提示（GitHub に別資料あり） |
| `RandomDots` | ランダムドット提示（KIMさんの改良版） |
| `RoomReversion` | 屋内環境（部屋）。汎用の実験環境 |
| `SphereLauncher` | 奥から飛んでくる球にタッチする実験 |
| `Tracking` | トラッキング実験（未使用） |
| `Walking` | 直進歩行実験（メイン実験の一つ） |
| `WalkingCourse` | 一般歩行実験 |

引継ぎ資料に記載のないもの（後から追加されたと思われる）:

| シーン | 内容（推定） |
|---|---|
| `ChackViewingAngle` | 視野角の確認用（AdjustCanvasDistance 関連） |
| `Introduction` | 被験者向け導入・練習用 |
| `Multi` | Photon PUN2 によるマルチプレイヤー試行 |
| `VRSJ` | VRSJ（日本バーチャルリアリティ学会）発表用デモ。`ResultData/vrsj/` にデータあり |
| `ForTest/` 以下 | 開発中のテストシーン群（reversion, CameraExchange, BackEyeTest など） |

## 共通の操作方法

多くの実験シーンで共通（詳細は各スクリプト参照）:

| キーボード | Touch コントローラ | 動作 |
|---|---|---|
| O | B ボタン | **実験の開始・中止**（世界の時間停止 ⇔ 再開。停止中は視野に色が付く/真っ黒） |
| L | Y ボタン | 視野**左右**反転 |
| U | X ボタン | 視野**上下**反転 |
| F | 左中指トリガー | 視野**前後**反転 |
| X | — | 反転リセット（バグったときの復旧もこれ） |
| — | 右中指トリガー（停止中に） | **CSV保存して終了** |
| — | 左人差し指トリガー | 進行方向表示の切替（Walking）／シーン切替（SwitchEnvironment） |
| P | B ボタン | メニュー開閉（MenuForReversion のあるシーン） |

## 実験の一般的な流れ（直進歩行実験の例）

1. `Walking` シーンを開き、エディタで実行（Quest 単体ならビルドしたアプリを起動）。
   このとき視野は真っ暗（= 時間停止中）。
2. 実世界で開始地点に立ち、正面を向いて**視点をリセット**する。
   - Quest Link: Oculus ボタン → 「視点をリセット」
   - Quest 単体: Oculus ボタン長押し
3. 視野反転を上記キーで好きに設定（実験中の変更も可能）。
4. O キー / B ボタンで実験開始（視野が見えるようになる）。
5. 課題（歩行等）を実施。
6. O / B で停止 → 右中指トリガーで CSV 保存。

## 実行環境ごとの注意

- **エディタ実行（Quest Link）**: CSV は `Assets/ResultData/<フォルダ>/` に保存される。
- **Quest 実機ビルド**: CSV は `Application.persistentDataPath`
  （`storage/emulated/0/Android/data/<プロダクト名>/files`）に保存。
  プロダクト名は Project Settings > Player > Product Name。取り出しは SideQuest 推奨。
- 実験内容の学術的背景は前任者の Overleaf（論文原稿）を参照とのこと（アクセス権は要確認）。

## 環境アセットとシーンの対応

| 環境 | 使用シーン | 視覚刺激の特徴 |
|---|---|---|
| 屋内の部屋（Room, BigFurniturePack） | RoomReversion など | 家具などオブジェクト密度が高い |
| 屋外の街（CartoonLowPolyCityLite） | OutdoorReversion | 広い空間・建物 |
| ランダムドット | RandomDots, RandomDotPixels | ドット数・運動を完全制御可能 |
| スカイボックス（SkySeries Freebie ほか） | 各種 | 遠景のみ |

→ **視点追従実験の「オブジェクト数・種類（オプティカルフロー）の操作」は、
これらの環境の使い分け＋RandomDots 系の刺激で実現できる**（06参照）。
