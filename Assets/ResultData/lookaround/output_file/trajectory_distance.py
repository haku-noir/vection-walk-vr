import pandas as pd
import sys
import matplotlib.pyplot as plt

# CSVファイルからデータを読み込む
if len(sys.argv) > 4 or len(sys.argv) < 2:
    print("usage: python trajectory_distance.py file_name end_row_num start_row_num")
rot_file = sys.argv[1]
if not rot_file.endswith(".csv"):
    rot_file += ".csv"

start_row = 0
df1 = pd.read_csv(rot_file)
if len(sys.argv) > 2:
    end_row = sys.argv[2]
if len(sys.argv) > 3:
    start_row = sys.argv[3]

all_distances = ((df1['rotY'] - df1['rotY'].shift())**2 + (df1['rotX'] - df1['rotX'].shift())**2)**0.5
if len(sys.argv) > 2:
    end_row = sys.argv[2]
    all_total_distance = all_distances[int(start_row):int(end_row)].sum()
else:
    all_total_distance = all_distances.sum()
print(f'Total distance traveled by the trajectory: {all_total_distance}')


#   python trajectory_distance.py 