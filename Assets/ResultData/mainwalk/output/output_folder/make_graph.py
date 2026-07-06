import pandas as pd
from scipy import stats
import math
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
        # if "0" in excel_file.name:
        #     continue
        # if "shiroyama" not in excel_file.name:
        #     continue
        excel_file = "coming_yokoyama_output.xlsx"
        if not excel_file.endswith(".xlsx"):
            excel_file += ".xlsx"
        sheets = pd.read_excel(excel_file, sheet_name=None)
        # if isinstance(excel_file, Path):
        #     excel_file = excel_file.name
        excel_file = os.path.splitext(os.path.basename(excel_file))[0]
        print(excel_file)


        for sheet_name, data in sheets.items():
            print(data.columns)
            print("Processing sheet:", sheet_name)
            if "whole" not in excel_file:
                data = data[1:]
                data["normal"][1] = None
            # mean_list = []
            # std_list = []
            # err_list = []
            # for col in data.columns:
            #     mean_list.append(data[col].mean())
            #     std_list.append(data[col].std())
            #     err_list.append(get_95(data[col]))
            # print(err_list)
            
            # x = data.columns.tolist()
            # y = mean_list 
            # plt.bar(x, y, width = 0.6, yerr=std_list, capsize=10)
            # plt.bar(x, y, width = 0.6, yerr=err_list, capsize=10)
            data.boxplot(showmeans=True, grid=False, whis=20)
            # sns.boxplot(data=data, showmeans=True)
            # sns.stripplot(data=data, jitter=True, color='black')
            # boxplot_annotate_brackets([(1, 3, "*"), (2, 3, "**")], data.values, fs=28)
            plt.xlabel("condition")
            plt.ylabel(sheet_name[0:4])
            plt.savefig(f"analyzed/graphs/graph_{sheet_name}_{excel_file}_long.png")
            print(f"analyzed/graphs/graph_{sheet_name}_{excel_file}_long.png")
            # if ("rmse" in sheet_name):
            # #     # print([data[col].tolist() for col in data.columns])
            # #     # data.plot.box(showmeans=True)
            #     plt.show()
            plt.close()


if __name__ == "__main__":
    main()