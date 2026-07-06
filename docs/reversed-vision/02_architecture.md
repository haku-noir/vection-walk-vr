# 02. アーキテクチャ — VR映像パイプラインと Player プレハブ

このプロジェクトの心臓部は「**カメラ映像を一度 RenderTexture → UI(RawImage) に落としてから
HMD に見せる**」という間接的な映像パイプラインである。
視野反転はこの UI を変形（スケール反転）することで実現しており、
**今後の視点追従実験（映像ソースの切替）もこのパイプラインをそのまま流用できる**。

## 映像パイプラインの全体像

```
[VR空間の世界]
     │ 撮影
     ▼
CenterEyeCapture (Camera)          ← HMDと同期して動く撮影用カメラ
     │ Target Texture に出力          Culling Mask: UI以外 / Target Eye: None
     ▼
CenterEye (RenderTexture)          ← Assets/Textures/CenterEye.renderTexture
     │ テクスチャとして貼付
     ▼
CenterRawImage (RawImage)          ← CenterCanvas 上のUI。
     │                                ここの localScale を -1 倍すると視野が反転する
     ▼
CenterEyeAnchor (Camera)           ← UIだけを撮影するカメラ（Culling Mask: UIのみ）
     │                                HMDのトラッキングに完全同期
     ▼
[HMD のディスプレイ]
```

つまり「カメラ1 → UI →（変形）→ カメラ2 → ディスプレイ」の2段構成。
カメラ映像を直接反転させる方法は上手くいかなかったため、この方式が採られている（前任者コメントより）。

### この構成の利点（視点追従実験にとって）

- `CenterRawImage` の **texture を差し替えるだけで HMD に映る映像ソースを切替できる**。
  - 現在: `CenterEye.renderTexture`（ライブ映像）
  - 追加例: 事前収録映像（VideoPlayer → RenderTexture）や、記録軌跡を再生する別カメラの出力
- 2枚の RawImage を重ねて透明度で合成する仕組みも既にある（`SetBlendingRatio.cs` + `Player_Blending.prefab`）。

## Player プレハブ（Assets/Prefabs/Player.prefab）

実験の要となるプレハブ。`OVRPlayerController` プレハブをベースに改造されている。
（Character Controller と OVRPlayerController スクリプトは余計な動作をするため**非アクティブ**にしてある）

### ヒエラルキー構造

```
Player
└── OVRCameraRig
    └── TrackingSpace
        ├── LeftEyeAnchor
        ├── CenterEyeAnchor          ← HMDトラッキング同期（位置・回転はHMD依存、Transform手動設定は無意味）
        │   ├── CenterEyeCapture     ← 世界を撮影するカメラ（Target Texture = CenterEye）
        │   └── CenterCanvas
        │       └── CenterRawImage   ← 視野となるUI（これを変形して反転）
        ├── RightEyeAnchor
        ├── (LeftCanvas/LeftRawImage, RightCanvas/RightRawImage)  ← 両眼視差対応の名残（未使用）
        ├── TrackerAnchor
        ├── LeftHandAnchor
        │   ├── LeftControllerAnchor
        │   └── OculusTouchForQuestAndRiftS_Left   ← コントローラの見た目
        ├── RightHandAnchor
        ├── LeftHandAnchorDetached
        └── RightHandAnchorDetached
```

- **CenterEyeAnchor**: HMD の Position/Rotation が毎フレーム反映される。HMD の動き＝このオブジェクトの動き。
- **LeftHandAnchor / RightHandAnchor**: コントローラのトラッキングが反映される。
- **OVRCameraRig の初期ローカル座標 (0, 1.5, 0.15)**: Player の足元位置に対する HMD のおおよその相対位置
  （目の高さ1.5m・前方0.15m）。

### 両眼視差について

`OVRCameraRig.usePerEyeCameras` を True にすると左右眼別カメラになるが、
**現状は両眼視差非対応**（False 前提）。`SetReversion` などは "center_" 系のオブジェクトのみを使う。
Left/Right 系のオブジェクト・変数は拡張用の名残で、動作未確認。

## 見落としがちな重要設定（引継ぎ資料より）

| 設定 | 対象 | 値 | 理由 |
|---|---|---|---|
| Culling Mask | CenterEyeCapture | **UI を外す** | 自分が映したUIを再撮影しないため |
| Culling Mask | CenterEyeAnchor | **UI のみ** | 世界が二重に見えないため |
| Target Eye | CenterEyeCapture | **None** | HMDに直接出力しないため |
| Target Eye | CenterEyeAnchor | Both | HMDへの出力担当 |
| Use Per Eye Cameras | OVRCameraRig | False | 両眼視差なし（非対応のため） |
| Tracking Origin Type | OVRManager | Eye Level | プレハブを置いた位置基準の映像 |

## OVRCameraRig / OVRManager の主要設定

- **OVRCameraRig.cs**
  - `Use Per Eye Cameras`: 両眼視差の有無。False で両眼同一映像。
  - `Disable Eye Anchor Cameras`: カメラ無効化。トラッキングだけしたい時に True。
- **OVRManager.cs**
  - `Target Devices`: 使用する HMD にチェックが入っているか確認。
  - `Tracking Origin Type`: Eye Level ならプレハブ位置からの映像。

## PostProcessVolume による視野の色付け

実験の一時停止中（`Time.timeScale = 0`）は `PostProcessVolume` をアクティブにして
視野全体に色を付け（真っ黒含む）、被験者に停止中であることを示す。
多くの実験スクリプトが `GameObject.Find("PostProcessVolume")` で取得するため、
**シーンに同名オブジェクトが必要**。

## ハードコードされたオブジェクト名一覧

スクリプトが `GameObject.Find` で検索する名前。ヒエラルキーで**改名禁止**:

| 名前 | 検索するスクリプト |
|---|---|
| `OVRCameraRig` | SetReversion, SetVelRev, MenuForReversion |
| `CenterEyeAnchor` | SetVelRev |
| `CenterEyeCapture` / `LeftEyeCapture` / `RightEyeCapture` | SetReversion, SetVelRev |
| `CenterRawImage` / `LeftRawImage` / `RightRawImage` | SetReversion, SetVelRev |
| `PostProcessVolume` | Walking, WalkingCourse, SphereGenerator, SphereLauncher, ControllerTouchSphere ほか |
| `standard` | SphereGenerator, ControllerTouchSphere（開始前の視点合わせ指標） |
| `Laser` | CrosshairAlignment |
| `Cylinder` | CheckpointManager |
| タグ `GameSystem` / `CheckPoint` / `Barrier` | ControllerTouchSphere, CylinderInteraction ほか |

## RenderTexture 一覧（Assets/Textures/）

| ファイル | 用途 |
|---|---|
| `CenterEye.renderTexture` | メイン視野（CenterEyeCapture の出力先）。**サイズはHMD接続時のカメラのアスペクト比に合わせる** |
| `LeftEye.renderTexture` / `RightEye.renderTexture` | 両眼視差用（未使用） |
| `CenterEye 1.renderTexture`, `capture.renderTexture`, `mirror.renderTexture`, `Eye anchor.renderTexture` | 実験・テスト用の複製 |

## 自作シェーダ（Assets/Shaders/）

- `CorrectARCameraShader.shader` — パススルー(AR)カメラ映像の補正試行（Test or Failed 系）
- `NewUnlitShader.shader` — 汎用 Unlit
