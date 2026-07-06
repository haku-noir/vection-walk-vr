import pandas as pd
import matplotlib.pyplot as plt
import sys
import os

# "Time"列がゼロにリセットされる問題を修正
def fix_time_column(df):
    time_values = df['time'].values.copy()
    # print(time_values)
    for i in range(1, len(time_values)):
        dif = time_values[i] - time_values[i - 1]
        # print(dif)
        if dif < 0:
            # print("old: {} += {}".format(time_values[i], time_values[i-1]))
            time_values[i:] += time_values[i - 1]
            # print("new: {}".format(time_values[i]))
        else:
            time_values[i] = time_values[i - 1] + dif
    df['time'] = time_values
    # print(time_values)
    return df

def get_integral(column_name: str, elapsed_time_file: str, transform_log_file: str, output_directory: str):
    # CSVファイルAとBの読み込み
    df_A = pd.read_csv(elapsed_time_file)
    df_B = pd.read_csv(transform_log_file)
    df_B = fix_time_column(df_B)
    
    # AのElapsedTimeを取得
    time_intervals = df_A['ElapsedTime'].tolist()

    # 座標の変化を記録するリストを初期化
    coordinate_changes = []

    # 各時間区間での座標の変化を計算
    for i in range(len(time_intervals)):

        if i == 0:
            start_time = 0
        else:
            start_time = time_intervals[i - 1]
        end_time = time_intervals[i]
        
        # Bのデータを時間でフィルタリング
        df_B_filtered = df_B[(df_B['time'] >= start_time) & (df_B['time'] < end_time)].copy()
        # print(end_time)
        
        # 区間内の座標の変化を計算
        if not df_B_filtered.empty:
            sum = 0
            # for i in range(len(df_B_filtered)-1):
            #     start_posX = df_B_filtered[column_name].iloc[i]
            #     end_posX = df_B_filtered[column_name].iloc[i+1]
            #     sum += abs(end_posX - start_posX)
            # change = sum

            df_B_filtered[f'{column_name}_diff'] = df_B_filtered[f'{column_name}'].diff().abs()
            change = df_B_filtered[f'{column_name}_diff'].sum()
            # print(df_B_filtered[f'{column_name}_diff'])
        else:
            change = 0
        
        # 結果をリストに追加
        coordinate_changes.append({
            'index': i,
            'start_time': start_time,
            'end_time': end_time,
            f'{column_name}_change': change
        })

    # 結果をデータフレームに変換
    df_result = pd.DataFrame(coordinate_changes)

    # 平均値と標準偏差の計算
    mean_change = df_result[f'{column_name}_change'].mean()
    std_change = df_result[f'{column_name}_change'].std()

    # グラフの作成
    plt.figure(figsize=(10, 6))
    plt.plot(df_result['index'], df_result[f'{column_name}_change'], marker='o', linestyle='-')
    plt.xlabel('Index')
    plt.ylabel(f'{column_name} Displacement Sum')
    plt.title(f'{column_name} Displacement Sum between Time Intervals')
    # plt.ylim(0, 0.2)  # 縦軸の範囲を制限
    plt.grid(True)
    # 平均値と標準偏差をテキストで表示
    plt.text(0.5, 0.95, f'Mean: {mean_change:.4f}\nStd Dev: {std_change:.4f}', 
        horizontalalignment='center', 
        verticalalignment='center', 
        transform=plt.gca().transAxes,
        bbox=dict(facecolor='white', alpha=0.5))

    figure_file_path = os.path.join(output_directory, f'_{column_name}.png')
    plt.savefig(figure_file_path)
    # df_result.to_csv(csv_file_path, index=False)
    
    plt.show()

    # 結果の表示（オプション）
    # print(df_result)


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Add file name!")
    elapsed_file = sys.argv[1] 
    log_file = sys.argv[2]
    if not elapsed_file.endswith(".csv"):
        elapsed_file += ".csv"
    if not log_file.endswith(".csv"):
        log_file += ".csv"

    split_file = elapsed_file.split("_")
    condition = split_file[1]

    # 結果を新しいCSVファイルに保存
    directory = f"graph/{condition}"
    if not os.path.exists(directory):
        os.makedirs(directory)

    get_integral('posX', elapsed_file, log_file, directory)
    get_integral('posY', elapsed_file, log_file, directory)
    get_integral('posZ', elapsed_file, log_file, directory)


#   python draw_pos.py extracted_FB3_lookaround_results_20231116_152023.csv_1 extracted_FB3_lookaround_results_20231116_152023.csv_2