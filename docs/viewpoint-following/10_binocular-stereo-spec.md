# 四角錐ガイドの両眼立体視化 — 設計仕様 v1

> 議論日: 2026-09-07 / 対象実装: `ViewpointFollowing.unity`（07・08・09 の構成を拡張）
> 対象ブランチ: `feature/binocular-stereo`（`feature/guide-4stroke-delayed-self` から分岐）
> 本書は合意済み事項・調査結果・実装アーキテクチャ案・未決事項をまとめた作業用仕様。

---

## 1. 背景・目的

09仕様（四角錐ガイド）の近断面・遠断面は、これまで**単眼**（`CenterEyeAnchor`／`CenterEyeCapture`の1点だけ）でレンダリングされ、その1枚の平面画像をそのまま両目に同一表示していた（`ViewpointFollowingSceneBuilder.cs`が`OVRCameraRig.usePerEyeCameras = false`に設定）。09仕様 §1.6 にも明記の通り、これは意図的な制限だった:

> 両眼視差は非対応（07 既知の制限）のため視差では解けない。

この制限のもとで、前後多義性は稜線オクルージョン（形状による単眼手がかり）で解消していた。

しかし実際のHMD使用を踏まえて検討した結果、**本来のVR環境が持つ両眼立体視（binocular stereopsis）を使わないのは大きな機会損失**であるとの結論に至った。近断面（d₁）と遠断面（d₂）という異なる奥行きに矩形を配置しているにもかかわらず、両眼視差という最も自然な奥行き手がかりを使っていなかったためである。本仕様は、視点追従実験の映像パイプライン全体（背景のパススルー映像＋四角錐ガイド）を**真の両眼立体視**に作り変えるための設計をまとめる。

### 1.1 単眼実装の限界（今回の検討で明らかになった点）

- 頂点（相手の記録視点）と自分の頭部位置が一致しても、単眼実装での「近断面・遠断面が重なる」は**1枚の2D画像内での投影上の一致**に過ぎず、両眼立体視のもとでの「単一の奥行きとして知覚される」こととは異なる
- Far At Infinityモードの遠断面も、実体は「自分の頭の前方d₂mに追従する2D平面」であり、奥行きの実体験を伴わない
- 2断面を用意した本来の理由（09 §1.3）は差動成分・スケール成分という**単眼幾何学**上の手がかりであり、両眼視差の活用が目的ではなかった

---

## 2. 決定事項（合意済み）

### 2.1 スコープ: 背景・箱の両方を両眼化する

背景（環境のパススルー映像）と四角錐ガイド（箱）の両方を両眼化する。背景だけ単眼のまま箱だけ両眼化すると、「背景は視差ゼロ・箱だけ視差あり」という不自然な知覚になり、両眼視差の効果を正しく検証できないと判断したため。

### 2.2 実装方式: 既存のカメラ構成を左右に複製する

Unity/OVRのSingle Pass Instanced等ネイティブなステレオパイプラインへの本格移行ではなく、**現在の「専用カメラ→RenderTexture→ChannelCompositorでBlit合成」という設計をそのまま維持し、カメラ・RT・DelayedFrameBuffer・ChannelCompositorを左目用／右目用の2セットに複製する**方式を採る。既存14コミット分の構造を壊さずに段階的に進められることを優先した。GPU負荷はほぼ倍増するため、実装後に計測し必要なら最適化する。

### 2.3 Far At Infinity: 左右別々に維持する

観測者基準の再アンカリング（`observer.position`/`observer.rotation`）を、左目・右目それぞれの実カメラを`observer`として個別に計算する形で維持する。「遠断面だけで誤差を伝える」という09 §3.4の実験条件を両眼環境でも継続比較できるようにするため。

### 2.4 稜線オクルージョン: 維持する

両眼視差だけで前後多義性が十分に解消されるかは実験で確かめるべき仮設なので、ON/OFFを選べる現行の`drawRidges`をそのまま残す。単眼手がかり有無を比較条件として使える。

### 2.5 FarAnchorトリックは既定条件で不要になる（設計の簡略化）

現行の`ConeGuide.cs`の`FarAnchor`/`Corners()`は、単眼の1枚絵の中で遠断面を観測者基準に再配置し「画面固定」に見せかけるためのCPU側の座標トリックだった。左右別カメラで本物の透視投影を行えば、Unity標準のカメラ変換が各目の実位置から自動的に正しい視差を生成するため、

- **Far At Infinity = OFF の断面（既定の近断面・遠断面）** は、通常のワールド座標に置いた3Dオブジェクトとして扱ってよく、再アンカリング計算は不要になる
- **Far At Infinity = ON の遠断面**（2.3で維持を決定）だけは、引き続き観測者（＝各目のカメラ）基準の再アンカリングが必要

### 2.6 既存の休眠インフラを再利用する

`Assets/Prefabs/Player.prefab`に以下がすでに存在することを確認した（`ReversedVision`実験用に作られ、視点追従実験では無効化されていたもの）:

| 既存オブジェクト | 役割 |
|---|---|
| `LeftEyeCapture` / `RightEyeCapture` | 各目の位置に追従するカメラ（`m_TargetTexture`が`LeftEye.renderTexture`/`RightEye.renderTexture`） |
| `LeftEye.renderTexture` / `RightEye.renderTexture` | 各目用の出力先RenderTexture |
| `LeftRawImage` / `RightRawImage` | 各目の映像を表示するRawImage |
| `LeftCanvas` / `RightCanvas` | `RenderMode: Screen Space - Camera`。描画カメラにその目自身のカメラを指定することで、その目にしか見えないようにしている |

この仕組みは`SetReversion.cs`（`ReversedVision`）が`OVRCameraRig.usePerEyeCameras`のON/OFFに応じて`left_image`/`right_image`の表示を切り替える形で使っている。**視点追従実験でもこの既存インフラをそのまま再利用し、表示内容だけ新しい両眼版ChannelCompositorの出力に差し替える方針**とする。「各目に別々の画をどう届けるか」というプラットフォーム的な配管部分を新規に設計する必要はない。

ただし**制御コード（`SetReversion.cs`/`SetVelRev.cs`）自体は流用しない**。これらは上下左右反転・ミラーハンドなど`ReversedVision`固有の機能と密結合しており、視点追従実験に必要なのは`LeftEyeCapture`/`RightEyeCapture`/`LeftCanvas`/`RightCanvas`/`LeftRawImage`/`RightRawImage`という**GameObject自体**だけである。配線は`ConeGuideSceneUpgrader.cs`側に新規メソッドとして実装し、`ReversedVision`側のスクリプトには一切依存しない（実験モジュール間の結合を避けるため）。

なお`LeftEyeCapture`のローカル位置`(0, 0, -0.05)`について調査したところ、親（`LeftEyeCapture`の`m_Father`）はネストされた`OVRCameraRig`プレハブ（guid `ce816f2e6abb0504092c23ed9b970dfd`）内の`stripped`参照であることを確認した。すなわち**実際のIPD分離はOVR標準の`LeftEyeAnchor`/`RightEyeAnchor`がランタイムに実機IPDへ自動追従する**ことで実現されており、`(0, 0, -0.05)`はその上に乗る小さな追加オフセット（レンズ面への食い込み回避などと推測されるが、視点追従実験にとっては無視してよい値）である。IPD計算を自前で行う必要はない。

### 2.7 提示条件（モード・極性・周波数）の左右共有: mirrorFromパターンを拡張する

左目用・右目用の2つの`ChannelCompositor`のうち一方を「マスター」とし、もう一方が毎フレーム値を**引く**形で同期する。`ConeGuide.mirrorFrom`（09仕様、acc009ff）と同じ設計判断——「押し出す」ではなく「引く」ことで2つのコンポーネントの更新順に依存せず同じフレームで値が揃う——をそのまま`ChannelCompositor`にも適用する。新しい共有コンポーネントは作らない。同期対象は背景・箱・ガイドそれぞれのモード／極性／周波数／輝度変調量／Enabledフラグ／遅延フレーム数（09 M9〜M11で追加したもの含む）。入力テクスチャ・カメラ・出力先RawImageなど目ごとに固有の参照は同期しない。

### 2.8 再生確認シーンも両眼化する

`ReplayPlayer`（再生確認シーン）も実験シーンと同様に両眼化する。記録済み軌跡（単眼6DOFのCSV）を読み込んで再生する点は変わらないが、その軌跡を**両眼で再レンダリングし直す**ことで、収録後にパラメータを変えて両眼提示条件を作り直せるようにする（既存のReswitchモードと同じ考え方）。単眼のときのAsExperienced/LiveOnly/PlayedOnlyモードは、両眼レンダリングでも同じ軌跡を両目に映すだけなので変更不要。Near/Far/Ridge一式の複製カメラを再生確認シーンにも用意する必要があるため、実装量は実験シーンとほぼ同等になる見込み。

### 2.9 命名規則

既存の`LeftEyeCapture`/`RightEyeCapture`/`LeftRawImage`/`RightRawImage`/`LeftCanvas`/`RightCanvas`（`Left`/`Right`を**前置**する命名）に合わせ、両眼化に伴い新設するオブジェクトもすべて`Left`/`Right`前置に統一する。`_L`/`_R`のような接尾辞は使わない（プロジェクト内の既存命名との一貫性を優先）。

| 種別 | 単眼（既存） | 両眼化後 |
|---|---|---|
| 箱チャンネル用カメラ | `LiveBoxCam` | `LeftLiveBoxCam` / `RightLiveBoxCam` |
| 　〃　（Self側） | `GhostBoxCam` | `LeftGhostBoxCam` / `RightGhostBoxCam` |
| ガイド用カメラ | `LiveBoxNearCam` 等 | `LeftLiveBoxNearCam` / `RightLiveBoxNearCam` 等 |
| RenderTexture | `BoxOther.renderTexture` 等 | `LeftBoxOther.renderTexture` / `RightBoxOther.renderTexture` 等 |
| Far At Infinity専用レイヤ | `ConeOtherFar` | `ConeOtherFarLeft` / `ConeOtherFarRight`（3.3参照。farAtInfinity ON時のみ生成） |
| `ChannelCompositor`のGameObject | （`ViewSwitcher`と同居） | `LeftRawImage`/`RightRawImage`と同じ階層にそれぞれ1つずつ配置する（マスター側は`LeftRawImage`側に置き、`mirrorFrom`で右目側が参照する） |

---

## 3. 実装アーキテクチャ（案）

### 3.1 レイヤ・カメラ・RTの複製方針

現行（単眼）は`Cone_Other`が近断面／遠断面／稜線の3レイヤ（`ConeOther`/`ConeOtherFar`/`ConeOtherRidge`）に分離され、下記が`CenterEyeCapture`配下にぶら下がっている:

- 箱チャンネル用: `LiveBoxCam`（3レイヤ全部）, `GhostBoxCam`（`ConeSelf`）
- ガイドチャンネル用: `LiveBoxNearCam` / `LiveBoxFarCam` / `LiveBoxRidgeCam`（各レイヤ単体）
- 遅延用: `DelayedFrameBuffer` ×3（近・遠・稜線）

これを左目・右目それぞれについて複製する（`LeftLiveBoxCam`/`RightLiveBoxCam`のように命名。命名規則は2.9参照）。レイヤ自体（`ConeOther`等）は左右で共有してよい——同じ`Cone_Other`ジオメトリを異なる位置のカメラ2台で撮るだけなので、新しいレイヤは不要（Far At Infinity用の`ConeOtherFarLeft`/`ConeOtherFarRight`のみ例外。3.3参照）。

### 3.2 ChannelCompositorの複製

`ChannelCompositor`を左目用・右目用の2インスタンスにする。背景・箱・ガイドそれぞれの入力テクスチャ（`bgLiveTexture`等）を対応する目のカメラ出力に差し替え、出力先`rawImage`をそれぞれ`LeftRawImage`/`RightRawImage`にする。提示条件（モード・極性・周波数等）の左右共有は2.7の`mirrorFrom`拡張で行う。

### 3.3 ConeGuideの扱い — 既存Splitパターンの拡張

`Cone_Other`/`Cone_Self`のジオメトリ自体（Transform）は左右で共有する1つのワールド座標オブジェクトのままでよい（2.5の通り、既定条件ではもうカメラごとの再アンカリングが不要なため）。

`ConeGuide.cs`は既に`Split`プロパティと`MeshBuffer`（09 M9・M10）によって、近断面・遠断面・稜線を独立した子オブジェクト・レイヤへ振り分ける仕組みを持つ。これを次のように拡張する:

- 近断面・稜線用の`MeshBuffer`（`near`/`ridge`）は**引き続き1つのまま**（Far At Infinityの有無に関わらず左右で共有できるため。2.5参照）
- 遠断面用の`MeshBuffer`は`farAtInfinity`が**OFF**のときは従来どおり1つ（`far`）のまま、**ON**のときだけ`farLeft`/`farRight`の2つに分岐させ、`ComputeFarAnchor()`をそれぞれ`observerLeft`/`observerRight`（左目・右目の実カメラ）で個別に計算する
- 遠断面の子オブジェクト・レイヤも同様に、`farAtInfinity`がONのときだけ2レイヤ・2子オブジェクトに分かれ、OFFのときは既存の単一レイヤのまま（`EnsureSplitChild`と同じ遅延生成パターンを流用）

`ConeGuide`コンポーネント自体は`Cone_Other`1つのまま——「どちらのCone_Otherを編集すればよいか分からない」という混乱を避けられる。ユーザーから見た変化は、Far At Infinityを有効にした瞬間だけ内部的にレイヤ・カメラが1本増える、という程度になる。

#### 3.3.1 実装時に判明した補正（当初案からの変更点）

上記の当初案には2つの見落としがあり、`ConeGuide.cs`の実装時に修正した:

1. **稜線の遠い側の端も`farAnchor`を使っていた**。Far At Infinity中の遠断面は左目・右目で別々の場所に「浮遊」する実体のない基準枠になるため、稜線をそこへ接続しようとすると稜線自体も左右で別々のジオメトリが必要になり、稜線用レイヤまで`RidgeLeft`/`RidgeRight`に分岐する事態になる。これを避けるため、**稜線の遠い側の端は`farAtInfinity`の値に関わらず常に頂点基準（`FarAnchor.Identity`）に固定する**よう変更した。副作用として、Far At Infinity有効時は稜線の終端と実際に見える遠断面の位置がわずかに食い違う（稜線は「無限遠モードが無かったときの遠断面位置」を指す）が、両者を無理に接続する複雑さを避ける方を優先した
2. **`farAtInfinity`はCone_Selfにも同期される共有パラメータ**（`mirrorFrom`経由）であるにもかかわらず、当初案は`Split`（`kind==Other`のときだけ真）を左右分離のトリガーにしていた。これだとCone_OtherでFar At Infinityを有効にした瞬間、`mirrorFrom`で追随するCone_Self側の遠断面が左右分離されないまま（単一メッシュのまま）両目のカメラに同じ内容で描画され、二重像の原因になる。実際には**遠断面の左右分離は`farAtInfinity`単独をトリガーにし、`kind`（Other/Self）とは独立**させる必要があると判明した。`ConeGuideLayers.cs`に`ConeSelfFarLeft`/`ConeSelfFarRight`レイヤを追加し、`ConeOtherFarLeft`/`ConeOtherFarRight`と対になる形にした（レイヤ番号 22/23）

この結果、Far At Infinityが有効な間に新設されるレイヤは`ConeOtherFarLeft`/`ConeOtherFarRight`/`ConeSelfFarLeft`/`ConeSelfFarRight`の4つ（既存の`ConeOtherFar`/`ConeOtherRidge`と合わせ、Far At Infinity対応だけで6レイヤを消費する）。プロジェクト全体のレイヤ予算（8〜31番の24枠）を圧迫するため、実装が進んだ段階で使用状況を確認すること。

**実機確認結果**: 上記1の設計どおり、Far At Infinity有効時は稜線の終端と実際の遠断面（左右に分離して浮遊する箱）が視覚的に接続されない見た目になることを実機で確認した。ユーザーはこれを不具合として調査を依頼したが、原因（意図的なトレードオフであること）を説明した上で、**現状の仕様（稜線は常に本来の遠断面位置＝頂点基準を指す）を受け入れる**という判断を得た。稜線を左右分離まで追従させる追加実装は行わない。

### 3.4 出力経路

```
[背景L] LeftEyeCapture ──────→ LeftBg RT ──────┐
[箱L]   LeftLiveBoxCam ──────→ LeftBoxOther RT ┤
[ガイドL] ...                                  ├─(ChannelCompositor, マスター)─→ LeftRawImage → LeftCanvas → 左目
                                                ┘
[背景R] RightEyeCapture ─────→ RightBg RT ─────┐
[箱R]   RightLiveBoxCam ─────→ RightBoxOther RT┤
[ガイドR] ...                                  ├─(ChannelCompositor, mirrorFrom左)─→ RightRawImage → RightCanvas → 右目
                                                ┘
```

`LeftCanvas`/`RightCanvas`は既存インフラ（2.6）をそのまま使い、描画カメラの指定によって各目にしか映らない構造を継続利用する。

---

## 4. 影響範囲の見立て

| 対象 | 変更要否 |
|---|---|
| `ConeGuide.cs` | Far At InfinityがONのときだけ遠断面をfarLeft/farRightに分岐させる処理を追加（3.3・3.3.1）。既定条件のジオメトリ計算は簡略化できる（2.5）。稜線の遠い側の端は常に頂点基準に固定（3.3.1） |
| `ConeGuideLayers.cs` | Far At Infinity用にConeOtherFarLeft/Right・ConeSelfFarLeft/Rightの4レイヤを追加（既定のConeOtherFarは維持。3.3.1） |
| `ChannelCompositor.cs` / `ChannelComposite.shader` | シェーダは変更なし。`ChannelCompositor.cs`に`mirrorFrom`同期（2.7）を追加した上で2インスタンス化 |
| `ConeGuideSceneUpgrader.cs` | 大幅拡張。カメラ・RT・DelayedFrameBufferの生成ロジックを左右分に倍化し、`LeftEyeCapture`/`RightEyeCapture`配下への配線を追加。`ReversedVision`のスクリプトには依存させない（2.6） |
| `ViewpointFollowingSceneBuilder.cs` | `usePerEyeCameras = false`の解除、`LeftCanvas`/`RightCanvas`の有効化ロジックに変更 |
| `FollowingLogger.cs` / `TrajectoryRecorder.cs` | 記録対象は頭部6DOF（`CenterEyeAnchor`）のままで変更不要（両眼化しても記録形式は変わらない） |
| `ReplayPlayer.cs` / 再生確認シーン | **両眼化した**（2.8・6.6）。`ConeGuideSceneUpgrader.cs`の汎用化により、`LiveReplayCamera`から左目用・右目用の子カメラを新設する形でNear/Far/Ridge一式の複製カメラを配線済み |

---

## 5. 未決事項

主要な設計判断（実装方式・スコープ・Far At Infinity・稜線オクルージョン・メッシュ分離方式・提示条件の左右共有・命名規則・再生確認シーンの扱い）は§2で決着した。残るのは実装しながら詰める細部のみ:

1. **パフォーマンス目標**。レンダリング負荷がほぼ倍になる（2.2）。上限フレームレートを事前に数値で決めるのではなく、まず実装して実機で計測し、コマ落ちが問題になった場合にRT解像度・断面数・箱チャンネルの同時使用数などを調整する方針とする（数値目標は計測後に設定）
2. **`LeftEyeCapture`/`RightEyeCapture`のローカルオフセット`(0, 0, -0.05)`の正確な用途**。IPDとは無関係と判明した（2.6）が、具体的に何のためのオフセットかは未確認。実装時に実機で確認し、視点追従実験に転用して問題ないか判断する

---

## 6. 実装状況

§2の決定事項に沿って、`ConeGuideLayers.cs`（Far At Infinity用レイヤ追加）→ `ConeGuide.cs`（遠断面のfarLeft/farRight分岐）→ `ChannelCompositor.cs`（`mirrorFrom`拡張）→ `ConeGuideSceneUpgrader.cs`（左右カメラ・RT・DelayedFrameBuffer・ChannelCompositorの配線）→ `ViewpointFollowingSceneBuilder.cs`（`usePerEyeCameras`解除・Canvas有効化）の順で進めている。

| 段階 | 内容 | 状況 |
|---|---|---|
| 1 | `ConeGuideLayers.cs`: Far At Infinity用の左目/右目別レイヤ（ConeOtherFarLeft/Right）を追加 | 完了 |
| 2 | `ConeGuide.cs`: 遠断面のFar At Infinityを左目/右目別メッシュに分岐（ConeSelfFarLeft/Right含む） | 完了 |
| 3 | `ChannelCompositor.cs`: `mirrorFrom`パターンで提示条件を左右共有 | 完了 |
| 4 | `ConeGuideSceneUpgrader.cs`: 左右カメラ・RT・DelayedFrameBuffer・ChannelCompositorの配線 | 完了（ViewpointFollowing.unity・ViewpointFollowingReplay.unity両対応。§6.6） |
| 5 | `ViewpointFollowingSceneBuilder.cs`: `usePerEyeCameras`解除・Canvas有効化 | 完了 |

当初計画していた5段階はすべて完了した。段階4・5の実装過程で新たに判明した残作業は
§6.1・§6.2にまとめる。

### 6.1 段階4（ConeGuideSceneUpgrader.cs）実装時に判明した追加事項

当初の§3の想定より実装範囲が広がった。判明した順に記録する:

1. **GhostCamera は実機IPDを持たない**。`Cone_Self`・背景の収録視点側（GhostCamera）は
   `OVRCameraRig` の一部ではなく，`LeftEyeAnchor`/`RightEyeAnchor`のような実機IPD追従が無い。
   新規スクリプト`GhostEyeOffset.cs`を追加し，ライブ側の実測IPD（`LeftEyeAnchor`/`RightEyeAnchor`
   間の距離）を毎フレーム収録視点側の左目用・右目用カメラ（`LeftGhostCamera`/`RightGhostCamera`，
   これも新規）に反映するようにした。HMD未接続時は成人平均IPD（63mm）にフォールバックする
2. **背景（収録視点）も左右別カメラでの環境再レンダリングが必要**。従来`GhostCamera`が
   直接`PlaybackEye.renderTexture`へ描画していたが，`LeftGhostCamera`/`RightGhostCamera`が
   新たに`LeftPlaybackEye.renderTexture`/`RightPlaybackEye.renderTexture`へ環境を再レンダリング
   する（`GhostCamera`自身は変更せず残置。他機能が参照している可能性への配慮）
3. **箱チャンネル・ガイドチャンネルの遠断面入力の Culling Mask は，Far At Infinity の
   有効/無効どちらでも動くよう「無効時の共通レイヤ」と「有効時のその目専用レイヤ」の
   両方を含める**必要がある（同時に中身を持つのは常にどちらか一方だけなので安全）
4. **再生確認シーン（ViewpointFollowingReplay.unity）は当初スコープ外としていた**。`OVRCameraRig`
   を持たないため，`LeftEyeCapture`等の休眠インフラが存在しない。§2.8の決定自体は
   変更しないが，具体的な実装は別途行う必要があると判明した（当時はこのシーンで
   メニューを実行するとエラーダイアログを出して中断していた）。
   → **解決済み**（§6.6）。`ConeGuideSceneUpgrader.cs`を汎用化し，両シーンに対応した
5. **錐ガイドのON/OFF（Kキー等，実験制御）を両目のChannelCompositorへ反映する対応**。
   `ChannelCompositor.mirrorFrom`は提示条件（パラメータ）の同期のみを行い，`enabled`
   （コンポーネント自体の有効/無効）は同期できない（無効化されたコンポーネントは
   `LateUpdate`が呼ばれず，同期元を「引く」動作自体が起きないため）。
   → **解決済み**（§6.3）。`FollowingExperimentManager.cs`/`ReplayPlayer.cs`に
   `channelCompositorRight`フィールドを追加し，ON/OFF切替・ResetPhaseを両目に
   明示的に反映するようにした

### 6.3 実験制御を左右両方のChannelCompositorへ反映する対応（追加コミット）

§6.1-5で判明した課題を解決した。`ChannelCompositor.mirrorFrom`が同期するのは
モード・極性・周波数等の**パラメータ**だけで，コンポーネント自体の`enabled`は
（無効化されている間は`LateUpdate`が呼ばれず「引く」動作自体が起きないため）
同期できない。この`enabled`の切替と，試行開始時の明示的な`ResetPhase()`呼び出しは，
呼び出し元（`FollowingExperimentManager`/`ReplayPlayer`）が左右両方へ直接反映する
形にした:

- `FollowingExperimentManager.cs` / `ReplayPlayer.cs`: `channelCompositorRight`
  フィールドを追加（未設定なら左目用のみ切り替える後方互換）。K キーでのON/OFF
  切替（`Update()`/`ApplyConeGuide()`）と，試行開始時の`ResetPhase()`
  （`FollowingExperimentManager.StartTrial()`のみ。`ReplayPlayer`は`OnEnable`で
  自動リセットされるため不要）を左右両方に反映する
- `ConeGuideSceneUpgrader.cs`: `WireExperimentControl()`が右目用
  ChannelCompositorも受け取り，`channelCompositorRight`へ配線するよう変更
- `FollowingLogger`は状態を読み取るだけ（`enabled`を書き換えない）なので
  左目用（マスター）のみの配線のままで良い（変更なし）

### 6.2 段階5（ViewpointFollowingSceneBuilder.cs）実装時に判明した追加事項

1. **`SetupPlayerPipeline` は4ストローク歩行シーンとも共用の関数だった**。当初「単純に
   `usePerEyeCameras`を解除するだけ」と見立てていたが，この関数は`BuildScene()`
   （視点追従実験）と`BuildFourStrokeScene()`（4ストローク歩行シーン，本仕様のスコープ外）
   の両方から呼ばれる共用ヘルパーだった。無条件に両眼化すると4ストローク歩行シーンの
   単眼パイプラインを壊してしまうため，`bool stereo`引数を追加し，視点追従実験側だけ
   `true`を渡すように変更した
2. **CenterCanvasを無効化すべきかは未検証**。`usePerEyeCameras=true`にした際，単眼時代の
   `CenterCanvas`（`CenterEyeAnchor`のカメラで描画）とLeftCanvas/RightCanvasが同時に
   有効なままだと二重表示が起きる可能性を考慮し，`CenterCanvas`を明示的に無効化する
   処理を追加した。ただし実機でこの組合せを検証したわけではないため，実際に問題が
   出るかどうか・無効化で十分かは初回のHMD確認時に要チェック
3. **再生確認シーン（ViewpointFollowingReplay.unity）はこの関数を使っていない**ことを
   確認した（`BuildReplayScene()`は別経路で「通常カメラ・HMD不要」の構成を組んでおり，
   `SetupPlayerPipeline`を呼ばない）。6.1-4の「再生確認シーンは今回のスコープ外」という
   判断と整合しており，今回の変更で影響を受けない

### 6.4 バグ修正: 既存シーンでは Game 画面に何も表示されない

段階4・5のコミット後，実際に`ViewpointFollowing.unity`へ「Tools > 視点追従実験 >
錐ガイドを現在のシーンに追加」を実行してもらったところ，**Sceneビューには錐が
存在するのにGameビューには何も表示されない**という報告があった。

**原因**: `usePerEyeCameras`の有効化と`LeftCanvas`/`RightCanvas`の有効化・
`CenterCanvas`の無効化（§3.3.1・§6.2）は，段階5で`ViewpointFollowingSceneBuilder.cs`
（シーンをゼロから構築する専用ツール）にしか実装しておらず，`ConeGuideSceneUpgrader.cs`
（既存シーンに差分を足す，実際にユーザーが使うツール）には実装していなかった。
ユーザーの既存シーンは以前（本仕様着手前）に構築されたものなので，
`usePerEyeCameras`はOFF・`LeftCanvas`/`RightCanvas`も無効のままであり，
錐やChannelCompositorをいくら正しく配線しても，そもそも表示経路自体が
単眼時代のまま塞がっていた。

**修正**: `ConeGuideSceneUpgrader.UpgradeCurrentScene()`にも同じ切替処理を追加した
（`SetActiveIfFound`ヘルパーを新設。`ViewpointFollowingSceneBuilder.cs`の同名
private関数とは別クラスのため個別実装）。これで新規構築・既存シーン更新の
どちらの経路でも両眼化の実行時設定が揃うようになった。

### 6.5 UX改善: 錐ガイドの提示条件設定をExperimentRigに集約

錐ガイドの提示条件（背景/箱/ガイドの各モード・極性・周波数・遅延量など）の実体は
`ChannelCompositor`（左目用＝マスター）にあり，物理的には`LeftRawImage`という，
実験操作の主眼である`ExperimentRig`とは離れた場所に存在する。ユーザーから
「設定する箇所が散らばっているのは良くない。ExperimentRigに集約してほしい」との
指摘を受けた。

**対応方針**: フィールドを複製してPush/Pull同期する方式ではなく，
**`FollowingExperimentManager`のカスタムInspectorで`ChannelCompositor`（マスター）の
Inspectorをそのまま埋め込んで表示する**方式を採った。データの複製が一切発生しない
（`ExperimentRig`のInspector上に表示されているのは`LeftRawImage`上の
ChannelCompositor本体そのもの）ため，同期漏れ・タイミングずれのリスクがなく，
右目側への反映は既存の`mirrorFrom`（Play中，毎フレーム）がそのまま働く。

- `Editor/FollowingExperimentManagerEditor.cs`（新規）: `[CustomEditor(typeof(FollowingExperimentManager))]`。
  通常のInspectorを描画した後，`manager.channelCompositor`を`CreateEditor()`で
  埋め込みEditor化し，`OnInspectorGUI()`をそのまま呼び出す。「LeftRawImage を選択」
  ボタンも用意し，必要なら本体へ直接ジャンプできるようにした

これにより`ExperimentRig`のInspectorだけで，モード切替キー（K/G/C/B等）で
操作する項目も含めて，錐ガイドに関する設定のほぼすべてを完結して編集できる。
`ReplayPlayer`（再生確認シーン用）にも同じ手法を適用できるが，現時点では未対応
（必要になれば同じパターンで追加できる）。

### 6.6 再生確認シーンの両眼化 — ConeGuideSceneUpgraderの汎用化

§6.1-4で「今回のスコープ外」としていた再生確認シーン（`ViewpointFollowingReplay.unity`）
について，改めて両眼化を実施した。`BuildReplayScene()`（`ViewpointFollowingSceneBuilder.cs`）
を再実行してシーンを作り直す方式は，ユーザーが既に構築・調整済みの実シーンを破壊するため
採らず，`ConeGuideSceneUpgrader.cs`（既存シーンに非破壊的に差分を足す，実際にユーザーが
使うツール）を汎用化して両シーンに対応させる方針とした。

**課題**: 実験シーン（`ViewpointFollowing.unity`）は元々`OVRCameraRig`を含む
`Player.prefab`が配置済みで，`LeftEyeCapture`/`RightEyeCapture`（実機トラッキング）を
ライブ側の映像取得元としてそのまま使えた。一方，再生確認シーンは`OVRCameraRig`を
持たず，ライブ側の視点は単一の`LiveReplayCamera`（CSV再生でスクリプト駆動，実機
トラッキングされない）のみである。

**対応**:

1. **`EnsurePlayerRig(Scene scene)`を新設**。`LeftEyeAnchor`が見つからない場合のみ，
   `Assets/Prefabs/Player.prefab`（`OVRCameraRig`を含む）を非破壊的にシーンへ追加する
   （既にあれば何もしない＝冪等）。追加したPlayer.prefab側の視野反転
   （`SetReversion`）・移動コントローラ（`PlayerInput`/`OVRPlayerController`/
   `CharacterController`）は，再生確認シーンでは視点をCSV再生が担うため全て無効化する。
   呼び出し順に注意が必要で，**ライブ視点/収録視点（`liveAnchor`/`ghostAnchor`）の
   検索より後**に呼ぶ（`LiveAnchorNames`の検索順は`{ "CenterEyeAnchor",
   "LiveReplayCamera" }`のため，先にPlayer.prefabを追加してしまうと，新設された
   `CenterEyeAnchor`が誤って「ライブ視点」として拾われ，既存の`LiveReplayCamera`が
   無視されてしまう）
2. **ライブ側の映像取得元の決定を分岐**。`LeftEyeCapture`/`RightEyeCapture`が直接
   見つかる場合（実験シーン）はそれをそのまま使う。見つからない場合（再生確認シーン）は，
   `LiveReplayCamera`から`LeftLiveReplayCam`/`RightLiveReplayCam`という左目用・右目用の
   子カメラを新設する——これは収録視点側（`GhostCamera`→`LeftGhostCamera`/
   `RightGhostCamera`）で既に確立していたパターンと全く同じで，`EnsureEnvironmentCamera`
   と`GhostEyeOffset`（実測IPDが無いため`LiveReplayCamera`の実測IPD＝ライブ側の
   `LeftEyeAnchor`/`RightEyeAnchor`間距離を毎フレーム反映）をそのまま再利用した
3. **`LeftLiveReplayCam`/`RightLiveReplayCam`は`BoxCameraNames`に含めない**。
   `GhostCamera`と同じ「環境を映すだけのカメラ」という扱いのため，既存カメラからの
   錐レイヤ除外処理（`ExcludeConeLayersFromExistingCameras`）の対象に含め，
   箱・ガイド専用カメラ（`BoxCameraNames`）とは区別した

これにより，`ConeGuideSceneUpgrader`は実験シーン・再生確認シーンのどちらに対しても
同一のメニュー操作（Tools > 視点追従実験 > 錐ガイドを現在のシーンに追加）で両眼化
できるようになった。完了ダイアログ・プレビュー切替メニュー（`TogglePreview`）の
検索対象カメラ一覧も両シーンに対応する内容へ更新した。
