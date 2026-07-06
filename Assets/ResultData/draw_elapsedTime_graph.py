import sys
import csv
import pandas as pd
import matplotlib.pyplot as plt

def find_interaction_numbers(csv_file):
    with open(csv_file, 'r', newline='', encoding='utf-8') as file:
        reader = csv.reader(file)
        header = next(reader)  # ヘッダーを読み飛ばす

        # InteractionNumberとElapsedTimeの列のインデックスを取得
        try:
            interaction_index = header.index('InteractionNumber')
            elapsed_time_index = header.index('ElapsedTime')
        except KeyError:
            print("Error!")
            return
        
        df = pd.DataFrame(columns = ['InteractionNumber', 'ElapsedTime'])
        # print(df)

        for row in reader:
            if len(row) == 0:
                break
            df.loc[len(df)] = row[0:2]

        print(type(df['ElapsedTime'][0]))
        return df

# コマンドライン引数からファイル名を取得
if len(sys.argv) < 2:
    print("ファイル名を指定してください。")
    sys.exit()

for i in range(1, len(sys.argv)):
    # file_name = sys.argv[i]

    # names = ["c0","c1","c2"]
    # # CSVファイルの読み込み
    # df = pd.read_csv(file_name, names = names, nrows = 53)
    # print(df)

    # # "elapsedTime"列のデータを取得
    # elapsed_time_data = df["c0"][1:51]  # 2行目から51行目までのデータを取得
    # print(elapsed_time_data)

    # 累積時間の計算
    # cumulative_times = elapsed_time_data.cumsum()

    df = find_interaction_numbers(sys.argv[i])
    print(df)

    # 折れ線グラフの描画
    plt.plot(df["InteractionNumber"][1:51], df["ElapsedTime"][1:51], marker='o', linestyle='-')

    plt.title('Cumulative Time vs. Trial Number')
    plt.xlabel('Trial Number')
    plt.ylabel('Cumulative Time (seconds)')
    plt.grid(True)
    plt.show()

