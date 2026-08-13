# 08. 4ストローク運動錯視の提示機能

視野映像に **4ストローク見かけの運動（4-stroke apparent motion）** を付与し、
歩行時の運動知覚を増強（Enhance）・消失（Zero）・逆転（Reversal）させる機能の解説。

元実装は、USB カメラ映像に同処理を掛ける Win32+OpenCV プログラム
（`docs/fourstroke/`、**参考資料のため git 管理外**）の FOURSTROKE_MODE であり、
その合成ロジックを Unity（シェーダ）へ忠実に移植した。

---

## 1. 原理（FOURSTROKE_MODE の要約）

2枚の画像 —— 過去フレーム D と現在フレーム C —— と、それぞれの輝度反転画像
D̄・C̄ を `D → C → D̄ → C̄ → D → …` の4コマ周期で提示すると、
D→C 間の実運動方向への動きが途切れなく続いて知覚される
（反転ペア間の輝度相関が負になるため、逆向きのジャンプが知覚されない）。

実装上は次の3要素で構成される：

| 要素 | 内容 |
|---|---|
| グレースケール化 | 反転操作を輝度軸上で対称にするため両フレームを輝度画像化（既定ON） |
| 輝度反転 | `result = 0.5 + (src − 0.5) × inv`（inv = +1: 無変換 / −1: 完全反転）。変調位相の後半（π〜2π）で C/D とも反転 |
| 台形波クロスフェード | 半周期を「D 静止 40% → 遷移 20% → C 静止 40%」の台形波 αC で合成（`表示 = C×αC + D×(1−αC)`） |

**極性スイッチ**（知覚の向き）:

| 極性 | 動作 | 知覚 |
|---|---|---|
| **Enhance** (+1) | 過去→現在の順で提示 | 実運動と同方向の信号が加算され**加速感** |
| **Zero** (0) | αC=1 固定（現在のみ。反転フリッカーは継続） | 錯視が消える**統制条件** |
| **Reversal** (−1) | 現在→過去の順で提示 | 実運動と逆向きの信号で**抵抗・逆行感** |

---

## 2. Unity 実装の構成（両シーン共通のコア）

`Assets/scripts/ViewpointFollowing/FourStroke/` に配置。

```
                現在フレーム C            過去フレーム D
                （ライブ映像）      ┌ 視点追従: 収録映像（PlaybackEye）
                CenterEye RT        └ 4st歩行 : DelayedFrameBuffer の遅延映像
                      │                   │
                      ▼                   ▼
              ┌─────────────────────────────────┐
              │ FourStrokeCompositor            │  位相・αC・反転フラグを計算し
              │   └ FourStroke.shader (Blit)    │  シェーダで合成
              └─────────────────────────────────┘
                              │ OutputTexture
                              ▼
                      CenterRawImage → HMD
```

| ファイル | 役割 |
|---|---|
| `FourStroke.shader` | 合成シェーダ（`Hidden/FourStroke`）。グレースケール化・反転・台形波合成を1パスで実行 |
| `FourStrokeCompositor.cs` | **共有コア**。位相（2πf·t、`Time.deltaTime` 積算のため一時停止中は停止）から αC・反転フラグを計算し、毎フレーム出力 RT へ Blit。`polarity` / `frequency` / `grayscale` を公開 |
| `DelayedFrameBuffer.cs` | 4ストローク歩行シーン用。ライブ映像（CenterEye RT）を約30fpsでリングバッファへ複製し、`delayFrames` 前（既定 8 ≈ 267ms）の映像を返す |
| `FourStrokeSelfManager.cs` | 4ストローク歩行シーンの進行管理（開始/停止/軌跡保存・調整キー・HUD） |

位相リセット時は t'=0.6（αC=1・非反転 = **現在フレームの静止提示**）から始まるため、
既存実験の「必ずライブ映像から提示を始める」慣習と整合する。

---

## 3. 使い方① — 視点追従実験（Follow モード）での 4ストローク提示

既存の `ViewpointFollowing.unity` で、ライブ映像と収録映像の**矩形波切替の代わりに**
4ストローク合成（C=ライブ、D=収録映像）で提示できる。**シーンの再生成は不要**
（`FollowingExperimentManager` が起動時に `FourStrokeCompositor` を自動配線する）。

- **ON/OFF**: 停止中に **4 キー**、または Inspector の `ViewSwitcher > Four Stroke Enabled`
  （試行中の条件変更を防ぐため、キーは停止中のみ有効）
- **極性**: 停止中に **V キー**（Enhance → Reversal → Zero の巡回）
- **変調周波数**: 既存の `switchFrequency` を共用（1周期 = 4ストローク1巡）
- **保存 CSV**: 4ストローク提示だった試行はファイル名にタグが付く
  例 `following_results_3.0Hz_PositionAndRotation_4stEnhance_20260714_130000.csv`
- **source 列**: αC ≥ 0.5 のとき 0（ライブ支配）、それ以外は 1（収録支配）として記録される
  （矩形波切替時と同じ 0/1 の意味づけ。デバッグ着色も同様に機能する）

> 注意: 4ストローク中は原理上、視野が**グレースケール＋周期的な輝度反転**になる。
> カラーのまま試したい場合は `FourStrokeCompositor > Grayscale` をオフにできる（非推奨・原典と異なる）。

## 4. 使い方② — 4ストローク歩行シーン（自分の過去映像との合成）

メニュー **Tools > 視点追従実験 > 4ストローク歩行シーンを生成** で
`Assets/Scenes/ViewpointFourStroke.unity` を生成する。

歩行中のライブ映像 C と、リングバッファから取り出した**数百ms前の自分の映像 D** を
4ストローク合成して HMD に提示する（元プログラムの「カメラ→過去の自分」構成の HMD 版）。
頭部軌跡は視点追従実験と同じ形式（`trajectory_*.csv`）で収録できる。

### 操作一覧

| キー / ボタン | 機能 |
|---|---|
| O / Bボタン | 開始・停止（保存なし。練習用） |
| P / Yボタン | 自動保存つき開始（停止時に軌跡CSVを保存） |
| 停止中に S / 右中指トリガー | 軌跡CSVの手動保存 |
| **4** | 4ストローク合成の ON/OFF（OFF はライブ映像そのまま） |
| **V** | 極性切替（Enhance → Reversal → Zero） |
| ↑ / ↓ | 変調周波数 ±0.1Hz（0.02〜10Hz） |
| ← / → | 遅延フレーム数 ∓1（0〜29、@30fps） |
| 1 / 2 / 3 | 環境密度（高密度 / 低密度 / なし） |

### 主要パラメータ既定値（元プログラムに合わせた値）

| パラメータ | 既定値 | 場所 |
|---|---|---|
| 変調周波数 | 3.0 Hz（1周期 ≈ 333ms） | `FourStrokeCompositor.frequency` |
| 遅延フレーム数 | 8（≈ 267ms @30fps） | `DelayedFrameBuffer.delayFrames` |
| バッファ取り込みレート | 30 fps | `DelayedFrameBuffer.captureFps` |
| 極性 | Enhance | `FourStrokeCompositor.polarity` |
| グレースケール化 | ON | `FourStrokeCompositor.grayscale` |

## 5. 使い方③ — 再生確認シーンでの後付け 4ストローク

`ViewpointFollowingReplay.unity`（[07](07_viewpoint-following-experiment.md#再生確認シーンviewpointfollowingreplayunity)）では、
保存済みの `following_results_*.csv` を **収録後に**別条件で見直せる。ライブ姿勢と収録姿勢を
2台のカメラで同時に再レンダリングし、同じ `ViewSwitcher` / `FourStrokeCompositor` で再合成するため、
実験時に 4ストロークを掛けていなくても後から追加できる。

- **M キー**で表示モードを **Reswitch** にする（C=ライブ再現映像、D=収録再現映像）
- **4 キー**で 4ストローク ON/OFF、**V キー**で極性、**↑/↓**で変調周波数（=切替周波数、±0.5Hz）
- HMD もデータ保存もなく、映像の見えだけを検討する用途（軌跡データは元 CSV のまま不変）

> 再生の一時停止は `playing` フラグで行い `timeScale` は変えないため、停止中も切替・合成は
> 実時間で進み続ける（静止フレーム上で4ストロークの見えを確認できる）。

---

## 6. 実装メモ

- 合成は `Graphics.Blit`（GPU 1パス）なので CPU 負荷はほぼゼロ。出力 RT と
  リングバッファ（`delayFrames+1` 枚のみ確保）はコンポーネント破棄時に解放される。
- 位相・遅延バッファの取り込みは `Time.deltaTime` 積算のため、一時停止
  （`timeScale=0`）中は変調・取り込みとも自動的に止まる（既存のポーズ機構と整合）。
- 合成の Blit は LateUpdate で行うため、提示映像は最大1フレーム（HMD で 11〜14ms）
  遅れる。3Hz 前後の変調に対しては無視できる遅延と判断している。
- Zero 極性でも輝度反転フリッカーは継続する（運動信号のみを除去した統制条件。原典 §4.4 と同じ）。
