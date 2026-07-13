"""視点追従実験の追従データ（following_results_*.csv）をグラフ化するスクリプト．

生成するグラフ（CSVと同じフォルダにPNGとして保存）:
  1. <CSV名>_axes.png : 6軸（位置X/Y/Z・回転X/Y/Z）それぞれについて，
     ライブ頭部と収録軌跡を2本の線で重ねて比較する（3行2列）
  2. <CSV名>_diff.png : ライブ−収録の差分を，並進3軸・回転3軸でそれぞれ
     1枚にまとめて比較する（2段）

使い方:
  python plot_following.py                 # このフォルダ内の最新の following_results_*.csv を使用
  python plot_following.py <CSVパス>       # ファイルを指定
  python plot_following.py --no-show       # ウィンドウ表示せずPNG保存のみ

背景の薄い網掛けは「収録映像を表示していた区間」（source=1）を示す．
"""

import argparse
import glob
import os
import sys

import pandas as pd
import matplotlib.pyplot as plt

# ---- 配色（プロジェクト共通のカテゴリカルパレット・固定順） ----
C_LIVE = "#2a78d6"   # ライブ頭部（青）
C_REC = "#1baf7a"    # 収録軌跡（アクア・破線と併用して色以外でも区別）
C_X, C_Y, C_Z = "#2a78d6", "#1baf7a", "#eda100"  # 差分グラフの軸別（青・アクア・黄）
C_SHADE = "0.5"      # 収録映像表示区間の網掛け（無彩色）
C_TEXT = "#3a3a38"

POS_AXES = [("livePosX", "recPosX", "Position X [m]"),
            ("livePosY", "recPosY", "Position Y [m]"),
            ("livePosZ", "recPosZ", "Position Z [m]")]
ROT_AXES = [("liveRotX", "recRotX", "Rotation X (pitch) [deg]"),
            ("liveRotY", "recRotY", "Rotation Y (yaw) [deg]"),
            ("liveRotZ", "recRotZ", "Rotation Z (roll) [deg]")]


def find_latest_csv(folder):
    """フォルダ内の最新の following_results_*.csv を返す（ファイル名の日時で判断）"""
    files = sorted(glob.glob(os.path.join(folder, "following_results_*.csv")))
    return files[-1] if files else None


def wrap_angle_diff(live, rec):
    """角度差を -180°〜180° に折り返す（350°−(−10°)=360° のような見かけの大差を防ぐ）"""
    return (live - rec + 180.0) % 360.0 - 180.0


def playback_spans(df):
    """source=1（収録映像の表示中）の連続区間 [(開始時刻, 終了時刻), ...] を求める"""
    spans, start = [], None
    src = df["source"].to_numpy()
    t = df["time"].to_numpy()
    for i in range(len(df)):
        if src[i] == 1 and start is None:
            start = t[i]
        elif src[i] == 0 and start is not None:
            spans.append((start, t[i]))
            start = None
    if start is not None:
        spans.append((start, t[-1]))
    return spans


def shade_playback(ax, spans):
    """収録映像の表示区間を薄い網掛けで示す"""
    for t0, t1 in spans:
        ax.axvspan(t0, t1, color=C_SHADE, alpha=0.12, linewidth=0)


def style_axis(ax, ylabel):
    """目盛・グリッドを控えめに整える（データの線が主役になるように）"""
    ax.set_ylabel(ylabel, fontsize=9, color=C_TEXT)
    ax.grid(True, linewidth=0.5, alpha=0.3)
    ax.tick_params(labelsize=8, colors=C_TEXT)
    for spine in ("top", "right"):
        ax.spines[spine].set_visible(False)


def plot_axes_comparison(df, spans, out_path, title):
    """図1: 6軸それぞれでライブと収録を2本の線で比較する（左列=並進，右列=回転）"""
    fig, axes = plt.subplots(3, 2, figsize=(12, 8), sharex=True)

    for row, ((lp, rp, plabel), (lr, rr, rlabel)) in enumerate(zip(POS_AXES, ROT_AXES)):
        for col, (live_col, rec_col, ylabel) in enumerate([(lp, rp, plabel), (lr, rr, rlabel)]):
            ax = axes[row][col]
            shade_playback(ax, spans)
            ax.plot(df["time"], df[live_col], color=C_LIVE, linewidth=1.6, label="Live head")
            ax.plot(df["time"], df[rec_col], color=C_REC, linewidth=1.6,
                    linestyle="--", label="Recorded")
            style_axis(ax, ylabel)

    for ax in axes[-1]:
        ax.set_xlabel("Time [s]", fontsize=9, color=C_TEXT)

    # 凡例は図全体で1つ（網掛けの説明も加える）
    handles, labels = axes[0][0].get_legend_handles_labels()
    handles.append(plt.Rectangle((0, 0), 1, 1, color=C_SHADE, alpha=0.12))
    labels.append("Playback shown")
    fig.legend(handles, labels, loc="upper right", ncol=3, fontsize=9, frameon=False)
    fig.suptitle(title, fontsize=11, color=C_TEXT, x=0.02, ha="left")
    fig.tight_layout(rect=(0, 0, 1, 0.95))
    fig.savefig(out_path, dpi=150)
    print(f"saved: {out_path}")
    return fig


def plot_diff_comparison(df, spans, out_path, title):
    """図2: ライブ−収録の差分を並進3軸・回転3軸でまとめて比較する"""
    fig, (ax_pos, ax_rot) = plt.subplots(2, 1, figsize=(12, 7), sharex=True)

    # --- 並進誤差 [m] ---
    shade_playback(ax_pos, spans)
    ax_pos.axhline(0, color="0.6", linewidth=0.8)
    for (lcol, rcol, _), color, name in zip(POS_AXES, [C_X, C_Y, C_Z], ["X", "Y", "Z"]):
        ax_pos.plot(df["time"], df[lcol] - df[rcol], color=color, linewidth=1.6,
                    label=f"{name} (live - rec)")
    style_axis(ax_pos, "Position error [m]")
    ax_pos.legend(loc="upper right", ncol=3, fontsize=9, frameon=False)

    # --- 回転誤差 [deg]（±180°に折り返し） ---
    shade_playback(ax_rot, spans)
    ax_rot.axhline(0, color="0.6", linewidth=0.8)
    for (lcol, rcol, _), color, name in zip(ROT_AXES, [C_X, C_Y, C_Z],
                                            ["X (pitch)", "Y (yaw)", "Z (roll)"]):
        ax_rot.plot(df["time"], wrap_angle_diff(df[lcol], df[rcol]), color=color,
                    linewidth=1.6, label=f"{name}")
    style_axis(ax_rot, "Rotation error [deg]")
    ax_rot.legend(loc="upper right", ncol=3, fontsize=9, frameon=False)
    ax_rot.set_xlabel("Time [s]", fontsize=9, color=C_TEXT)

    fig.suptitle(title, fontsize=11, color=C_TEXT, x=0.02, ha="left")
    fig.tight_layout(rect=(0, 0, 1, 0.95))
    fig.savefig(out_path, dpi=150)
    print(f"saved: {out_path}")
    return fig


def main():
    parser = argparse.ArgumentParser(description="視点追従実験の追従データをグラフ化する")
    parser.add_argument("csv", nargs="?", default=None,
                        help="following_results_*.csv のパス（省略時はこのフォルダの最新ファイル）")
    parser.add_argument("--no-show", action="store_true", help="ウィンドウ表示せずPNG保存のみ行う")
    args = parser.parse_args()

    here = os.path.dirname(os.path.abspath(__file__))
    csv_path = args.csv or find_latest_csv(here)
    if csv_path is None or not os.path.exists(csv_path):
        sys.exit(f"CSVが見つかりません: {csv_path or here}")

    df = pd.read_csv(csv_path)
    spans = playback_spans(df)
    base = os.path.splitext(csv_path)[0]
    name = os.path.basename(base)

    plot_axes_comparison(df, spans, base + "_axes.png", f"Live vs Recorded (6 axes) - {name}")
    plot_diff_comparison(df, spans, base + "_diff.png", f"Live - Recorded error - {name}")

    if not args.no_show:
        plt.show()


if __name__ == "__main__":
    main()
