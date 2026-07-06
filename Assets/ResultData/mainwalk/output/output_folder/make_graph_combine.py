import pandas as pd
from scipy import stats
import math
import sys
from pathlib import Path
import matplotlib.pyplot as plt
import seaborn as sns
import os
from vistats import boxplot_annotate_brackets
from vistats.util import ttest_result2asterisk_tuples, tukeyhsd_result2asterisk_tuples

def get_95(data):
    a = 0.95 #信頼水準
    d = data.count()-1 #自由度
    m = data.mean() #標本平均
    s = data.sem() #標準誤差
    print(d)
    print(m)
    print(s)
    print(data)
    target_95per_section = stats.norm.interval(confidence= a, loc = m, scale =s)
    print(target_95per_section)
    target_95per = (target_95per_section[1] - target_95per_section[0])/2
    return target_95per 


def main():
    # for excel_file in Path('.').glob('*.xlsx'):
    #     excel_file = "wholegoing_shiroyama_output.xlsx"
    #     if not excel_file.endswith(".xlsx"):
    #         excel_file += ".xlsx"
        # if "0" in excel_file.name:
        #     continue
        # if "shiroyama" not in excel_file.name:
        #     continue
        
        go_file = sys.argv[1]
        if not go_file.endswith(".xlsx"):
            go_file += ".xlsx"
        come_file = sys.argv[2]
        if not come_file.endswith(".xlsx"):
            come_file += ".xlsx"
        go_sheets = pd.read_excel(go_file, sheet_name=None)
        come_sheets = pd.read_excel(come_file, sheet_name=None)
        # sheets = pd.read_excel(excel_file, sheet_name=None)
        # if isinstance(excel_file, Path):
        #     excel_file = excel_file.name
        # excel_file = os.path.splitext(os.path.basename(excel_file))[0]
        # print(excel_file)


        for ((sheet_name1, data1), (sheet_name2, data2)) in zip(go_sheets.items(), come_sheets.items()):
            # print(data.columns)
            print("Processing sheet:", sheet_name1)
            # if "whole" not in go_sheets:
            #     data1 = data1[1:]
            #     data1["normal"][1] = None
            #     data2 = data2[1:]
            #     data2["normal"][1]

            x = data1.columns.tolist()
            x1 = [1, 2, 3, 4, 5, 6, 7, 8]
            x2 = [1.3, 2.3, 3.3, 4.3, 5.3, 6.3, 7.3, 8.3]
            print(data1.iloc[0])
            data1_list = data1.iloc[0].tolist()
            data2_list = data2.iloc[0].tolist()

            # コマンドライン引数の最後に1を指定した場合，円柱への接近・後退で分類
            flag = ""
            if len(sys.argv) > 3:
                if sys.argv[3] == "1":
                    print("arrange data")
                    flag = "1"
                    fb_list = []
                    for index, e in enumerate(x):
                        if "FB" in e:
                            fb_list.append(index)
                    for i in fb_list:
                        tmp = data1_list[i]
                        data1_list[i] = data2_list[i]
                        data2_list[i] = tmp
            
            if flag == "":
                plt.bar(x1, data1_list, width=0.3, label="forward", align="center")
                plt.bar(x2, data2_list, width=0.3, label="backward", align="center")
            elif flag == "1":
                plt.bar(x1, data1_list, width=0.3, label="approach", align="center")
                plt.bar(x2, data2_list, width=0.3, label="leave", align="center")
            else: # 本来通らないはずの例外処理
                print("flag error!")
                plt.close()
                return

            plt.xlabel("condition")
            # plt.ylabel(sheet_name1[0:4])
            plt.legend(loc=2)
            plt.xticks([1.15, 2.15, 3.15, 4.15, 5.15, 6.15 ,7.15 ,8.15], x)
            plt.savefig(f"analyzed/graphs/graph{flag}_{sheet_name1}_{go_file}_{come_file}.png")
            # if ("rmse" in sheet_name1):
            #     # print([data[col].tolist() for col in data.columns])
            #     # data.plot.box(showmeans=True)
                # plt.show()
            plt.close()


if __name__ == "__main__":
    main()


#   python make_graph_combine.py wholegoing_shiroyama_output.xlsx wholecoming_shiroyama_output.xlsx