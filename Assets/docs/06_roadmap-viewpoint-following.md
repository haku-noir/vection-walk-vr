# 06. 視点追従実験への改造ロードマップ

## 実験の想定（要件）

- 事前に **VR空間内を歩行した際の映像（視点）を収録**しておく。
- 被験者が同じVR空間を歩行する際、HMD には**収録映像と現在のライブ映像を
  一定周波数で交互に切替えて**提示する。
- 被験者の HMD 座標が、事前収録の位置に**どの程度追従できたか**を計測する。
- 操作変数:
  1. VR空間のオブジェクトの数・種類（= 視覚刺激のオプティカルフロー）
  2. 映像の切替周波数

## 現有資産の流用マップ

| 必要な機能 | 流用できる既存資産 | 改造の程度 |
|---|---|---|
| HMD映像をテクスチャ経由で提示する仕組み | **映像パイプライン全体**（CenterEyeCapture → RenderTexture → CenterRawImage、02参照） | そのまま使える |
| 頭部軌跡の収録 | `LogRecording.cs`（50Hzで頭部位置・回転をCSV保存） | ほぼそのまま |
| 2映像の切替・合成 | `SetBlendingRatio.cs` + `Player_Blending.prefab`（2枚のRawImageの透明度合成） | 透明度を周波数で 0/1 交互にする拡張 |
| カメラのターゲットテクスチャ操作 | `SetTexture.cs` | そのまま部品に |
| 別オブジェクトへのカメラ同期・固定 | `CameraSync.cs`（fix_position/fix_rotation付き） | 再生カメラの制御に |
| オプティカルフロー操作（刺激の統制） | `RandomDots/` 一式（ドット数・運動・FOVを制御） | そのまま条件として使える |
| 環境の密度違い | Room（屋内・高密度）/ CartoonLowPolyCityLite（屋外）/ スカイボックスのみ | シーンの組合せ |
| 実験の開始/停止・視野マスク | `Time.timeScale` + PostProcessVolume のポーズパターン（各実験スクリプト共通） | パターンを踏襲 |
| 追従誤差の記録 | `Walking.cs` のログ構造（time,pos,rot,＋課題固有列） | 列を差し替え |
| 動画としての収録（案Bの場合） | **Unity Recorder（導入済み** `com.unity.recorder` 3.0.4**）** | 設定のみ |

視野反転そのもの（`SetReversion` の反転処理）は使わないが、
**「HMDに見せる映像を自由に加工する」ための土台はすべて揃っている**。

## 設計方針の推奨: 「映像の録画」ではなく「軌跡の録画＋再レンダリング」

収録映像の実現方法は2案ある。**案Aを推奨**する。

### 案A（推奨）: 軌跡を記録し、再生時にゴーストカメラで再レンダリング

1. 収録走: 被験者（または実験者）が歩行し、頭部軌跡（position + rotation, 50Hz）を CSV に記録。
2. 実験走: シーン内に第2の撮影カメラ（**GhostCamera**）を置き、記録軌跡どおりに動かして
   `PlaybackRT`（RenderTexture）へ描画。ライブの CenterEyeCapture は従来どおり `CenterEye` RT へ。
3. `CenterRawImage` に見せるテクスチャを `CenterEye` ⇔ `PlaybackRT` で周波数 f で交互切替。

利点:
- **環境条件（オブジェクト数）を変えても同一軌跡を使い回せる**
  → 「同じ歩行、違うオプティカルフロー」の統制が取れる。実験計画上決定的に有利。
- 解像度・アスペクト比が常にライブ映像と一致（動画ファイルの画質・遅延問題がない）。
- 記録データが軽い（CSV数MB vs 動画数GB）。Quest 実機でも扱いやすい。
- 再生時刻の補間（Lerp/Slerp）で任意フレームレートに対応可能。

### 案B: Unity Recorder で動画収録 → VideoPlayer で再生

シンプルだが、環境を変えるたびに再収録が必要・ファイルが重い・
再生タイミングの制御が粗い、という欠点がある。案Aが動かない場合の代替。

## 新規開発するもの（案Aベース）

`Assets/scripts/ViewpointFollowing/` を新設して以下を実装する想定:

| スクリプト（案） | 役割 |
|---|---|
| `TrajectoryRecorder.cs` | 収録走で頭部軌跡をCSV保存（LogRecording を整理して流用。time, posX..Z, rotX..Z または四元数） |
| `TrajectoryPlayer.cs` | CSV を読み、GhostCamera の Transform を時刻補間しながら再生（位置=Lerp、回転=Quaternion.Slerp。オイラー角の補間は330°→30°等で破綻するので**四元数で保存・補間するのが安全**） |
| `ViewSwitcher.cs` | 切替周波数 f [Hz] で CenterRawImage の texture（または2枚のRawImageのアルファ）をライブ⇔再生で交互切替。`Time.unscaledTime` 基準の累積時間方式でデューティ比 50% を保証 |
| `FollowingExperimentManager.cs` | 実験条件（環境・周波数・試行順）の管理、開始/停止、条件のカウンタバランス |
| `FollowingLogger.cs` | 実験走のログ: time, ライブ頭部 pos/rot, 対応する収録 pos/rot, 現在表示中のソース（live/playback）を1行に記録 → 追従誤差解析の元データ |

### GhostCamera の設定（CenterEyeCapture の複製でよい）

- Target Texture: `PlaybackRT`（`CenterEye.renderTexture` を複製して作る）
- Culling Mask: UI を外す（CenterEyeCapture と同じ）
- Target Eye: None
- **TrackingSpace の外**に置く（HMDトラッキングの影響を受けないため）。
  ワールド座標系で TrajectoryPlayer が直接動かす。

### 追従度の評価指標（解析側）

- 位置誤差: `|livePos(t) − recordedPos(t)|`（水平成分のみも併記推奨）
- 進行方向誤差・頭部向き誤差（ヨー角差）
- 時間的ずれ: 収録軌跡に対する遅れ（ラグ相関、または DTW で経路対応をとる）
- 切替周波数・環境条件ごとの上記の比較（既存 `anova.py` の発展）

## 実装マイルストーン（段階的に動くものを作る）

1. **軌跡収録シーン**: RoomReversion を複製 → LogRecording ベースで軌跡CSVを保存できるだけのシーン
2. **再生の確認**: GhostCamera + TrajectoryPlayer で、収録軌跡の映像が PlaybackRT に映ることを
   エディタ上で確認（RawImage に PlaybackRT を直貼りして目視）
3. **切替の実装**: ViewSwitcher で 0.5〜数Hz の切替を実装し、HMD で体感確認
4. **ロギング**: FollowingLogger で追従誤差の元データを記録、Python で軌跡を重ね描き
5. **条件管理**: 環境×周波数の条件切替、教示・練習試行
6. **本実験**: Quest 実機ビルドでの動作確認（persistentDataPath への保存、SideQuest での回収）

## 注意点・リスク

- **VR酔い・光過敏への配慮**: 映像の交互切替はフリッカー刺激になる。特に数Hz〜十数Hz の
  点滅は光感受性発作のリスク帯域なので、**切替周波数の範囲は倫理審査・安全基準と併せて慎重に決める**こと。
  休憩・中断手順（Oボタンで即座に停止＝視野マスク）は既存のポーズ機構を必ず残す。
- **ポーズ機構との干渉**: 既存実験は `Time.timeScale = 0` で停止するが、
  `ViewSwitcher` や `TrajectoryPlayer` を Update + `Time.time` で書くと停止中に進んでしまう/
  止まってしまうので、**どの時計（time / unscaledTime）に依存させるか**を最初に決めて統一する。
- **収録と再生の空間基準合わせ**: 記録座標は TrackingSpace 基準（local）か
  ワールド基準かで意味が変わる。視点リセット（Oculusボタン長押し）で原点が変わるため、
  **実験開始時の視点リセット手順を厳密にプロトコル化**する（既存実験の手順2を踏襲）。
- **オブジェクト名のハードコード**: 既存スクリプトは `GameObject.Find` 依存（02参照）。
  新規スクリプトは `[SerializeField]` 参照で書き、この問題を持ち込まないこと。
- **文字コード**: 新規スクリプトは UTF-8(BOM付き) で作成する。既存の Shift-JIS ファイルを
  編集するときはエンコーディングを壊さないよう注意（01参照）。
- **Photon（Multi シーン）は使わない想定**なら、ビルド対象から外しておくとビルドが軽くなる。

## 将来の拡張のヒント

- `SetVelRev.cs`（視野の流れだけを反転）は「視覚フィードバックの操作」という点で
  本実験と発想が近い。頭部運動と映像の対応関係を操作するコードの参考になる。
- `AdjustCanvasDistance.cs` で擬似FOVを変えられるので、視野角も操作変数に追加可能。
- 切替（矩形波）だけでなく `SetBlendingRatio` による**連続的なクロスフェード**も
  同じ構成で試せる（切替周波数の対照条件として面白い可能性）。
