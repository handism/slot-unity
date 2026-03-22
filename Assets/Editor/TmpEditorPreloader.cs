using TMPro;
using UnityEditor;

/// <summary>
/// Play モード開始前に TMP Settings をエディタメモリにプリロードする。
/// Unity 6 でドメインリロード後にシーンビューが描画される際、TMP がフォントを
/// 見つけられず "Can't Generate Mesh, No Font Asset has been assigned." を
/// 出力する既知バグへの対処。
/// </summary>
[InitializeOnLoad]
internal static class TmpEditorPreloader
{
    static TmpEditorPreloader()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            // TMP Settings をメモリに乗せ、デフォルトフォントを確定させる
            _ = TMP_Settings.defaultFontAsset;
        }
    }
}
