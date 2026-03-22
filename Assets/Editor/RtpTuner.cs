#if UNITY_EDITOR
using SlotGame.Core;
using SlotGame.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
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
                "This will update Symbol payouts and recreate Reel/Payline/PayoutTable assets. Sprite references are preserved. Are you sure?",
                "Yes, Tune RTP",
                "Cancel"))
            {
                return;
            }

            // 1. Delete existing assets
            DeleteAssets();

            // 2. Recreate all assets
            SlotGame.Editor.ScriptableObjectCreator.CreateAllAssets();

            // 3. シーンの GameManager に Reel Strips を自動アサイン
            AssignReelStripsToScene();

            // 4. Calculate RTP
            RtpCalculator.Calculate();
        }

        private static void DeleteAssets()
        {
            // Symbols は sprite 参照、Reels は Scene の SerializeField 参照を保持するため削除しない。
            // 各 Create メソッドが既存アセットの中身のみ上書きする。
            DeleteDirectory("Assets/ScriptableObjects/Paylines");
            DeleteDirectory("Assets/ScriptableObjects/PayoutTable");

            AssetDatabase.Refresh();
        }

        private static void AssignReelStripsToScene()
        {
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            if (gameManager == null)
            {
                Debug.LogWarning("[RtpTuner] シーン上に GameManager が見つかりません。Reel Strips の自動アサインをスキップします。");
                return;
            }

            var so   = new SerializedObject(gameManager);
            var prop = so.FindProperty("reelStrips");
            prop.arraySize = 5;
            for (int i = 0; i < 5; i++)
            {
                var strip = AssetDatabase.LoadAssetAtPath<ReelStripData>(
                    $"Assets/ScriptableObjects/Reels/Reel{i}.asset");
                prop.GetArrayElementAtIndex(i).objectReferenceValue = strip;
            }
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(gameManager.gameObject.scene);
            Debug.Log("[RtpTuner] GameManager.reelStrips を自動アサインしました。シーンを保存してください。");
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
