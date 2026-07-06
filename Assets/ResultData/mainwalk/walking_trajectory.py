import pandas as pd
import math
import sys
import os
import numpy as np
import matplotlib.pyplot as plt
from matplotlib.patches import Circle, Ellipse
from matplotlib import collections
from sklearn.metrics import mean_squared_error
from scipy import interpolate
from pathlib import Path
import json
from scipy import signal

# ローパスフィルタを適用する関数
def lowpass_filter(data, window_size):
    window = np.ones(window_size) / window_size
    smoothed_data = np.convolve(data, window, mode='same')
    return smoothed_data

def  LPF_CF(times,x,fmax):
    freq_X = np.fft.fftfreq(times.shape[0],times[1] - times[0])
    X_F = np.fft.fft(x)
    X_F[freq_X>fmax] = 0
    X_F[freq_X<-fmax] = 0
#     虚数は削除
    x_CF  = np.fft.ifft(X_F).real    
    return x_CF


def spline1(x,y,point):
    f = interpolate.interp1d(x, y,kind="cubic") 
    X = np.linspace(x[0],x[-1],num=point,endpoint=True)
    Y = f(X)
    return X,Y

def spline3(x,y,point,deg):
    tck,u = interpolate.splprep([x,y],k=deg,s=0) 
    u = np.linspace(0,1,num=point,endpoint=True) 
    spline = interpolate.splev(u,tck)
    return spline[0],spline[1]


def CircleFitting(x, y):
    """Circle Fitting with least squared
        input: point x-y positions  

        output  cxe x center position
                cye y center position
                re  radius of circle 

    """

    sumx = sum(x)
    sumy = sum(y)
    sumx2 = sum([ix ** 2 for ix in x])
    sumy2 = sum([iy ** 2 for iy in y])
    sumxy = sum([ix * iy for (ix, iy) in zip(x, y)])

    F = np.array([[sumx2, sumxy, sumx],
                  [sumxy, sumy2, sumy],
                  [sumx, sumy, len(x)]])

    G = np.array([[-sum([ix ** 3 + ix * iy ** 2 for (ix, iy) in zip(x, y)])],
                  [-sum([ix ** 2 * iy + iy ** 3 for (ix, iy) in zip(x, y)])],
                  [-sum([ix ** 2 + iy ** 2 for (ix, iy) in zip(x, y)])]])

    try:
        T = np.linalg.inv(F).dot(G)
    except np.linalg.LinAlgError:
        return 0, 0, float("inf")

    cxe = float(T[0] / -2)
    cye = float(T[1] / -2)

    try:
        re = math.sqrt(abs(cxe ** 2 + cye ** 2 - T[2]))
    except np.linalg.LinAlgError:
        return cxe, cye, float("inf")
    return cxe, cye, re

def calc_curvature_circle_fitting(x, y, npo=1):
    """
    x,y: x-y position list
    npo: the number of points using Calculation curvature
    ex) npo=1: using 3 point
        npo=2: using 5 point
        npo=3: using 7 point
    """

    cv_list = []
    cxe_list = []
    cye_list = []
    n_data = len(x)

    for i in range(n_data):
        lind = i - npo
        hind = i + npo + 1

        if lind < 0:
            lind = 0
        if hind >= n_data:
            hind = n_data

        xs = x[lind:hind]
        ys = y[lind:hind]
        # xs, ys = spline1(xs, ys, 10*len(xs))
        xs, ys = spline3(ys, xs, 10*len(xs), 2)
        (cxe, cye, re) = CircleFitting(xs, ys)
        cxe_list.append(cxe)
        cye_list.append(cye)

        if len(xs) >= 3:
            # sign evaluation
            c_index = int((len(xs) - 1) / 2.0)
            sign = (xs[0] - xs[c_index]) * (ys[-1] - ys[c_index]) - (ys[0] - ys[c_index]) * (xs[-1] - xs[c_index])

            # check straight line
            a = np.array([xs[0] - xs[c_index], ys[0] - ys[c_index]])
            b = np.array([xs[-1] - xs[c_index], ys[-1] - ys[c_index]])
            theta = math.degrees(math.acos(np.dot(a, b) / (np.linalg.norm(a) * np.linalg.norm(b))))

            if theta == 180.0:
                cv_list.append(0.0)  # straight line
            elif sign > 0:
                cv_list.append(1.0 / -re)
            else:
                cv_list.append(1.0 / re)
        else:
            cv_list.append(0.0)

    return cv_list, cxe_list, cye_list


def draw_fit_circle(xlist, ylist, npo=1):
    cv_list, cxe_list, cye_list = calc_curvature_circle_fitting(xlist, ylist, npo)
    for index, (cv, cxe, cye) in enumerate(zip(cv_list, cxe_list, cye_list)):
        if index % 10000 == 0:
            if cv == 0:
                re = 0
            else:
                re = 1 / cv
            plt.figure()
            #円描画
            theta=np.arange(0,2*math.pi,0.1)
            xe=[]
            ye=[]
            for itheta in theta:
                xe.append(re*math.cos(itheta)+cxe)
                ye.append(re*math.sin(itheta)+cye)
            xe.append(xe[0])
            ye.append(ye[0])

            plt.plot(xlist,ylist,"ob")
            plt.plot(xe,ye,"-r")
    # plt.plot(cx,cy,"xb")
    plt.axis("equal")
    plt.grid(True)
    plt.show()


def get_curvatures(xlist, ylist):
    curvatures = calc_curvature_circle_fitting(xlist, ylist, npo=3)
    # curvatures = draw_fit_circle(xlist, ylist, npo = 3)
    # curvatures = calc_curvature_with_yaw_diff(xlist, ylist, )
    return curvatures


# 一歩一歩で区切る
def split_by_steps(df):
    # trajectory_z = np.array(dataframe[['time','posZ']])
    # dz = np.gradient(trajectory_z[:, 1])
    # dz = butter_lowpass_filter(dz, 8, 50, order=4)

    # z方向の速度の極性が反転する時刻を取得
    df['diff'] = df['posY'].diff()
    df = df.dropna()
    df['sign'] = np.sign(df['diff'])
    sign_change = df['sign'].diff().abs() > 1
    # times = df.loc[sign_change, 'time']
    indices = df[sign_change].index.tolist()

    return indices

def split_dataframe(df, indices):
    dfs = []
    prev_index = 0

    for index in indices:
        dfs.append(df.iloc[prev_index:index+1].reset_index(drop=True))
        prev_index = index + 1    
    # 最後の部分を追加
    if prev_index < len(df):
        dfs.append(df.iloc[prev_index:].reset_index(drop=True))
    
    return dfs


# 結果の格納されたリストを返す
def get_result(dataframe, file=""):
    trajectory = np.array(dataframe[['posX','posZ']])
    num = dataframe['posX'].count()
    # trajectory = np.array(dataframe[['posX','posZ','time']])
    distance = np.linalg.norm(np.diff(trajectory, axis=0), axis=1).sum()
    rmse_posX = np.sqrt(mean_squared_error(dataframe['posX'], np.zeros(len(dataframe))))
    # print(len(dataframe['posX']))
    print(f'Trajectory Distance: {distance}')
    print(f'RMSE for posX: {rmse_posX}')

    # curvatures = get_curvatures(trajectory[:,0], trajectory[:,1])
    # curvatures = get_curvatures(lowpass_filter(trajectory[:,0], 10), lowpass_filter(trajectory[:,1], 10))
    # curvatures = get_curvatures(LPF_CF(trajectory[:,2], trajectory[:,0], 10), LPF_CF(trajectory[:,2], trajectory[:,1], 10))
    dx = np.gradient(trajectory[:, 0])
    dy = np.diff(trajectory[:, 1])
    dx = butter_lowpass_filter(dx, 8, 50, order=4)
    # dx = lowpass_filter(np.gradient(trajectory[:, 0]), 10)
    # dy = lowpass_filter(np.gradient(trajectory[:, 1]), 10)
    # d2x = lowpass_filter(np.gradient(dx), 20)
    # d2y = lowpass_filter(np.gradient(dy), 20)
    # curvatures = (dx * d2y - dy * d2x) / (dx**2 + dy**2)**(3/2)
    # cv_sign_changes = np.sum(np.diff(np.sign(curvatures)) != 0)
    # print(f"cv sign change: {cv_sign_changes}")
    dx_sign_changes = np.sum(np.diff(np.sign(dx)) != 0)
    print(f"dx sign change: {dx_sign_changes}")
    # d2x_sign_changes = np.sum(np.diff(np.sign(d2x)) != 0)
    # print(f"d2x sign change: {d2x_sign_changes}")
    # dy_sign_changes = np.sum(np.diff(np.sign(dy)) != 0)
    # print(f"dy sign change: {dy_sign_changes}")
    print(f"std: {np.std(dx)}")

    miss_data = np.array(dataframe[['angle','miss','out','outf']])

    if "FB" in file:
        # print("FBNOW!!!!!!!!!!")
        count = 0
        frontlist = np.array(dataframe[['forX','forZ']])

        for i in range(num):
            origin = np.array([trajectory[i,0], trajectory[i,1]])
            target = np.array([0, 0])                 # (0,0)じゃないか？？？？？？？？？？？？？？？？？？？？？
            origin2target = target - origin
            front = np.array([-frontlist[i,0], -frontlist[i,1]])
            naiseki = np.inner(origin2target, front)
            seki = np.linalg.norm(origin2target) * np.linalg.norm(front)
            c = naiseki / seki
            ang = np.rad2deg(np.arccos(np.clip(c, -1.0, 1.0)))
            miss_data[i][0] = ang
            # print(ang)
            
            if i > 0 and abs(ang) > 5:
                miss_data[i,3] = miss_data[i-1, 3] + 1
                if i > 9 and miss_data[i-10, 3] + 8 <= miss_data[i-1, 3] and miss_data[i-10, 3] == miss_data[i-9, 3]:
                    miss_data[i,2] = miss_data[i-1, 2] + 1
                    # count += 1
                    # print(f"{miss_data[i,2]}, {miss_data[i-1,2]}")
                else:
                    miss_data[i,2] = miss_data[i-1, 2]
            else:
                miss_data[i,3] = miss_data[i-1, 3]
                miss_data[i,2] = miss_data[i-1, 2]
            # print(miss_data[i,3])
            # print(f"{miss_data[i,2]}, {miss_data[i-1,2]}, {count}")

    angle_mean = np.mean(miss_data[:, 0])
    angle_std = np.std(miss_data[:, 0])
    not_on_line_count = int(miss_data[-1, 1] - miss_data[0, 1])
    not_looking_count = int(miss_data[-1, 2] - miss_data[0, 2])
    not_looking_frame = int(miss_data[-1, 3] - miss_data[0, 3])

    result_list = [distance, rmse_posX, dx_sign_changes, np.std(dx), angle_mean, angle_std, not_on_line_count, not_looking_count, not_looking_frame, num]
    # plt.plot(np.arange(0, len(dx)), dx, label='dx')
    # plt.plot(np.arange(0, len(dx)), dy, label='dy')
    # plt.legend()
    # plt.show()
    # plt.plot(np.arange(0, len(d2x)), d2x, label='d2x')
    # plt.plot(np.arange(0, len(d2x)), d2y, label='d2y')
    # plt.legend()
    # plt.show()
    return result_list

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

def draw_graph(dataframe, filename="", save_file_identifier = 1, save_directory = "output/graphs/"):
    
    trajectory = np.array(dataframe[['posX','posZ']])
    
    dx = np.gradient(trajectory[:, 0])
    dy = np.gradient(trajectory[:, 1])
    
    plt.plot(np.arange(0, len(dx)), dataframe['posX'], label='左右方向')
    plt.xlabel('時間[frame]', fontname="MS Gothic")
    plt.ylabel('x座標[m]', fontname="MS Gothic")
    plt.legend(prop={"family":"MS Gothic"})
    plt.yticks(np.arange(-1, 1.1, 1))
    plt.savefig(os.path.join(save_directory, f"Xco/coordinate{save_file_identifier}_{filename}.png"))
    # plt.show()
    plt.close()

    plt.plot(np.arange(0, len(dy)), dataframe['posZ'], label='前後方向')
    plt.xlabel('時間[frame]', fontname="MS Gothic")
    plt.ylabel('y座標[m]', fontname="MS Gothic")
    plt.legend(prop={"family":"MS Gothic"})
    plt.yticks(np.arange(0, 8.1, 1))
    plt.savefig(os.path.join(save_directory, f"Yco/coordinate{save_file_identifier}_{filename}.png"))
    # plt.show()
    plt.close()

    plt.plot(np.arange(0, len(dx)), butter_lowpass_filter(dx, 8, 50, order=4), label='左右方向')
    plt.plot(np.arange(0, len(dy)), butter_lowpass_filter(dy, 8, 50, order=4), label='前後方向')
    plt.xlabel('時間[frame]', fontname="MS Gothic")
    plt.ylabel('速度[m/frame]', fontname="MS Gothic")
    plt.legend(prop={"family":"MS Gothic"})
    plt.yticks(np.arange(-0.020, 0.021, 0.010))
    plt.ylim(-0.025, 0.025)
    plt.savefig(os.path.join(save_directory, f"vel/velocity{save_file_identifier}_{filename}.png"))
    # plt.show()
    plt.close()


# リストの要素数分だけのリスト要素をもったリストを作成
def make_list_of_list(list):
    big_list = []
    for i in range(len(list)):
        big_list.append([])
    return big_list


# 軌跡および正面方向を表す矢印を描画
def draw_trajectory(dataframe, filename="", save_file_identifier = 1, save_directory = "output/graphs/"):
    fig, ax = plt.subplots()
    ax.plot(dataframe['posX'], dataframe['posZ'])
    
    line_ends_list = []
    for index, row in dataframe.iterrows():
        if index % 10 == 0:
            start_point = np.array([row['posX'], row['posZ']])
            vector = np.array([row['forX'], row['forZ']])
            if index % 20 == 0:
                # ベクトルをプロット
                plt.quiver(*start_point, *vector, angles='xy', scale_units='xy', scale=1, width=0.005, headwidth=4, headlength=5, color='red')
            if "FB" in filename:
                # print("FB")
                line_ends_list.append([(start_point[0], start_point[1]), (start_point[0] - 10 * vector[0], start_point[1] - 10 * vector[1])])
            else:
                line_ends_list.append([(start_point[0], start_point[1]), (start_point[0] + 10 * vector[0], start_point[1] + 10 * vector[1])])
    line_collection = collections.LineCollection(line_ends_list, color = "red", linewidth=0.2)
    ax.add_collection(line_collection)

    C1 = Circle(xy = (0, 0), radius = 0.15, color = "green", alpha = 0.5)
    C2 = Circle(xy = (0, 8), radius = 0.15, color = "green", alpha = 0.5)
    ax.add_patch(C1)
    ax.add_patch(C2)

    # グラフの装飾
    plt.xlabel('左右方向の座標[m]', fontname="MS Gothic")
    plt.ylabel('前後方向（進行方向）の座標[m]', fontname="MS Gothic")
    # plt.axis('equal')
    plt.xticks(np.arange(-1, 1.1, 1))
    plt.yticks(np.arange(0, 8.1, 1))
    plt.minorticks_on()

    plt.title('歩行軌跡と視線方向', fontname="MS Gothic")
    # plt.legend()
    plt.grid(True)
    os.makedirs(save_directory, exist_ok=True)
    # print(filename)
    plt.savefig(os.path.join(save_directory, f"orbit/trajectory{save_file_identifier}_{filename}.png"))
    plt.close()
    # plt.show()


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

# 2次元リスト内の各リストの合計をとる。標準偏差などルートがとられているものは、別で計算する。
def sum_lists(results_list, root_index_list=[], count_index=None, start_trial=0):
    if not results_list:  # リストが空の場合は空のリストを返す
        return []

    # 新しいリストを作成し、要素の初期値を0とする
    summed_results = [0] * len(results_list)
    # print(len(results_list))

    # print(f"results: {results_list}")
    # 各要素の同じインデックスの要素を加算
    for index, sublist in enumerate(results_list):
        # 標準偏差が保存されているものは、２乗した後にサンプル数をかけて足し合わせることで、偏差の２乗和となる
        if index in root_index_list and count_index != None:
            if start_trial > len(sublist):
                summed_results[index] = 0
            else:
                for trial in range(start_trial, len(sublist)):
                    # print(len(sublist))
                    # print(results_list)
                    # print(sublist)
                    summed_results[index] += (sublist[trial]) ** 2 * results_list[count_index][trial]
                    # print(summed_results)
        # print(type(sublist[index]))
        else:
            summed_results[index] = sum(sublist[start_trial:])

    return summed_results


def main():
    error_text = {}
    for file in Path('.').glob('*.csv'):
    # for i in range(1, len(sys.argv)):
    #     # CSVファイルの読み込み
    #     file = sys.argv[i]
    #     if not file.endswith(".csv"):
    #         file += ".csv"
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


        # 結果を取得
        print("whole")
        whole_result_list = get_result(df, file)
        for index, filtered_data1 in enumerate(filtered_data_list1):
            print("going: " + str(index))
            # result_list = [distance, rmse_posX, sign_changes, np.std(dx), angle_mean, angle_std, not_on_line_count, not_looking_count, not_looking_frame, num]
            try:
                result1_list = get_result(filtered_data1, file)            
            except ValueError as e:
                print(f"value error happened when going: {e}")
                error_text[file] = f"value error happened when going: {e}"
                result1_list = [-1, -1, -1, -1, -1, -1, -1, -1, -1, -1]

            if index == 0:
                results1_list = make_list_of_list(result1_list)
            # print(result1_list)
            # print(results1_list)
            for i in range(0, len(results1_list)):
                results1_list[i].append(result1_list[i])
                
            # グラフ出力
            if "shiroyama_walk_results_20240204_152314" in file:
                if (index == 2 or index == 8):
                    draw_trajectory(filtered_data1, "going_"+file, index)
                    draw_graph(filtered_data1, "going_"+file, index)
            if (index % 8 == 1):
                draw_trajectory(filtered_data1, "going_"+file, index)
                draw_graph(filtered_data1, "going_"+file, index)
        
        for index, filtered_data2 in enumerate(filtered_data_list2):
            print("coming: " + str(index))
            try:
                result2_list = get_result(filtered_data2, file)
            except ValueError as e:
                print(f"value error happened when coming: {e}")
                error_text[file] = f"value error happened when coming: {e}"
                result2_list = [-1, -1, -1, -1, -1, -1, -1, -1, -1, -1]

            if index == 0:
                results2_list = make_list_of_list(result2_list)
            for i in range(0, len(results2_list)):
                results2_list[i].append(result2_list[i])
                
            # グラフ出力
            if (index % 8 == 1):
                draw_trajectory(filtered_data2, "coming_"+file, index)
                draw_graph(filtered_data2, "coming_"+file, index)

        #continue # csvの上書きをしたくないとき用

        # print(f'len(time): {len(time_above_plus)}')
        # print(results1_list)
        if len(result1_list) > 1:
            whole_list1 = sum_lists(results1_list, [1, 3, 5], 9, 1)
        if len(result2_list):
            whole_list2 = sum_lists(results2_list, [1, 3, 5], 9, 1)
        try:
            result_df1 = pd.DataFrame({
                'time1': [x - y for (x, y) in zip(time_above_plus1, time_above_plus)],
                'dist1': results1_list[0],
                'rmse1': results1_list[1],
                'sign1': results1_list[2],
                'dx_std1': results1_list[3],
                'angle_mean1': results1_list[4],
                'angle_std1': results1_list[5],
                'not_on_line_count1': results1_list[6],
                'not_looking_count1': results1_list[7],
                'not_looking_frame1': results1_list[8]
            })
            result_df2 = pd.DataFrame({
                'time2': [x - y for (x, y) in zip(time_below_minus, time_below_minus1)],
                'dist2': results2_list[0],
                'rmse2': results2_list[1],
                'sign2': results2_list[2],
                'dx_std2': results2_list[3],
                'angle_mean2': results2_list[4],
                'angle_std2': results2_list[5],
                'not_on_line_count2': results2_list[6],
                'not_looking_count2': results2_list[7],
                'not_looking_frame2': results2_list[8],
            })
            whole_result_df = pd.DataFrame({
                'time': max(time_below_minus[0], time_above_plus1[-1]),
                'dist': whole_result_list[0],
                'rmse': math.sqrt(whole_result_list[1]/whole_result_list[9]),
                'sigh': whole_result_list[2],
                "dx_std": math.sqrt(whole_result_list[3]/whole_result_list[9]),
                "angle_mean": whole_result_list[4]/whole_result_list[9],
                'angle_std': math.sqrt(whole_result_list[5]/whole_result_list[9]),
                'not_on_line_count': whole_result_list[6],
                'not_looking_count': whole_result_list[7],
                'not_looking_frame': whole_result_list[8]
            }, index=[0])
            whole_df1 = pd.DataFrame({
                'time': result_df1['time1'].sum(), 
                'dist': whole_list1[0],
                'rmse': math.sqrt(whole_list1[1]/whole_result_list[9]),
                'sigh': whole_list1[2],
                "dx_std": math.sqrt(whole_list1[3]/whole_result_list[9]),
                "angle_mean": whole_list1[4]/whole_result_list[9],
                'angle_std': math.sqrt(whole_list1[5]/whole_result_list[9]),
                'not_on_line_count': whole_list1[6],
                'not_looking_count': whole_list1[7],
                'not_looking_frame': whole_list1[8]
            }, index=[0])
            whole_df2 = pd.DataFrame({
                'time': result_df2['time2'].sum(),
                'dist': whole_list2[0],
                'rmse': math.sqrt(whole_list2[1]/whole_result_list[9]),
                'sigh': whole_list2[2],
                "dx_std": math.sqrt(whole_list2[3]/whole_result_list[9]),
                "angle_mean": whole_list2[4]/whole_result_list[9],
                'angle_std': math.sqrt(whole_list2[5]/whole_result_list[9]),
                'not_on_line_count': whole_list2[6],
                'not_looking_count': whole_list2[7],
                'not_looking_frame': whole_list2[8]
            }, index=[0])
        except ValueError as e:
            print(f"value error in {file}: {e}")
            error_text[file] = f"value error happened : All arrays must be of the same length : time length is {len([x - y for (x, y) in zip(time_above_plus1, time_above_plus)])}, dist length is {len(results1_list[1])}"
            continue

        # continue # csvの上書きをしたくないとき用2

        # CSVファイルとして書き出し
        result_df1.to_csv(f'output/going_{file}', index = False, float_format='%11.6f')
        result_df2.to_csv(f'output/coming_{file}', index = False, float_format='%11.6f')
        whole_result_df.to_csv(f'output/whole_{file}', index = False, float_format='%11.6f')
        whole_df1.to_csv(f'output/wholegoing_{file}', index = False, float_format='%11.6f')
        whole_df2.to_csv(f'output/wholecoming_{file}', index = False, float_format='%11.6f')

        error_text[file] = "done"
    
    # ndarrayをリストに変換し、テキストファイルとして出力
    for key, value in error_text.items():
        if isinstance(value, np.ndarray):
            error_text[key] = value.tolist()
    with open('output/happened_errors.txt', 'w') as error_file:
        json.dump(error_text, error_file, sort_keys=True, indent=4)


if __name__ == "__main__":
    main()


    # curvatures = [0.0]
    # for i in np.arange(1, len(trajectory[:,0])-1):
    #     dxn = trajectory[i, 0] - trajectory[i-1, 0]
    #     dxp = trajectory[i+1, 0] - trajectory[i, 0]
    #     dyn = trajectory[i, 1] - trajectory[i-1, 1]
    #     dyp = trajectory[i+1, 1] - trajectory[i, 1]
    #     dn = np.hypot(dxn, dyn)
    #     dp = np.hypot(dxp, dyp)
    #     dx = 1.0 / (dn + dp) * (dp / dn * dxn + dn / dp * dxp)
    #     ddx = 2.0 / (dn + dp) * (dxp / dp - dxn / dn)
    #     dy = 1.0 / (dn + dp) * (dp / dn * dyn + dn / dp * dyp)
    #     ddy = 2.0 / (dn + dp) * (dyp / dp - dyn / dn)
    #     curvature = (ddy * dx - ddx * dy) / ((dx ** 2 + dy ** 2) ** 1.5)
    #     curvatures.append(curvature)

    # curvatures = calc_curvature_circle_fitting(trajectory[:,0], trajectory[:,1], npo=1)

    # curvatures = calc_curvature_with_yaw_diff(trajectory[:,0], trajectory[:,1], df['rotY'][:])


#   python walking_trajectory.py 