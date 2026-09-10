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

    /// <summary>
    /// この錐を実際に描画する<b>左目用</b>カメラ（箱カメラ）の視点．<b>Far At Infinity</b> の
    /// ときだけ使う．Other なら LeftLiveBoxCam，Self なら LeftGhostBoxCam
    /// （ConeGuideSceneUpgrader が自動配線する。10 仕様 §2.3 拡張）．
    /// </summary>
    [Tooltip("この錐を描画する左目用カメラの視点（Other=LeftLiveBoxCam / Self=LeftGhostBoxCam）。Far At Infinity でのみ使用")]
    public Transform observerLeft;

    /// <summary>
    /// この錐を実際に描画する<b>右目用</b>カメラ（箱カメラ）の視点．<b>Far At Infinity</b> の
    /// ときだけ使う．Other なら RightLiveBoxCam，Self なら RightGhostBoxCam
    /// （ConeGuideSceneUpgrader が自動配線する。10 仕様 §2.3 拡張）．
    /// </summary>
    [Tooltip("この錐を描画する右目用カメラの視点（Other=RightLiveBoxCam / Self=RightGhostBoxCam）。Far At Infinity でのみ使用")]
    public Transform observerRight;

    [Header("幾何（仕様 09 §3.1）")]
    /// <summary>近断面距離 d1[m]（既定 1.0m）</summary>
    [Tooltip("近断面距離 d1[m]（0.8m未満は輻輳・調節矛盾で不快）")]
    [Range(0.5f, 2f)] public float nearDistance = 1.0f;

    /// <summary>遠断面距離 d2[m]（既定 3.0m）</summary>
    [Tooltip("遠断面距離 d2[m]（前後感度 = (d2-d1)/(d1·d2)）")]
    [Range(1.5f, 6f)] public float farDistance = 3.0f;

    [Header("遠断面の無限遠モード（拡張）")]
    /// <summary>
    /// 遠断面を実質無限遠として扱うか（既定 OFF）．
    ///
    /// ON にすると，遠断面は頂点(<see cref="target"/>)ではなく<see cref="observerLeft"/>／
    /// <see cref="observerRight"/>（この錐を実際に描画する左目用・右目用カメラ）の
    /// <b>位置と向きの両方</b>を基準に再配置される．これにより，頂点と観測者の間の
    /// <b>並進誤差（横ズレ δ・前後ズレ D）</b>だけでなく<b>回転誤差（ヨー等）</b>の影響も
    /// 打ち消され，遠断面は画面上で<b>完全に静止したリファレンス枠</b>になる
    /// （位置・サイズ・向きのいずれも変化しない）．誤差の手がかりは近断面だけが担う形になる．
    ///
    /// 両眼立体視化（10 仕様 §2.3・§2.5）に伴い，遠断面は左目用・右目用のカメラ位置を
    /// それぞれ基準に再アンカリングした<b>2つの独立したメッシュ</b>（左目にしか映らないもの・
    /// 右目にしか映らないもの）になる．OFF のときは通常のワールド座標上の3Dオブジェクトとして
    /// 扱われ，左右カメラの実際の透視投影が自動的に正しい両眼視差を生成するため，
    /// 再アンカリングは不要（単一のメッシュを両目のカメラで共有できる）．
    ///
    /// 側稜（<see cref="drawRidges"/>）の遠い側の端は，このフラグの値に関わらず常に
    /// 頂点基準（<see cref="target"/>から見た実際の距離 d2 の位置）のままである。ON のときの
    /// 遠断面は3D空間上の実在位置を持たない「浮遊するリファレンス枠」になるため，稜線を
    /// そこへ接続する意味がなく，稜線と遠断面の見た目が一致しなくなる（10 仕様検討時に判明）．
    /// </summary>
    [Tooltip("遠断面を画面上に完全固定する（並進・回転どちらの誤差にも反応しない）。Observer Left/Right の設定が必要")]
    public bool farAtInfinity = false;

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

    [Header("パラメータの同期")]
    /// <summary>
    /// 幾何・見た目のパラメータを<b>この錐からコピーする</b>（同期元）．None なら同期しない．
    ///
    /// 2つの錐は同じ見えでなければならない．片方だけ大きさや線の設定を変えると，
    /// 誤差ゼロでも2つの箱に差が残り，時分割で消えるはずのワブル（仕様 §1.4）が消えなくなる．
    /// 錐ごとに独立したコンポーネントなので取り違えやすく，それを防ぐための仕組み．
    /// </summary>
    /// <remarks>
    /// 同期されるのは幾何・見た目（d1 / d2 / 遠断面無限遠モード / α / 断面枚数 / 線幅 / 線の色 / 稜線 / 別色モード）と，
    /// 姿勢処理の条件（ヨー・ピッチ・ロールの処理とカットオフ）．
    /// 追従対象・種別・Observer・シェーダ・フィルタの内部状態は錐ごとの固有値なのでコピーしない．
    ///
    /// 「引く」側が同期元を参照する形にしてあるので，2つの錐の更新順に依存せず
    /// 同じフレームで値が揃う．
    /// </remarks>
    [Tooltip("パラメータの同期元（None なら同期しない）。2つの錐は同じ見えである必要がある")]
    public ConeGuide mirrorFrom;

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

    // ---- 近断面/遠断面/稜線の分離（kind=Other のときだけ．09 §3.7 拡張） ----
    // ガイドチャンネルが近い箱・遠い箱に独立した4ストロークを掛けられるよう，
    // kind=Other のときは遠断面・稜線を別オブジェクト・別レイヤに分離する。
    // kind=Self はガイドチャンネルの入力ではないため分離せず，従来どおり
    // 1つのメッシュ（near バッファ）にまとめる
    private GameObject farObject;
    private GameObject ridgeObject;
    private Mesh farMesh;
    private Mesh ridgeMesh;

    // ---- 遠断面の左目/右目分離（farAtInfinity が有効なときだけ．10 §3.3 拡張） ----
    // Split の有無（kind）に関わらず，Far At Infinity が有効な間はどちらの kind でも
    // 遠断面が左目用・右目用の2つの独立したメッシュ・オブジェクト・レイヤに分かれる。
    // OFF の間はどちらの kind も使わない（kind=Other なら far、kind=Self なら near に含まれる）
    private GameObject farLeftObject;
    private GameObject farRightObject;
    private Mesh farLeftMesh;
    private Mesh farRightMesh;

    /// <summary>近断面・遠断面・稜線を3つの独立したオブジェクト・レイヤに分離するか</summary>
    private bool Split { get { return kind == ConeKind.Other; } }

    /// <summary>メッシュ組み立て用のバッファ（頂点・色・三角形インデックス）．使い回す</summary>
    private sealed class MeshBuffer
    {
        public readonly List<Vector3> vertices = new List<Vector3>(512);
        public readonly List<Color> colors = new List<Color>(512);
        public readonly List<int> triangles = new List<int>(1024);

        public void Clear()
        {
            vertices.Clear();
            colors.Clear();
            triangles.Clear();
        }
    }

    // ---- メッシュ組み立て用のバッファ（毎回確保しないよう使い回す） ----
    // Split=false（kind=Self）のときは far/ridge も near に書き込んで1つのメッシュにまとめる
    private readonly MeshBuffer near = new MeshBuffer();
    private readonly MeshBuffer far = new MeshBuffer();
    private readonly MeshBuffer ridge = new MeshBuffer();
    // farAtInfinity が有効な間だけ使う，遠断面の左目用・右目用バッファ
    private readonly MeshBuffer farLeft = new MeshBuffer();
    private readonly MeshBuffer farRight = new MeshBuffer();

    /// <summary>形状パラメータの変更検知用（前回ビルド時の値）</summary>
    private GeometryKey lastKey;
    private bool meshDirty = true;
    /// <summary>相互参照の警告を1回だけ出すためのフラグ</summary>
    private bool mirrorCycleWarned;
    /// <summary>Far At Infinity が有効なのに ObserverLeft 未設定の警告を1回だけ出すためのフラグ</summary>
    private bool leftObserverMissingWarned;
    /// <summary>Far At Infinity が有効なのに ObserverRight 未設定の警告を1回だけ出すためのフラグ</summary>
    private bool rightObserverMissingWarned;

    /// <summary>現在の断面距離（近い順）．HUD・デバッグ用</summary>
    public float NearDistance { get { return Mathf.Min(nearDistance, farDistance); } }
    /// <summary>現在の最遠断面距離．HUD・デバッグ用</summary>
    public float FarDistance { get { return Mathf.Max(nearDistance, farDistance); } }

    /// <summary>
    /// 前後感度 |dr/dD|(D=0) = (d2−d1)/(d1·d2)（仕様 §1.3）．パラメータ設計の確認用．
    /// Far At Infinity 有効時は d2→∞ の極限値 1/d1 を返す．
    /// </summary>
    public float DepthSensitivity
    {
        get
        {
            float d1 = NearDistance;
            if (farAtInfinity && (observerLeft != null || observerRight != null))
            {
                return d1 > 0f ? 1f / d1 : 0f;
            }
            float d2 = FarDistance;
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

        // 同期元があれば先に値を引く（このあとの変更検知でメッシュも同じフレームで作り直される）
        SyncFromMirrorSource();

        // 頂点＝対象視点の姿勢を先に確定する（Far At Infinity のメッシュ計算に必要なため）．
        // 回転は姿勢処理（ヨー/ピッチ/ロール）を通す．poseFilter のチェックを外せば
        // 素通し（＝追従対象の姿勢そのまま）になる
        Vector3 apexPos = transform.position;
        Quaternion apexRot = transform.rotation;
        if (target != null)
        {
            apexPos = target.position;
            apexRot = (poseFilter != null && poseFilter.enabled)
                ? poseFilter.Filter(target.rotation)
                : target.rotation;
        }

        // 形状パラメータが変わっていたらメッシュを作り直す．
        // Far At Infinity 中は Observer が毎フレーム動くため，その間は常に作り直す
        GeometryKey key = GeometryKey.From(this);
        bool infinityTracksObserver = farAtInfinity && (observerLeft != null || observerRight != null);
        if (meshDirty || !key.Equals(lastKey) || infinityTracksObserver)
        {
            BuildMesh(apexPos, apexRot);
            lastKey = key;
            meshDirty = false;
        }

        // レイヤは kind から決まる（Inspector で kind を変えても追随させる）
        ApplyLayer();

        if (target != null)
        {
            transform.SetPositionAndRotation(apexPos, apexRot);
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
    /// 同期元の錐から幾何・見た目と姿勢処理の条件をコピーする．
    /// 値が同じときは代入しない（編集中に無駄にシーンを変更済みにしないため）．
    /// </summary>
    private void SyncFromMirrorSource()
    {
        ConeGuide source = mirrorFrom;
        if (source == null || source == this) return;

        // 相互参照は互いに上書きし合って発散するので同期しない
        if (source.mirrorFrom == this)
        {
            if (!mirrorCycleWarned)
            {
                Debug.LogWarning("[ConeGuide] " + name + " と " + source.name
                    + " が互いを同期元にしています。どちらか一方の Mirror From を None にしてください。", this);
                mirrorCycleWarned = true;
            }
            return;
        }
        mirrorCycleWarned = false;

        // --- 幾何・見た目（GeometryKey が対象そのもの） ---
        if (!GeometryKey.From(source).Equals(GeometryKey.From(this)))
        {
            nearDistance = source.nearDistance;
            farDistance = source.farDistance;
            farAtInfinity = source.farAtInfinity;
            halfAngleDeg = source.halfAngleDeg;
            sectionCount = source.sectionCount;
            lineWidthDeg = source.lineWidthDeg;
            lineColor = source.lineColor;
            drawRidges = source.drawRidges;
            ridgeExtendToApex = source.ridgeExtendToApex;
            dualColorMode = source.dualColorMode;
            nearColor = source.nearColor;
            farColor = source.farColor;
        }

        // --- 姿勢処理の条件（仕様 §3.2 は錐ごとに条件を分けていない） ---
        // フィルタの内部状態（LPF）は追従対象ごとに別なのでコピーしない
        ConePoseFilter mine = poseFilter;
        ConePoseFilter theirs = source.poseFilter;
        if (mine == null || theirs == null || mine == theirs) return;

        if (mine.yawMode != theirs.yawMode) mine.yawMode = theirs.yawMode;
        if (mine.pitchMode != theirs.pitchMode) mine.pitchMode = theirs.pitchMode;
        if (mine.rollMode != theirs.rollMode) mine.rollMode = theirs.rollMode;
        if (mine.yawCutoffHz != theirs.yawCutoffHz) mine.yawCutoffHz = theirs.yawCutoffHz;
        if (mine.pitchCutoffHz != theirs.pitchCutoffHz) mine.pitchCutoffHz = theirs.pitchCutoffHz;
        if (mine.rollCutoffHz != theirs.rollCutoffHz) mine.rollCutoffHz = theirs.rollCutoffHz;
        if (mine.enabled != theirs.enabled) mine.enabled = theirs.enabled;
    }

    /// <summary>
    /// 種別に応じたレイヤを自分自身（近断面）に設定する．Split のときは
    /// 遠断面・稜線の子オブジェクトにもそれぞれ専用レイヤを設定する（09 §3.7 拡張）．
    /// Far At Infinity が有効なときは，遠断面の左目用・右目用の子オブジェクトにも
    /// kind に応じた専用レイヤを設定する（10 §3.3 拡張）．
    /// </summary>
    private void ApplyLayer()
    {
        bool isOther = kind == ConeKind.Other;
        int layer = isOther ? ConeGuideLayers.OtherLayer : ConeGuideLayers.SelfLayer;
        if (gameObject.layer != layer) gameObject.layer = layer;

        if (Split)
        {
            if (farObject != null && farObject.layer != ConeGuideLayers.OtherFarLayer)
            {
                farObject.layer = ConeGuideLayers.OtherFarLayer;
            }
            if (ridgeObject != null && ridgeObject.layer != ConeGuideLayers.OtherRidgeLayer)
            {
                ridgeObject.layer = ConeGuideLayers.OtherRidgeLayer;
            }
        }

        if (farAtInfinity)
        {
            int farLeftLayer = isOther ? ConeGuideLayers.OtherFarLeftLayer : ConeGuideLayers.SelfFarLeftLayer;
            int farRightLayer = isOther ? ConeGuideLayers.OtherFarRightLayer : ConeGuideLayers.SelfFarRightLayer;
            if (farLeftObject != null && farLeftObject.layer != farLeftLayer)
            {
                farLeftObject.layer = farLeftLayer;
            }
            if (farRightObject != null && farRightObject.layer != farRightLayer)
            {
                farRightObject.layer = farRightLayer;
            }
        }
    }

    // ==================== メッシュ生成 ====================

    /// <summary>
    /// Far At Infinity 用の再アンカリング（位置＋向き）．<see cref="Identity"/> のときは
    /// 従来どおり頂点(<see cref="target"/>)そのものを原点とするローカル座標になる．
    /// </summary>
    private struct FarAnchor
    {
        public Vector3 offset;
        public Quaternion rotation;
        public static readonly FarAnchor Identity =
            new FarAnchor { offset = Vector3.zero, rotation = Quaternion.identity };
    }

    /// <summary>
    /// 断面 N 枚の枠と側稜 4 本を，太さを持つ角柱として1つ以上のメッシュに組み立てる．
    /// </summary>
    /// <param name="apexPos">頂点(<see cref="target"/>)のワールド座標（この錐の今フレームの位置）</param>
    /// <param name="apexRot">頂点の姿勢処理後の回転（この錐の今フレームの回転）</param>
    /// <remarks>
    /// <b>Far At Infinity</b>（<see cref="farAtInfinity"/>）が有効なとき，最遠断面だけは
    /// 頂点ではなく <see cref="observerLeft"/>／<see cref="observerRight"/>（この錐を描画する
    /// 左目用・右目用カメラ）の<b>位置と向きの両方</b>を基準に，左右それぞれ独立に再アンカリングする．
    ///
    /// ワールド座標は通常 <c>apexPos + apexRot・localVertex</c> になる（メッシュのローカル座標
    /// はこの錐の Transform で変換されるため）．最遠断面のローカル頂点に
    /// <c>FarAnchor{ offset = apexRot⁻¹・(observerPos − apexPos), rotation = apexRot⁻¹・observerRot }</c>
    /// を適用すると，
    /// <c>apexPos + apexRot・(offset + rotation・localDir) = observerPos + observerRot・localDir</c>
    /// となり，<b>頂点と観測者の間の並進（δ, D）も相対回転（θ）も式から完全に消える</b>．
    /// この錐を描画するカメラは常に observerLeft／observerRight 自身なので
    /// （<see cref="ComputeFarAnchor"/> 参照），結果として最遠断面は各目の画面上に
    /// <b>完全に静止したリファレンス枠</b>になる（並進・回転どちらの誤差にも反応しない）．
    /// 誤差の手がかりは近断面だけが担う．d2 の値自体は見かけの角度に影響しない
    /// （方向だけで決まる）ので，線幅計算などはそのまま d2 を使い続けてよい．
    ///
    /// Far At Infinity が有効な間，最遠断面は実世界の1点に対応しなくなる（左目・右目で
    /// それぞれ異なる場所に「浮遊」する）ため，<b>側稜の遠い側の端は常に頂点基準
    /// （<see cref="FarAnchor.Identity"/>）のままとする</b>。稜線を無限遠の最遠断面へ
    /// 接続すると，稜線自体も左右で別々の見え方をする必要が生じ，構造が大きく複雑化する
    /// 割に，浮遊するリファレンス枠と実世界の奥行き手がかりを混ぜること自体の意味が薄い
    /// （10 仕様検討時の判断）．
    ///
    /// <see cref="Split"/> が true（kind=Other）のときは，最遠断面を <c>far</c>，
    /// 稜線を <c>ridge</c>，それ以外（近断面）を <c>near</c> という3つの独立した
    /// バッファに振り分け，それぞれ別の Mesh（<see cref="mesh"/> / <see cref="farMesh"/> /
    /// <see cref="ridgeMesh"/>）・別オブジェクト・別レイヤに割り当てる．false（kind=Self）
    /// のときは稜線・非最遠断面を <c>near</c> にまとめる．
    ///
    /// <see cref="farAtInfinity"/> が true のときは，kind に関わらず最遠断面だけは
    /// <c>far</c>／<c>near</c> ではなく <c>farLeft</c>／<c>farRight</c> という左目用・右目用の
    /// バッファへ振り分け，それぞれ別の Mesh（<see cref="farLeftMesh"/> / <see cref="farRightMesh"/>）・
    /// 別オブジェクト・別レイヤ（kind ごとに異なる）に割り当てる．このとき <c>far</c> バッファは
    /// 空のままになり（Split=true でも），対応する Mesh も空になって非表示になる．
    /// </remarks>
    private void BuildMesh(Vector3 apexPos, Quaternion apexRot)
    {
        bool split = Split;
        bool stereoFar = farAtInfinity;

        near.Clear();
        far.Clear();
        ridge.Clear();
        farLeft.Clear();
        farRight.Clear();

        MeshBuffer ridgeBuf = split ? ridge : near;

        int n = Mathf.Clamp(sectionCount, 2, 4);
        float d1 = NearDistance;
        float dN = FarDistance;
        float tanAlpha = Mathf.Tan(halfAngleDeg * Mathf.Deg2Rad);
        // 線幅は角度指定．距離 d での「半」線幅 = d·tan(幅/2)（ワールド線幅 = 2·d·tan(幅/2)）
        float tanHalfWidth = Mathf.Tan(lineWidthDeg * 0.5f * Mathf.Deg2Rad);

        FarAnchor farAnchorLeft = ComputeFarAnchor(apexPos, apexRot, observerLeft, ref leftObserverMissingWarned, "Left");
        FarAnchor farAnchorRight = ComputeFarAnchor(apexPos, apexRot, observerRight, ref rightObserverMissingWarned, "Right");

        // --- 断面の枠（各 4 辺） ---
        for (int i = 0; i < n; i++)
        {
            float t = (n == 1) ? 0f : (float)i / (n - 1);
            float d = Mathf.Lerp(d1, dN, t);
            Color c = SectionColor(t);
            float half = d * tanHalfWidth;       // この断面での線の半太さ
            bool isFarthest = (i == n - 1);

            if (isFarthest && stereoFar)
            {
                // Far At Infinity 中の最遠断面は左目・右目で別々に再アンカリングされるため，
                // 実世界に1つの断面としては存在しない。左右それぞれのメッシュに分けて生成する
                Vector3[] cornerLeft = Corners(d, d * tanAlpha, farAnchorLeft);
                Vector3[] cornerRight = Corners(d, d * tanAlpha, farAnchorRight);
                for (int e = 0; e < 4; e++)
                {
                    AddSegment(farLeft, cornerLeft[e], cornerLeft[(e + 1) % 4], half, half, c, c);
                    AddSegment(farRight, cornerRight[e], cornerRight[(e + 1) % 4], half, half, c, c);
                }
            }
            else
            {
                Vector3[] corner = Corners(d, d * tanAlpha, FarAnchor.Identity);
                MeshBuffer buf = (isFarthest && split) ? far : near;
                for (int e = 0; e < 4; e++)
                {
                    AddSegment(buf, corner[e], corner[(e + 1) % 4], half, half, c, c);
                }
            }
        }

        // --- 側稜（4本）: 前後多義性の解消手段（仕様 §1.6） ---
        // 遠い側の端は常に頂点基準（Far At Infinity の影響を受けない。上の <remarks> 参照）
        if (drawRidges)
        {
            // 既定は角錐台の側稜（最近断面〜最遠断面）．ON なら頂点まで延ばす
            float dStart = ridgeExtendToApex ? 0f : d1;
            Vector3[] a = Corners(dStart, dStart * tanAlpha, FarAnchor.Identity);
            Vector3[] b = Corners(dN, dN * tanAlpha, FarAnchor.Identity);
            Color ca = SectionColor(0f);   // 手前側（頂点寄り）の色
            Color cb = SectionColor(1f);   // 最遠断面の色

            for (int e = 0; e < 4; e++)
            {
                // 頂点からの距離に比例して太らせ，見かけの太さを一定に保つ
                AddSegment(ridgeBuf, a[e], b[e], dStart * tanHalfWidth, dN * tanHalfWidth, ca, cb);
            }
        }

        ApplyBuffer(mesh, near);
        if (split)
        {
            ApplyBuffer(farMesh, far);
            ApplyBuffer(ridgeMesh, ridge);
        }
        if (stereoFar)
        {
            ApplyBuffer(farLeftMesh, farLeft);
            ApplyBuffer(farRightMesh, farRight);
        }
    }

    /// <summary>組み立てたバッファの内容を Mesh へ反映する</summary>
    private static void ApplyBuffer(Mesh target, MeshBuffer buffer)
    {
        target.Clear();
        target.SetVertices(buffer.vertices);
        target.SetColors(buffer.colors);
        target.SetTriangles(buffer.triangles, 0);
        target.RecalculateBounds();
    }

    /// <summary>
    /// Far At Infinity 用の再アンカリング（<see cref="FarAnchor"/>）を，指定した目のカメラ
    /// （<paramref name="observerTransform"/>）基準に計算する．無効（OFF，または該当する
    /// Observer 未設定）なら <see cref="FarAnchor.Identity"/>（＝従来どおり頂点基準・回転も
    /// そのまま）を返す．左目・右目それぞれに対して個別に呼び出す（10 §3.3 拡張）．
    /// </summary>
    /// <param name="observerTransform"><see cref="observerLeft"/> または <see cref="observerRight"/></param>
    /// <param name="missingWarned">その目用の「未設定警告を出した」フラグ（呼び出し元が保持）</param>
    /// <param name="eyeLabel">警告メッセージに出す目の名前（"Left"／"Right"）</param>
    private FarAnchor ComputeFarAnchor(Vector3 apexPos, Quaternion apexRot,
        Transform observerTransform, ref bool missingWarned, string eyeLabel)
    {
        if (!farAtInfinity) return FarAnchor.Identity;

        if (observerTransform == null)
        {
            if (!missingWarned)
            {
                Debug.LogWarning("[ConeGuide] " + name + ": Far At Infinity が有効ですが Observer"
                    + eyeLabel + " が未設定のため，そちらの目は通常の有限距離（頂点基準）として描画します。", this);
                missingWarned = true;
            }
            return FarAnchor.Identity;
        }
        missingWarned = false;

        Quaternion apexRotInv = Quaternion.Inverse(apexRot);
        return new FarAnchor
        {
            offset = apexRotInv * (observerTransform.position - apexPos),
            rotation = apexRotInv * observerTransform.rotation,
        };
    }

    /// <summary>
    /// 距離 d・半幅 w の矩形断面の4隅（ローカル座標，反時計回り）を，
    /// <paramref name="anchor"/>（Far At Infinity 用の位置・向きの再アンカリング）を適用して返す．
    /// 錐の軸は +Z（カメラの前方）．<paramref name="anchor"/> は通常
    /// <see cref="FarAnchor.Identity"/>（＝頂点そのものが原点，向きも素通し）を渡す．
    /// </summary>
    private static Vector3[] Corners(float d, float w, FarAnchor anchor)
    {
        return new[]
        {
            anchor.offset + anchor.rotation * new Vector3(-w, -w, d),
            anchor.offset + anchor.rotation * new Vector3( w, -w, d),
            anchor.offset + anchor.rotation * new Vector3( w,  w, d),
            anchor.offset + anchor.rotation * new Vector3(-w,  w, d),
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
    private void AddSegment(MeshBuffer buf, Vector3 a, Vector3 b, float halfA, float halfB, Color colorA, Color colorB)
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

        int baseIndex = buf.vertices.Count;
        for (int k = 0; k < 4; k++)
        {
            buf.vertices.Add(a + offset[k] * halfA);
            buf.colors.Add(colorA);
        }
        for (int k = 0; k < 4; k++)
        {
            buf.vertices.Add(b + offset[k] * halfB);
            buf.colors.Add(colorB);
        }

        // 側面 4 枚
        for (int k = 0; k < 4; k++)
        {
            int k2 = (k + 1) % 4;
            AddQuad(buf, baseIndex + k, baseIndex + k2, baseIndex + 4 + k2, baseIndex + 4 + k);
        }
        // 端面 2 枚（線の端でも深度が正しく書かれるように閉じておく）
        AddQuad(buf, baseIndex + 3, baseIndex + 2, baseIndex + 1, baseIndex + 0);
        AddQuad(buf, baseIndex + 4, baseIndex + 5, baseIndex + 6, baseIndex + 7);
    }

    /// <summary>四角形（頂点4つ）を三角形2枚としてインデックスに追加する</summary>
    private static void AddQuad(MeshBuffer buf, int i0, int i1, int i2, int i3)
    {
        buf.triangles.Add(i0); buf.triangles.Add(i1); buf.triangles.Add(i2);
        buf.triangles.Add(i0); buf.triangles.Add(i2); buf.triangles.Add(i3);
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

        if (Split)
        {
            EnsureSplitChild(ref farObject, ref farMesh, "ConeGuide_Far");
            EnsureSplitChild(ref ridgeObject, ref ridgeMesh, "ConeGuide_Ridge");
        }
        else
        {
            ReleaseSplitChildren();
        }

        if (farAtInfinity)
        {
            EnsureSplitChild(ref farLeftObject, ref farLeftMesh, "ConeGuide_FarLeft");
            EnsureSplitChild(ref farRightObject, ref farRightMesh, "ConeGuide_FarRight");
        }
        else
        {
            ReleaseStereoFarChildren();
        }

        return true;
    }

    /// <summary>
    /// 遠断面／稜線／遠断面の左目用・右目用を独立レイヤで描くための子オブジェクト
    /// （MeshFilter+MeshRenderer）を用意する．メイン（近断面）と同じマテリアルを共有し，
    /// シーンには保存しない．
    /// </summary>
    private void EnsureSplitChild(ref GameObject child, ref Mesh childMesh, string name)
    {
        if (child == null)
        {
            child = new GameObject(name)
            {
                hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy,
            };
            child.transform.SetParent(transform, false);
            child.AddComponent<MeshFilter>();
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        if (childMesh == null)
        {
            childMesh = new Mesh
            {
                name = name + "Mesh",
                hideFlags = HideFlags.DontSave,
            };
            child.GetComponent<MeshFilter>().sharedMesh = childMesh;
            meshDirty = true;
        }

        child.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private void ReleaseSplitChildren()
    {
        SafeDestroy(farMesh);
        SafeDestroy(ridgeMesh);
        farMesh = null;
        ridgeMesh = null;
        if (farObject != null) SafeDestroy(farObject);
        if (ridgeObject != null) SafeDestroy(ridgeObject);
        farObject = null;
        ridgeObject = null;
    }

    /// <summary>farAtInfinity が OFF になったときに，遠断面の左目用・右目用の子オブジェクトを解放する</summary>
    private void ReleaseStereoFarChildren()
    {
        SafeDestroy(farLeftMesh);
        SafeDestroy(farRightMesh);
        farLeftMesh = null;
        farRightMesh = null;
        if (farLeftObject != null) SafeDestroy(farLeftObject);
        if (farRightObject != null) SafeDestroy(farRightObject);
        farLeftObject = null;
        farRightObject = null;
    }

    private void ReleaseResources()
    {
        ReleaseSplitChildren();
        ReleaseStereoFarChildren();
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
        public bool ridges, apex, dual, infinity;
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
                infinity = g.farAtInfinity,
                line = g.lineColor,
                nearC = g.nearColor,
                farC = g.farColor,
            };
        }

        public bool Equals(GeometryKey o)
        {
            return near == o.near && far == o.far && alpha == o.alpha && width == o.width
                && sections == o.sections && ridges == o.ridges && apex == o.apex && dual == o.dual
                && infinity == o.infinity
                && line == o.line && nearC == o.nearC && farC == o.farC;
        }
    }
}
