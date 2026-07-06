import sys
import pandas as pd
import matplotlib.pyplot as plt

def split_dataframe(csv_file_path):
    # CSVファイルの読み込み
    df = pd.read_csv(csv_file_path)

    # "HMD"と書かれた行のインデックスを取得
    hmd_index = df.index[df['time'] == 'HMD'].tolist()

    if not hmd_index:
        print("No 'HMD' found in the 'time' column.")
        return

    # "HMD"が最初に出現する位置
    hmd_start_index = hmd_index[0]

    # "HMD"以前のデータフレーム
    before_hmd_df = df.iloc[:hmd_start_index]

    # "HMD"以後のデータフレーム
    after_hmd_df = df.iloc[hmd_start_index+2:]

    return before_hmd_df, after_hmd_df

def plot_trajectory(csv_file_path):
    # CSVファイルの読み込み
    # df = pd.read_csv(csv_file_path)
    df, _ = split_dataframe(csv_file_path)
    print(df.head())

    # 時間ごとの座標データを抽出
    # time_points = df[['time', 'posX', 'posY']]
    # print(time_points)

    df.to_csv("employee.csv")
    
    df = pd.read_csv("employee.csv")
    # time_points = df[['time', 'posX', 'posY']]

    # グラフの描画
    plt.figure(figsize=(8, 6))
    plt.plot(df['posX'], df['posY'], color='b')
    plt.plot(df['gazeX'], df['gazeY'], color='r')
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
