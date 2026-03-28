using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SlotGame.View;

namespace SlotGame.Editor
{
    public static class BonusRoundUIFixer
    {
        [MenuItem("SlotGame/Fix BonusRound UI")]
        public static void FixUI()
        {
            string scenePath = "Assets/Scenes/BonusRound.unity";
            var scene = EditorSceneManager.OpenScene(scenePath);
            if (!scene.IsValid())
            {
                Debug.LogError($"[BonusRoundUIFixer] Could not find scene at {scenePath}");
                return;
            }

            var view = Object.FindFirstObjectByType<BonusRoundView>();
            if (view == null)
            {
                Debug.LogError("[BonusRoundUIFixer] BonusRoundView not found in scene.");
                return;
            }

            GameObject panelGO = view.gameObject;
            Transform panelT = panelGO.transform;

            // 1. Instruction Text
            Transform instructionT = panelT.Find("InstructionText");
            GameObject instructionGO;
            if (instructionT == null)
            {
                instructionGO = new GameObject("InstructionText", typeof(RectTransform));
                instructionGO.transform.SetParent(panelT, false);
                var tmp = instructionGO.AddComponent<TextMeshProUGUI>();
                tmp.text = "宝箱を 3 個選んでください！";
                tmp.fontSize = 32;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;

                var rt = instructionGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 0.75f);
                rt.anchorMax = new Vector2(1, 0.85f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
            else
            {
                instructionGO = instructionT.gameObject;
            }

            // 2. Result Panel
            Transform resultPanelT = panelT.Find("ResultPanel");
            GameObject resultPanelGO;
            GameObject resultTextGO = null;
            Button okBtn = null;

            if (resultPanelT == null)
            {
                resultPanelGO = new GameObject("ResultPanel", typeof(Image));
                resultPanelGO.transform.SetParent(panelT, false);
                resultPanelGO.GetComponent<Image>().color = new Color(0, 0, 0, 0.8f);
                
                var rt = resultPanelGO.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;

                // 3. Result Multiplier Text
                resultTextGO = new GameObject("ResultMultiplierText", typeof(RectTransform));
                resultTextGO.transform.SetParent(resultPanelGO.transform, false);
                var tmp = resultTextGO.AddComponent<TextMeshProUGUI>();
                tmp.text = "合計倍率　×0";
                tmp.fontSize = 48;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.yellow;

                var rtText = resultTextGO.GetComponent<RectTransform>();
                rtText.anchorMin = new Vector2(0, 0.5f);
                rtText.anchorMax = new Vector2(1, 0.7f);
                rtText.offsetMin = rtText.offsetMax = Vector2.zero;

                // 4. OK Button
                var okBtnGO = new GameObject("OKButton", typeof(Image));
                okBtnGO.transform.SetParent(resultPanelGO.transform, false);
                okBtnGO.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.2f);
                okBtn = okBtnGO.AddComponent<Button>();

                var rtBtn = okBtnGO.GetComponent<RectTransform>();
                rtBtn.sizeDelta = new Vector2(200, 80);
                rtBtn.anchoredPosition = new Vector2(0, -100);

                var okLabelGO = new GameObject("Label", typeof(RectTransform));
                okLabelGO.transform.SetParent(okBtnGO.transform, false);
                var tmpLabel = okLabelGO.AddComponent<TextMeshProUGUI>();
                tmpLabel.text = "OK";
                tmpLabel.fontSize = 24;
                tmpLabel.alignment = TextAlignmentOptions.Center;
                tmpLabel.color = Color.white;
                
                var rtLabel = okLabelGO.GetComponent<RectTransform>();
                rtLabel.anchorMin = Vector2.zero;
                rtLabel.anchorMax = Vector2.one;
                rtLabel.offsetMin = rtLabel.offsetMax = Vector2.zero;

                // 5. Link OK Button onClick
                UnityEventTools.AddPersistentListener(okBtn.onClick, view.OnResultDismissed);
                
                resultPanelGO.SetActive(false);
            }
            else
            {
                resultPanelGO = resultPanelT.gameObject;
                var t = resultPanelT.Find("ResultMultiplierText");
                if (t != null) resultTextGO = t.gameObject;
            }

            // Wire fields to BonusRoundView
            var so = new SerializedObject(view);
            
            SetProperty(so, "instructionText", instructionGO.GetComponent<TMP_Text>());
            SetProperty(so, "resultPanel", resultPanelGO);
            if (resultTextGO != null)
                SetProperty(so, "resultMultiplierText", resultTextGO.GetComponent<TMP_Text>());
            
            so.ApplyModifiedProperties();

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[BonusRoundUIFixer] BonusRound UI setup completed and scene saved.");
        }

        private static void SetProperty(SerializedObject so, string name, Object value)
        {
            var prop = so.FindProperty(name);
            if (prop == null)
            {
                Debug.LogError($"[BonusRoundUIFixer] Property '{name}' not found on {so.targetObject.name}. Make sure the code is compiled and the field name is correct.");
                return;
            }
            prop.objectReferenceValue = value;
        }
    }
}
