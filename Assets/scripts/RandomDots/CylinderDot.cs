using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using Meta.WitAi;

/// <summary>
/// 円柱状にランダムドットを生成するクラス
/// </summary>
public class CylinderDot : MonoBehaviour
{
    private GameObject Camera;
    private Vector3 initCameraPos;
    private Vector3 prevCameraPos;

    public enum TransformMethod
    {
        /// <summary>
        /// Oculusが半径の長さだけ横方向に等速直線移動した場合に、点が円筒の一端から他端に到達するよう等角速度で運動する
        /// </summary>
        [Tooltip("Oculusが半径の長さだけ横方向に等速直線移動した場合に、点が半円筒の一端から他端に到達するよう等角速度で運動する。")]
        uniformAngularMotion,
        /// <summary>
        /// 前額並行面に垂直に投影した場合に、Oculusとは逆方向に同じ速度で運動する
        /// </summary>
        [Tooltip("前額並行面に垂直に投影した場合に、Oculusとは逆方向に同じ速度で運動する")]
        uniformLinearMotion,
        /// <summary>
        /// 前額並行面に視線上に投影した場合に、Oculusとは逆方向に同じ速度で運動する
        /// </summary>
        [Tooltip("前額並行面に視線上に投影した場合に、Oculusとは逆方向に同じ速度で運動する")]
        lineOfSight,
        None
    };
    /// <summary>
    /// ランダムドットの動かし方
    /// </summary>
    [Tooltip("ランダムドットの動かし方")]
    public TransformMethod method = TransformMethod.uniformAngularMotion;
    /// <summary>
    /// 順応を狙うか否か．0より大きいとき，その大きさに応じて円柱の軸との距離を0から徐々に離す．
    /// </summary>
    [Tooltip("0より大きいとき，その大きさに応じて円柱の軸との距離を0から徐々に離す．")]
    public float adaptation = 0;

    /// <summary>
    /// ドット数
    /// </summary>
    [Tooltip("ドット数")]
    public int num_dots = 200;
    /// <summary>
    /// 見た目の大きさを一定に保つ
    /// </summary>
    [Tooltip("ドットとの距離が変わっても見た目の大きさを一定に保つ")]
    public bool keepsize = true;
    /// <summary>
    /// HMD の運動に対して同期して動くランダムドットの運動の速さの比
    /// </summary>
    [Tooltip(" HMD の運動に対して同期して動くランダムドットの運動の速さの比（普通は1）")]
    public float speedratio = 1;
    /// <summary>
    /// ドットの大きさ
    /// </summary>
    [Tooltip("ドットの大きさ")]
    public float dotsize = 0.03f;

    /// <summary>
    /// 円柱の軸との距離
    /// </summary>
    [Tooltip("円柱の軸との距離")]
    public float distance = 4;
    private float distance_max;
    /// <summary>
    /// 円柱の半径
    /// </summary>
    [Tooltip("円柱の半径")]
    public float radius = 3;
    /// <summary>
    /// 円柱の高さ
    /// </summary>
    [Tooltip("円柱の高さ")]
    public float height = 4;

    /// <summary>
    /// 円柱の表示範囲
    /// </summary>
    [Tooltip("円柱の表示範囲")]
    public float domain_of_theta = 90;
    private float domain;

    /// <summary>
    /// 平面への投影
    /// </summary>
    [Tooltip("平面への投影")]
    public bool PlaneProjected;

    /// <summary>
    /// 受動運動視（ドットが自動で運動）
    /// </summary>
    [Tooltip("ドットが自動で運動")]
    public bool passive = false;
    /// <summary>
    /// 自動で運動する際の速度
    /// </summary>
    [Tooltip("自動で運動する際の速度")]
    public float velocity = 1;
    /// <summary>
    /// 速度反転
    /// </summary>
    [Tooltip("速度反転（逆向きに動く）")]
    public bool reverse;
    /// <summary>
    /// 円柱端での反射
    /// </summary>
    [Tooltip("ドットが円柱の一端まで来た時に速度反転して跳ね返ってくるか他端から出てくるか")]
    public bool reflection = false;

    private Texture2D texture;
    /// <summary>
    /// テクスチャ上でのでピクセル単位での表示にするか
    /// </summary>
    [Tooltip("テクスチャ上でのでピクセル単位での表示にするか")]
    public bool pixel_projection = false;
    /// <summary>
    /// テクスチャの幅[pixel]
    /// </summary>
    [Tooltip("テクスチャの幅[pixel]")]
    public int pixel_width = 1920;
    /// <summary>
    /// テクスチャの高さ[pixel]
    /// </summary>
    [Tooltip("テクスチャの高さ[pixel]")]
    public int pixel_height = 1832;
    /// <summary>
    /// UI（テクスチャ）までの距離
    /// </summary>
    [Tooltip("UI（テクスチャ）までの距離")]
    public int UI_distance = 10;
    private GameObject img_obj;
    private Color[] pixels;

    struct Dot
    {
        public GameObject obj;
        public Vector3 initPos;
        public float theta;
        public float initTheta;
        public float speed;
        public int direction;
    }
    private Dot[] dots;

    private Vector3 prevposForTest;

    /// <summary>
    /// ドットに色を付けるか否か
    /// </summary>
    [Tooltip("ドットに色を付けるか否か")]
    public bool coloring;
    /// <summary>
    /// ドットの色
    /// </summary>
    [Tooltip("ドットの色")]
    public Color color;
    

    // Start is called before the first frame update
    void Start()
    {
        Camera = GameObject.Find("CenterEyeAnchor");
        initCameraPos = Camera.transform.localPosition;
        prevCameraPos = initCameraPos;
        texture = new Texture2D(pixel_width, pixel_height);
        img_obj = GameObject.Find("CenterRawImage");

        distance_max = distance;
        if (adaptation > 0)
        {
            distance = 0.1f;
        }
        radius = Mathf.Abs(radius);
        domain = domain_of_theta * Mathf.Deg2Rad;

        dots = new Dot[num_dots];
        for (int i = 0; i < num_dots; i++)
        {
            dots[i].obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dots[i].obj.transform.parent = Camera.transform;

            if (method == TransformMethod.uniformLinearMotion)
                dots[i].theta = Mathf.Asin(UnityEngine.Random.Range(-1f, 1f)); // -π/2～π/2で返してくれる
            else
                dots[i].theta = UnityEngine.Random.Range(-domain, domain);
            dots[i].initTheta = dots[i].theta;

            dots[i].initPos = new Vector3(-Mathf.Sin(dots[i].theta)*radius, UnityEngine.Random.Range(-height/2, height/2), distance+Mathf.Cos(dots[i].theta)*radius);
            dots[i].obj.transform.localPosition = dots[i].initPos;
            //dots[i].speed = velocity; 
            dots[i].direction = 1;
        }
    }

    // Update is called once per frame
    void Update()
    {

        domain = domain_of_theta * Mathf.Deg2Rad;
        for (int i = 0; i < num_dots; i++)
        {
            /* 受動運動視（自動で点を動かす場合） */
            if (passive) // 
            {
                if (method == TransformMethod.uniformAngularMotion)
                {
                    dots[i].theta = get_theta_of_uniform_angular(dots[i].theta, velocity * Time.deltaTime, ref dots[i].direction, reflection);
                }
                else if (method == TransformMethod.uniformLinearMotion)
                {
                    dots[i].theta = get_theta_of_uniform_linear(dots[i].theta, velocity * Time.deltaTime, ref dots[i].direction);
                }
                else if (method == TransformMethod.lineOfSight)
                {
                    dots[i].theta = get_theta_of_line_of_sight_projected(dots[i].theta, velocity * Time.deltaTime, ref dots[i].direction, reflection);
                }

                //if (i == 0) {
                //    Debug.Log(get_velocity(dots[i].obj));
                //}
            }
            /* 能動運動視（CenterEyeAnchorを動かす場合） */
            else
            {
                dots[i].initPos = new Vector3(dots[i].initPos.x, dots[i].initPos.y, distance + Mathf.Cos(dots[i].theta) * radius);//new Vector3(-Mathf.Sin(dots[i].theta)*radius, Random.Range(-height/2, height/2), distance+Mathf.Cos(dots[i].theta)*radius);
                Vector3 dif = Camera.transform.localPosition - prevCameraPos;

                if (method == TransformMethod.uniformAngularMotion)
                {
                    dots[i].theta = get_theta_of_uniform_angular(dots[i].theta, speedratio * dif.x, ref dots[i].direction);
                }
                else if (method == TransformMethod.uniformLinearMotion)
                {
                    dots[i].theta = get_theta_of_uniform_linear(dots[i].theta, speedratio * dif.x, ref dots[i].direction);
                }
                else if(method == TransformMethod.lineOfSight)
                {
                    dots[i].theta = get_theta_of_line_of_sight_projected(dots[i].theta, speedratio * dif.x, ref dots[i].direction);
                }

                if (i == 0)
                {
                    //Debug.Log(dif);
                    //Debug.Log(dots[i].initPos);
                    //Debug.Log(dots[i].obj.transform.localPosition);
                    //Debug.Log(get_velocity(dots[i].obj));
                }
            }

            dots[i].obj.transform.localPosition = new Vector3(-Mathf.Sin(dots[i].theta) * radius, dots[i].initPos.y, distance + Mathf.Cos(dots[i].theta) * radius);
            if (reverse)
                dots[i].obj.transform.localPosition = new Vector3(dots[i].obj.transform.localPosition.x, dots[i].obj.transform.localPosition.y, distance - Mathf.Cos(dots[i].theta) * radius);
            if (PlaneProjected)
                dots[i].obj.transform.localPosition = new Vector3(dots[i].obj.transform.localPosition.x, dots[i].obj.transform.localPosition.y, distance);

            // ピクセル状で表示
            if (pixel_projection)
            {
                // 両眼視差の実装めんどくね、と思って放置
                pixels = convObjToPixels(dots, pixel_width, pixel_height, UI_distance);
                makeTextureOnImage(img_obj, pixels, pixel_width, pixel_height, UI_distance);
                foreach (Dot dot in dots)
                {
                    dot.obj.SetActive(false);
                }
            }
            else
            {
                foreach (Dot dot in dots)
                {
                    dot.obj.SetActive(true);
                }
            }

            if (keepsize)
                dots[i].obj.transform.localScale = dotsize * Vector3.one * Vector3.Distance(project_to_XZ(dots[i].obj.transform.position), Camera.transform.position) / distance_max; // 網膜上の大きさを一定に

            if (coloring)
                dots[i].obj.GetComponent<Renderer>().material.color = color;
            else
                dots[i].obj.GetComponent<Renderer>().material.color = Color.white;

            while (distance < distance_max)
            {
                distance += adaptation;
                if (i==0)
                    Debug.Log(distance);
            }
        }
        prevCameraPos = Camera.transform.localPosition;
    }

    /// <summary>
    /// Oculusが半径の長さだけ横方向に等速直線移動した場合に、点が円筒の一端から他端に到達するよう等角速度運動する。 
    /// </summary>
    /// <param name="prev_theta"> 元のドットの位置を示す円柱上での角度 </param>
    /// <param name="difference"> 移動量 </param>
    /// <param name="direction"> 移動方向 </param>
    /// <param name="reflection"> 端での反転の有無 </param>
    /// <param name="for_test"> 開発時のテスト用変数のため実用上は不要 </param>
    /// <returns> 新たなドットの位置を示す円柱上での角度 </returns>
    float get_theta_of_uniform_angular(float prev_theta, float difference, ref int direction, bool reflection = false, int for_test = 0)
    {
        // xが直径2r分動くとθはπ動く
        prev_theta += direction * difference * Mathf.PI / (2 * radius);  

         // thetaの変域は [-domain, domain] とする。
        float sub = Mathf.Abs(prev_theta) - domain;
        if (sub > 0)
        {
            if (reflection)
            {
                direction = (int)-Mathf.Sign(prev_theta);
                prev_theta = Mathf.Sign(prev_theta) * (domain - sub);
            }
            else
            {
                prev_theta = -Mathf.Sign(prev_theta) * (domain - sub);
            }
        }

        return prev_theta;
    }

    /// <summary>
    /// 前額並行面に垂直に投影した場合に、Oculusとは逆方向に同じ速度で運動する
    /// </summary>
    /// <param name="prev_theta"> 元のドットの位置を示す円柱上での角度 </param>
    /// <param name="difference"> 移動量 </param>
    /// <param name="direction"> 移動方向 </param>
    /// <param name="reflection"> 端での反転の有無 </param>
    /// <param name="for_test"> 開発時のテスト用変数のため実用上は不要 </param>
    /// <returns> 新たなドットの位置を示す円柱上での角度 </returns>
    float get_theta_of_uniform_linear(float prev_theta, float difference, ref int direction, bool reflection = false, int for_test = 0)
    {
        // 点のx座標 -rsinθ に相対座標の変化 -difference を加える。逆方向だから difference にはマイナスをつける。
        float dest_on_plane = -radius * Mathf.Sin(prev_theta) - difference;

        // 点のx座標の変域は広くとも [-r, r] である。すなわち、thetaの変域は広くとも [-π/2, π/2] となる。これ以上拡張することは不可能である。
        float domain_x = radius * Mathf.Sin(domain);
        if (domain > Mathf.PI/2)
            domain_x = radius;
        // thetaの変域は [-domain, domain] 、すなわちx座標の変域は [-domain_x, domain_x] である。
        float sub = Mathf.Abs(dest_on_plane) - domain_x;
        if (sub > 0)
        {
            if (reflection)
            {
                direction = (int)-Mathf.Sign(prev_theta);
                dest_on_plane = Mathf.Sign(dest_on_plane) * (domain_x - sub);
            }
            else
            {
                dest_on_plane = -Mathf.Sign(dest_on_plane) * (domain_x - sub);
            }
        }

        prev_theta = -Mathf.Asin(dest_on_plane / radius);
        return prev_theta;
    }

    /// <summary>
    /// 前額並行面に視線上に投影した場合に、Oculusとは逆方向に同じ速度で運動する。
    /// </summary>
    /// <param name="prev_theta"> 元のドットの位置を示す円柱上での角度 </param>
    /// <param name="difference"> 移動量 </param>
    /// <param name="direction"> 移動方向（非対応） </param>
    /// <param name="reflection"> 端での反転の有無（非対応） </param>
    /// <param name="for_test"> 開発時のテスト用変数のため実用上は不要 </param>
    /// <returns> 新たなドットの位置を示す円柱上での角度 </returns>
    float get_theta_of_line_of_sight_projected (float prev_theta, float difference, ref int direction, bool reflection = false, int for_test = 0)
    {
        float distance_abs = Mathf.Abs(distance);
        // 前額並行面の視線上に点を投影した場合の、移動先の点のx座標の逆符号。（式の意味としては後でマイナスをとった方がわかりやすい。）cue pictureフォルダのparallaxを参照。
        float dest_on_line = difference + (distance_abs * radius * Mathf.Sin(prev_theta)) / (distance_abs + radius * Mathf.Cos(prev_theta));

        //
        float sub = Mathf.Abs(dest_on_line) - radius;
        //if (for_test == 1)
        //    Debug.Log("before = " + dest_on_line);
        if (sub > 0)
        {
            dest_on_line += -Mathf.Sign(dest_on_line) * 2 * radius;
            //if (for_test == 1) 
            //    Debug.Log("\tafter = " + dest_on_line);
        }

        float sin = (dest_on_line * Mathf.Pow(distance_abs, 2) 
                        + dest_on_line * Mathf.Sqrt(Mathf.Pow(distance_abs * radius, 2) - Mathf.Pow(dest_on_line * distance_abs, 2) + Mathf.Pow(dest_on_line * radius, 2))) 
                            / (radius * (Mathf.Pow(dest_on_line, 2) + Mathf.Pow(distance_abs, 2)));
        //if (for_test == 1)
        //{
        //    Debug.Log("sin = " + sin);
        //    Debug.Log("theta = " + Mathf.Asin(sin));
        //}
        prev_theta = Mathf.Asin(sin);
        return prev_theta;
    }

    /// <summary>
    /// オブジェクトの速度を取得
    /// </summary>
    /// <remarks>
    /// 基本は動作確認用
    /// </remarks>
    /// <param name="obj"> 対象オブジェクト </param>
    /// <returns> 速度 </returns>
    float get_velocity(GameObject obj)
    {
        prevposForTest = obj.transform.localPosition;
        float velocity = ((obj.transform.localPosition - prevposForTest) / Time.deltaTime).magnitude;
        return velocity;
    }

    /// <summary>
    /// ベクトルのy成分を0にする
    /// </summary>
    /// <param name="position"> 元のベクトル </param>
    /// <returns> 射影後のベクトル </returns>
    Vector3 project_to_XZ(Vector3 position)
    {
        Vector3 projected = new Vector3(position.x, 0, position.z);
        return projected;
    }

    /// <summary>
    /// z方向の長さ比に応じて視線上に位置を移動
    /// </summary>
    /// <remarks>
    /// やっていることは3次元での相似の計算．
    /// 方位を保ってz座標を変えようと思えばその比に応じて同時にx,y座標も変えなければならない．
    /// </remarks>
    /// <param name="pos"></param>
    /// <param name="distance"></param>
    /// <returns></returns>
    Vector2 calc2DDotPosition(Vector3 pos, float distance)
    {
        Vector3 new_pos = distance / pos.z * pos;
        return new_pos; // 自動的にキャスト
    }

    /// <summary>
    /// 基底のスケール変換と平行移動
    /// </summary>
    /// <remarks>
    /// 使い方忘れたので放置
    /// </remarks>
    Vector2 convToSimilar2DSpace(Vector2 pos, int width, int height, float distance, float horizontal_fov, float vertical_fov)
    {
        float ratio = Mathf.Max(width / (2 * distance * Mathf.Tan(horizontal_fov)), height / (2 * distance * Mathf.Tan(vertical_fov)));
        Vector2 new_pos = pos / ratio; // スケール変換
        new_pos -= new Vector2(-width/2, -height/2); // 平行移動
        // new_pos = new Vector2((int)Math.Ceiling(new_pos.x), (int)Math.Ceiling(new_pos.y));

        return new_pos;
    }

    /// <summary>
    /// テクスチャ上の座標からピクセル配列のインデックスに変換．
    /// 指定座標のピクセルを黒にする．
    /// </summary>
    /// <remarks>
    /// テクスチャのピクセルごとの情報は配列で管理されている．
    /// 座標(x,y)の点はピクセル配列ではインデックス(x+y*width)に格納される．左下が原点だったはず．
    /// 画像処理分野で主に用いられる手法なので必要であればそちらで調べてみると良い
    /// </remarks>
    /// <param name="coordinates"> テクスチャ上の座標 </param>
    /// <param name="width"> テクスチャの幅 </param>
    /// <param name="height"> テクスチャの高さ </param>
    /// <returns> ピクセル配列 </returns>
    Color[] convCoordinatesToPixels(Vector2[] coordinates, int width, int height)
    {
        Vector2Int xy_int;
        Color[] pixels = new Color[width * height];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        foreach (Vector2 coordinate in coordinates)
        {
            xy_int = new Vector2Int((int)Math.Ceiling(coordinate.x), (int)Math.Ceiling(coordinate.y));
            pixels[xy_int.x + xy_int.y * width] = Color.black;
        }
        return pixels;
    }

    /// <summary>
    /// ドットの空間座標からテクスチャのピクセル配列に変換
    /// </summary>
    /// <param name="dots"> 対象のドット </param>
    /// <param name="width"> テクスチャの幅 </param>
    /// <param name="height"> テクスチャの高さ </param>
    /// <param name="distance"> テクスチャまでの距離 </param>
    /// <returns> ピクセル配列 </returns>
    Color[] convObjToPixels(Dot[] dots, int width, int height, float distance)
    {
        Vector2[] pos_on_ui = new Vector2[dots.Length];
        for(int i = 0; i < dots.Length; i++)
        {
            pos_on_ui[i] = calc2DDotPosition(dots[i].obj.transform.localPosition, distance);
        }

        Vector2[] pos_on_texture = pos_on_ui; // 浅いコピーでOK
        for (int i = 0; i < pos_on_ui.Length; i++)
        {
            pos_on_texture[i] = calc2DDotPosition(dots[i].obj.transform.localPosition, distance);
        }

        Color[] pixels = new Color[width * height];
        pixels = convCoordinatesToPixels(pos_on_texture, width, height);
        return pixels;
    }

    /// <summary>
    /// テクスチャとして img_obj の RawImage コンポーネントに貼り付け
    /// </summary>
    /// <param name="img_obj"> RawImage コンポーネントをもつオブジェクト </param>
    /// <param name="pixels"> テクスチャの白黒の点を示すピクセル配列 </param>
    /// <param name="width"> テクスチャの幅 </param>
    /// <param name="height"> テクスチャの高さ </param>
    /// <param name="distance"> テクスチャまでの距離 </param>
    void makeTextureOnImage(GameObject img_obj, Color[] pixels, int width, int height, float distance)
    {
        RawImage image = img_obj.GetComponent<RawImage>();

        texture.SetPixels(pixels);
        texture.Apply();
        image.texture = texture;
        //Destroy(texture);
    }
}
