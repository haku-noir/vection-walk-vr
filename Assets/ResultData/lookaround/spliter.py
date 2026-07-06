import sys

def split_csv(input_filename, output_prefix):
    with open(input_filename, 'r') as file:
        lines = file.readlines()

    # 空行で区切って行グループを作成
    line_groups = []
    current_group = []
    # print(lines)
    for line in lines:
        print(line)
        if line.strip():  # 行が空でない場合
            current_group.append(line)
            # print("mix")
        else:  # 行が空の場合
            # print("wow")
            if current_group:
                line_groups.append(current_group)
                current_group = []
                # print(line_groups)
    if current_group:
                line_groups.append(current_group)
                current_group = []

    # グループごとに新しいCSVファイルを書き込む
    print(len(line_groups))
    for i, group in enumerate(line_groups, start=1):
        # if i == 1:
        #     continue
        output_filename = f"{output_prefix}_{i}.csv"
        print(output_filename)
        with open(output_filename, 'w') as output_file:
            output_file.writelines(group)
            print("write")

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Add file name!")
    for i in range(1, len(sys.argv)):
        input_file = sys.argv[i] 
        if not input_file.endswith(".csv"):
            input_file += ".csv"
        output_prefix = "output_file/extracted_" + input_file
        split_csv(input_file, output_prefix)


#   python spliter.py 