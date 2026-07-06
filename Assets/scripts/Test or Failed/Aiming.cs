using UnityEngine;

public class Aiming : MonoBehaviour
{
    public float sensitivity = 2.0f; // マウス感度
    private float initX;
    private float initY;

    //private void Start()
    //{
    //    initX = Input.GetAxis("Mouse X");
    //    initY = Input.GetAxis("Mouse Y");
    //}

    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            // マウスの移動量を取得
            float mouseX = Input.GetAxis("Mouse X") - initX;
            float mouseY = Input.GetAxis("Mouse Y") - initY;

            // カメラの回転を計算
            Vector3 rotation = new Vector3(-mouseY, mouseX, 0) * sensitivity;
            print(mouseX);

            // カメラの回転を適用
            //transform.Rotate(rotation);
            transform.eulerAngles = transform.rotation.eulerAngles + rotation;
        }
    }
}
