import pandas as pd
import sys
import matplotlib.pyplot as plt

# CSVファイルからデータを読み込む
if len(sys.argv) != 3:
    print("Add file name!")
rot_file = sys.argv[2]
count_file = sys.argv[1]
if not rot_file.endswith(".csv"):
    rot_file += ".csv"
if not count_file.endswith(".csv"):
    count_file += ".csv"
df1 = pd.read_csv(rot_file)
df2 = pd.read_csv(count_file)

# print("Enter the InteractionNumber")
# n = input()
n = 10

for k in range(max(int(n)-1,0), int(n)+1):
    # df2からInteractionTimeが切り替わる時間を取得
    interaction_num_start_list = [df2.iloc[i,1] for i in range(0,len(df2['InteractionNumber']))]
    if k == "0":
        start_time = 0
    else:
        start_time = interaction_num_start_list[int(k)-1]
    end_time = interaction_num_start_list[int(k)]

    # ElapsedTimeが上記の範囲内のデータを抽出
    trajectory_data = df1[(df1['time'] >= start_time) & (df1['time'] <= end_time)][['rotX', 'rotY']]
    # print(trajectory_data)

    # グラフを描画
    plt.plot(trajectory_data['rotY'], trajectory_data['rotX'], label='Trajectory')

if n == "0":
    plt.scatter(0, 0, color='red', label='Start')  # 最初のデータポイントに赤い目印
else:
    plt.scatter(trajectory_data['rotY'].iloc[0], trajectory_data['rotX'].iloc[0], color='red', label='Start')  # 最初のデータポイントに赤い目印
plt.scatter(trajectory_data['rotY'].iloc[-1], trajectory_data['rotX'].iloc[-1], color='blue', label='End')  # 最後のデータポイントに青い目印

plt.xlabel('rotY')
plt.ylabel('rotX')
plt.title(f'Trajectory of (rotX, rotY) when InteractionTime is {n}')
plt.legend()

interaction_num_start_list = [df2.iloc[i,1] for i in range(0,len(df2['InteractionNumber']))]
start_time = 0
end_time = interaction_num_start_list[49]
trajectory_data = df1[(df1['time'] >= start_time) & (df1['time'] <= end_time)][['rotX', 'rotY']]
# print(df1['time'])
# print(trajectory_data)

# # 軌跡が描く距離を計算
distances = ((trajectory_data['rotY'] - trajectory_data['rotY'].shift())**2 + (trajectory_data['rotX'] - trajectory_data['rotX'].shift())**2)**0.5
total_distance = distances.sum()
all_distances = ((df1['rotY'] - df1['rotY'].shift())**2 + (df1['rotX'] - df1['rotX'].shift())**2)**0.5
all_total_distance = all_distances.sum()
print(f'Total distance traveled by the trajectory: {total_distance} / {all_total_distance}')

# plt.show()


#   python rotXrotYtrajectory.py 