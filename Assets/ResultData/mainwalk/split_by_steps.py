import pandas as pd
import sys
import numpy as np
import matplotlib.pyplot as plt
from pathlib import Path
from scipy import signal
import warnings

def butter_lowpass(lowcut, fs, order=4):
    '''バターワースローパスフィルタを設計する関数
    '''
    nyq = 0.5 * fs
    low = lowcut / nyq
    b, a = signal.butter(order, low, btype='low')
    return b, a
def butter_lowpass_filter(x, lowcut, fs, order=4):
    '''データにローパスフィルタをかける関数
    '''
    b, a = butter_lowpass(lowcut, fs, order=order)
    y = signal.filtfilt(b, a, x)
    return y
    

# 往路の開始終了時刻
def get_growing_time(dataframe, boundary):
    filtered = dataframe[(abs(dataframe['posZ'] < boundary)) & (abs(dataframe['posZ'].shift(-1)) >= boundary)]
    time_list = filtered['time'].values
    return filtered, time_list
# 復路の開始終了時刻
def get_declining_time(dataframe, boundary):
    filtered = dataframe[(abs(dataframe['posZ'] > boundary)) & (abs(dataframe['posZ'].shift(-1)) <= boundary)]
    time_list = filtered['time'].values
    return filtered, time_list


# リスト内の任意の要素組の差を一定以上にする
def filter_indices(prev_indices:list, dif_min):
    if not prev_indices:
        return []

    # 最初の要素は常に保持する
    filtered_indices = [prev_indices[0]]

    for i in range(1, len(prev_indices)):
        if prev_indices[i] - filtered_indices[-1] >= dif_min:
            filtered_indices.append(prev_indices[i])

    return filtered_indices

# 一歩一歩で区切る
# stepでの区切りとなるインデックスを取得
def split_by_steps(df: pd.core.frame.DataFrame, dif_min:int = 10, add_offset:bool = True):
    # trajectory_z = np.array(dataframe[['time','posZ']])
    # dz = np.gradient(trajectory_z[:, 1])
    # dz = butter_lowpass_filter(dz, 8, 50, order=4)
    # print(df)
    offset = int(df.head(1).index[0])

    # y方向の速度の極性が負から正に反転する時刻を取得
    df['diff'] = df['posY'].diff()
    df = df.dropna()
    df['sign'] = np.sign(df['diff'])
    # sign_change = df['sign'].diff().abs() > 1
    sign_change = (df['sign'].shift(1) < 0) & (df['sign'] > 0)
    # times = df.loc[sign_change, 'time']
    indices = df[sign_change].index.tolist()
    prev_indices = [i - 1 for i in indices]
    prev_indices = filter_indices(prev_indices, dif_min)
    if add_offset == True:
        prev_indices.insert(0, offset)
    # print(prev_indices)

    return prev_indices
    

# データフレームを与えられたインデックスで区切る
def split_dataframe(df: pd.core.frame.DataFrame, indices: list, offset:bool =True):
    dfs = []
    prev_index = 0
    # print(df)
    # print(df.iloc[1])
    offset = df.head(1).index[0]

    for indice in indices:
        index = indice - offset
        if index == 0:
            continue
        dfs.append(df.iloc[prev_index:index+1].reset_index(drop=True))
        prev_index = index + 1    
    # 最後の部分を追加
    if prev_index < len(df):
        dfs.append(df.iloc[prev_index:].reset_index(drop=True))    
    return dfs

# posX, posZ から円柱に対する視線の角度のずれを計算する。ベクトルの内積を利用。
# 元の計測データ中の値は全て正になってしまっているのでまずいことが判明。-180～180でないのがまずい。
def get_yaw(df: pd.core.frame.DataFrame, file: str ="", target: str = ""):
    yaw_data = []
    if "FB" in file:
        # print("FBNOW!!!!!!!!!!")
        trajectory = np.array(df[['posX','posZ']]) # 位置
        num = df['posX'].count()
        frontlist = np.array(df[['forX','forZ']]) # 向いている方向ベクトル

        for i in range(num):
            origin = np.array([trajectory[i,0], trajectory[i,1]]) # 自分の位置
            target = np.array([0, 0]) # 見えている円柱の位置
            origin2target = target - origin # 円柱の相対位置（ベクトル）
            front = np.array([-frontlist[i,0], -frontlist[i,1]]) # 視軸の反転によって向いている方向ベクトルは逆になる
            # ベクトルの内積から角度を求める
            naiseki = np.inner(origin2target, front)
            seki = np.linalg.norm(origin2target) * np.linalg.norm(front)
            c = naiseki / seki
            ang = np.rad2deg(np.arccos(np.clip(c, -1.0, 1.0)))
            if ang > 180:
                ang = 360 - ang
            yaw_data.append(ang)
            # print(ang)
    else:
        trajectory = np.array(df[['posX','posZ']])
        num = df['posX'].count()
        frontlist = np.array(df[['forX','forZ']])

        for i in range(num):
            origin = np.array([trajectory[i,0], trajectory[i,1]])
            target = np.array([0, 8]) 
            origin2target = target - origin
            front = np.array([frontlist[i,0], frontlist[i,1]])
            naiseki = np.inner(origin2target, front)
            seki = np.linalg.norm(origin2target) * np.linalg.norm(front)
            c = naiseki / seki
            ang = np.rad2deg(np.arccos(np.clip(c, -1.0, 1.0)))
            if ang > 180:
                ang = 360 - ang
            yaw_data.append(ang)
            # print(ang)

    return yaw_data
            


def main():
    warnings.filterwarnings('ignore')

    error_text = {}
    # for file in Path('.').glob('*.csv'):
    for i in range(1, len(sys.argv)):
        # CSVファイルの読み込み
        file = sys.argv[i]
        if not file.endswith(".csv"):
            file += ".csv"
        df = pd.read_csv(file)
        if isinstance(file, Path):
            file = file.name

        if "shiroyama" not in file:
            continue


        # 往路復路の区切りとなる時刻をとってくる
        _, time_above_plus = get_growing_time(df, 0.5) # 往路開始
        _, time_above_plus1 = get_growing_time(df, 7.5) # 往路終了
        _, time_below_minus1 = get_declining_time(df, 7.5) # 復路開始
        _, time_below_minus = get_declining_time(df, 0.5) # 復路終了
        # print(time_above_plus)
        # print(time_above_plus1)
        # print(time_below_minus1)
        # print(time_below_minus)
        try:
            # 半往復もできなかったとき
            if len(time_above_plus1) == 0 and len(time_below_minus) == 0:
                time_above_plus = np.append(time_above_plus, 0)
                time_below_minus1 = np.append(time_below_minus1, 0)
                if df['posZ'][len(df['posZ'])-1] - df['posZ'][0] > 0:
                    time_above_plus1 = np.append(time_above_plus1, df['time'][len(df['time'])-1])
                    time_below_minus = np.append(time_below_minus, 0)  
                else:
                    time_above_plus1 = np.append(time_above_plus1, 0)
                    time_below_minus = np.append(time_below_minus, df['time'][len(df['time'])-1])
            # 半往復はしており、かつ2往復目に入れていないとき
            if len(time_above_plus1) == 1 and len(time_above_plus) == 0:
                time_above_plus = np.append(time_above_plus, 0)
                if len(time_below_minus) == 0:
                    time_below_minus = np.append(time_below_minus, df['time'][len(df['time'])-1])
                    if len(time_below_minus1) == 0:
                        time_below_minus1 = np.append(time_below_minus1, df['time'][len(df['time'])-1])
            elif len(time_below_minus) == 1 and len(time_below_minus1) == 0:
                time_below_minus1 = np.append(time_below_minus1, 0)
                if len(time_above_plus1) == 0:
                    time_above_plus1 = np.append(time_above_plus1, df['time'][len(df['time'])-1])
                    if len(time_above_plus) == 0:
                        time_above_plus = np.append(time_above_plus, df['time'][len(df['time'])-1])

            # print(time_above_plus1)
            # print(time_above_plus)
            # 真ん中スタートなので、1往復目の開始タイムは記録されていない
            if (min(time_above_plus1) < min(time_above_plus)):
                time_above_plus = np.insert(time_above_plus, 0, 0.0)
            if (min(time_below_minus1) > min(time_below_minus)):
                time_below_minus1 = np.insert(time_below_minus1, 0, 0.0)
        except ValueError:
            print(f"value error in {file}")
            error_text[file] = [time_above_plus.tolist(), time_above_plus1.tolist(), time_below_minus1.tolist(), time_below_minus.tolist()]
            continue
            sys.exit(1)
        error_text[file] = "now"


        filtered_data_list1 = [df[(df['time'] > x) & (df['time'] <= y)] for (x, y) in zip(time_above_plus, time_above_plus1)]
        filtered_data_list2 = [df[(df['time'] > x) & (df['time'] <= y)] for (x, y) in zip(time_below_minus1, time_below_minus)]


        yaw_std_list1 = []
        yaw_avg_list1 = []
        max = 0
        for index, filtered_data1 in enumerate(filtered_data_list1):
            step_index_list1 = split_by_steps(filtered_data1, 16, True)
            # 一番フレーム数の多いstep間隔の探索
            # for i in range(1, len(step_index_list1)):
            #     dif = step_index_list1[i] - step_index_list1[i-1]
            #     if dif > max:
            #         max = dif
            #         print(f"{index}, {max}")
        max = 40
        # ウィンドウをずらしながらその中での標準偏差を求める（幅は2*max:歩行は2歩で1周期）
        if "FB" in file: # not in にすると円柱から離れる向き
            target_data = filtered_data_list2
        else:
            target_data = filtered_data_list1
        for index, filtered_data1 in enumerate(target_data):
            print(len(filtered_data1))
            if index > 10:
                continue
            if "shiroyama_walk_results_20240204_152314" in file:
                if index == 1:
                    continue
            # print(filtered_data1)
            for i in range(len(filtered_data1) - max):
                windowed_df = filtered_data1.iloc[i:i+2*max] 
                # if i == 0:
                    # print(windowed_df)
                # yaw_list = get_yaw(windowed_df, file)
                # yaw_std = np.std(yaw_list) # これを使った時と結果の値が違う？処理が重いので下を利用
                data = windowed_df['rotY']
                yaw_std = data.std() # 対象データを設定
                yaw_std_list1.append(yaw_std)
                yaw_avg_list1.append(abs(data.mean()))
            yaw_std_list1.append(0)
            yaw_avg_list1.append(0)


        # yaw_std_list1 = []
        # for index, filtered_data1 in enumerate(filtered_data_list1):
        #     # if index != 4:
        #     #     continue
        #     print("going: " + str(index))
        #     # print(type(filtered_data1))
        #     try:
        #         step_index_list1 = split_by_steps(filtered_data1, 16, True)
        #         print(step_index_list1)
        #         split_dfs1 = split_dataframe(filtered_data1, step_index_list1) # stepごとに区切られたdf
        #     except ValueError as e:
        #         print(f"value error happened when going: {e}")
        #         error_text[file] = f"value error happened when going: {e}"
        #         split_dfs1 = []
        #     # print(filtered_data1)
        #     # print(len(split_dfs1))
        #     # print(len(yaw_std_list1))
        #     yaw_list = []
        #     print(len(split_dfs1))
        #     for df1 in split_dfs1[0:]:
        #         # print(df1)
        #         yaw_list = get_yaw(df1, file) # 各stepにおけるyawの抽出
        #         # print(yaw_list)
        #         yaw_std = np.std(yaw_list)
        #         yaw_std_list1.append(yaw_std)
        #         # print(yaw_std)
        #     # yaw_std_list1.append(-1)
            
        #     # print(yaw_std_list1[-10:])

        # yaw_std_list2 = []
        # for index, filtered_data2 in enumerate(filtered_data_list2):
        #     print("coming: " + str(index))
        #     try:
        #         step_index_list2 = split_by_steps(filtered_data2)
        #         split_dfs2 = split_dataframe(filtered_data1, step_index_list2)
        #     except ValueError as e:
        #         print(f"value error happened when going: {e}")
        #         error_text[file] = f"value error happened when going: {e}"
        #         split_dfs2 = [] # 各stepにおけるyawの抽出
        #     for df2 in split_dfs2:
        #         yaw_list = get_yaw(df2, file)
        #         yaw_std = np.std(yaw_list)
        #         yaw_std_list2.append(yaw_std)
        #     yaw_std_list2.append(-1)
        
        # step_index = list(range(len(yaw_std_list1)))
        # plt.plot(step_index, yaw_std_list1, marker='o', linestyle='None')
        frame = [i for i in range(len(yaw_std_list1))]
        plt.plot(frame, yaw_std_list1, marker='o', linestyle='None', markersize=2)
        plt.plot(frame, yaw_avg_list1, marker='o', linestyle='None', markersize=1)
        # plt.ylim(0, 16)
        plt.xlabel('Window index')
        plt.ylabel('Yaw Std')
        # plt.title('Yaw Std transaction')
        plt.grid(True)
        plt.show()
        
        # 相関係数の計算
        s1 = pd.Series(yaw_std_list1)
        s2 = pd.Series(yaw_avg_list1)
        res = s1.corr(s2)
        print(res)




if __name__ == "__main__":
    main()


#   python split_by_steps.py shiroyama_walk_results_20240204_152314