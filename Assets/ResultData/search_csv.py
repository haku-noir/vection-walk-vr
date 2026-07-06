import sys
import csv
import pandas as pd

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

        for row in reader:
            if len(row) == 0:
                break
            df.loc[len(df)] = row[0:2]

        return df

def find_tracking_log(csv_file):
    with open(csv_file, 'r', newline='', encoding='utf-8') as file:
        reader = csv.reader(file)
        header_index = 0
        empty_row_count = 0

        for index, row in enumerate(reader):
            if len(row) == 0:
                empty_row_count += 1
            if "time" in row:
                header_index = index
                header = row
                # print(header_index)
                # print(header)
                try:
                    time_index = header.index('time')
                    posX_index = header.index('posX')
                    posY_index = header.index('posY')
                    posZ_index = header.index('posZ')
                    rotX_index = header.index('rotX')
                    rotY_index = header.index('rotY')
                    rotZ_index = header.index('rotZ')
                    break
                except KeyError:
                    return
        
        # 最終行は0が並んでいることが多いので削除
        df = pd.read_csv(csv_file, header = header_index - empty_row_count)
        df = df[:-1]
        # print(pd.concat([df.head(5), df.tail(7)]))

        # 各列の平均値と標準偏差を計算
        average_values = df.mean()
        std_dev_values = df.std()

        # 平均値と標準偏差を表の一番下に追加
        df.loc['Average'] = average_values
        df.loc['Std Dev'] = std_dev_values

        # print(pd.concat([df.head(5), df.tail(7)]))
        return df
        

if __name__ == "__main__":
    # コマンドライン引数からCSVファイルのパスを取得
    if len(sys.argv) != 2:
        print("Usage: python script.py <csv_file>")
        sys.exit(1)

    csv_file_path = sys.argv[1]
    elapsed_time_df = find_interaction_numbers(csv_file_path)
    tracking_log_df = find_tracking_log(csv_file_path)
    print(elapsed_time_df)
    print(tracking_log_df)
