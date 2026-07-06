import pandas as pd
import openpyxl
from pathlib import Path

# for excel_file_path in Path('.').glob('*.xlsx'):
#     # Excelファイルのパス
excel_file_path = "wholegoing_shiroyama_output.xlsx"

# 列の順序を指定
desired_column_order = ["normal", "LRUDFB", "UD", "LRFB", "LR", "UDFB", "FB", "LRUD"]

# Excelファイルを読み込む
excel_data = pd.read_excel(excel_file_path, sheet_name=None)

# 各シートについて処理を行う
for sheet_name, sheet_data in excel_data.items():
    try:
        print(sheet_name)
        print(sheet_data)
        # 列の順序を変更
        print(type(sheet_data))
        print(sheet_data.columns)
        sheet_data.reindex(columns=["normal", "LRUDFB", "UD", "LRFB", "LR", "UDFB", "FB", "LRUD"])
        print(sheet_data)
        
        # 変更したデータをExcelファイルに書き込む
        with pd.ExcelWriter("ordered"+excel_file_path, engine="xlsxwriter") as writer:
            # writer.book = openpyxl.load_workbook(excel_file_path)
            sheet_data.to_excel(writer, sheet_name="ordered"+sheet_name, index=False)
            
            print("列の順序を変更しました。")

    except KeyError as e:
        print(e)

# input_folder = '.'
