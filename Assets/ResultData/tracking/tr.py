import sys
import pandas as pd
import matplotlib.pyplot as plt

def plot_trajectory(csv_file_path):
    # CSVファイルの読み込み
    df = pd.read_csv(csv_file_path)
    print(df)
    
    # 時間ごとの座標データを抽出
    time_points = df[['time', 'posX', 'posY']]
    print(time_points)

    # グラフの描画
    plt.figure(figsize=(8, 6))
    plt.plot(time_points['posX'], time_points['posY'], color='b')
    plt.title('Point Trajectory')
    plt.xlabel('x')
    plt.ylabel('y')
    # plt.grid(True)
    plt.show()

if __name__ == "__main__":
    # コマンドライン引数からファイル名を取得
    if len(sys.argv) != 2:
        print("Usage: python script.py <csv_file_path>")
        sys.exit(1)

    csv_file_path = sys.argv[1]
    plot_trajectory(csv_file_path)
