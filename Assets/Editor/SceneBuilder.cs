using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using SlotGame.Audio;
using SlotGame.Core;
using SlotGame.Data;
using SlotGame.View;

namespace SlotGame.Editor
{
    /// <summary>
    /// SlotGame/Build All Scenes メニューから Boot / Main / BonusRound の
    /// 3 シーンを自動構築し、Build Settings に登録する。
    /// 既存シーンは上書き（冪等実行対応）。
    /// </summary>
    public static class SceneBuilder
    {
        private const string ScenesPath   = "Assets/Scenes";
        private const string PrefabPath   = "Assets/Art/Prefabs/SymbolView.prefab";
        private const string SOBasePath   = "Assets/ScriptableObjects";
        private const string AudioBasePath = "Assets/Audio";
        private const string BgmNormalPath      = AudioBasePath + "/BGM/bgm_normal.mp3";
        private const string BgmFreeSpinPath    = AudioBasePath + "/BGM/bgm_free_spin.mp3";
        private const string BgmBonusRoundPath  = AudioBasePath + "/BGM/bgm_bonus_round.mp3";
        private const string SeSpinStartPath    = AudioBasePath + "/SE/se_spin_start.mp3";
        private const string SeReelStopPath     = AudioBasePath + "/SE/se_reel_stop.mp3";
        private const string SeSmallWinPath     = AudioBasePath + "/SE/se_small_win.mp3";
        private const string SeBigWinPath       = AudioBasePath + "/SE/se_big_win.mp3";
        private const string SeMegaWinPath      = AudioBasePath + "/SE/se_mega_win.mp3";
        private const string SeScatterAppearPath = AudioBasePath + "/SE/se_scatter_appear.mp3";
        private const string SeButtonClickPath  = AudioBasePath + "/SE/se_button_click.mp3";

        private const string SpriteDragonPath  = "Assets/Art/Sprites/Generated/Dragon.png";
        private const string SpriteWildPath    = "Assets/Art/Sprites/Generated/Wild.png";
        private const string SpritePhoenixPath = "Assets/Art/Sprites/Generated/Phoenix.png";

        // ─── エントリーポイント ─────────────────────────────────────────

        [MenuItem("SlotGame/Build All Scenes")]
        public static void BuildAllScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[SceneBuilder] Build All Scenes cannot run during Play Mode. Stop Play Mode and run it again.");
                return;
            }

            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets", "Art");
            EnsureFolder("Assets/Art", "Prefabs");

            CreateSymbolViewPrefab();
            BuildBootScene();
            BuildTitleScene();
            BuildMainScene();
            BuildBonusRoundScene();
            AddScenesToBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SceneBuilder] All scenes built successfully!");
        }

        [MenuItem("SlotGame/Build All Scenes", true)]
        private static bool ValidateBuildAllScenes()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        // ─── SymbolView Prefab ──────────────────────────────────────────

        private static void CreateSymbolViewPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                Debug.Log("[SceneBuilder] SymbolView.prefab already exists — skipped.");
                return;
            }

            var go = new GameObject("SymbolView");
            go.AddComponent<Image>();
            go.AddComponent<SymbolView>();
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 180);

            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);

            Debug.Log("[SceneBuilder] SymbolView.prefab created.");
        }

        // ─── Boot.unity ─────────────────────────────────────────────────

        private static void BuildBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // EventSystem
            CreateEventSystem();

            // Canvas
            var canvas = CreateCanvas("Canvas", RenderMode.ScreenSpaceOverlay);

            // ProgressBar (Slider)
            var sliderGO = new GameObject("ProgressBar", typeof(Slider));
            SetParent(sliderGO, canvas);
            var rt = sliderGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.1f);
            rt.anchorMax = new Vector2(0.9f, 0.15f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var slider = sliderGO.GetComponent<Slider>();

            // BootManager
            var bootGO = new GameObject("BootManager");
            SceneManager.MoveGameObjectToScene(bootGO, scene);
            var boot = bootGO.AddComponent<BootManager>();
            WireField(boot, "progressBar", slider);
            WireField(boot, "gameConfig", AssetDatabase.LoadAssetAtPath<GameConfigData>($"{SOBasePath}/GameConfig.asset"));

            EditorSceneManager.SaveScene(scene, $"{ScenesPath}/Boot.unity");
            Debug.Log("[SceneBuilder] Boot.unity built.");
        }

        private static void BuildTitleScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);

            // EventSystem
            CreateEventSystem();

            // Main Camera
            CreateUICamera(scene, "Main Camera", new Color(0.02f, 0.03f, 0.06f));

            // Canvas
            var canvas = CreateCanvas("Canvas", RenderMode.ScreenSpaceOverlay);

            // Background
            var bg = new GameObject("Background", typeof(Image));
            SetParent(bg, canvas);
            StretchFull(bg);
            StyleImage(bg.GetComponent<Image>(), new Color(0.02f, 0.03f, 0.06f));

            // Decorative Glows
            var glow1 = new GameObject("Glow1", typeof(Image));
            SetParent(glow1, bg);
            AnchorCenter(glow1, new Vector2(-400f, 200f), new Vector2(1200f, 1200f));
            StyleImage(glow1.GetComponent<Image>(), new Color(0.1f, 0.2f, 0.5f, 0.15f));

            var glow2 = new GameObject("Glow2", typeof(Image));
            SetParent(glow2, bg);
            AnchorCenter(glow2, new Vector2(400f, -200f), new Vector2(1000f, 1000f));
            StyleImage(glow2.GetComponent<Image>(), new Color(0.4f, 0.1f, 0.3f, 0.12f));

            // Floating Symbols
            var sym1 = CreateDecorativeSymbol(bg, "Symbol_Dragon", SpriteDragonPath, new Vector2(-600f, -100f), 240f, 0.4f);
            var sym2 = CreateDecorativeSymbol(bg, "Symbol_Wild", SpriteWildPath, new Vector2(650f, 250f), 200f, 0.3f);
            var sym3 = CreateDecorativeSymbol(bg, "Symbol_Phoenix", SpritePhoenixPath, new Vector2(-550f, 350f), 180f, 0.25f);

            // Logo Shadow (Glow effect)
            var logoGlow = CreateTMPText(canvas, "LogoGlow", "FANTASY SLOT", 124);
            StyleHeadline(logoGlow.GetComponent<TMP_Text>(), 12f);
            logoGlow.GetComponent<TMP_Text>().color = new Color(0.24f, 0.76f, 0.95f, 0.3f);
            AnchorCenter(logoGlow, new Vector2(0f, 190f), new Vector2(1500f, 200f));

            // Logo
            var logo = CreateTMPText(canvas, "Logo", "FANTASY SLOT", 150);
            StyleHeadline(logo.GetComponent<TMP_Text>(), 20f);
            var logoText = logo.GetComponent<TMP_Text>();
            logoText.textWrappingMode = TextWrappingModes.NoWrap;
            logoText.overflowMode = TextOverflowModes.Overflow;
            logoText.color = new Color(1f, 0.9f, 0.5f, 1f);
            AnchorCenter(logo, new Vector2(0f, 220f), new Vector2(1500f, 220f));

            // Start Button
            var startBtnGO = CreateButton(canvas, "StartButton", "START GAME", new Vector2(480, 110), new Color(0.95f, 0.72f, 0.22f));
            AnchorCenter(startBtnGO, new Vector2(0f, -240f), new Vector2(480f, 110f));
            var startBtn = startBtnGO.GetComponent<Button>();
            var startBtnCG = startBtnGO.AddComponent<CanvasGroup>();

            // TitleManager
            var titleGO = new GameObject("TitleManager");
            SceneManager.MoveGameObjectToScene(titleGO, scene);
            var titleManager = titleGO.AddComponent<TitleManager>();
            var effects = titleGO.AddComponent<TitleEffects>();

            // Wiring Effects
            WireField(effects, "logoTransform", logo.GetComponent<RectTransform>());
            WireField(effects, "startButtonTransform", startBtnGO.GetComponent<RectTransform>());
            WireField(effects, "startButtonCanvasGroup", startBtnCG);

            var effectsSO = new SerializedObject(effects);
            var glowProp = effectsSO.FindProperty("backgroundGlows");
            glowProp.arraySize = 2;
            glowProp.GetArrayElementAtIndex(0).objectReferenceValue = glow1.GetComponent<RectTransform>();
            glowProp.GetArrayElementAtIndex(1).objectReferenceValue = glow2.GetComponent<RectTransform>();

            var symProp = effectsSO.FindProperty("floatingSymbols");
            symProp.arraySize = 3;
            symProp.GetArrayElementAtIndex(0).objectReferenceValue = sym1.GetComponent<RectTransform>();
            symProp.GetArrayElementAtIndex(1).objectReferenceValue = sym2.GetComponent<RectTransform>();
            symProp.GetArrayElementAtIndex(2).objectReferenceValue = sym3.GetComponent<RectTransform>();
            effectsSO.ApplyModifiedPropertiesWithoutUndo();

            // Button Action
            UnityEventTools.AddPersistentListener(startBtn.onClick, titleManager.StartGame);

            EditorSceneManager.SaveScene(scene, $"{ScenesPath}/Title.unity");
            Debug.Log("[SceneBuilder] Title.unity built.");
        }

        private static GameObject CreateDecorativeSymbol(GameObject parent, string name, string spritePath, Vector2 pos, float size, float alpha)
        {
            var go = new GameObject(name, typeof(Image));
            SetParent(go, parent);
            var img = go.GetComponent<Image>();
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            img.color = new Color(1f, 1f, 1f, alpha);
            AnchorCenter(go, pos, new Vector2(size, size));
            return go;
        }

        // ─── Main.unity ─────────────────────────────────────────────────

        private static void BuildMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // EventSystem
            CreateEventSystem();

            // Main Camera
            var camGO = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(camGO, scene);
            var cam = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
            camGO.tag = "MainCamera";
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = new Color(0.03f, 0.05f, 0.09f);
            cam.orthographic     = true;
            cam.orthographicSize = 5f;

            // Main Canvas（Screen Space - Camera）
            var mainCanvasGO = CreateCanvas("Main Canvas", RenderMode.ScreenSpaceCamera);
            var mainCanvas   = mainCanvasGO.GetComponent<Canvas>();
            mainCanvas.worldCamera   = cam;
            mainCanvas.planeDistance = 100;
            SetupCanvasScaler(mainCanvasGO, 0.5f);

            // Background
            var bg = new GameObject("Background", typeof(Image));
            SetParent(bg, mainCanvasGO);
            StretchFull(bg);
            StyleImage(bg.GetComponent<Image>(), new Color(0.03f, 0.05f, 0.09f), new Color(0.01f, 0.02f, 0.05f, 0.88f), 0f);

            var upperGlow = new GameObject("UpperGlow", typeof(Image));
            SetParent(upperGlow, bg);
            StretchTo(upperGlow, new Vector2(-0.05f, 0.58f), new Vector2(1.05f, 1.04f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            StyleImage(upperGlow.GetComponent<Image>(), new Color(0.07f, 0.17f, 0.28f, 0.38f));

            var lowerGlow = new GameObject("LowerGlow", typeof(Image));
            SetParent(lowerGlow, bg);
            StretchTo(lowerGlow, new Vector2(-0.05f, -0.04f), new Vector2(1.05f, 0.32f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            StyleImage(lowerGlow.GetComponent<Image>(), new Color(0.05f, 0.12f, 0.22f, 0.28f));

            // ReelGrid + 5 reels
            var (reelGrid, reelControllers) = CreateReelGrid(mainCanvasGO);

            // WinPopup
            var (winPopupGO, winPopupView) = CreateWinPopup(mainCanvasGO);

            // SettingsPanel
            var (settingsPanelGO, settingsView) = CreateSettingsPanel(mainCanvasGO);
            settingsPanelGO.SetActive(false);

            // PaytablePanel
            var (paytablePanelGO, paytableView) = CreatePaytablePanel(mainCanvasGO);
            paytablePanelGO.SetActive(false);

            // HUD Canvas（Screen Space - Overlay）
            var hudCanvasGO = CreateCanvas("HUD Canvas", RenderMode.ScreenSpaceOverlay);
            hudCanvasGO.GetComponent<Canvas>().sortingOrder = 10;
            SetupCanvasScaler(hudCanvasGO, 0.5f);

            var (mainHUDGO, mainHUDView, spinButton, autoSpinButton, settingsButton, paytableButton, betButtons) = CreateMainHUD(hudCanvasGO);
            var (freeSpinGO, freeSpinView) = CreateFreeSpinHUD(hudCanvasGO);
            freeSpinGO.SetActive(false);

            // Managers
            var managerRoot = new GameObject("Managers");
            SceneManager.MoveGameObjectToScene(managerRoot, scene);

            var gameManagerGO  = CreateChild(managerRoot, "GameManagerGO");
            var spinManagerGO  = CreateChild(managerRoot, "SpinManagerGO");
            var bonusManagerGO = CreateChild(managerRoot, "BonusManagerGO");
            var uiManagerGO    = CreateChild(managerRoot, "UIManagerGO");
            var audioManagerGO = CreateChild(managerRoot, "AudioManagerGO");

            var gameManager  = gameManagerGO.AddComponent<GameManager>();
            var spinManager  = spinManagerGO.AddComponent<SpinManager>();
            var bonusManager = bonusManagerGO.AddComponent<BonusManager>();
            var uiManager    = uiManagerGO.AddComponent<UIManager>();
            var audioManager = audioManagerGO.AddComponent<AudioManager>();

            // AudioSources（AudioManager の子として追加）
            var bgmSourceGO = new GameObject("BGMSource");
            bgmSourceGO.transform.SetParent(audioManagerGO.transform, false);
            var bgmSource = bgmSourceGO.AddComponent<AudioSource>();
            bgmSource.loop        = true;
            bgmSource.playOnAwake = false;

            var seSourceGO = new GameObject("SESource");
            seSourceGO.transform.SetParent(audioManagerGO.transform, false);
            var seSource = seSourceGO.AddComponent<AudioSource>();
            seSource.playOnAwake = false;

            // ─── ワイヤリング ────────────────────────────────────────────

            // AudioManager
            WireField(audioManager, "bgmSource", bgmSource);
            WireField(audioManager, "seSource", seSource);
            WireAudioClips(audioManager);

            // UIManager
            WireField(uiManager, "mainHUD",     mainHUDView);
            WireField(uiManager, "freeSpinHUD", freeSpinView);
            WireField(uiManager, "winPopup",    winPopupView);
            WireField(uiManager, "settingsView", settingsView);
            WireField(uiManager, "paytableView", paytableView);

            // BonusManager
            WireField(bonusManager, "spinManager", spinManager);

            // SpinManager → ReelController[]
            var so    = new SerializedObject(spinManager);
            var reels = so.FindProperty("reels");
            reels.arraySize = 5;
            for (int i = 0; i < 5; i++)
                reels.GetArrayElementAtIndex(i).objectReferenceValue = reelControllers[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            // GameManager
            var gso = new SerializedObject(gameManager);
            gso.FindProperty("spinManager").objectReferenceValue  = spinManager;
            gso.FindProperty("bonusManager").objectReferenceValue = bonusManager;
            gso.FindProperty("uiManager").objectReferenceValue    = uiManager;
            gso.FindProperty("audioManager").objectReferenceValue = audioManager;

            var strips = gso.FindProperty("reelStrips");
            strips.arraySize = 5;
            for (int i = 0; i < 5; i++)
                strips.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<ReelStripData>($"{SOBasePath}/Reels/Reel{i}.asset");

            gso.FindProperty("paylineData").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<PaylineData>($"{SOBasePath}/Paylines/PaylineData.asset");
            gso.FindProperty("payoutData").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<PayoutTableData>($"{SOBasePath}/PayoutTable/PayoutTableData.asset");
            gso.FindProperty("gameConfig").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameConfigData>($"{SOBasePath}/GameConfig.asset");
            gso.ApplyModifiedPropertiesWithoutUndo();

            // HUD button bindings
            UnityEventTools.AddPersistentListener(spinButton.onClick, gameManager.OnSpinButtonPressed);
            UnityEventTools.AddIntPersistentListener(autoSpinButton.onClick, gameManager.OnAutoSpinButtonPressed, 10);
            UnityEventTools.AddPersistentListener(settingsButton.onClick, gameManager.OnSettingsButtonPressed);
            UnityEventTools.AddPersistentListener(paytableButton.onClick, gameManager.OnPaytableButtonPressed);
            for (int i = 0; i < betButtons.Length; i++)
            {
                int bet = new[] { 10, 20, 50, 100 }[i];
                UnityEventTools.AddIntPersistentListener(betButtons[i].onClick, gameManager.OnBetChanged, bet);
            }

            EditorSceneManager.SaveScene(scene, $"{ScenesPath}/Main.unity");
            Debug.Log("[SceneBuilder] Main.unity built.");
        }

        // ─── ReelGrid ──────────────────────────────────────────────────

        private static (GameObject, ReelController[]) CreateReelGrid(GameObject parent)
        {
            var shadowGO = new GameObject("ReelFrameShadow", typeof(Image));
            SetParent(shadowGO, parent);
            var shadowRT = shadowGO.GetComponent<RectTransform>();
            shadowRT.anchorMin        = new Vector2(0.5f, 0.5f);
            shadowRT.anchorMax        = new Vector2(0.5f, 0.5f);
            shadowRT.pivot            = new Vector2(0.5f, 0.5f);
            shadowRT.anchoredPosition = new Vector2(0f, -8f);
            shadowRT.sizeDelta        = new Vector2(1140f, 742f);
            StyleImage(shadowGO.GetComponent<Image>(), new Color(0f, 0f, 0f, 0.3f));

            var frameGO = new GameObject("ReelFrame", typeof(Image));
            SetParent(frameGO, parent);
            var frameRT = frameGO.GetComponent<RectTransform>();
            frameRT.anchorMin        = new Vector2(0.5f, 0.5f);
            frameRT.anchorMax        = new Vector2(0.5f, 0.5f);
            frameRT.pivot            = new Vector2(0.5f, 0.5f);
            frameRT.anchoredPosition = new Vector2(0f, 6f);
            frameRT.sizeDelta        = new Vector2(1120f, 720f);
            StyleImage(frameGO.GetComponent<Image>(), new Color(0.07f, 0.1f, 0.16f, 0.96f), new Color(0.2f, 0.63f, 0.88f, 0.16f), 3f);

            var bezelGO = new GameObject("ReelBezel", typeof(Image));
            SetParent(bezelGO, frameGO);
            StretchTo(bezelGO, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-534f, -334f), new Vector2(534f, 334f));
            StyleImage(bezelGO.GetComponent<Image>(), new Color(0.12f, 0.17f, 0.25f, 0.98f), new Color(0.95f, 0.72f, 0.22f, 0.24f), 2f);

            var stageGO = new GameObject("ReelStage", typeof(Image));
            SetParent(stageGO, bezelGO);
            StretchTo(stageGO, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(28f, 28f), new Vector2(-28f, -28f));
            StyleImage(stageGO.GetComponent<Image>(), new Color(0.96f, 0.98f, 1f, 0.95f), new Color(0.56f, 0.79f, 0.96f, 0.16f), 1.5f);

            var gridGO = new GameObject("ReelGrid", typeof(RectTransform));
            SetParent(gridGO, stageGO);

            var rt = gridGO.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(940, 540);

            var hlg = gridGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing             = 5f;
            hlg.childAlignment      = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth   = false;
            hlg.childControlHeight  = false;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var controllers = new ReelController[5];

            for (int i = 0; i < 5; i++)
            {
                var reelGO = new GameObject($"Reel{i}", typeof(RectTransform));
                SetParent(reelGO, gridGO);
                var reelRt = reelGO.GetComponent<RectTransform>();
                reelRt.sizeDelta = new Vector2(180f, 540f);

                // LayoutElement でサイズ固定
                var le = reelGO.AddComponent<LayoutElement>();
                le.preferredWidth  = 180f;
                le.preferredHeight = 540f;

                // RectMask2D でクリップ
                reelGO.AddComponent<RectMask2D>();

                // ReelController（RequireComponent で ReelView も自動追加）
                var rc = reelGO.AddComponent<ReelController>();

                // ReelView の symbolViewPrefab をワイヤリング
                var rv = reelGO.GetComponent<ReelView>();
                WireField(rv, "symbolViewPrefab", prefab);

                // ReelController の reelStrip をワイヤリング
                var strip = AssetDatabase.LoadAssetAtPath<ReelStripData>(
                    $"{SOBasePath}/Reels/Reel{i}.asset");
                WireField(rc, "reelStrip", strip);

                controllers[i] = rc;
            }

            return (gridGO, controllers);
        }

        // ─── WinPopup ──────────────────────────────────────────────────

        private static (GameObject, WinPopupView) CreateWinPopup(GameObject parent)
        {
            var go = new GameObject("WinPopup", typeof(RectTransform), typeof(CanvasGroup));
            SetParent(go, parent);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 16f);
            rt.sizeDelta        = new Vector2(760, 360);

            go.GetComponent<CanvasGroup>().alpha = 0;
            var view = go.AddComponent<WinPopupView>();

            var glow = new GameObject("Glow", typeof(Image));
            SetParent(glow, go);
            StretchFull(glow);
            StyleImage(glow.GetComponent<Image>(), new Color(0.12f, 0.63f, 0.95f, 0.18f));

            var plate = CreatePanel(go, "Plate", new Vector2(660f, 268f), new Color(0.05f, 0.08f, 0.13f, 0.94f));
            StyleImage(plate.GetComponent<Image>(), new Color(0.05f, 0.08f, 0.13f, 0.94f), new Color(0.95f, 0.73f, 0.23f, 0.32f), 3f);
            AddEdgeShadow(plate, new Color(0f, 0f, 0f, 0.35f), new Vector2(0f, -10f));

            var levelText = CreateTMPText(plate, "WinLevelText",  "WIN!", 44);
            var amtText   = CreateTMPText(plate, "WinAmountText", "0", 86);
            StyleHeadline(levelText.GetComponent<TMP_Text>(), 8f);
            StyleValueText(amtText.GetComponent<TMP_Text>(), 4f);

            var amtRT = amtText.GetComponent<RectTransform>();
            amtRT.anchorMin        = new Vector2(0f, 0.08f);
            amtRT.anchorMax        = new Vector2(1f, 0.72f);
            amtRT.offsetMin        = new Vector2(24f, 0f);
            amtRT.offsetMax        = new Vector2(-24f, 0f);

            var lvlRT = levelText.GetComponent<RectTransform>();
            lvlRT.anchorMin        = new Vector2(0f, 0.68f);
            lvlRT.anchorMax        = new Vector2(1f, 0.96f);
            lvlRT.offsetMin        = new Vector2(24f, 0f);
            lvlRT.offsetMax        = new Vector2(-24f, 0f);

            WireField(view, "winAmountText", amtText.GetComponent<TMP_Text>());
            WireField(view, "winLevelText",  levelText.GetComponent<TMP_Text>());

            return (go, view);
        }

        // ─── SettingsPanel ─────────────────────────────────────────────

        private static (GameObject, SettingsView) CreateSettingsPanel(GameObject parent)
        {
            var go = new GameObject("SettingsPanel", typeof(Image));
            SetParent(go, parent);
            StretchFull(go);
            StyleImage(go.GetComponent<Image>(), new Color(0.01f, 0.03f, 0.06f, 0.82f));

            var view = go.AddComponent<SettingsView>();

            var dialog = CreatePanel(go, "Dialog", new Vector2(780f, 470f), new Color(0.05f, 0.08f, 0.13f, 0.98f));
            StyleImage(dialog.GetComponent<Image>(), new Color(0.05f, 0.08f, 0.13f, 0.98f), new Color(0.25f, 0.78f, 0.96f, 0.2f), 2f);
            AddEdgeShadow(dialog, new Color(0f, 0f, 0f, 0.35f), new Vector2(0f, -12f));
            var title = CreateTMPText(dialog, "Title", "設定", 42);
            StyleHeadline(title.GetComponent<TMP_Text>(), 10f);
            StretchTo(title, new Vector2(0f, 0.82f), new Vector2(1f, 1f), new Vector2(0f, -16f), new Vector2(0f, -24f));

            var bgmLabel = CreateTMPText(dialog, "BGMLabel", "BGM", 28);
            var seLabel  = CreateTMPText(dialog, "SELabel",  "SE", 28);
            var bgmSlider  = CreateSlider(dialog, "BGMSlider");
            var seSlider   = CreateSlider(dialog, "SESlider");
            var bgmValText = CreateTMPText(dialog, "BGMValueText", "100%", 24);
            var seValText  = CreateTMPText(dialog, "SEValueText",  "100%", 24);
            StyleSectionLabel(bgmLabel.GetComponent<TMP_Text>());
            StyleSectionLabel(seLabel.GetComponent<TMP_Text>());
            StyleValueText(bgmValText.GetComponent<TMP_Text>(), 4f);
            StyleValueText(seValText.GetComponent<TMP_Text>(), 4f);
            var resetBtn   = CreateButton(dialog, "ResetCoinsButton", "コインリセット", new Vector2(240f, 62f), new Color(0.42f, 0.22f, 0.18f));
            var closeBtn   = CreateButton(dialog, "CloseButton", "閉じる", new Vector2(180f, 62f), new Color(0.14f, 0.24f, 0.38f));

            AnchorTopLeft(bgmLabel, new Vector2(84f, -138f), new Vector2(100f, 36f));
            AnchorTopLeft(bgmSlider, new Vector2(82f, -186f), new Vector2(470f, 30f));
            AnchorTopLeft(bgmValText, new Vector2(582f, -176f), new Vector2(110f, 44f));

            AnchorTopLeft(seLabel, new Vector2(84f, -242f), new Vector2(100f, 36f));
            AnchorTopLeft(seSlider, new Vector2(82f, -290f), new Vector2(470f, 30f));
            AnchorTopLeft(seValText, new Vector2(582f, -280f), new Vector2(110f, 44f));

            AnchorBottomLeft(resetBtn, new Vector2(80f, 52f), new Vector2(240f, 62f));
            AnchorBottomRight(closeBtn, new Vector2(-80f, 52f), new Vector2(180f, 62f));

            WireField(view, "bgmSlider",       bgmSlider.GetComponent<Slider>());
            WireField(view, "seSlider",        seSlider.GetComponent<Slider>());
            WireField(view, "bgmValueText",    bgmValText.GetComponent<TMP_Text>());
            WireField(view, "seValueText",     seValText.GetComponent<TMP_Text>());
            WireField(view, "resetCoinsButton", resetBtn.GetComponent<Button>());
            WireField(view, "closeButton",     closeBtn.GetComponent<Button>());

            return (go, view);
        }

        // ─── PaytablePanel ─────────────────────────────────────────────

        private static (GameObject, PaytableView) CreatePaytablePanel(GameObject parent)
        {
            var go = new GameObject("PaytablePanel", typeof(Image));
            SetParent(go, parent);
            StretchFull(go);
            StyleImage(go.GetComponent<Image>(), new Color(0.01f, 0.03f, 0.06f, 0.84f));

            var view = go.AddComponent<PaytableView>();

            var dialog = CreatePanel(go, "Dialog", new Vector2(1000f, 840f), new Color(0.05f, 0.08f, 0.13f, 0.98f));
            StyleImage(dialog.GetComponent<Image>(), new Color(0.05f, 0.08f, 0.13f, 0.98f), new Color(0.95f, 0.73f, 0.23f, 0.18f), 2f);
            AddEdgeShadow(dialog, new Color(0f, 0f, 0f, 0.35f), new Vector2(0f, -12f));
            var title = CreateTMPText(dialog, "Title", "配当表", 40);
            StyleHeadline(title.GetComponent<TMP_Text>(), 10f);
            StretchTo(title, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(200f, -74f), new Vector2(-200f, -18f));

            var header = new GameObject("HeaderRow", typeof(HorizontalLayoutGroup));
            SetParent(header, dialog);
            StretchTo(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(48f, -130f), new Vector2(-48f, -86f));
            var headerLayout = header.GetComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = PaytableView.ColumnSpacing;
            headerLayout.padding = new RectOffset((int)PaytableView.RowSidePadding, (int)PaytableView.RowSidePadding, 0, 0);
            headerLayout.childAlignment = TextAnchor.MiddleCenter;
            headerLayout.childControlWidth = false;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            StyleImage(header.AddComponent<Image>(), new Color(1f, 1f, 1f, 0.03f), new Color(0.24f, 0.77f, 0.95f, 0.22f), 1f);
            var symbolHeaderText = CreateSizedLabel(header, "シンボル", PaytableView.SymbolColumnWidth, 28).GetComponent<TMP_Text>();
            StyleSectionLabel(symbolHeaderText);
            symbolHeaderText.textWrappingMode = TextWrappingModes.NoWrap;
            var payout3HeaderText = CreateSizedLabel(header, "3", PaytableView.ColumnWidth, 28).GetComponent<TMP_Text>();
            StyleSectionLabel(payout3HeaderText);
            payout3HeaderText.textWrappingMode = TextWrappingModes.NoWrap;
            var payout4HeaderText = CreateSizedLabel(header, "4", PaytableView.ColumnWidth, 28).GetComponent<TMP_Text>();
            StyleSectionLabel(payout4HeaderText);
            payout4HeaderText.textWrappingMode = TextWrappingModes.NoWrap;
            var payout5HeaderText = CreateSizedLabel(header, "5", PaytableView.ColumnWidth, 28).GetComponent<TMP_Text>();
            StyleSectionLabel(payout5HeaderText);
            payout5HeaderText.textWrappingMode = TextWrappingModes.NoWrap;

            var scrollView = new GameObject("ScrollView", typeof(Image), typeof(Mask), typeof(ScrollRect));
            SetParent(scrollView, dialog);
            StretchTo(scrollView, Vector2.zero, Vector2.one, new Vector2(48f, 96f), new Vector2(-48f, -144f));
            StyleImage(scrollView.GetComponent<Image>(), new Color(1f, 1f, 1f, 0.03f), new Color(0.95f, 0.73f, 0.23f, 0.08f), 1f);
            var mask = scrollView.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            var contentRoot = new GameObject("ContentRoot", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            SetParent(contentRoot, scrollView);
            StretchTo(contentRoot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -8f), new Vector2(0f, -8f));
            var contentRect = contentRoot.GetComponent<RectTransform>();
            contentRect.pivot = new Vector2(0.5f, 1f);
            var contentLayout = contentRoot.GetComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 6f;
            contentLayout.padding = new RectOffset(0, 0, 0, 0);
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandHeight = false;
            contentRoot.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scrollView.GetComponent<ScrollRect>();
            scrollRect.viewport = scrollView.GetComponent<RectTransform>();
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 28f;

            var rowTemplate = new GameObject("RowTemplate", typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            SetParent(rowTemplate, dialog);
            rowTemplate.SetActive(false);
            StyleImage(rowTemplate.GetComponent<Image>(), new Color(1f, 1f, 1f, 0.05f), new Color(0.26f, 0.78f, 0.96f, 0.12f), 1f);
            var rowTemplateRT = rowTemplate.GetComponent<RectTransform>();
            rowTemplateRT.sizeDelta = new Vector2(0f, PaytableView.RowHeight);
            var rowLayout = rowTemplate.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = PaytableView.ColumnSpacing;
            rowLayout.padding = new RectOffset((int)PaytableView.RowSidePadding, (int)PaytableView.RowSidePadding, 6, 6);
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowTemplate.GetComponent<LayoutElement>().preferredHeight = PaytableView.RowHeight;

            var symbolCell = new GameObject("SymbolCell", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            SetParent(symbolCell, rowTemplate);
            symbolCell.GetComponent<LayoutElement>().preferredWidth = PaytableView.SymbolColumnWidth;
            StyleImage(symbolCell.GetComponent<Image>(), new Color(1f, 1f, 1f, 0.02f), new Color(0.95f, 0.73f, 0.23f, 0.08f), 1f);
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            SetParent(icon, symbolCell);
            AnchorCenter(icon, Vector2.zero, new Vector2(PaytableView.IconSize, PaytableView.IconSize));
            for (int i = 0; i < 3; i++)
            {
                var text = CreateSizedLabel(rowTemplate, "-", PaytableView.ColumnWidth, 24).GetComponent<TMP_Text>();
                StyleValueText(text, 2f);
                text.alignment = TextAlignmentOptions.Right;
            }

            var closeBtn = CreateButton(dialog, "CloseButton", "閉じる", new Vector2(180f, 58f), new Color(0.14f, 0.24f, 0.38f));
            AnchorBottomRight(closeBtn, new Vector2(-48f, 52f), new Vector2(180f, 58f));

            WireField(view, "contentRoot", contentRoot.GetComponent<Transform>());
            WireField(view, "closeButton", closeBtn.GetComponent<Button>());
            WireField(view, "rowPrefab", rowTemplate);

            return (go, view);
        }

        // ─── MainHUD ───────────────────────────────────────────────────

        private static (GameObject, MainHUDView, Button, Button, Button, Button, Button[]) CreateMainHUD(GameObject parent)
        {
            var go = new GameObject("MainHUD");
            SetParent(go, parent);
            StretchFull(go);

            var view = go.AddComponent<MainHUDView>();

            var topBar = new GameObject("TopBar", typeof(Image));
            SetParent(topBar, go);
            StretchTo(topBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(48f, -110f), new Vector2(-48f, -28f));
            StyleImage(topBar.GetComponent<Image>(), new Color(0.04f, 0.08f, 0.13f, 0.72f), new Color(0.24f, 0.76f, 0.95f, 0.18f), 2f);
            AddEdgeShadow(topBar, new Color(0f, 0f, 0f, 0.22f), new Vector2(0f, -8f));

            var logoText = CreateTMPText(topBar, "LogoText", "スロットマシーン", 42);
            AnchorTopLeft(logoText, new Vector2(28f, -14f), new Vector2(420f, 54f));
            var logoLabel = logoText.GetComponent<TMP_Text>();
            logoLabel.alignment = TextAlignmentOptions.Left;
            StyleHeadline(logoLabel, 10f);

            var paytableBtn = CreateButton(topBar, "PaytableButton", "配当表", new Vector2(190f, 58f), new Color(0.14f, 0.24f, 0.38f));
            var settingsBtn = CreateButton(topBar, "SettingsButton", "設定", new Vector2(190f, 58f), new Color(0.1f, 0.18f, 0.3f));
            AnchorTopRight(paytableBtn, new Vector2(-28f, -16f), new Vector2(190f, 58f));
            AnchorTopRight(settingsBtn, new Vector2(-234f, -16f), new Vector2(190f, 58f));

            var bottomBar = new GameObject("BottomBar", typeof(Image));
            SetParent(bottomBar, go);
            StretchTo(bottomBar, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(48f, 28f), new Vector2(-48f, 152f));
            StyleImage(bottomBar.GetComponent<Image>(), new Color(0.04f, 0.08f, 0.13f, 0.76f), new Color(0.95f, 0.72f, 0.22f, 0.14f), 2f);
            AddEdgeShadow(bottomBar, new Color(0f, 0f, 0f, 0.28f), new Vector2(0f, -8f));

            var blockBottom = 24f;
            var blockHeight = 72f;
            var blockSpacing = 16f;

            var coinCard = CreateInfoCard(bottomBar, "CoinCard", "COIN", "1,000", new Vector2(28f, blockBottom), new Vector2(200f, blockHeight));
            var winCard  = CreateInfoCard(bottomBar, "WinCard", "WIN", "------", new Vector2(28f + 200f + blockSpacing, blockBottom), new Vector2(200f, blockHeight));

            var betDock = new GameObject("BetDock", typeof(Image));
            SetParent(betDock, bottomBar);
            AnchorBottomLeft(betDock, new Vector2(28f + 200f + blockSpacing + 200f + blockSpacing, blockBottom), new Vector2(518f, blockHeight));
            StyleImage(betDock.GetComponent<Image>(), new Color(1f, 1f, 1f, 0.04f), new Color(0.24f, 0.76f, 0.95f, 0.12f), 1.5f);

            var betLabel = CreateTMPText(betDock, "BetLabel", "BET", 22);
            AnchorCenter(betLabel, new Vector2(-208f, 0f), new Vector2(76f, 34f));
            var betLabelText = betLabel.GetComponent<TMP_Text>();
            betLabelText.alignment = TextAlignmentOptions.Center;
            StyleSectionLabel(betLabelText);

            var autoSpinBtn = CreateButton(bottomBar, "AutoSpinButton", "オート x10", new Vector2(170f, 72f), new Color(0.14f, 0.24f, 0.38f));
            var spinBtn     = CreateButton(bottomBar, "SpinButton", "GO！", new Vector2(186f, 72f), new Color(0.95f, 0.72f, 0.22f));
            AnchorBottomRight(autoSpinBtn, new Vector2(-226f, 24f), new Vector2(170f, 72f));
            AnchorBottomRight(spinBtn, new Vector2(-28f, 24f), new Vector2(186f, 72f));

            var betValues  = new[] { 10, 20, 50, 100 };
            var betButtons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                var btn = CreateButton(betDock, $"BetButton{i}", betValues[i].ToString(), new Vector2(92f, 52f), new Color(0.16f, 0.23f, 0.37f));
                AnchorCenter(btn, new Vector2(-98f + i * 98f, 0f), new Vector2(92f, 52f));
                betButtons[i] = btn.GetComponent<Button>();
            }

            WireField(view, "coinText",       coinCard.Value.GetComponent<TMP_Text>());
            WireField(view, "winText",        winCard.Value.GetComponent<TMP_Text>());
            WireField(view, "spinButton",     spinBtn.GetComponent<Button>());
            WireField(view, "autoSpinButton", autoSpinBtn.GetComponent<Button>());

            var so = new SerializedObject(view);
            var btnProp = so.FindProperty("betButtons");
            btnProp.arraySize = 4;
            for (int i = 0; i < 4; i++)
                btnProp.GetArrayElementAtIndex(i).objectReferenceValue = betButtons[i];

            var valProp = so.FindProperty("betValues");
            valProp.arraySize = 4;
            for (int i = 0; i < 4; i++)
                valProp.GetArrayElementAtIndex(i).intValue = betValues[i];

            so.ApplyModifiedPropertiesWithoutUndo();

            return (
                go,
                view,
                spinBtn.GetComponent<Button>(),
                autoSpinBtn.GetComponent<Button>(),
                settingsBtn.GetComponent<Button>(),
                paytableBtn.GetComponent<Button>(),
                betButtons);
        }

        // ─── FreeSpinHUD ───────────────────────────────────────────────

        private static (GameObject, FreeSpinHUDView) CreateFreeSpinHUD(GameObject parent)
        {
            var go = new GameObject("FreeSpinHUD", typeof(Image));
            SetParent(go, parent);
            StyleImage(go.GetComponent<Image>(), new Color(0.04f, 0.08f, 0.13f, 0.78f), new Color(0.95f, 0.72f, 0.22f, 0.18f), 2f);
            AddEdgeShadow(go, new Color(0f, 0f, 0f, 0.25f), new Vector2(0f, -8f));

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 1f);
            rt.anchorMax        = new Vector2(0.5f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -126f);
            rt.sizeDelta        = new Vector2(620, 100);

            var view = go.AddComponent<FreeSpinHUDView>();

            var remainText  = CreateTMPText(go, "RemainingText", "フリースピン残り: 0", 30);
            var totalWinTxt = CreateTMPText(go, "TotalWinText",  "TOTAL WIN: 0", 24);
            StyleHeadline(remainText.GetComponent<TMP_Text>(), 8f);
            StyleValueText(totalWinTxt.GetComponent<TMP_Text>(), 4f);

            var remRT = remainText.GetComponent<RectTransform>();
            remRT.anchorMin        = new Vector2(0f, 0.44f);
            remRT.anchorMax        = new Vector2(1f, 1f);
            remRT.offsetMin        = new Vector2(18f, 0f);
            remRT.offsetMax        = new Vector2(-18f, 0f);

            var totRT = totalWinTxt.GetComponent<RectTransform>();
            totRT.anchorMin        = new Vector2(0f, 0f);
            totRT.anchorMax        = new Vector2(1f, 0.46f);
            totRT.offsetMin        = new Vector2(18f, 0f);
            totRT.offsetMax        = new Vector2(-18f, 0f);

            WireField(view, "remainingText", remainText.GetComponent<TMP_Text>());
            WireField(view, "totalWinText",  totalWinTxt.GetComponent<TMP_Text>());

            return (go, view);
        }

        // ─── BonusRound.unity ──────────────────────────────────────────

        private static void BuildBonusRoundScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);

            CreateEventSystem();

            var canvasGO = CreateCanvas("Canvas", RenderMode.ScreenSpaceOverlay);

            var panelGO = new GameObject("BonusRoundPanel", typeof(Image));
            SetParent(panelGO, canvasGO);
            StretchFull(panelGO);
            panelGO.GetComponent<Image>().color = new Color(0.1f, 0.05f, 0.15f, 0.95f);

            var view = panelGO.AddComponent<BonusRoundView>();

            // TotalWinText
            var totalWinText = CreateTMPText(panelGO, "TotalWinText", "0", 48);
            var twRT = totalWinText.GetComponent<RectTransform>();
            twRT.anchorMin        = new Vector2(0, 0.1f);
            twRT.anchorMax        = new Vector2(1, 0.25f);
            twRT.offsetMin        = twRT.offsetMax = Vector2.zero;

            // ChestGrid（3×3）
            var gridGO = new GameObject("ChestGrid", typeof(RectTransform));
            SetParent(gridGO, panelGO);
            var gridRT = gridGO.GetComponent<RectTransform>();
            gridRT.anchorMin        = new Vector2(0.5f, 0.5f);
            gridRT.anchorMax        = new Vector2(0.5f, 0.5f);
            gridRT.pivot            = new Vector2(0.5f, 0.5f);
            gridRT.anchoredPosition = Vector2.zero;
            gridRT.sizeDelta        = new Vector2(500, 500);

            var glg = gridGO.AddComponent<GridLayoutGroup>();
            glg.cellSize         = new Vector2(150, 150);
            glg.spacing          = new Vector2(10, 10);
            glg.constraint       = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount  = 3;
            glg.childAlignment   = TextAnchor.MiddleCenter;

            var chestButtons = new Button[9];
            var rewardTexts  = new TMP_Text[9];

            for (int i = 0; i < 9; i++)
            {
                var btnGO = new GameObject($"ChestButton{i}", typeof(Image));
                SetParent(btnGO, gridGO);
                btnGO.GetComponent<Image>().color = new Color(0.6f, 0.4f, 0.1f);

                var btn = btnGO.AddComponent<Button>();
                chestButtons[i] = btn;

                var rewardGO = CreateTMPText(btnGO, "RewardText", "?", 36);
                rewardTexts[i] = rewardGO.GetComponent<TMP_Text>();
                rewardTexts[i].alignment = TextAlignmentOptions.Center;
            }

            // BonusRoundView ワイヤリング
            var so = new SerializedObject(view);
            var chestProp = so.FindProperty("chestButtons");
            chestProp.arraySize = 9;
            for (int i = 0; i < 9; i++)
                chestProp.GetArrayElementAtIndex(i).objectReferenceValue = chestButtons[i];

            var rewardProp = so.FindProperty("rewardTexts");
            rewardProp.arraySize = 9;
            for (int i = 0; i < 9; i++)
                rewardProp.GetArrayElementAtIndex(i).objectReferenceValue = rewardTexts[i];

            so.FindProperty("totalWinText").objectReferenceValue = totalWinText.GetComponent<TMP_Text>();
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, $"{ScenesPath}/BonusRound.unity");
            Debug.Log("[SceneBuilder] BonusRound.unity built.");
        }

        // ─── Build Settings ────────────────────────────────────────────

        private static void AddScenesToBuildSettings()
        {
            var paths = new[]
            {
                $"{ScenesPath}/Boot.unity",
                $"{ScenesPath}/Title.unity",
                $"{ScenesPath}/Main.unity",
                $"{ScenesPath}/BonusRound.unity",
            };

            var buildScenes = new EditorBuildSettingsScene[paths.Length];
            for (int i = 0; i < paths.Length; i++)
                buildScenes[i] = new EditorBuildSettingsScene(paths[i], true);

            EditorBuildSettings.scenes = buildScenes;
            Debug.Log("[SceneBuilder] Build Settings updated.");
        }

        // ─── ユーティリティ ────────────────────────────────────────────

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static GameObject CreateCanvas(string name, RenderMode renderMode)
        {
            var go     = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = renderMode;

            var rectTransform = go.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.localScale = Vector3.one;

            SetupCanvasScaler(go, 0.5f);
            return go;
        }

        private static void SetupCanvasScaler(GameObject canvasGO, float match)
        {
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            if (scaler == null) return;
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = match;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
        }

        private static Camera CreateUICamera(Scene scene, string name, Color backgroundColor)
        {
            var camGO = new GameObject(name);
            SceneManager.MoveGameObjectToScene(camGO, scene);

            var cam = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
            camGO.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundColor;
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;

            return cam;
        }

        private static void SetParent(GameObject child, GameObject parent)
        {
            child.transform.SetParent(parent.transform, false);
        }

        private static GameObject CreateChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static void StretchFull(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin  = Vector2.zero;
            rt.anchorMax  = Vector2.one;
            rt.offsetMin  = Vector2.zero;
            rt.offsetMax  = Vector2.zero;
        }

        private static GameObject CreateTMPText(GameObject parent, string name, string text, float size)
        {
            var go  = new GameObject(name, typeof(RectTransform));
            SetParent(go, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = new Color(0.95f, 0.97f, 1f, 1f);
            return go;
        }

        private static GameObject CreateButton(GameObject parent, string name, string label)
        {
            return CreateButton(parent, name, label, new Vector2(170f, 56f), new Color(0.2f, 0.2f, 0.4f));
        }

        private static GameObject CreateButton(GameObject parent, string name, string label, Vector2 size, Color backgroundColor)
        {
            var go  = new GameObject(name, typeof(Image));
            SetParent(go, parent);
            var image = go.GetComponent<Image>();
            StyleImage(image, backgroundColor, new Color(1f, 1f, 1f, Mathf.Clamp01(backgroundColor.a * 0.18f + 0.08f)), 1.5f);
            AddEdgeShadow(go, new Color(0f, 0f, 0f, 0.22f), new Vector2(0f, -5f));
            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor      = backgroundColor;
            colors.highlightedColor = Color.Lerp(backgroundColor, Color.white, 0.14f);
            colors.pressedColor     = Color.Lerp(backgroundColor, Color.black, 0.18f);
            colors.selectedColor    = backgroundColor;
            colors.disabledColor    = new Color(backgroundColor.r * 0.5f, backgroundColor.g * 0.5f, backgroundColor.b * 0.5f, 0.55f);
            button.colors = colors;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;

            var accent = new GameObject("Accent", typeof(Image));
            SetParent(accent, go);
            StretchTo(accent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -8f), new Vector2(-10f, -4f));
            StyleImage(accent.GetComponent<Image>(), new Color(1f, 1f, 1f, 0.14f));

            var labelGO = CreateTMPText(go, "Label", label, 22);
            StretchFull(labelGO);
            var text = labelGO.GetComponent<TMP_Text>();
            text.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            text.characterSpacing = 5f;
            text.margin = new Vector4(8f, 6f, 8f, 6f);
            text.color = backgroundColor.grayscale > 0.6f
                ? new Color(0.12f, 0.1f, 0.06f, 1f)
                : new Color(0.95f, 0.97f, 1f, 1f);
            text.outlineWidth = 0.16f;
            text.outlineColor = new Color(0f, 0f, 0f, 0.18f);

            return go;
        }

        private static GameObject CreateSlider(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            SetParent(go, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 30);
            var s = go.GetComponent<Slider>();
            s.minValue = 0;
            s.maxValue = 1;
            s.value    = 1;

            var background = new GameObject("Background", typeof(Image));
            SetParent(background, go);
            StretchFull(background);
            StyleImage(background.GetComponent<Image>(), new Color(1f, 1f, 1f, 0.08f), new Color(0.25f, 0.77f, 0.95f, 0.12f), 1f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            SetParent(fillArea, go);
            StretchTo(fillArea, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));

            var fill = new GameObject("Fill", typeof(Image));
            SetParent(fill, fillArea);
            StretchFull(fill);
            StyleImage(fill.GetComponent<Image>(), new Color(0.95f, 0.72f, 0.22f, 1f), new Color(1f, 1f, 1f, 0.18f), 1f);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            SetParent(handleArea, go);
            StretchTo(handleArea, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));

            var handle = new GameObject("Handle", typeof(Image));
            SetParent(handle, handleArea);
            AnchorCenter(handle, Vector2.zero, new Vector2(24f, 40f));
            StyleImage(handle.GetComponent<Image>(), new Color(0.95f, 0.95f, 0.98f, 1f), new Color(0.95f, 0.72f, 0.22f, 0.24f), 1f);

            s.targetGraphic = handle.GetComponent<Image>();
            s.fillRect = fill.GetComponent<RectTransform>();
            s.handleRect = handle.GetComponent<RectTransform>();
            s.direction = Slider.Direction.LeftToRight;
            return go;
        }

        private static GameObject CreatePanel(GameObject parent, string name, Vector2 size, Color color)
        {
            var panel = new GameObject(name, typeof(Image));
            SetParent(panel, parent);
            StyleImage(panel.GetComponent<Image>(), color);
            AnchorCenter(panel, Vector2.zero, size);
            return panel;
        }

        private static GameObject CreateSizedLabel(GameObject parent, string text, float width, float fontSize)
        {
            var label = CreateTMPText(parent, $"Label_{text}", text, fontSize);
            var layoutElement = label.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
            return label;
        }

        private static (GameObject Root, GameObject Label, GameObject Value) CreateInfoCard(GameObject parent, string name, string label, string value, Vector2 anchoredPos, Vector2 size)
        {
            var card = new GameObject(name, typeof(Image));
            SetParent(card, parent);
            AnchorBottomLeft(card, anchoredPos, size);
            StyleImage(card.GetComponent<Image>(), new Color(1f, 1f, 1f, 0.04f), new Color(0.24f, 0.76f, 0.95f, 0.14f), 1.5f);

            var labelGo = CreateTMPText(card, $"{name}Label", label, 18);
            AnchorTopLeft(labelGo, new Vector2(18f, -10f), new Vector2(size.x - 36f, 22f));
            var labelText = labelGo.GetComponent<TMP_Text>();
            labelText.alignment = TextAlignmentOptions.Right;
            StyleSectionLabel(labelText);

            var valueGo = CreateTMPText(card, $"{name}Value", value, 34);
            AnchorBottomLeft(valueGo, new Vector2(18f, 4f), new Vector2(size.x - 36f, 40f));
            var valueText = valueGo.GetComponent<TMP_Text>();
            valueText.alignment = TextAlignmentOptions.Right;
            StyleValueText(valueText, 1f);
            ConfigureNumericValueText(valueText, 22f);

            return (card, labelGo, valueGo);
        }

        private static void StyleImage(Image image, Color color)
        {
            StyleImage(image, color, null, 0f);
        }

        private static void StyleImage(Image image, Color color, Color? outlineColor, float outlineDistance)
        {
            if (image == null) return;
            image.color = color;

            var outline = image.GetComponent<Outline>();
            if (outlineColor.HasValue && outlineDistance > 0f)
            {
                if (outline == null) outline = image.gameObject.AddComponent<Outline>();
                outline.effectColor = outlineColor.Value;
                outline.effectDistance = new Vector2(outlineDistance, -outlineDistance);
                outline.useGraphicAlpha = true;
            }
            else if (outline != null)
            {
                Object.DestroyImmediate(outline);
            }
        }

        private static void AddEdgeShadow(GameObject go, Color color, Vector2 distance)
        {
            var shadow = go.GetComponent<Shadow>();
            if (shadow == null) shadow = go.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private static void StyleHeadline(TMP_Text text, float spacing)
        {
            if (text == null) return;
            text.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            text.characterSpacing = spacing;
            text.color = new Color(0.95f, 0.97f, 1f, 1f);
        }

        private static void StyleSectionLabel(TMP_Text text)
        {
            if (text == null) return;
            text.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            text.characterSpacing = 5f;
            text.color = new Color(0.67f, 0.8f, 0.92f, 0.92f);
        }

        private static void StyleValueText(TMP_Text text, float spacing)
        {
            if (text == null) return;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = spacing;
            text.color = new Color(0.98f, 0.99f, 1f, 1f);
        }

        private static void ConfigureNumericValueText(TMP_Text text, float minFontSize)
        {
            if (text == null) return;
            text.enableAutoSizing = true;
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = minFontSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            text.characterSpacing = 0f;
            text.margin = new Vector4(0f, 0f, 4f, 0f);
        }

        private static void AnchorTopLeft(GameObject go, Vector2 anchoredPos, Vector2 size)
        {
            Anchor(go, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPos, size);
        }

        private static void AnchorTopRight(GameObject go, Vector2 anchoredPos, Vector2 size)
        {
            Anchor(go, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), anchoredPos, size);
        }

        private static void AnchorBottomLeft(GameObject go, Vector2 anchoredPos, Vector2 size)
        {
            Anchor(go, Vector2.zero, Vector2.zero, Vector2.zero, anchoredPos, size);
        }

        private static void AnchorBottomRight(GameObject go, Vector2 anchoredPos, Vector2 size)
        {
            Anchor(go, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), anchoredPos, size);
        }

        private static void AnchorCenter(GameObject go, Vector2 anchoredPos, Vector2 size)
        {
            Anchor(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPos, size);
        }

        private static void Anchor(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        private static void StretchTo(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        /// <summary>SerializedObject 経由で単一 SerializedField をワイヤリングする。</summary>
        private static void WireField(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(fieldName).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireAudioClips(AudioManager audioManager)
        {
            var bgmNormal      = LoadAudioClip(BgmNormalPath);
            var bgmFreeSpin    = LoadAudioClip(BgmFreeSpinPath);
            var bgmBonusRound  = LoadAudioClip(BgmBonusRoundPath);
            var seSpinStart    = LoadAudioClip(SeSpinStartPath);
            var seReelStop     = LoadAudioClip(SeReelStopPath);
            var seSmallWin     = LoadAudioClip(SeSmallWinPath);
            var seBigWin       = LoadAudioClip(SeBigWinPath);
            var seMegaWin      = LoadAudioClip(SeMegaWinPath);
            var seScatter      = LoadAudioClip(SeScatterAppearPath);
            var seButtonClick  = LoadAudioClip(SeButtonClickPath);

            WireField(audioManager, "bgmNormal", bgmNormal);
            WireField(audioManager, "bgmFreeSpin", bgmFreeSpin);
            WireField(audioManager, "bgmBonusRound", bgmBonusRound);

            WireField(audioManager, "seSpinStart", seSpinStart);
            WireField(audioManager, "seReelStop", seReelStop);
            WireField(audioManager, "seSmallWin", seSmallWin);
            WireField(audioManager, "seBigWin", seBigWin);
            WireField(audioManager, "seMegaWin", seMegaWin);
            WireField(audioManager, "seScatterAppear", seScatter);
            WireField(audioManager, "seFreeSpinStart", seScatter);
            WireField(audioManager, "seBonusStart", seBigWin);
            WireField(audioManager, "seChestSelect", seButtonClick);
            WireField(audioManager, "seChestOpen", seBigWin);
            WireField(audioManager, "seButtonClick", seButtonClick);
        }

        private static AudioClip LoadAudioClip(string assetPath)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null)
            {
                Debug.LogWarning($"[SceneBuilder] Audio clip not found: {assetPath}");
            }

            return clip;
        }
    }
}
