using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 視点一致のガイドとなる「四角錐ガイド」（09 仕様 §1）をランタイム生成し，
/// 指定した Transform（＝錐の頂点にする視点）の姿勢に追従させるクラス．
///
/// 頂点から距離 d の位置に矩形断面を N 枚描き，その間を4本の側稜でつなぐ．
/// 誤差ゼロで全断面が完全に重なり「1枚の矩形として静止する」ヌル指示になる（仕様 §1.3）．
///
/// ■ Cone_Other: 頂点＝収録軌跡の視点（GhostCamera）。ライブ映像側にのみ描画
/// ■ Cone_Self : 頂点＝ライブ頭部の視点（CenterEyeAnchor）。収録映像側にのみ描画
/// </summary>
/// <remarks>
/// - 線は LineRenderer のビルボードではなく<b>太さを持つ3Dジオメトリ（角柱）</b>として生成する．
///   稜線オクルージョン（仕様 §1.6）が成立するために必要．深度処理は ConeLine.shader 側で
///   ZWrite On / ZTest LEqual としている．
/// - 線幅は<b>角度</b>で指定する．断面は頂点から固定距離にあるので，各断面のワールド線幅は
///   2·d·tan(lineWidthDeg/2) で計算する（仕様 §3.1）．
/// - 追従は LateUpdate で行う（対象カメラの姿勢確定後に反映するため）．
/// </remarks>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class ConeGuide : MonoBehaviour
{
    /// <summary>
    /// 錐の種別（どちらのレイヤに置くかの決定に使う）
    /// </summary>
    public enum ConeKind
    {
        /// <summary>頂点＝相手（収録軌跡）の視点．ライブ映像側にのみ描画される</summary>
        Other,
        /// <summary>頂点＝自分（ライブ頭部）の視点．収録映像側にのみ描画される</summary>
        Self,
    }

    [Header("追従対象")]
    /// <summary>
    /// 錐の頂点にする視点．Cone_Other なら GhostCamera，Cone_Self なら CenterEyeAnchor．
    /// </summary>
    [Tooltip("錐の頂点にする視点（Other=GhostCamera / Self=CenterEyeAnchor）")]
    public Transform target;

    /// <summary>この錐の種別（描画レイヤの決定に使う）</summary>
    [Tooltip("錐の種別（Other=ライブ視野に描く / Self=収録視野に描く）")]
    public ConeKind kind = ConeKind.Other;

    [Header("幾何（仕様 09 §3.1）")]
    /// <summary>近断面距離 d1[m]（既定 1.0m）</summary>
    [Tooltip("近断面距離 d1[m]（0.8m未満は輻輳・調節矛盾で不快）")]
    [Range(0.5f, 2f)] public float nearDistance = 1.0f;

    /// <summary>遠断面距離 d2[m]（既定 3.0m）</summary>
    [Tooltip("遠断面距離 d2[m]（前後感度 = (d2-d1)/(d1·d2)）")]
    [Range(1.5f, 6f)] public float farDistance = 3.0f;

    /// <summary>開き半角 α[deg]（既定 15° = 見かけ直径30°）</summary>
    [Tooltip("開き半角 α[deg]（断面半幅 = d·tanα。既定15°=見かけ直径30°）")]
    [Range(5f, 30f)] public float halfAngleDeg = 15f;

    /// <summary>断面枚数 N（既定 2．3枚以上は密度勾配による補助手がかり）</summary>
    [Tooltip("断面枚数 N（既定2。3枚以上は密度勾配による補助手がかり＝比較条件用）")]
    [Range(2, 4)] public int sectionCount = 2;

    [Header("線の描画（仕様 09 §3.1）")]
    /// <summary>
    /// 線幅（<b>角度指定</b>[deg]，既定 0.3°）．
    /// 距離によらず一定の見かけ太さにするため，各断面のワールド線幅は 2·d·tan(幅/2) となる．
    /// </summary>
    [Tooltip("線幅（角度指定[deg]）。各断面のワールド線幅は 2·d·tan(幅/2)")]
    [Range(0.05f, 1f)] public float lineWidthDeg = 0.3f;

    /// <summary>線の色（単色．既定 白）</summary>
    [Tooltip("線の色（単色。輝度変調と両立させるため既定は色分けなし）")]
    public Color lineColor = Color.white;

    /// <summary>
    /// 側稜（4本）を描くか（既定 ON）．前後多義性の解消手段（仕様 §1.6 採用案）．
    /// </summary>
    [Tooltip("側稜（4本）を描くか。前後多義性の解消手段（仕様 §1.6）")]
    public bool drawRidges = true;

    /// <summary>
    /// 側稜を頂点まで延ばすか（既定 OFF = 最近断面〜最遠断面のみ＝角錐台の側稜）．
    /// ON にすると頂点（＝相手の視点位置）まで線が収束する．
    /// </summary>
    [Tooltip("側稜を頂点まで延ばすか（OFF=最近断面〜最遠断面のみ）")]
    public bool ridgeExtendToApex = false;

    /// <summary>
    /// 別色モード（近＝シアン／遠＝マゼンタ．既定 OFF）．
    /// 4ストロークの輝度変調方式（仕様 §2.3）とは排他になるため，比較条件としてのみ使う．
    /// </summary>
    [Tooltip("別色モード（近=シアン/遠=マゼンタ）。4ストロークとは排他。比較条件用")]
    public bool dualColorMode = false;

    /// <summary>別色モード時の近断面の色</summary>
    [Tooltip("別色モード時の近断面の色")]
    public Color nearColor = new Color(0.20f, 0.90f, 1.00f);

    /// <summary>別色モード時の遠断面の色</summary>
    [Tooltip("別色モード時の遠断面の色")]
    public Color farColor = new Color(1.00f, 0.25f, 0.90f);

    [Header("シェーダ（未設定なら自動検索）")]
    /// <summary>線の描画シェーダ（Hidden/ConeLine）</summary>
    [Tooltip("線の描画シェーダ（Hidden/ConeLine。未設定なら自動検索）")]
    public Shader shader;

    /// <summary>
    /// 姿勢処理（ヨー/ピッチ/ロールの Raw / LowPass / Zero）．
    /// 未設定なら target の回転をそのまま使う（M3 で追加される）．
    /// </summary>
    [Tooltip("箱の姿勢処理（未設定なら target の回転をそのまま使う）")]
    public ConePoseFilter poseFilter;

    // ---- 生成物（シーンに保存しない） ----
    private Mesh mesh;
    private Material material;

    // ---- メッシュ組み立て用のバッファ（毎回確保しないよう使い回す） ----
    private readonly List<Vector3> vertices = new List<Vector3>(512);
    private readonly List<Color> colors = new List<Color>(512);
    private readonly List<int> triangles = new List<int>(1024);

    /// <summary>形状パラメータの変更検知用（前回ビルド時の値）</summary>
    private GeometryKey lastKey;
    private bool meshDirty = true;

    /// <summary>現在の断面距離（近い順）．HUD・デバッグ用</summary>
    public float NearDistance { get { return Mathf.Min(nearDistance, farDistance); } }
    /// <summary>現在の最遠断面距離．HUD・デバッグ用</summary>
    public float FarDistance { get { return Mathf.Max(nearDistance, farDistance); } }

    /// <summary>
    /// 前後感度 |dr/dD|(D=0) = (d2−d1)/(d1·d2)（仕様 §1.3）．パラメータ設計の確認用．
    /// </summary>
    public float DepthSensitivity
    {
        get
        {
            float d1 = NearDistance, d2 = FarDistance;
            return (d1 > 0f && d2 > 0f) ? (d2 - d1) / (d1 * d2) : 0f;
        }
    }

    private void OnEnable()
    {
        ApplyLayer();
        meshDirty = true;
        ResetPose(); // 有効化のたびにフィルタ状態を取り直す（過渡応答を出さない）
    }

    private void OnDisable()
    {
        // 再生停止・コンポーネント無効化のたびにリークしないよう解放する
        ReleaseResources();
    }

    private void OnDestroy()
    {
        ReleaseResources();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Inspector でパラメータを変えたら次のフレームで作り直す
        meshDirty = true;
    }
#endif

    private void LateUpdate()
    {
        if (!EnsureResources()) return;

        // 形状パラメータが変わっていたらメッシュを作り直す
        GeometryKey key = GeometryKey.From(this);
        if (meshDirty || !key.Equals(lastKey))
        {
            BuildMesh();
            lastKey = key;
            meshDirty = false;
        }

        // レイヤは kind から決まる（Inspector で kind を変えても追随させる）
        ApplyLayer();

        // 頂点＝対象視点に追従する．回転は姿勢処理（ヨー/ピッチ/ロール）を通す．
        // poseFilter のチェックを外せば素通し（＝追従対象の姿勢そのまま）になる
        if (target != null)
        {
            Quaternion rot = (poseFilter != null && poseFilter.enabled)
                ? poseFilter.Filter(target.rotation)
                : target.rotation;
            transform.SetPositionAndRotation(target.position, rot);
        }
    }

    /// <summary>
    /// 姿勢処理の内部状態（LPF）をリセットする．試行開始時に呼ぶと，
    /// 開始直後にフィルタの過渡応答が出ない．
    /// </summary>
    public void ResetPose()
    {
        if (poseFilter != null) poseFilter.ResetState();
    }

    /// <summary>
    /// 種別に応じたレイヤを自分自身に設定する（子は持たない構成なので自分だけでよい）
    /// </summary>
    private void ApplyLayer()
    {
        int layer = (kind == ConeKind.Other) ? ConeGuideLayers.OtherLayer : ConeGuideLayers.SelfLayer;
        if (gameObject.layer != layer) gameObject.layer = layer;
    }

    // ==================== メッシュ生成 ====================

    /// <summary>
    /// 断面 N 枚の枠と側稜 4 本を，太さを持つ角柱として1つのメッシュに組み立てる．
    /// </summary>
    private void BuildMesh()
    {
        vertices.Clear();
        colors.Clear();
        triangles.Clear();

        int n = Mathf.Clamp(sectionCount, 2, 4);
        float d1 = NearDistance;
        float dN = FarDistance;
        float tanAlpha = Mathf.Tan(halfAngleDeg * Mathf.Deg2Rad);
        // 線幅は角度指定．距離 d での「半」線幅 = d·tan(幅/2)（ワールド線幅 = 2·d·tan(幅/2)）
        float tanHalfWidth = Mathf.Tan(lineWidthDeg * 0.5f * Mathf.Deg2Rad);

        // --- 断面の枠（各 4 辺） ---
        for (int i = 0; i < n; i++)
        {
            float t = (n == 1) ? 0f : (float)i / (n - 1);
            float d = Mathf.Lerp(d1, dN, t);
            Color c = SectionColor(t);
            float half = d * tanHalfWidth;       // この断面での線の半太さ
            Vector3[] corner = Corners(d, d * tanAlpha);

            for (int e = 0; e < 4; e++)
            {
                AddSegment(corner[e], corner[(e + 1) % 4], half, half, c, c);
            }
        }

        // --- 側稜（4本）: 前後多義性の解消手段（仕様 §1.6） ---
        if (drawRidges)
        {
            // 既定は角錐台の側稜（最近断面〜最遠断面）．ON なら頂点まで延ばす
            float dStart = ridgeExtendToApex ? 0f : d1;
            Vector3[] a = Corners(dStart, dStart * tanAlpha);
            Vector3[] b = Corners(dN, dN * tanAlpha);
            Color ca = SectionColor(0f);   // 手前側（頂点寄り）の色
            Color cb = SectionColor(1f);   // 最遠断面の色

            for (int e = 0; e < 4; e++)
            {
                // 頂点からの距離に比例して太らせ，見かけの太さを一定に保つ
                AddSegment(a[e], b[e], dStart * tanHalfWidth, dN * tanHalfWidth, ca, cb);
            }
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
    }

    /// <summary>
    /// 距離 d・半幅 w の矩形断面の4隅（ローカル座標，反時計回り）を返す．
    /// 錐の軸は +Z（カメラの前方）．
    /// </summary>
    private static Vector3[] Corners(float d, float w)
    {
        return new[]
        {
            new Vector3(-w, -w, d),
            new Vector3( w, -w, d),
            new Vector3( w,  w, d),
            new Vector3(-w,  w, d),
        };
    }

    /// <summary>
    /// 断面の色を返す（t: 0=最近断面, 1=最遠断面）．
    /// 単色モードでは lineColor 一色，別色モードでは近＝シアン／遠＝マゼンタの補間．
    /// </summary>
    private Color SectionColor(float t)
    {
        return dualColorMode ? Color.Lerp(nearColor, farColor, t) : lineColor;
    }

    /// <summary>
    /// 線分 A→B を「太さを持つ角柱（四角断面のチューブ）」としてメッシュに追加する．
    /// ビルボードではなく実体のあるジオメトリなので，交差部で ZTest による
    /// 稜線オクルージョン（仕様 §1.6）が働く．
    /// </summary>
    /// <param name="a">始点（ローカル座標）</param>
    /// <param name="b">終点（ローカル座標）</param>
    /// <param name="halfA">始点側の半太さ[m]</param>
    /// <param name="halfB">終点側の半太さ[m]</param>
    /// <param name="colorA">始点側の色</param>
    /// <param name="colorB">終点側の色</param>
    private void AddSegment(Vector3 a, Vector3 b, float halfA, float halfB, Color colorA, Color colorB)
    {
        Vector3 dir = b - a;
        float length = dir.magnitude;
        if (length < 1e-6f) return;
        dir /= length;

        // 軸に直交する基底を作る（軸周りの回転は四角断面なので見えに影響しない）
        Vector3 reference = Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right;
        Vector3 u = Vector3.Normalize(Vector3.Cross(dir, reference));
        Vector3 v = Vector3.Cross(dir, u);

        // 断面の4隅（周方向の順）．半太さ h のとき一辺 2h の正方形になる
        Vector3[] offset = { u + v, u - v, -u - v, -u + v };

        int baseIndex = vertices.Count;
        for (int k = 0; k < 4; k++)
        {
            vertices.Add(a + offset[k] * halfA);
            colors.Add(colorA);
        }
        for (int k = 0; k < 4; k++)
        {
            vertices.Add(b + offset[k] * halfB);
            colors.Add(colorB);
        }

        // 側面 4 枚
        for (int k = 0; k < 4; k++)
        {
            int k2 = (k + 1) % 4;
            AddQuad(baseIndex + k, baseIndex + k2, baseIndex + 4 + k2, baseIndex + 4 + k);
        }
        // 端面 2 枚（線の端でも深度が正しく書かれるように閉じておく）
        AddQuad(baseIndex + 3, baseIndex + 2, baseIndex + 1, baseIndex + 0);
        AddQuad(baseIndex + 4, baseIndex + 5, baseIndex + 6, baseIndex + 7);
    }

    /// <summary>四角形（頂点4つ）を三角形2枚としてインデックスに追加する</summary>
    private void AddQuad(int i0, int i1, int i2, int i3)
    {
        triangles.Add(i0); triangles.Add(i1); triangles.Add(i2);
        triangles.Add(i0); triangles.Add(i2); triangles.Add(i3);
    }

    // ==================== リソース管理 ====================

    /// <summary>
    /// メッシュ・マテリアルを用意する（シーンには保存しない）
    /// </summary>
    private bool EnsureResources()
    {
        if (mesh == null)
        {
            mesh = new Mesh
            {
                name = "ConeGuideMesh",
                hideFlags = HideFlags.DontSave,
            };
            GetComponent<MeshFilter>().sharedMesh = mesh;
            meshDirty = true;
        }

        if (material == null)
        {
            if (shader == null) shader = Shader.Find("Hidden/ConeLine");
            if (shader == null)
            {
                Debug.LogError("[ConeGuide] シェーダ Hidden/ConeLine が見つかりません");
                enabled = false;
                return false;
            }
            material = new Material(shader)
            {
                name = "ConeGuideMaterial",
                hideFlags = HideFlags.DontSave,
            };

            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            // ガイドは影を落とさない・受けない（環境の照明から独立させる）
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        // 色は頂点カラーで運ぶので，マテリアル側の tint は白のまま使う
        material.SetColor("_Color", Color.white);
        return true;
    }

    private void ReleaseResources()
    {
        SafeDestroy(mesh);
        SafeDestroy(material);
        mesh = null;
        material = null;
    }

    /// <summary>
    /// 実行中と編集中の両方で安全に破棄する（ExecuteAlways のため両対応が必要）
    /// </summary>
    private static void SafeDestroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Object.Destroy(obj);
        else Object.DestroyImmediate(obj);
    }

    /// <summary>
    /// 形状に影響するパラメータ一式（変更検知に使う）
    /// </summary>
    private struct GeometryKey
    {
        public float near, far, alpha, width;
        public int sections;
        public bool ridges, apex, dual;
        public Color line, nearC, farC;

        public static GeometryKey From(ConeGuide g)
        {
            return new GeometryKey
            {
                near = g.nearDistance,
                far = g.farDistance,
                alpha = g.halfAngleDeg,
                width = g.lineWidthDeg,
                sections = g.sectionCount,
                ridges = g.drawRidges,
                apex = g.ridgeExtendToApex,
                dual = g.dualColorMode,
                line = g.lineColor,
                nearC = g.nearColor,
                farC = g.farColor,
            };
        }

        public bool Equals(GeometryKey o)
        {
            return near == o.near && far == o.far && alpha == o.alpha && width == o.width
                && sections == o.sections && ridges == o.ridges && apex == o.apex && dual == o.dual
                && line == o.line && nearC == o.nearC && farC == o.farC;
        }
    }
}
