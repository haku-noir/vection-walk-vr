import sys
import pandas as pd
import matplotlib.pyplot as plt

# コマンドライン引数からファイル名を取得
if len(sys.argv) < 2:
    print("ファイル名を指定してください。")
    sys.exit()

file_name = sys.argv[1]

# CSVファイルの読み込み
df = pd.read_csv(file_name)

# "elapsedTime"列のデータを取得
elapsed_time_data = df['ElapsedTime'][1:51]  # 2行目から51行目までのデータを取得

# 折れ線グラフの描画
plt.plot(range(1, 51), elapsed_time_data, marker='o', linestyle='-')

plt.title('Cumulative Time vs. Trial Number')
plt.xlabel('Trial Number')
plt.ylabel('Cumulative Time (seconds)')
plt.grid(True)
plt.show()
