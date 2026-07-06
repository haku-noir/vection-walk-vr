import sys
import csv
import pandas as pd
import search_csv

if __name__ == "__main__":

    print("Input folder path.")
    folder = input()

    print("Input file name. If you going to end, input \"q\"")
    file = ""
    file_list = []
    while True:
        file = input()
        if file == "q":
            break
        if file.find(".csv") < 0:
            file += ".csv"
        # print(folder + "/" + file)
        file_list.append(folder + "/" + file)

    elapsed_time_index = ['10', '20', '30', '40', '50']
    elapsed_list_df = pd.DataFrame(columns = elapsed_time_index)
    log_avg_list_df = pd.DataFrame(columns = ['posX', 'posY', 'posZ', 'rotX', 'rotY', 'rotZ'])
    log_std_list_df = pd.DataFrame(columns = ['posX', 'posY', 'posZ', 'rotX', 'rotY', 'rotZ'])

    for file_name in file_list:
        elapsed_time_df = search_csv.find_interaction_numbers(file_name)
        extracted_df = pd.Series([elapsed_time_df.at[9, 'ElapsedTime'], elapsed_time_df.at[19, 'ElapsedTime'], elapsed_time_df.at[29, 'ElapsedTime'], 
                                  elapsed_time_df.at[39, 'ElapsedTime'], elapsed_time_df.at[49, 'ElapsedTime']], index = elapsed_time_index, name = file_name).to_frame().T
        elapsed_list_df = pd.concat([elapsed_list_df, extracted_df])

        log_avg_list_df = pd.concat([log_avg_list_df, search_csv.find_tracking_log(file_name).loc['Average':'Average', 'posX':'rotZ'].rename(index = {'Average': file_name})])
        # print(search_csv.find_tracking_log(file_name))
        # print(search_csv.find_tracking_log(file_name).loc['Average':'Average', 'posX':'rotZ'].rename(index = {'Average': file_name}))
        log_std_list_df = pd.concat([log_std_list_df, search_csv.find_tracking_log(file_name).loc['Std Dev':'Std Dev', 'posX':'rotZ'].rename(index = {'Std Dev': file_name})])

    print("elapsed")
    print(elapsed_list_df)
    print("avg")
    print(log_avg_list_df)
    print("std dev")
    print(log_std_list_df)

    print("Input output file name.")
    output_name = input() 
    
    while True:
        try:
            with pd.ExcelWriter(f'{output_name}.xlsx') as writer:
                elapsed_list_df.to_excel(writer, sheet_name = "ElapsedTime")
                log_avg_list_df.to_excel(writer, sheet_name = "Average")
                log_std_list_df.to_excel(writer, sheet_name = "Std Dev")
            break
        except PermissionError:
            print("Close the file!")
            input()
    
# _でsplitしてLRとかを抽出したい
# elapsedTimeの間隔を指定したい


