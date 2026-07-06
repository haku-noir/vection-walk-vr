# 05. 計測データと解析

## データ保存の仕組み

各実験スクリプトは `FixedUpdate()`（**50Hz**、`Fixed Timestep = 0.02`）でログをメモリ上の
List に蓄積し、保存操作（停止中に右中指トリガー）で CSV に書き出す。

| 実行環境 | 保存先 |
|---|---|
| Unity エディタ | `Assets/ResultData/<フォルダ>/<ファイル名>.csv` |
| Quest 実機ビルド | `Application.persistentDataPath`（= `Android/data/<プロダクト名>/files/`） |

- ログが一定数未満（スクリプトにより 100〜500 行）だと保存せずに終了する仕様。
- `OnApplicationQuit` による自動保存は**実機でスリープに入れた場合は呼ばれない**ので過信しないこと（前任者コメント）。
- 角度は 0°〜360° を **-180°〜180° に変換**（`CenterRotValue`）して記録される。

## 主な CSV フォーマット

### TrackingAccuracyLog（LogRecording.cs → `ResultData/`）

```
time, realtime, posX, posY, posZ, rotX, rotY, rotZ,
headPosX, headPosY, headPosZ,
LhandPosX/Y/Z, LhandRotX/Y/Z, RhandPosX/Y/Z, RhandRotX/Y/Z
```
- time: 実験開始からの秒数 / realtime: 実時刻（ms, Ticks由来）
- pos/rot: 頭部のローカル位置・回転 / headPos: 頭部ワールド位置
- **視点追従実験で必要な「頭部軌跡の記録」フォーマットの雛形として最適**

### walk_results（Walking.cs → `ResultData/walk/`）

```
time, posX, posY, posZ, rotX, rotY, rotZ, forX, forY, forZ, angle, miss, out, outf
```
- for*: 頭部前方ベクトル / angle: ターゲットとの角度（KeepLooking）
- miss: コースアウト累積回数 / out, outf: 注視逸脱回数・フレーム数
- ファイル名に反転条件が入る: `walk_results_LRUD20231029_222850.csv` など

### walk_results（WalkingCourse.cs / GameSystem.cs → `ResultData/walk/`）

```
time, phase, posX, posY, posZ, rotX, rotY, rotZ
```

### その他の実験

- ControllerTouchSphere / SphereGenerator / SphereLauncher / CrosshairAlignment も
  それぞれ試行時間（interactionTimes）・ミス数・頭/手の軌跡を同様の形式で保存する。
- 乱数シードが固定されている（SphereGenerator: 121, SphereLauncher: 123,
  CrosshairAlignment: 100）ため、**提示系列は再現可能**。

## ResultData フォルダの構成（実験生データ）

| フォルダ | 対応する実験（推定含む） |
|---|---|
| `walk/` | 直進歩行実験（Walking） |
| `walk_course/` | 一般歩行実験（WalkingCourse） |
| `mainwalk/` | 本実験の歩行データ（解析パイプラインが最も充実） |
| `outdoor_walk/` | 屋外歩行（OutdoorReversion） |
| `lookaround/` | 見回し実験（AroundSphere 系） |
| `touch/` | タッチ課題（ControllerTouchSphere） |
| `catch/` | 球キャッチ課題（SphereLauncher） |
| `kataashi/` | 片足立ち課題 |
| `tracking/` | トラッキング精度（LogRecording） |
| `vrsj/` | VRSJ 発表用データ |
| `any/` | 雑多（GameSystem のデフォルト保存先） |
| `*.xlsx`（NoPos_NoInst 等） | 集計済みデータ（位置提示/指示の有無 2×2 条件と思われる） |

※ 生データの詳細な解説は無し。必要なら前任者に連絡（README.txt に連絡先）。

## Python 解析スクリプト

`ResultData/` 直下および各実験フォルダ内に散在。主なもの:

| スクリプト | 内容 |
|---|---|
| `combine_csv.py` / `search_csv.py` | CSV の結合・検索 |
| `draw_elapsedTime_graph.py` | 経過時間グラフ |
| `mainwalk/walking_trajectory.py` | 歩行軌跡の描画 |
| `mainwalk/split_by_steps.py` | ステップ単位の分割 |
| `mainwalk/output/output_folder/anova.py` | 分散分析 |
| `mainwalk/output/output_folder/make_graph*.py` | グラフ生成 |
| `lookaround/output_file/trajectory_distance.py` ほか | 軌跡・距離解析 |
| `tracking/trajectory.py` | トラッキング軌跡 |

> 解析は「甘々」とのこと（前任者談）。視点追従実験では解析パイプラインの再設計を推奨
> （特に「収録軌跡 vs 実歩行軌跡の距離・時間差」の定量化。06 参照）。
