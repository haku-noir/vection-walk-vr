# 03. スクリプトリファレンス

`Assets/scripts/` 以下の自作スクリプトの一覧。
★ = 視点追従実験への改造で特に流用価値が高いもの（詳細は 06 参照）。

> **文字コード注意**: これらの .cs は日本語コメントが **Shift-JIS** で保存されている。
> UTF-8 で開くと文字化けする（コードは無事）。編集時はエンコーディングに注意。

## ReversedVision/ — 視野反転実験のコア

### SetReversion.cs ★（視野反転の本体）

視野反転の実装本体。Player プレハブ相当のオブジェクトにアタッチ。

- **左右反転** (`leftRightReversion`): RawImage の `localScale.x` を -1倍
- **上下反転** (`upDownReversion`): RawImage の `localScale.y` を -1倍
- **前後反転** (`frontBackReversion`): CenterEyeCapture カメラ自体を 180° 回転 ＋
  目の回転直径 (`eye_rotation_diameter`=0.15m) 分だけ位置補正。
  カメラ180°回転は「前後反転＋左右反転」になるため、左右反転を1回打ち消す処理が入っている
- Inspector のチェックボックス／キー入力（L, U, F, X=リセット）／コントローラ（Y, X, 左中指トリガー）で切替
- 前後反転時に `MirrorHand` と連動して手の表示も対称化できる（`showMirrorHand`）
- 両眼視差非対応（center_ 系のみ使用）

### SetVelRev.cs（速度反転・実験的）

視野を直接反転させず、**頭部運動に対する視野の流れ（オプティカルフロー）だけを反転**させる実験的クラス。
静止していれば反転に気づかない。反転切替時の頭部姿勢を基準に、以降の頭部回転差分に
符号反転ベクトル `rot_rev` を掛けて CenterEyeCapture の姿勢を決める。
（オプティカルフロー操作という点で視点追従実験の発想に近い）

### MenuForReversion.cs

VR内メニュー（反転切替UIパネル）の管理。Bボタン/Pキーで開閉。
メニュー表示中は必ず CenterEyeAnchor で直接見る（usePerEyeCameras を一時的に False）。
レイヤー13 を cullingMask に加減してメニューの可視性を切替。

### SetBlendingRatio.cs ★（2映像の合成）

**2つの RawImage を透明度指定で重ね合わせる**。`Player_Blending.prefab` で使用。
transparancy1（通常1固定）と transparancy2 を Inspector で調整。
→ 視点追従実験の「収録映像とライブ映像の切替・混合」にそのまま発展させられる。

### MirrorHand.cs

前後反転時に、体の前額面に対して**対称な位置に手（コントローラ）の分身を表示**する。
頭部位置から対称面（`PlaneOfSymmetryFromEye`=-0.15m）を定め、
体基準ローカル座標系で z 反転 → ワールドに戻す座標変換で実現。

### SwitchMirrorWorlds.cs

視野反転と同時に**部屋（環境オブジェクト）自体も localScale 反転**させ、
被験者に反転を気づかせないための仕掛け。L/U/F キーで部屋の x/y/z スケールを -1倍。

### Walking.cs（直進歩行実験）

直進歩行実験の管理・記録。機能が多い分、直線経路前提。

- FixedUpdate（50Hz）で頭部の位置・回転・前方ベクトル、注視角度、コースアウト回数等を記録
- 頭部の |x| > 0.2m でコースアウトとみなし警告音（`Alerm`）＋ miss カウント
- 進行方向の表示切替（線/マーカー、左人差し指トリガー）
- 保存: `walk_results_{LR}{UD}{FB}{日時}.csv`（反転条件がファイル名に入る）→ `ResultData/walk/`

### WalkingCourse.cs（汎用歩行実験）

Walking の機能削減版。汎用コース歩行用。phase 番号と miss 数を記録。
保存: `walk_results_{日時}.csv` → `ResultData/walk/`

### GameSystem.cs

セーブ・終了処理の汎用クラス（継承利用を想定していたが動作未確認、とのこと）。
時間停止(PauseGame)・CSV保存(SaveAndQuitGame)・OnApplicationQuit 時の自動保存を持つ。

### KeepLooking.cs

対象オブジェクトを**視野中央から左右5°以内に捉え続けているか**を判定。
逸れた回数 `out_count`・逸れていたフレーム数 `out_frame` を公開（Walking が記録）。
警告音のオンオフ可。

### CrosshairAlignment.cs

十字（クロスヘア）位置合わせ課題。ランダムな方向（±30°）に十字を出し、
視線の十字と 0.2 秒重なったら次へワープ。40試行で自動保存・終了。
シード固定 (`Random.InitState(100)`) で提示順は再現可能。

### SphereGenerator.cs（AroundSphere シーン）

プレイヤー周囲の半球面上（半径5m、θ/φ範囲指定）のランダム位置に球を生成する課題。
シード固定（121）。前後反転時は生成範囲とライトも前後対称化（`FrontBackReversed`）。

### ControllerTouchSphere.cs（AroundSphere シーン）

周囲のランダム位置（直方体領域）に現れる球に**コントローラでタッチ**する課題。
タッチまでの時間・ミス数・頭/両手の軌跡を記録。MirrorHand の分身の手でもタッチ可能
（`NondominantHandTouch` を動的にアタッチ）。

### SelectSphereExperiment.cs

AroundSphere 系実験の切替器。`SeekAndLook`（見るだけ; SphereGenerator）と
`SeekAndMoveAndTouch`（触りに行く; ControllerTouchSphere）を enum で選択。

### SphereLauncher.cs（SphereLauncher シーン）

奥（10m先）からプレイヤーへ球を斜方投射し、タッチできたかを記録する課題。
発射間隔・平均到達時間・重力を Inspector で設定。シード固定（123）。

### CylinderInteraction.cs / CheckpointManager.cs / CheckPointBarrier.cs（OutdoorReversion シーン）

屋外環境でランダム位置に現れる赤い円柱にタッチする課題。
- CylinderInteraction: 円柱の生成・タッチ判定・到達時間記録。`Barrier` タグ領域を避けて生成
- CheckpointManager: 円柱の表示/非表示切替と実験初期化
- CheckPointBarrier: 出現禁止領域に生成された円柱を移動させる

### SwitchEnvironment.cs

シーン切替（"reverse" ⇔ "the last revelation"）。左人差し指トリガーで発動。
※対象シーン名は現存しないため、過去のシーン用と思われる。

### ChangeColorOfButton.cs / ResetColorOfButton.cs

メニューUIのボタン色切替（クリックで黄⇔白）とリセット。

## Common/ — 汎用ユーティリティ

### LogRecording.cs ★（トラッキング記録の見本）

HMD・両手のトラッキングを**そのまま記録する**ためのクラス（LogRecording シーン）。
- FixedUpdate（50Hz）で頭部 local/world 位置・回転、両手の位置・回転、実時刻(ms)を記録
- O/B で開始・停止、停止中に右中指トリガーで保存
- 保存: `TrackingAccuracyLog{日時}.csv` → `ResultData/`
- 画面にリアルタイムで座標をテキスト表示可能（showLog）
→ **視点追従実験の「事前収録軌跡の記録」はこのクラスの流用が最短**。

### CameraSync.cs ★

アタッチしたオブジェクトを CenterEyeAnchor（等）に**同期して動かす**。
`fix_position` / `fix_rotation` で位置・回転それぞれ同期の一時停止が可能。
→ 収録カメラ・再生カメラの制御に流用可能。

### PlayerInput.cs

キーボード（WASD+Shift/Alt）と Touch コントローラ（左スティック移動・右スティック回転）で
Player を移動させる。OutdoorReversion 等のコントローラ操作シーンで使用。
右スティック押込みで回転リセット。

### SetTexture.cs ★

カメラの `targetTexture` をスクリプトから設定する小物。
→ 映像ソース切替の基本部品になる。

### その他

| スクリプト | 概要 |
|---|---|
| Alerm.cs | 警告音・BGM 再生 |
| AudioAnnounce.cs | 指定オブジェクトが特定 z 座標に達したら効果音 |
| EnablePassthrough.cs | Quest のパススルー映像を Unity 内で見る簡易実装 |
| Laser.cs / LaserCylinder.cs | 手から伸びるレーザーポインタ（メニュー操作・照準用） |
| NonDominantHandLauncher.cs / NondominantHandTouch.cs | 非利き手タッチを各実験スクリプトへ中継 |
| PointPlacer.cs | ポイント配置ユーティリティ |
| RandomWalker.cs | オブジェクトをランダムウォークさせる（移動範囲・速度・平均化フィルタ付き） |
| SetColor.cs | オブジェクトの色設定 |
| ShowTransform.cs | Transform 値の表示（デバッグ） |
| SwitchActiveState.cs / SwitchState.cs | アクティブ状態の切替 |
| TargetIndicator.cs | ターゲット方向の指示表示 |
| Vector2ToCSV.cs | Vector2 リストの CSV 書出し（`100Vector2Data.csv` の生成元と思われる） |

## RandomDots/ — ランダムドット刺激 ★（オプティカルフロー操作）

視覚刺激としてのランダムドット提示。**「オブジェクトの数・種類でオプティカルフローを
操作する」という視点追従実験の操作変数に直結する資産**。KIMさんによる改良版もこの系統。

| スクリプト | 概要 |
|---|---|
| CylinderDot.cs | 円柱面上にランダムドットを生成。HMD の運動に対するドットの動き方を3モードから選択（等角速度/逆方向等速 等）。順応用に軸距離を漸増するオプションあり |
| PlaneDot.cs | 平面上のランダムドット。HMD 運動に対する同期比率・ドットサイズ・平面距離/幅を設定可能 |
| QuickRandomDotTextureUI.cs | テクスチャに**ピクセル単位**でランダムドットを描画・運動させる（GitHub に詳細ありとのこと） |
| SingleDot.cs | 単一ドット |
| AdjustCanvasDistance.cs ★ | カメラと UI の距離で**擬似的に視野角(FOV)を変更**する。視野映像パイプラインの調整部品 |

## Test or Failed/ — 実験的・未使用

過去の試行錯誤置き場。基本的に参照不要だが、一部は参考になる:

- `FPSCounter.cs` — FPS 計測（デバッグに有用）
- `CorrectARCamera.cs` + `CorrectARCameraShader.shader` — パススルー補正の試行
- `RandomDotTextureUI.cs` — QuickRandomDotTextureUI の旧版
- ほか（Aiming, CameraScaler, ConversionTest, EnterCount, ForIntroduction, ItemButton,
  MenuController, NewBehaviourScript, OVRInputForTest, UITest, sideline, test_angle,
  test_camera_transform）
