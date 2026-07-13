# vection-walk-vr

Meta Quest（Oculus）HMD を用いた VR 歩行実験のための Unity プロジェクト。

- **視野反転実験**（前任者から引き継ぎ）: 視野を左右・上下・前後に反転させた状態での歩行・タッチ課題
- **視点追従実験**（開発中・本体）: 事前収録した歩行視点映像とライブ映像を一定周波数で交互に HMD へ提示し、
  歩行軌跡が収録軌跡にどの程度追従するかを、環境のオプティカルフロー量・切替周波数を変えて調べる

## 動作環境

| 項目 | 内容 |
|---|---|
| Unity | **2021.3.45f2**（Android Build Support 必須） |
| VR SDK | Oculus Integration（同梱）+ XR Plugin Management |
| 対象機器 | Meta Quest シリーズ（エディタ実行は Quest Link 経由） |

## セットアップ

```bash
git lfs install          # 必ず先に（画像・音声・FBX等がLFS管理）
git clone https://github.com/haku-noir/vection-walk-vr.git
```

Unity Hub で 2021.3.45f2 を入れてプロジェクトを開く。

## 視点追従実験のクイックスタート

1. Unity メニュー **Tools > 視点追従実験 > 実験シーンを生成** を実行
   （`Assets/Scenes/ViewpointFollowing.unity` が自動構築される。初回のみ）
2. シーンを再生し、HMD を装着して開始地点（緑マーカー）で正面を向き**視点をリセット**
3. **収録走**: `P` キー（または Y ボタン）で開始 → コースを歩行 → `O`（または B ボタン）で停止すると軌跡CSVが自動保存される
4. `M` キー（または A ボタン）で **Follow モード**へ切替
5. **実験走**: `P` で開始（収録映像とライブ映像が交互に提示される）→ 歩行 → 再生終了で自動停止し追従データCSVが自動保存される
   （練習・動作確認は `O` で開始すればCSVは保存されない）
6. 切替周波数は `ExperimentRig` の **ViewSwitcher**（Inspector）、環境密度は `1`/`2`/`3` キーで変更

データは エディタ実行時 `Assets/ResultData/following/`、実機ビルド時 `Android/data/<プロダクト名>/files/following/` に CSV で保存される。

詳細な使い方・データ形式・実装構成 → **[docs/viewpoint-following/07_viewpoint-following-experiment.md](docs/viewpoint-following/07_viewpoint-following-experiment.md)**

## ドキュメント

プロジェクト全体のドキュメントは [`docs/`](docs/README.md) にある。
（前任者の視野反転プロジェクトの説明は `docs/reversed-vision/`、視点追従実験は `docs/viewpoint-following/`）

| ドキュメント | 内容 |
|---|---|
| [01 概要](docs/reversed-vision/01_project-overview.md) | 開発環境・フォルダ構成・既知の問題 |
| [02 アーキテクチャ](docs/reversed-vision/02_architecture.md) | VR映像パイプラインと Player プレハブの構造 |
| [03 スクリプト](docs/reversed-vision/03_scripts-reference.md) | 全スクリプトのリファレンス |
| [04 シーンと実験](docs/reversed-vision/04_scenes-and-experiments.md) | 視野反転実験のシーン一覧・操作方法 |
| [05 データと解析](docs/reversed-vision/05_data-and-analysis.md) | CSV形式・保存先・解析スクリプト |
| [06 ロードマップ](docs/viewpoint-following/06_roadmap-viewpoint-following.md) | 視点追従実験の設計方針 |
| [07 視点追従実験](docs/viewpoint-following/07_viewpoint-following-experiment.md) | **視点追従実験の使い方・構成（本体）** |

## リポジトリ運用

- バイナリアセット（png/wav/fbx 等）は **Git LFS** 管理（`.gitattributes` 参照）
- `Library/` `Temp/` `Logs/` 等は `.gitignore` 済み（Unity 標準構成）
- 自作スクリプトの日本語コメント: 既存の `Assets/scripts/`（Common/ReversedVision/RandomDots）は
  **Shift-JIS**、新規の `ViewpointFollowing/` は **UTF-8(BOM付き)**。エンコーディングに注意して編集すること
