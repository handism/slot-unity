using UnityEditor;
using UnityEngine;
using SlotGame.View;
using SlotGame.Data;

namespace SlotGame.Editor
{
    public static class ViewSetupTool
    {
        private const string PrefabPath = "Assets/Prefabs/PaylineView.prefab";

        [MenuItem("SlotGame/Setup Payline View")]
        public static void SetupPaylineView()
        {
            // --- Prefab の作成 ---
            var go = new GameObject("PaylineView");
            var lr = go.AddComponent<LineRenderer>();
            go.AddComponent<PaylineView>();

            // LineRenderer の設定
            lr.startWidth = 20f;
            lr.endWidth   = 20f;
            lr.positionCount = 0;
            lr.useWorldSpace = true; // RectTransform の世界座標を使うので true
            
            // マテリアル設定 (Sprites-Default を探し、無ければ代用)
            var material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            if (material != null)
            {
                lr.material = material;
            }

            // Prefab として保存
            if (!System.IO.Directory.Exists("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            
            Debug.Log($"[ViewSetupTool] Created PaylineView Prefab at {PrefabPath}");

            // --- Scene 内の UIManager へのアサイン ---
            var uiManager = Object.FindAnyObjectByType<UIManager>();
            if (uiManager == null)
            {
                Debug.LogWarning("[ViewSetupTool] UIManager not found in the current scene. Please open the 'Main' scene.");
                return;
            }

            var paylinePrefab = AssetDatabase.LoadAssetAtPath<PaylineView>(PrefabPath);
            var paylineData   = AssetDatabase.LoadAssetAtPath<PaylineData>("Assets/ScriptableObjects/Paylines/PaylineData.asset");

            // UIManager の SerializeField にアサイン（EditorUtility でシリアライズ情報を取得）
            var so = new SerializedObject(uiManager);
            so.FindProperty("paylinePrefab").objectReferenceValue = paylinePrefab;
            so.FindProperty("paylineData").objectReferenceValue   = paylineData;
            
            // paylineParent を自動設定（あれば）
            var parentGo = GameObject.Find("PaylineContainer"); // 決め打ち（無ければ UIManager 直下）
            if (parentGo != null)
                so.FindProperty("paylineParent").objectReferenceValue = parentGo.transform;

            so.ApplyModifiedProperties();
            
            Debug.Log("[ViewSetupTool] Assigned PaylineView and PaylineData to UIManager successfully!");
        }
    }
}
