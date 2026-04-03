using UnityEngine;

namespace SlotGame.Core
{
    using SlotGame.Model;
    using SlotGame.Utility;

    /// <summary>
    /// Boot シーンで生成され、DontDestroyOnLoad によって全シーンを通じて生存する初期化コンテナ。
    /// Boot→Title→Main の遷移でデータを明示的に引き渡す役割を担う。
    /// </summary>
    public class GameContextInitializer : MonoBehaviour
    {
        /// <summary>シングルトンインスタンス。Boot シーンを経由しない場合は null。</summary>
        public static GameContextInitializer Instance { get; private set; }

        public GameState        GameState       { get; private set; }
        public SaveDataManager  SaveDataManager { get; private set; }
        public IRandomGenerator Random          { get; private set; }
        public SaveData         SaveData        { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Boot シーンから呼び出し、全依存データを一括設定する。</summary>
        public void Provide(
            GameState        gameState,
            SaveDataManager  saveDataManager,
            IRandomGenerator random,
            SaveData         saveData)
        {
            GameState       = gameState;
            SaveDataManager = saveDataManager;
            Random          = random;
            SaveData        = saveData;
        }
    }
}
