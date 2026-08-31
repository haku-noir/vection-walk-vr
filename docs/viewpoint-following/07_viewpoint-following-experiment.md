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
| O | B ボタン | 試行の開始・停止（**保存なし**。練習・動作確認用。停止中は視野に色が付く） |
| P | Y ボタン | **自動保存つき開始**（本番用。試行の停止時＝再生終了の自動停止時を含む＝に CSV を自動保存） |
| S（停止中） | 右中指トリガー | CSV 手動保存（O で開始した試行のデータを残したくなったとき用） |
| M（停止中） | A ボタン | モード切替（Record ⇔ Follow） |
| 4（停止中） | — | **4ストローク提示の ON/OFF**（詳細は [08_fourstroke.md](08_fourstroke.md)） |
| V（停止中） | — | 4ストロークの極性切替（Enhance → Reversal → Zero） |
| 1 / 2 / 3 | — | 環境密度切替（高密度 / 低密度 / なし） |

四角錐ガイド（[09_cone-guide-spec.md](09_cone-guide-spec.md)）を使う場合の操作。**すべて停止中のみ有効**:

| キーボード | 動作 |
|---|---|
| K | **錐ガイド経路の ON/OFF**（オフの間は上記の従来動作と完全に同じ） |
| G | 背景チャンネル巡回（Live固定 → Ghost固定 → 矩形波交替 → 4ストローク） |
| C | 箱チャンネル巡回（Off → Other固定 → Self固定 → 矩形波交替 → 4ストローク） |
| B | 箱の4ストローク極性切替（Enhance → Reversal → Zero） |
| ↑ / ↓ | f_bg（背景の切替周波数）±0.5Hz |
| ← / → | f_box（箱の切替周波数）±0.5Hz（同期がオンなら自動的に解除される） |

錐ガイドが有効なとき、`4` キーは背景チャンネルの「矩形波交替 ⇔ 4ストローク」、
`V` キーは**背景の**4ストローク極性を切り替える（キーの意味は従来と同じ。箱の極性は `B`）。
錐ガイドは **Follow モードでのみ**有効になる（収録走では素のライブ映像を提示する）。

停止は常に `O` / B ボタン。`P` で開始した試行は停止と同時に CSV が保存されるので、
本番は `P` で開始するだけでよい（`O` 開始なら何度試してもファイルは増えない）。

エディタ実行時は Game ビュー左上に現在のモード・状態・周波数・軌跡の読込状況が表示される。

### 1. 収録走（Record モード）

1. シーンを再生（初期状態は Record モード・停止中・視野マスクあり）
2. 実世界の開始地点に立ち、正面（+z、赤マーカー方向）を向いて**視点をリセット**
   （Quest Link: Oculus ボタン → 視点をリセット / 実機: Oculus ボタン長押し）
3. `P` で開始（自動保存つき）→ コースを歩行 → 歩き終えたら `O` で停止
4. 停止と同時に `trajectory_日時.csv` が自動保存される
   （練習では `O` で開始すれば保存されない。その場合も停止後 `S` で手動保存は可能）

### 2. 実験走（Follow モード）

1. `M` で Follow モードへ切替（最新の trajectory ファイルが自動選択される。
   特定のファイルを使う場合は ExperimentRig > TrajectoryPlayer > File Name に指定）
2. **開始地点合わせは自動**: 試行開始（`O`/`P`）の瞬間に、今立っている位置と向き（ヨー）が
   収録開始時点に一致するよう OVRCameraRig が自動調整される
   （FollowingExperimentManager > Align To Recording On Start、初期値オン）。
   そのため厳密な立ち位置合わせは不要だが、**実空間でコース分（約10m）歩ける方向を向いて**開始すること
   （今向いている実方向が、仮想空間でのコース進行方向になる）
3. 必要なら実験条件を設定:
   - **切替周波数**: ViewSwitcher > Switch Frequency
   - **環境密度**: `1`/`2`/`3` キー
   - **4ストローク提示**: 停止中に `4` キー（矩形波切替の代わりに、ライブと収録映像を
     グレースケール化＋輝度反転＋クロスフェードの4ストローク合成で提示する。
     周波数は Switch Frequency を共用。極性は `V` キー。詳細は [08_fourstroke.md](08_fourstroke.md)。
     有効だった試行はファイル名に `_4stEnhance` などのタグが付く）
   - **再生成分**: TrajectoryPlayer > Playback Components
     | 値 | 収録映像の位置 | 収録映像の回転 |
     |---|---|---|
     | PositionAndRotation | 収録軌跡 | 収録軌跡 |
     | PositionOnly | 収録軌跡 | **現在のHMD** |
     | RotationOnly | **現在のHMD** | 収録軌跡 |

     PositionOnly では「収録どおりの位置から今の頭の向きで見た映像」、
     RotationOnly では「今いる位置から収録どおりの向きで見た映像」が提示される。
     CSV の recPos/recRot 列には**実際に提示された映像カメラの姿勢**（条件による
     置き換え後の値）が記録される（条件はファイル名に入る。
     例: `following_results_1.0Hz_PositionOnly_～.csv`）
4. `P` で開始（自動保存つき）→ ライブ映像から始まり、設定周波数で収録映像と交互に切り替わる
5. 歩行 → 収録軌跡の再生が終わると**自動停止**し、`following_results_周波数_再生成分_日時.csv` が
   自動保存される（`O` での途中停止でも保存される。練習は `O` で開始すれば保存されない）

### 実験条件を変えて繰り返す

同じ trajectory ファイルのまま、周波数・環境密度を変えて 4〜5 を繰り返す。
**同一軌跡・別条件の比較ができるのがこの設計（軌跡収録＋再レンダリング方式）の利点**。

## 四角錐ガイド（09 仕様）のセットアップ

視点位置を頂点とする四角錐（断面矩形2枚のワイヤフレーム）を、視点一致のガイドとして
提示する機能。設計の背景は [09_cone-guide-spec.md](09_cone-guide-spec.md) を参照。

**初回のみ**: 対象のシーン（`ViewpointFollowing.unity` / `ViewpointFollowingReplay.unity`）を開き、
メニュー **Tools > 視点追従実験 > 錐ガイドを現在のシーンに追加** を実行する。

このメニューは**既存シーンを作り直さず、足りない要素だけを差分で足す**（何度実行してもよい）。
実行後、シーンは未保存状態になるので内容を確認して手動保存すること。追加されるもの:

- レイヤ `ConeOther`(16) / `ConeSelf`(17) の登録と、既存カメラの Culling Mask からの除外
- `Cone_Other` / `Cone_Self`（各 `ConeGuide` + `ConePoseFilter`）
- `Cone_SelfRef`（頂点＝観測者＝自分自身の完全固定リファレンス。09 §3.5 参照。
  `Cone_Other` と同じレイヤ・カメラでライブ視野に映る。新規カメラ・RT は不要）
- 箱用 RenderTexture（`BoxOther` / `BoxSelf`。アルファ付き・深度付き）と `LiveBoxCam` / `GhostBoxCam`
- `ChannelCompositor`（`ViewSwitcher` と同じオブジェクトに追加。**既定は無効**）

主なパラメータ（既定値は 09 §3.1 / §3.2 / §3.4–3.6 のとおり）:

| 場所 | パラメータ | 既定 |
|---|---|---|
| `Cone_*` > ConeGuide | d₁ / d₂ / 開き半角 α / 断面枚数 N | 1.0m / 3.0m / 15° / 2 |
| 〃 | 線幅（**角度指定**） / 線の色 / 稜線描画 / 別色モード | 0.3° / 白 / ON / **ON**（近=シアン/遠=マゼンタ） |
| `Cone_Other` / `Cone_Self` > ConeGuide | Far At Infinity / Observer | OFF / 自動配線（09 §3.4） |
| `Cone_SelfRef` > ConeGuide | Far At Infinity / 全断面ロック / 線の色 | ON / ON / 緑（固定） |
| `Cone_*` > ConePoseFilter | ヨー / ピッチ / ロール | Raw / LowPass 0.5Hz / Zero |
| ExperimentRig > ChannelCompositor | 背景チャンネル / 箱チャンネル | 矩形波交替 / Off |
| 〃 | f_box の f_bg 同期 / 輝度変調量 Δ | ON / 0.35 |
| ExperimentRig > FollowingExperimentManager | 初期オフセット（横 / 前後 / ヨー） | 0m / 0m / 0° |

> **2つの錐のパラメータは自動同期される。** `Cone_Self > ConeGuide > Mirror From` に
> `Cone_Other` が設定されており、幾何・見た目（d₁ / d₂ / α / 断面枚数 / 線幅 / 線の色 /
> 稜線 / 別色モード）と姿勢処理の条件が `Cone_Other` から引かれる。
> **編集は `Cone_Other` 側で行うこと**（`Cone_Self` 側を変えても上書きされる）。
> 2つの錐の見えが違うと、誤差ゼロでも差が残って時分割のワブル（09 §1.4）が
> 消えなくなるため。個別に設定したい場合は `Mirror From` を None にする。
>
> **箱の見かけの大きさは開き半角 α だけで決まる**（見かけ直径 = 2α）。
> 誤差ゼロのとき観察者は錐の頂点にいるので、d₁ / d₂ を変えても見かけの大きさは変わらず、
> 前後感度 `(d₂−d₁)/(d₁·d₂)` と差動成分が変わる。線幅も角度指定なので大きさに依存しない。

> **初期オフセット**は `Align To Recording On Start`（開始地点合わせ）の**後**に適用される。
> 整合をオフにした試行では適用されない（CSV の offset* 列も 0 になる）。
> 座標系は誤差の成分分解と同じコース基準（+Z = 進行方向、+X = 進行方向に対して右）。

> **別色モードと箱の4ストロークは排他**。この組合せを選ぶと別色モードが自動的に無効化され、
> 警告ログと HUD 表示が出る（箱の4ストロークは輝度変調方式のため。奥行き手がかりは
> 稜線オクルージョンが担うので単色でも前後の多義性は解消される）。

## 再生確認シーン（ViewpointFollowingReplay.unity）

実験後に記録した視点を Game ビューで見直すためのシーン。**HMD 不要**。
メニュー **Tools > 視点追従実験 > 再生確認シーンを生成** で作成する（初回のみ）。

ライブ姿勢と収録姿勢を**2台のカメラで同時に再レンダリング**し、実験シーンと同じ
`ViewSwitcher` / `FourStrokeCompositor` で再合成する構成のため、**そのとき表示していた映像を
再現するだけでなく、収録後にパラメータを変えて表示を作り直せる**（切替周波数の変更・4ストロークの追加）。

- シーンを再生すると、データフォルダ内で**最も新しい CSV**（trajectory / following_results）を
  自動で読み込んで再生が始まる。ファイル指定は ReplayRig > ReplayPlayer > **File Name**
- `trajectory_*.csv` → 収録走の頭部視点をそのまま再生（ライブのみ。周波数変更・4ストロークは無効）
- `following_results_*.csv` → **Display Mode**（M キーで巡回）で表示を選択:

  | モード | 表示内容 |
  |---|---|
  | AsExperienced | 実験時と同じ時分割切替を再現（source列に従いライブ⇔収録を切替） |
  | LiveOnly | 被験者が実際に移動した頭部（ライブ）の視点のみ |
  | PlayedOnly | 提示された収録映像側の視点のみ |
  | **Reswitch** | **収録後にパラメータを変えて再合成**（切替周波数の変更・4ストロークの追加） |

- 画面上部のバナーで表示中の視点が分かる（**オレンジ=収録映像 / 青=ライブ**）

| 操作 | 動作 |
|---|---|
| Space | 再生 / 一時停止 |
| R | 最初から再生 |
| ← / → | 5秒 巻き戻し / 早送り |
| M | 表示モード切替（AsExperienced → LiveOnly → PlayedOnly → Reswitch） |
| ↑ / ↓ | 切替周波数 ±0.5Hz（**Reswitch** で反映） |
| 4 | 4ストローク合成 ON/OFF（**Reswitch** で反映） → [08](08_fourstroke.md) |
| V | 4ストロークの極性切替（Enhance → Reversal → Zero） |
| 1 / 2 / 3 | 環境密度切替（実験時の条件に手動で合わせる） |
| K | 四角錐ガイド ON/OFF（**Reswitch** で反映） → [09](09_cone-guide-spec.md) |
| G / C / B | 背景チャンネル / 箱チャンネル / 箱の4ストローク極性 の巡回 |

錐ガイドも **Reswitch モードでのみ**有効になる（「収録後にパラメータを変えて再合成する」
用途のため）。f_bg は `↑`/`↓` を共用し、f_box は `ChannelCompositor` の同期設定か
Inspector で指定する（`←`/`→` は既存のシーク操作のまま）。

その他: Playback Speed（0.1〜4倍速）、Loop（繰り返し再生）を Inspector で設定できる。
環境密度は CSV に記録されていないため、実験時の条件に合わせて手動で切り替えること。

> **Reswitch について**: 実験時の source 列に沿った切替を再現するのではなく、指定した周波数で
> 改めてライブ⇔収録を切り替える（4ストローク ON 時はその周波数を変調周波数として合成する）。
> 「別の切替周波数だったら／4ストロークを足したらどう見えるか」を、同じ収録データで後から検討できる。
> なお切替・合成の位相は実時間（Time.deltaTime）で進むため、一時停止中も点滅は続く
> （静止フレーム上で4ストロークの見えを確認できる。表示を固定したいときは LiveOnly / PlayedOnly にする）。

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
recPosX/Y/Z,  recRotX/Y/Z,      ← その瞬間に実際に提示された映像カメラの位置・回転
                                  （再生成分の条件による置き換え後の値）
errXZ, err3D,                    ← 追従誤差（水平面 / 3次元）
boxMode, fBox, boxPolarity,      ← 箱チャンネルの条件（錐ガイド。未使用なら Off / 0 / -）
offsetLat, offsetFwd, offsetYaw, ← 実際に適用された初期オフセット量
errLat, errFwd, errYaw           ← 誤差の成分分解（コース進行方向 +Z 基準、符号つき）
```
- source: その瞬間に表示していた映像（0 = ライブ, 1 = 収録）。錐ガイド使用時は**背景チャンネル基準**
- freq: 切替周波数[Hz]（= f_bg。ファイル名にも入る）
- errXZ が歩行追従度の主指標。時間方向のずれの解析（ラグ相関・DTW）は生の pos 列から行う
- errLat = ライブ−収録の X 成分（横）、errFwd = 同 Z 成分（前後）、
  errYaw = 収録に対するライブのヨー角差（−180°〜180°）。
  **前後多義性が効いているなら errFwd にだけ大きな誤差または符号反転が残るはず**（09 §6）
- **既存17列（time〜err3D）の順序と意味は変えていない**。新規列は末尾に追加してあるので、
  既存の解析スクリプトと再生確認シーンはそのまま動く（いずれも列名でアクセスしている）

ファイル名の条件タグ（該当する条件のときだけ付く。錐ガイド未使用なら従来と同じ名前）:

| タグ | 条件 |
|---|---|
| `_boxOther` / `_boxSelf` / `_boxBoth` | 箱チャンネル = Other固定 / Self固定 / 矩形波交替 |
| `_boxBoth4stEnhance` など | 箱チャンネル = 4ストローク（極性つき） |
| `_4stEnhance` など | 背景チャンネル = 4ストローク（極性つき。従来からの規則） |

例: `following_results_5.0Hz_PositionAndRotation_boxBoth_4stEnhance_20260813_130000.csv`

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
| `ReplayPlayer.cs` | 再生確認: 保存済みCSVからライブ/収録姿勢を2台のカメラで再レンダリングし、`ViewSwitcher`で再合成（周波数変更・4ストロークの後付け可、HMD不要） |
| `FollowingPaths.cs` | データ保存先パスの一元管理（エディタ/実機の分岐） |
| `FourStroke/FourStrokeCompositor.cs` | 4ストローク合成の共有コア（詳細は [08_fourstroke.md](08_fourstroke.md)） |
| `FourStroke/DelayedFrameBuffer.cs` | ライブ映像のリングバッファ（4ストローク歩行シーン用の過去映像） |
| `FourStroke/FourStrokeSelfManager.cs` | 4ストローク歩行シーンの進行管理 |
| `Editor/ViewpointFollowingSceneBuilder.cs` | 実験シーンの自動構築（メニュー: Tools > 視点追従実験） |
| `ConeGuide/ConeGuide.cs` | 四角錐ガイドの生成と追従（断面枠＋側稜を太さのある3Dジオメトリで描く） |
| `ConeGuide/ConeLine.shader` | 錐の線（ZWrite On / ZTest LEqual で稜線オクルージョンを成立させる） |
| `ConeGuide/ConePoseFilter.cs` | 箱の姿勢処理（ヨー/ピッチ/ロールを Raw / LowPass / Zero） |
| `ConeGuide/ChannelPhase.cs` | 1チャンネル分の位相計算（矩形波 / 4ストローク）。背景用・箱用に独立適用 |
| `ConeGuide/ChannelCompositor.cs` | 背景と箱の2チャンネル独立合成（4入力 → CenterRawImage） |
| `ConeGuide/ChannelComposite.shader` | 上記の合成シェーダ（箱は輝度変調方式で重ねる） |
| `ConeGuide/Editor/ConeGuideSceneUpgrader.cs` | 既存シーンへの錐ガイド追加（メニュー: Tools > 視点追従実験 > 錐ガイドを現在のシーンに追加） |

### 映像パイプライン（02 の構成に GhostCamera 系統を追加）

```
[ライブ系統]  CenterEyeCapture ──→ CenterEye RT ──┐
                                                  ├─(ViewSwitcherが切替)─→ CenterRawImage ─→ CenterEyeAnchor ─→ HMD
[収録系統]    GhostCamera ──────→ PlaybackEye RT ─┘
              ↑ TrajectoryPlayer が収録軌跡どおりに駆動
```

錐ガイドを有効（`K` キー）にすると、4カメラ / 4RT の2チャンネル構成に切り替わる:

```
[背景ch] CenterEyeCapture ─→ CenterEye RT ────┐
         GhostCamera ─────→ PlaybackEye RT ───┤
                                              ├─(ChannelCompositor)─→ CenterRawImage → HMD
[箱ch]   LiveBoxCam ───────→ BoxOther RT ─────┤   背景と箱を独立に選択・合成
         GhostBoxCam ──────→ BoxSelf RT ──────┘
```

- `LiveBoxCam` / `GhostBoxCam` は各背景カメラの**子**（姿勢・投影が完全一致）。
  Culling Mask はそれぞれ `ConeOther` / `ConeSelf` のみ、背景は透明クリア
- `Cone_Other`（頂点＝収録視点）はライブ映像側にのみ、
  `Cone_Self`（頂点＝ライブ頭部）は収録映像側にのみ映る
- `Cone_SelfRef`（頂点＝観測者＝自分自身，完全固定の的）は `Cone_Other` と同じ
  `ConeOther` レイヤなのでライブ映像側に一緒に映る（09 §3.5）
- 箱は下地に対する**輝度変調**として重なる（`final = BG + boxMask × sign × Δ`）
- **錐ガイドがオフの間は `ChannelCompositor` が無効化され、上の従来経路がそのまま動く**

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

- **座標系**: 軌跡はワールド座標で記録・再生する。Follow 開始時に
  `AlignToRecordingStart()` が OVRCameraRig を回転・平行移動して、現在の頭部の
  水平位置（XZ）とヨーを収録開始時点に一致させる（高さは被験者自身の目の高さを維持）。
  このため厳密な視点リセットのプロトコルは不要（機能をオフにした場合は従来どおり
  収録時と同じ立ち位置・向きでの視点リセットが前提になる）。
- **時計**: 記録は `FixedUpdate`（50Hz）、再生・切替は `Time.deltaTime`。
  いずれも `Time.timeScale = 0`（停止中）で自動的に止まる。
  停止に影響されたくない処理を追加する場合のみ `unscaledTime` を使うこと。
- **回転の保存は四元数**。オイラー角の補間は 350°→10° などで破綻するため、
  再生用データとしては使わない（CSV のオイラー列は解析用の参考値）。
- **参照は Inspector 配線**（`[SerializeField]`/public）。既存コードの
  `GameObject.Find` 依存（02参照）をこのフォルダには持ち込まない。
- **CSV の小数点は InvariantCulture**（`.` 固定）で読み書きする。

## トラブルシューティング

### Follow中、映像が切り替わっていない／固まっているように見える

まず **ViewSwitcher の `Debug Tint`**（初期値オン）を確認する。オンのとき**収録映像の表示中は
視野がオレンジ色に着色**されるので、切替が起きているか・どちらのソースが表示中かが一目でわかる
（エディタのGameビュー左上のHUDにも「表示中: ライブ/収録」「再生時刻」「ライブ頭部/収録位置」が出る）。
**本番実験では必ずオフにすること。**

ありがちな原因:

- **その場に立ったまま収録した軌跡を、同じ場所に立ったまま再生している**
  → ライブ映像と収録映像がほぼ同じ絵になり、切り替わっていても見分けがつかない。
  収録走では実際にコースを歩き、Follow走の動作確認では収録時と違う場所に立つ／違う方向を向くと分かりやすい。
- 収録映像は**再生専用**なので、収録映像の表示中に頭を動かしても映像は変わらない（これは仕様）。
  切替周波数が低い（例: 0.1Hz = 各5秒表示）と「固まっている」ように感じやすい。
- モードの切替は `M` キー / A ボタンを推奨（Inspector から直接変えても追随するが、実行中の変更は挙動が保証されない）。

### Oculusに接続できない（Unable to start Oculus XR Plugin）

ヘッドセット側で Quest Link を起動して**Link空間に入った状態**で Unity の Play を押す。
それでも失敗する場合は Unity と Unity Hub を終了 → Link 接続 → Unity 再起動。

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
