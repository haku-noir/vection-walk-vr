using UnityEditor;
using UnityEngine;

/// <summary>
/// <see cref="FollowingExperimentManager"/> のカスタムInspector．
///
/// 錐ガイドの提示条件（背景/箱/ガイドの各モード・極性・周波数・遅延量など）は
/// 実体として <see cref="ChannelCompositor"/>（左目用＝マスター，<c>LeftRawImage</c>上に
/// ある）に存在するが，実験を操作するユーザーが毎回 <c>LeftRawImage</c> まで探しに行く
/// 必要があるのは分かりにくい（10 仕様，両眼立体視化のユーザーフィードバックで指摘）．
///
/// このエディタは，<see cref="FollowingExperimentManager.channelCompositor"/>（マスター）
/// の Inspector をそのまま <c>ExperimentRig</c> の Inspector に埋め込んで表示する．
/// <b>データの複製やPush/Pull同期は一切行わない</b>——ここに表示されているのは
/// <c>LeftRawImage</c> 上の ChannelCompositor 本体そのもの（同じ <see cref="SerializedObject"/>）
/// であり，ここで編集すればそのまま本体が変わる．右目用への反映は既存の
/// <see cref="ChannelCompositor.mirrorFrom"/>（Play中，毎フレーム）がそのまま働く．
/// </summary>
[CustomEditor(typeof(FollowingExperimentManager))]
public class FollowingExperimentManagerEditor : Editor
{
    private Editor compositorEditor;
    private bool showCompositor = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var manager = (FollowingExperimentManager)target;
        ChannelCompositor compositor = manager.channelCompositor;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("錐ガイド設定（両眼立体視）", EditorStyles.boldLabel);

        if (compositor == null)
        {
            EditorGUILayout.HelpBox(
                "Channel Compositor が未設定です。"
                + "メニュー「Tools > 視点追従実験 > 錐ガイドを現在のシーンに追加」を実行してください。",
                MessageType.Info);
            return;
        }

        showCompositor = EditorGUILayout.Foldout(showCompositor,
            "ChannelCompositor（左目用＝マスター。ここでの変更が本体そのもの）", true);
        if (!showCompositor) return;

        EditorGUILayout.HelpBox(
            "ここは LeftRawImage 上の ChannelCompositor 本体を直接表示・編集しています。"
            + "Play中は Mirror From 経由で右目用へ自動的に反映されます。",
            MessageType.None);

        if (compositorEditor == null || compositorEditor.target != compositor)
        {
            if (compositorEditor != null) DestroyImmediate(compositorEditor);
            compositorEditor = CreateEditor(compositor);
        }

        EditorGUI.indentLevel++;
        compositorEditor.OnInspectorGUI();
        EditorGUI.indentLevel--;

        EditorGUILayout.Space();
        if (GUILayout.Button("LeftRawImage を選択（本体を直接見る）"))
        {
            Selection.activeObject = compositor.gameObject;
        }
    }

    private void OnDisable()
    {
        if (compositorEditor != null)
        {
            DestroyImmediate(compositorEditor);
            compositorEditor = null;
        }
    }
}
