import pandas as pd
import sys
import numpy as np
import matplotlib.pyplot as plt
from matplotlib.patches import Circle, Ellipse
from sklearn.metrics import mean_squared_error


for i in range(1, len(sys.argv)):
    # CSVファイルの読み込み
    file = sys.argv[i]
    if not file.endswith(".csv"):
        file += ".csv"
    df = pd.read_csv(file)

    # phaseが0以上3以下のデータを抽出
    filtered_data = df[(df['phase'] >= 0) & (df['phase'] <= 3)]

    # 2D平面上の軌跡の座標
    trajectory = np.array(df[['posX', 'posZ']])
    # 軌跡の距離を計算
    distance = np.linalg.norm(np.diff(trajectory, axis=0), axis=1).sum()
    # posXに関するRMSEを計算
    # rmse_posX = np.sqrt(mean_squared_error(df['posX'], np.zeros(len(df))))

    # 結果の表示
    print(f'Trajectory Distance: {distance}')
    # print(f'RMSE for posX: {rmse_posX}')

        # 軌跡の曲率を計算
    dx = np.gradient(trajectory[:, 0])
    dy = np.gradient(trajectory[:, 1])
    d2x = np.gradient(dx)
    d2y = np.gradient(dy)
    curvature = (dx * d2y - dy * d2x) / (dx**2 + dy**2)**(3/2)

    # 曲率の正負が反転する回数を数える
    sign_changes = np.sum(np.diff(np.sign(curvature)) != 0)

    # 結果の表示
    print(f'Number of sign changes in curvature: {sign_changes}')

    # posZが0以上から0未満に変化したときのtimeの値を抽出
    change_below_minus1 = df[(df['posZ'] >= 1.25) & (df['posZ'].shift(-1) < 1.25)]
    time_below_minus1 = change_below_minus1['time'].values
    print(time_below_minus1)

    # posZが0未満から0以上に変化したときのtimeの値を抽出
    change_above_7 = df[(df['posZ'] < 1.25) & (df['posZ'].shift(-1) >= 1.25)]
    time_above_7 = change_above_7['time'].values
    print(time_above_7)

    # 2次元平面上に軌跡をプロット
    fig, ax = plt.subplots()
    ax.plot(df['posX'], df['posZ'], label='Trajectory')
    

    # 円を描く
    # center = (0.7, 5)
    # radius = 0.35
    # circle = Circle(center, radius, fill=False, color='red', linestyle='dashed', linewidth=2)
    # ax.add_patch(circle)

    # # 楕円を描く
    # center_ellipse = (0, 6.5)
    # width_ellipse = 0.25 * 2  # 長半径から直径
    # height_ellipse = 0.35 * 2  # 短半径から直径
    # ellipse = Ellipse(center_ellipse, width_ellipse, height_ellipse, fill=False, color='blue', linestyle='dashed', linewidth=2)
    # ax.add_patch(ellipse)

    # グラフの装飾
    plt.xlabel('posX')
    plt.ylabel('posZ')
    plt.axis('equal')
    # plt.gca().set_aspect('equal', adjustable='box')
    # plt.xticks([-1,0,1])

    plt.title('Trajectory of Points for phase 0-3')
    plt.legend()
    plt.grid(True)
    plt.show()


#   python walkc_trajectory.py 