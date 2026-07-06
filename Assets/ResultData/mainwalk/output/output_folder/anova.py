import pandas as pd
from scipy.stats import f
from scipy.stats import f_oneway
import math
from pathlib import Path
import statistics

def anova(df, significance_level=0.05):
    mean_list = []
    sum = 0
    count = 0
    inner_square_sum = 0
    # print(df)
    for col in df.columns:
        mean = df[col].mean() # 群ごとの平均
        mean_list.append(mean)
        sum += df[col].mean() * df[col].count() # 群ごとの合計を加える
        count += df[col].count()
        for i in df[col]:
            if math.isnan(i):
                continue
            inner_square_sum = inner_square_sum + (i - mean)**2 # 偏差の二乗和
    entire_mean = sum / count
    entire_square_sum = 0
    for col in df.columns:
        for i in df[col]:
            # print(f"i: {i}")
            if math.isnan(i):
                print("nan detected")
                continue
            entire_square_sum += (i - entire_mean)**2
            # print(entire_square_sum)
    among_square_sum = entire_square_sum - inner_square_sum

    among_freedom = len(df.columns) - 1
    entire_freedom = count - 1
    inner_freedom = entire_freedom - among_freedom

    among_var = among_square_sum / among_freedom
    inner_var = inner_square_sum / inner_freedom
    var_ratio = among_var / inner_var

    f_value_border = f.ppf(1-significance_level, among_freedom, entire_freedom)
    p_value = f.sf(var_ratio, among_freedom, entire_freedom)

    if len(df.columns) > 1:
        correct = f_oneway(*[df[col] for col in df.columns])
    else:
        correct = -1
    list1 = [[among_square_sum, among_freedom, among_var, var_ratio, p_value, f_value_border], 
            [inner_square_sum, inner_freedom, inner_var, None, None, None], 
            [entire_square_sum, entire_freedom, None, None, None, correct]
             ]
    # print(list1)
    index1 = ["グループ間", "グループ内", "全体"]
    columns1 = ["平方和", "自由度", "分散", "分散比", "P値", "F境界値,参考値"]
    resultdf = pd.DataFrame(data = list1, index = index1, columns = columns1)
    return resultdf


def turkey_kramer(df, significance_level=0.05):
    mean_list = []
    var_list = []
    count_list = []
    # print(df)
    for col in df.columns:
        mean = df[col].mean() # 群ごとの平均
        mean_list.append(mean)
        var = df[col].var()
        var_list.append(var)
        count_list.append(df[col].count())
    # mean_dif_list = []
    qtk_list = []
    for i in range(len(mean_list)):
        for j in range(i+1, len(mean_list)):
            qtk_list.append(abs(mean_list[i] - mean_list[j]) / math.sqrt(statistics.mean(var_list) * ((1 / count_list[i]) + (1 / count_list[j])) / 2)) ####/2
    # print(mean_list)
    # print(qtk_list)

    list1 = []
    if significance_level == 0.05:
        studentized = pd.read_csv("005_studentized_range.csv", header=None).values.tolist()
    elif significance_level == 0.01:
        studentized = pd.read_csv("001_studentized_range.csv", header=None).values.tolist()
    # print(len(studentized))
    border = 0
    target = sum(count_list) - len(df.columns)
    # print(sum(count_list))
    # print(len(df.columns))
    # print(target)
    for i in range(1, len(studentized)):
        # print(studentized[i][0])
        if target == studentized[i][0]:
            # print("hit")
            border = studentized[i][len(df.columns)]
            break
        if target < studentized[i][0]:
            smaller_than_a_max = studentized[i-1][0]
            larger_than_a_min = studentized[i][0]
            # print(f"{smaller_than_a_max}: {studentized[i-1][len(df.columns)]}")
            # print(f"{larger_than_a_min}: {studentized[i][len(df.columns)]}")
            calcA = (1 / target - 1 / larger_than_a_min) / (1 / smaller_than_a_max - 1 / larger_than_a_min) * studentized[i-1][len(df.columns)]
            calcB = (1 / smaller_than_a_max - 1 / target) / (1 / smaller_than_a_max - 1 / larger_than_a_min) * studentized[i][len(df.columns)]
            # print(calcA)
            # print(calcB)
            border = calcA + calcB
            break
    # if border == 0:
    #     smaller_than_a_max = max(num for num in studentized[:][0] if num < target)
    #     larger_than_a_min = min(num for num in studentized[:][0] if num > target)
    #     calcA = (1 / target - 1 / larger_than_a_min) / (1 / smaller_than_a_max - 1 / larger_than_a_min) * studentized[smaller_than_a_max][1]
    #     calcB = (1 / smaller_than_a_max - 1 / target) / (1 / smaller_than_a_max - 1 / larger_than_a_min) * studentized[larger_than_a_min][1]
    #     border = calcA + calcB

    for i in range(len(qtk_list)):
        list1.append([qtk_list[i], border, qtk_list[i] - border])
    print(list1)

    index1 = []
    columns1 = ["qtk", "border", "difference"]
    for i in range(len(df.columns)):
        for j in range(i+1, len(df.columns)):
            index1.append(f"{df.columns.values[i]}-{df.columns.values[j]}")
    resultdf = pd.DataFrame(data = list1, index = index1, columns = columns1)
    return resultdf


def main():
    # for excel_file in Path('.').glob('*.xlsx'):
        excel_file = "coming_shiroyama_output.xlsx"
        if not excel_file.endswith(".xlsx"):
            excel_file += ".xlsx"
        sheets = pd.read_excel(excel_file, sheet_name=None)
        if isinstance(excel_file, Path):
            excel_file = excel_file.name
        output_file = f"analyzed/anova_{excel_file}"
        # writer = pd.ExcelWriter(output_file)

        with pd.ExcelWriter(output_file) as writer:

            for sheet_name, data in sheets.items():
                print("Processing sheet:", sheet_name)
                data = data[1:]
                # data["normal"][1] = None

                # anova_analysis(data, sheet_name, writer)
                result_anova = anova(data, 0.05)
                result_anova.to_excel(writer, sheet_name="anova_"+sheet_name)
                result_turkey = turkey_kramer(data, 0.05)
                result_turkey.to_excel(writer, sheet_name="turkey_"+sheet_name)

        # writer.save()

if __name__ == "__main__":
    main()
