using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SlotGame.Core
{
    using SlotGame.Model;
    using SlotGame.Utility;
    using SlotGame.Data;

    /// <summary>Boot シーンの初期化処理。依存性の注入を行い Main シーンへ遷移する。</summary>
    public class BootManager : MonoBehaviour
    {
        [SerializeField] private Slider         progressBar;
        [SerializeField] private GameConfigData gameConfig;

        private async void Start()
        {
            // 1. 基本システムの初期化
            DOTween.Init(recycleAllByDefault: true, useSafeMode: true, logBehaviour: LogBehaviour.ErrorsOnly);
            DOTween.defaultAutoPlay = AutoPlay.All;

            if (progressBar != null) progressBar.value = 0;

            // 2. データのロードと Model の生成
            var config = gameConfig != null ? gameConfig.ToModelConfig() : null;
            var saveDataManager = new SaveDataManager(config);
            var save = saveDataManager.Load();
            var gameState = new GameState(
                config?.InitialCoins ?? 1000,
                config?.MaxCoins ?? 9_999_999,
                config?.ValidBetAmounts ?? new[] { 10, 20, 50, 100 },
                save.coins,
                save.betAmount
            );
            gameState.RestoreStats(save.totalSpins, save.maxWin);

            var random = new SystemRandomGenerator();

            // 3. Title シーンのロード
            var op = SceneManager.LoadSceneAsync("Title", LoadSceneMode.Single);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                if (progressBar != null) progressBar.value = op.progress;
                await UniTask.Yield();
            }

            if (progressBar != null) progressBar.value = 1f;
            await UniTask.Delay(200);

            // 4. 静的コンテナにデータをセットしてシーン遷移
            GameContext.GameState       = gameState;
            GameContext.SaveDataManager = saveDataManager;
            GameContext.Random          = random;
            GameContext.SaveData        = save;

            op.allowSceneActivation = true;
        }
    }
}
