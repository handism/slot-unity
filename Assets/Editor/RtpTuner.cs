#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SlotGame.Utility.Editor
{
    /// <summary>
    /// RTP 調整用の便利メニュー。
    /// アセット再生成と RTP 計算を一度に実行する。
    /// </summary>
    public static class RtpTuner
    {
        [MenuItem("SlotGame/Tune RTP (Recreate Assets & Calculate)")]
        public static void TuneRtp()
        {
            if (!EditorUtility.DisplayDialog(
                "Tune RTP",
                "This will delete all existing Symbol, Reel, Payline, and PayoutTable assets and recreate them. Are you sure?",
                "Yes, Tune RTP",
                "Cancel"))
            {
                return;
            }

            // 1. Delete existing assets
            DeleteAssets();

            // 2. Recreate all assets
            SlotGame.Editor.ScriptableObjectCreator.CreateAllAssets();

            // 3. Calculate RTP
            RtpCalculator.Calculate();
        }

        private static void DeleteAssets()
        {
            // Note: This is a simple and potentially destructive operation.
            // It's fine for this project's context.
            DeleteDirectory("Assets/ScriptableObjects/Symbols");
            DeleteDirectory("Assets/ScriptableObjects/Reels");
            DeleteDirectory("Assets/ScriptableObjects/Paylines");
            DeleteDirectory("Assets/ScriptableObjects/PayoutTable");

            AssetDatabase.Refresh();
        }

        private static void DeleteDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                if (!AssetDatabase.DeleteAsset(path))
                {
                    Debug.LogError($"Failed to delete directory: {path}. It might not be empty. Trying to delete contents.");
                    // Attempt to delete files inside if directory deletion fails
                    var guids = AssetDatabase.FindAssets("", new[] { path });
                    foreach (var guid in guids)
                    {
                        var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                        AssetDatabase.DeleteAsset(assetPath);
                    }
                    AssetDatabase.DeleteAsset(path); // Try again
                }
            }
             AssetDatabase.Refresh();
        }
    }
}
#endif
