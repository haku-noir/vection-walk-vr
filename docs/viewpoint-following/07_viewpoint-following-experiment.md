# 07. 視点追従実験 — 使い方と構成

視野反転プロジェクトの映像パイプライン（02参照）を土台に実装した、視点追従実験の本体ドキュメント。

## 実験の概要

被験者は VR 空間の直線コース（緑マーカー → 赤マーカー、約10m）を歩行する。
HMD には **事前に収録した歩行視点の映像** と **現在のライブ映像** が一定周波数で交互に提示され、
被験者の頭部（HMD）が収録軌跡にどの程度追従するかを計測する。

操作変数:
1. **環境のオブジェクト密度**（視覚刺激のオプティカルフロー量）— `1`/`2`/`3` キーで切替
2. **映像の切替周波数** — ViewSwitcher の Inspector で設定（0.1〜10 Hz）

## セットアップ（初回のみ）

Unity メニュー **Tools > 視点追従実験 > 実験シーンを生成** を実行すると、
`Assets/Scenes/ViewpointFollowing.unity` が自動構築・保存される。

ビルダー（`ViewpointFollowingSceneBuilder.cs`）が行うこと:

- `Assets/Textures/PlaybackEye.renderTexture` を CenterEye の複製として作成（収録映像の描画先）
- Player プレハブを配置し、この実験に不要な機能を無効化
  （SetReversion / PlayerInput / OVRPlayerController / CharacterController、左右眼用Canvas）
- **GhostCamera** を作成（CenterEyeCapture と同設定のカメラ。出力先だけ PlaybackEye）
- **ExperimentRig** を作成し、下記の全スクリプトをアタッチ・参照配線
- 歩行コース環境（床・高密度/低密度オブジェクト群・開始/終了マーカー）を生成
- PostProcessVolume（停止中の視野マスク）を配置

> シーンを作り直したい場合はもう一度メニューを実行すればよい（新しいシーンとして上書き保存される）。

## 実験の手順

### 操作一覧

| キーボード | Touch コントローラ | 動作 |
|---|---|---|
| O | B ボタン | 試行の開始・停止（停止中は視野に色が付く） |
| S（停止中） | 右中指トリガー | CSV 保存 |
| M（停止中） | A ボタン | モード切替（Record ⇔ Follow） |
| 1 / 2 / 3 | — | 環境密度切替（高密度 / 低密度 / なし） |

エディタ実行時は Game ビュー左上に現在のモード・状態・周波数・軌跡の読込状況が表示される。

### 1. 収録走（Record モード）

1. シーンを再生（初期状態は Record モード・停止中・視野マスクあり）
2. 実世界の開始地点に立ち、正面（+z、赤マーカー方向）を向いて**視点をリセット**
   （Quest Link: Oculus ボタン → 視点をリセット / 実機: Oculus ボタン長押し）
3. `O` で開始 → コースを歩行 → 歩き終えたら `O` で停止
4. `S` で保存 → `trajectory_日時.csv` が保存される

### 2. 実験走（Follow モード）

1. `M` で Follow モードへ切替（最新の trajectory ファイルが自動選択される。
   特定のファイルを使う場合は ExperimentRig > TrajectoryPlayer > File Name に指定）
2. **収録時と同じ立ち位置・向きで視点をリセット**（重要。座標系を収録時と揃えるため）
3. 必要なら切替周波数（ViewSwitcher > Switch Frequency）と環境密度（`1`/`2`/`3`）を設定
4. `O` で開始 → ライブ映像から始まり、設定周波数で収録映像と交互に切り替わる
5. 歩行 → 収録軌跡の再生が終わると**自動停止**（`O` で途中停止も可能）
6. `S` で保存 → `following_results_周波数_日時.csv` が保存される

### 実験条件を変えて繰り返す

同じ trajectory ファイルのまま、周波数・環境密度を変えて 4〜6 を繰り返す。
**同一軌跡・別条件の比較ができるのがこの設計（軌跡収録＋再レンダリング方式）の利点**。

## データ形式

保存先: エディタ = `Assets/ResultData/following/`、実機 = `persistentDataPath/following/`

### trajectory_*.csv（収録走、50Hz）

```
time, posX, posY, posZ, qX, qY, qZ, qW, eulerX, eulerY, eulerZ
```
- pos: 頭部のワールド座標 / q: ワールド回転（四元数、再生時の補間用）
- euler: -180°〜180° に変換済みのオイラー角（人間の確認・解析用）

### following_results_*.csv（実験走、50Hz）

```
time, source, freq,
livePosX/Y/Z, liveRotX/Y/Z,     ← ライブの頭部位置・回転
recPosX/Y/Z,  recRotX/Y/Z,      ← その瞬間の収録軌跡上の位置・回転
errXZ, err3D                     ← 追従誤差（水平面 / 3次元）
```
- source: その瞬間に表示していた映像（0 = ライブ, 1 = 収録）
- freq: 切替周波数[Hz]（ファイル名にも入る）
- errXZ が歩行追従度の主指標。時間方向のずれの解析（ラグ相関・DTW）は生の pos 列から行う

## 実装構成

### スクリプト（Assets/scripts/ViewpointFollowing/、UTF-8 BOM付き）

| スクリプト | 役割 |
|---|---|
| `FollowingExperimentManager.cs` | 実験全体の進行管理（モード・開始/停止/保存・視野マスク連動）。全体の入口はここ |
| `TrajectoryRecorder.cs` | 収録走: CenterEyeAnchor のワールド位置・回転（四元数）を 50Hz で記録し CSV 保存 |
| `TrajectoryPlayer.cs` | 実験走: 軌跡 CSV を読み、GhostCamera を時刻補間（Lerp / Slerp）しながら再生 |
| `ViewSwitcher.cs` | CenterRawImage の texture を Live ⇔ Playback で交互切替（デューティ比50%の矩形波） |
| `FollowingLogger.cs` | 実験走: ライブ頭部位置と収録位置・表示ソースを 50Hz で記録し CSV 保存 |
| `EnvironmentSwitcher.cs` | 環境オブジェクト密度の切替（1/2/3 キー） |
| `FollowingPaths.cs` | データ保存先パスの一元管理（エディタ/実機の分岐） |
| `Editor/ViewpointFollowingSceneBuilder.cs` | 実験シーンの自動構築（メニュー: Tools > 視点追従実験） |

### 映像パイプライン（02 の構成に GhostCamera 系統を追加）

```
[ライブ系統]  CenterEyeCapture ──→ CenterEye RT ──┐
                                                  ├─(ViewSwitcherが切替)─→ CenterRawImage ─→ CenterEyeAnchor ─→ HMD
[収録系統]    GhostCamera ──────→ PlaybackEye RT ─┘
              ↑ TrajectoryPlayer が収録軌跡どおりに駆動
```

### シーン構造（ViewpointFollowing.unity）

```
ViewpointFollowing
├── Directional Light
├── Floor / StartMarker(緑, z=0) / GoalMarker(赤, z=10)
├── Env_Rich    ← 高密度環境（通路脇1mおきの柱 + 外側の球）
├── Env_Sparse  ← 低密度環境（5mおきの柱のみ、初期は非表示）
├── Player      ← 既存プレハブ（視野反転・コントローラ移動は無効化済み）
├── GhostCamera ← 収録映像の再レンダリング用（Followモード時のみアクティブ）
├── PostProcessVolume ← 停止中の視野マスク
└── ExperimentRig     ← 上記スクリプト一式（参照配線済み）
```

## 設計上の決まりごと（改造時に守ること）

- **座標系**: 軌跡はワールド座標で記録・再生する。収録時と実験時で
  視点リセットの立ち位置・向きを揃えるプロトコルが前提。
- **時計**: 記録は `FixedUpdate`（50Hz）、再生・切替は `Time.deltaTime`。
  いずれも `Time.timeScale = 0`（停止中）で自動的に止まる。
  停止に影響されたくない処理を追加する場合のみ `unscaledTime` を使うこと。
- **回転の保存は四元数**。オイラー角の補間は 350°→10° などで破綻するため、
  再生用データとしては使わない（CSV のオイラー列は解析用の参考値）。
- **参照は Inspector 配線**（`[SerializeField]`/public）。既存コードの
  `GameObject.Find` 依存（02参照）をこのフォルダには持ち込まない。
- **CSV の小数点は InvariantCulture**（`.` 固定）で読み書きする。

## 安全上の注意

- 映像の交互切替は**フリッカー刺激**になる。特に数Hz〜十数Hzは光感受性発作のリスク帯域のため、
  使用する周波数範囲は倫理審査と照らして決定し、被験者への事前説明・中断手順を必ず用意すること。
- `O`（Bボタン）でいつでも停止でき、停止中は視野がマスクされる（既存実験と同じ挙動）。
- 実空間の歩行実験なので、コース上と周囲の障害物・介助者の配置は視野反転実験のプロトコルを踏襲する。

## 既知の制限・今後の課題

- 両眼視差は既存実験と同様に非対応（中央1系統のみ）。
- 収録映像の再生開始タイミングは試行開始と同時（`O` を押した瞬間）。
  被験者の歩き出しと収録の歩き出しを揃えたい場合は、収録走で開始合図から歩き出すよう統制する。
- 切替周波数を試行中に変えることは想定していない（試行間で変更する）。
- 解析スクリプト（軌跡の重ね描き・誤差の集計・条件間比較）は未作成。
  `following_results_*.csv` から Python で作る（05 の既存スクリプトが参考になる）。
