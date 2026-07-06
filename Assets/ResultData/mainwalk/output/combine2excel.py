import pandas as pd
from pathlib import Path

def merge_csv_to_excel(input_folder, output_folder):
    # 入力フォルダ内のCSVファイルをグループ別に辞書に追加
    grouped_dataframes = {}
    
    for file_path in Path(input_folder).glob('*.csv'):
        df = pd.read_csv(file_path)
        # ファイル名を"_"で分割して種別と識別名を抽出
        parts = file_path.stem.split('_')
        category = parts[0]
        # if category in ["going", "coming"]:
        #     continue
        identifier = parts[1]
        if identifier != "shiroyama":
            continue
        genre = parts[4]
        if len(genre) <= 8:
            genre = "normal"
        else:
            genre = genre[0:-8]
        
        if category not in grouped_dataframes:
            grouped_dataframes[category] = {}

        if identifier not in grouped_dataframes[category]:
            grouped_dataframes[category][identifier] = {}

        # 列ごとにデータを辞書に追加
        for column in df.columns:
            if column not in grouped_dataframes[category][identifier]:
                grouped_dataframes[category][identifier][column] = []
            grouped_dataframes[category][identifier][column].append(df[[column]].rename(columns={column: genre}))

    # 各グループごとにExcelファイルに書き出し
    for category, identifiers in grouped_dataframes.items():
        # print(category)
        for identifier, dataframes in identifiers.items():
            output_excel = f'{output_folder}/{category}_{identifier}_output.xlsx'
            print(output_excel)
            with pd.ExcelWriter(output_excel, engine='xlsxwriter') as writer:
                for column, dfs in dataframes.items():
                    # 列ごとにデータを横に並べたシートを作成
                    sheet_name = f'{column}_Sheet'
                    # desired_column_order = ["normal", "LRUDFB", "UD", "LRFB", "LR", "UDFB", "FB", "LRUD"]
                    pd.concat(dfs, axis=1).to_excel(writer, sheet_name=sheet_name, index=False)

# プログラムと同じディレクトリ内にある場合、相対パスで指定
input_folder = '.'
output_folder = 'output_folder'

# CSVファイルからデータをまとめてExcelファイルに書き出し
merge_csv_to_excel(input_folder, output_folder)


#   python combine2excel.py