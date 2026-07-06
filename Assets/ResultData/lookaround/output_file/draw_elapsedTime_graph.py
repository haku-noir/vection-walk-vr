import sys
# import csv
import re
import pandas as pd
import matplotlib.pyplot as plt

# def find_interaction_numbers(csv_file):
#     with open(csv_file, 'r', newline='', encoding='utf-8') as file:
#         reader = csv.reader(file)
#         header = next(reader)  # ヘッダーを読み飛ばす

#         # InteractionNumberとElapsedTimeの列のインデックスを取得
#         try:
#             interaction_index = header.index('InteractionNumber')
#             elapsed_time_index = header.index('ElapsedTime')
#         except KeyError:
#             print("Error!")
#             return
        
#         df = pd.DataFrame(columns = ['InteractionNumber', 'ElapsedTime'])

#         for row in reader:
#             if len(row) == 0:
#                 break
#             df.loc[len(df)] = row[0:2]

#         return df


def get_condition(condition_alphabet):
    s = condition_alphabet

    if s == "no":
        return "Normal", "tab:blue"
    elif s == "LR":
        return "Left-Right", "tab:orange"
    elif s == "UD":
        return "Up-Down", "tab:green"
    elif s == "FB":
        return "Front-Back", "tab:red"
    elif s == "UDLR":
        return "Up-Down & Left-Right", "tab:purple"
    elif s == "UDFB":
        return "Up-Down & Front-Back", "tab:brown"
    elif s == "LRFB":
        return "Left-Right & Front-Back", "tab:pink"
    elif s == "UDLRFB":
        return "All", "tab:gray"
    
def main():
    # コマンドライン引数からファイル名を取得
    if len(sys.argv) < 2:
        print("ファイル名を指定してください。")
        sys.exit()

    for i in range(1, len(sys.argv)):
        file_name = sys.argv[i]
        if not file_name.endswith(".csv"):
            file_name += ".csv"

        # names = ["c0","c1","c2"]
        # # CSVファイルの読み込み
        # df = pd.read_csv(file_name, names = names, nrows = 53)
        # print(df)

        # # "elapsedTime"列のデータを取得
        # elapsed_time_data = df["c0"][1:51]  # 2行目から51行目までのデータを取得
        # print(elapsed_time_data)

        # 累積時間の計算
        # cumulative_times = elapsed_time_data.cumsum()

        # df = find_interaction_numbers(sys.argv[i])
        # print(df)

        # CSVファイルの読み込み（51行目までを読み込むならnrows=51）
        df = pd.read_csv(file_name)

        # "elapsedTime"列のデータを取得
        # elapsed_time_data = df['ElapsedTime'][1:51]  # 2行目から51行目までのデータを取得

        # # 折れ線グラフの描画
        # plt.plot(range(1, 51), elapsed_time_data, linestyle='-')

        pattern = re.compile(r'extracted_(\w+)(\d{1})_(\w+)_results_')
        match = pattern.search(file_name)
        if match:
            print(match.group(1))
        else:
            print("matching error")
        

        # 折れ線グラフの描画

        plt.plot(df["InteractionNumber"][0:50]+1, df["ElapsedTime"][0:50], linestyle='-', label=get_condition(match.group(1))[0])

    plt.title('Cumulative Time vs. Trial Number')
    plt.xlabel('Trial Number')
    plt.ylabel('Cumulative Time (seconds)')
    plt.legend()
    plt.grid(True)
    plt.show()


if __name__ == "__main__":
    main()

# python draw_elapsedTime_graph.py 