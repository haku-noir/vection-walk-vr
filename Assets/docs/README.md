# プロジェクトドキュメント

視野反転VR実験プロジェクト（先輩から引き継ぎ）のドキュメント集です。
今後このプロジェクトを基に **視点追従実験** へ改造することを前提に整理しています。

## ドキュメント一覧

| ファイル | 内容 |
|---|---|
| [01_project-overview.md](01_project-overview.md) | プロジェクト概要・開発環境・フォルダ構成・セットアップ手順 |
| [02_architecture.md](02_architecture.md) | VR映像パイプライン（視野反転の仕組み）と Player プレハブの構造 |
| [03_scripts-reference.md](03_scripts-reference.md) | 全スクリプトのリファレンス |
| [04_scenes-and-experiments.md](04_scenes-and-experiments.md) | シーン一覧・実験の実行方法・操作方法 |
| [05_data-and-analysis.md](05_data-and-analysis.md) | 計測データのCSV形式・保存先・解析スクリプト |
| [06_roadmap-viewpoint-following.md](06_roadmap-viewpoint-following.md) | 視点追従実験への改造ロードマップ（流用可能な資産と新規開発項目） |

## 原典資料

- `Assets/ResultData/視野反転引継ぎ用資料.md` — 前任者（山﨑駿さん）による引継ぎ資料
- `Assets/ResultData/README.txt` — 前任者の連絡先と実行方法

## まず読むべきもの

1. 初めてこのプロジェクトを触る人 → `01_project-overview.md` → `02_architecture.md`
2. 実験を再現したい人 → `04_scenes-and-experiments.md` → `05_data-and-analysis.md`
3. 視点追従実験の開発を始める人 → `02_architecture.md` → `06_roadmap-viewpoint-following.md`
