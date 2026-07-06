# 01. プロジェクト概要

## このプロジェクトは何か

Meta Quest（Oculus）HMD を用いた **視野反転実験** のための Unity プロジェクト。
HMD に映る視野を左右・上下・前後に反転させた状態で歩行やタッチ課題を行わせ、
HMD のトラッキングデータ（頭部・両手の位置姿勢）を CSV に記録して人間の運動適応を調べる。

前任者: 山﨑駿さん（連絡先は `Assets/ResultData/README.txt` 参照）

> **今後の方針**: このプロジェクトを基に「視点追従実験」（事前収録した歩行映像と
> リアルタイム映像を交互に HMD へ提示し、歩行軌跡の追従度合いを測る実験）へ改造する。
> 詳細は [06_roadmap-viewpoint-following.md](06_roadmap-viewpoint-following.md) を参照。

## 開発環境

| 項目 | 内容 |
|---|---|
| Unity バージョン | **2021.3.45f2**（`ProjectSettings/ProjectVersion.txt`） |
| 必要モジュール | Windows Build Support, Android Build Support（Quest 向けビルド用） |
| VR SDK | Oculus Integration（`Assets/Oculus/`）+ com.unity.xr.oculus 3.4.1 |
| ターゲット機器 | Meta Quest シリーズ（PC 上のエディタ実行は Quest Link 経由） |
| 主要パッケージ | PostProcessing 3.4.0 / Unity Recorder 3.0.4 / TextMeshPro / XR Management 4.5.0 |

> **注意**: 引継ぎ資料には「2021.3.24f1 で作成」とあるが、現在のプロジェクトは
> 2021.3.45f2 に更新済み。Unity Hub では 2021.3.45f2（または 2021.3 LTS の近いパッチ）を使うこと。

## セットアップ手順

1. GitHub からクローン: `git clone https://github.com/haku-noir/vection-walk-vr.git`
   - **Git LFS 必須**。クローン前に `git lfs install` を実行しておく（画像・音声・FBX等がLFS管理）。
2. Unity Hub で 2021.3.45f2 のエディタをインストール（Android Build Support を含める）。
3. プロジェクトを開く（初回は Library 生成に時間がかかる）。
4. 見たいシーンを `Assets/Scenes/` から開いて実行。
   - HMD で見る場合は Quest Link を接続するか、Android ビルドして Quest にインストール。

## リポジトリ・フォルダ構成

```
Study-main/
├── Assets/
│   ├── scripts/            ★ 自作スクリプト（本体）
│   │   ├── Common/         　 汎用（ログ記録・入力・カメラ同期など）
│   │   ├── ReversedVision/ 　 視野反転実験のコア
│   │   ├── RandomDots/     　 ランダムドット刺激（オプティカルフロー操作）
│   │   └── Test or Failed/ 　 実験的・未使用コード（参考程度）
│   ├── Scenes/             ★ 実験シーン（04参照）
│   ├── Prefabs/            ★ Player プレハブなど自作プレハブ（02参照）
│   ├── Textures/           ★ CenterEye 等の RenderTexture（映像パイプラインの要）
│   ├── Shaders/               自作シェーダ2本
│   ├── ResultData/         ★ 実験生データ・Python解析スクリプト・引継ぎ資料
│   ├── Oculus/                Oculus Integration（外部アセット）
│   ├── Photon/                Photon PUN2（マルチプレイヤー用、Multi シーンで使用）
│   ├── Audio/                 効果音
│   ├── Room/, BigFurniturePack/, CartoonLowPolyCityLite/, SkySeries Freebie/,
│   │   Real Stars Skybox/     環境アセット（屋内・屋外・スカイボックス）
│   ├── unity-chan!/, ZRNAssets/(Query-Chan), avatar/   キャラクターアセット
│   └── docs/               ★ このドキュメント
├── Packages/manifest.json     パッケージ定義
└── ProjectSettings/           プロジェクト設定
```

★ = 引き継ぎ・改造にあたって重要なフォルダ

## 既知の問題・注意点

- **スクリプトの文字コードが Shift-JIS**: `Assets/scripts/` 以下の .cs ファイルは
  日本語コメントが Shift-JIS で書かれている。UTF-8 として開くと文字化けする。
  VSCode なら「Reopen with Encoding → Japanese (Shift JIS)」で開ける。
  今後編集するファイルは **UTF-8(BOM付き) に変換してから編集する**ことを推奨
  （Unity・VSCode・Git いずれとも相性が良い）。
- **壊れていたキューブマップ**: 以下4ファイルは引き継ぎ時点で実データが欠落しており
  （Git LFS のポインタのみ）、Git 管理開始時にリポジトリから削除した。
  いずれも現行シーンからは未参照で、実験には影響しない。
  - `Assets/Real Stars Skybox/StarSkybox04*/**.cubemap` ×3（元PNGは残っているのでUnity内で再生成可能）
  - `Assets/ZRNAssets/PQAssets/Query-Chan/Materials/Cubemap.cubemap`
- **`GameObject.Find` への依存**: 多くのスクリプトが "OVRCameraRig", "CenterEyeCapture",
  "PostProcessVolume" などのオブジェクト名をハードコードで検索している。
  ヒエラルキー上の**オブジェクト名を変更するとスクリプトが動かなくなる**ので注意（02参照）。
- **Quest 実機のデータ保存先**: ビルドしたアプリでは CSV が
  `Application.persistentDataPath`（実機の `Android/data/<パッケージ名>/files`）に保存される。
  取り出しには SideQuest の利用を推奨（引継ぎ資料より）。
