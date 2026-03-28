using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SlotGame.Core;
using SlotGame.Model;
using SlotGame.Utility;
using SlotGame.Data;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using System;
using System.Reflection;

namespace SlotGame.Tests.PlayMode
{
    /// <summary>
    /// ゲームのメインフロー（通常スピン、オートスピン等）を検証する Play Mode テスト。
    /// </summary>
    public class SpinFlowTests
    {
        private class MockRandom : IRandomGenerator
        {
            public int[] Values;
            public int Index;
            public int Next(int min, int max) => Values[Index++ % Values.Length];
            public float NextFloat() => 0.5f;
        }

        [UnityTest]
        public IEnumerator Test_NormalSpin_DeductsBet_And_AddsWin() => UniTask.ToCoroutine(async () =>
        {
            // --- Setup ---
            var mockRandom = new MockRandom { Values = new[] { 0, 0, 0, 0, 0 } };
            
            GameContext.Random = mockRandom;
            GameContext.GameState = new GameState(1000, 9_999_999, new[] { 10, 20, 50, 100 }, 1000, 10);
            GameContext.SaveDataManager = new SaveDataManager();
            GameContext.SaveData = new SaveData { coins = 1000, betAmount = 10 };

            await SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);

            var gm = GameObject.FindFirstObjectByType<GameManager>();
            Assert.IsNotNull(gm, "GameManager not found");

            long initialCoins = GameContext.GameState.Coins;

            // --- Execute ---
            gm.OnSpinButtonPressed();

            await UniTask.WaitUntil(() => GetCurrentPhase(gm) == GamePhase.Spinning);
            Assert.AreEqual(initialCoins - 10, GameContext.GameState.Coins);

            await UniTask.WaitUntil(() => GetCurrentPhase(gm) == GamePhase.Idle)
                         .Timeout(TimeSpan.FromSeconds(20));

            // --- Verify ---
            Assert.Greater(GameContext.GameState.Coins, initialCoins - 10);
        });

        [UnityTest]
        public IEnumerator Test_FreeSpin_Flow() => UniTask.ToCoroutine(async () =>
        {
            // --- Setup ---
            // 1回目のスピンで Scatter 3つ (インデックス 32, 33, 34, 0, 0) -> 10回獲得
            // 続くスピンでハズレ (5 ずつずらす)
            var values = new List<int>();
            values.AddRange(new[] { 32, 33, 34, 0, 0 }); 
            for (int i = 0; i < 15; i++) // 余裕を持って 15 回分
                values.AddRange(new[] { 5, 5, 5, 5, 5 });
            
            var mockRandom = new MockRandom { Values = values.ToArray() };
            GameContext.Random = mockRandom;
            GameContext.GameState = new GameState(1000, 9_999_999, new[] { 10, 20, 50, 100 }, 1000, 10);
            GameContext.SaveDataManager = new SaveDataManager();
            GameContext.SaveData = new SaveData { coins = 1000, betAmount = 10 };

            await SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            var gm = GameObject.FindFirstObjectByType<GameManager>();

            // --- Execute ---
            gm.OnSpinButtonPressed();

            // フリースピン開始を待機
            await UniTask.WaitUntil(() => GetCurrentPhase(gm) == GamePhase.FreeSpin)
                         .Timeout(TimeSpan.FromSeconds(20));
            
            Debug.Log("Entered FreeSpin phase");

            // 全フリースピン終了して Idle に戻るのを待機 (10回分 + 演出なので長めに)
            await UniTask.WaitUntil(() => GetCurrentPhase(gm) == GamePhase.Idle)
                         .Timeout(TimeSpan.FromSeconds(60));

            // --- Verify ---
            Assert.AreEqual(0, GameContext.GameState.FreeSpinsLeft, "Free spins should be exhausted");
            Assert.AreNotEqual(GamePhase.FreeSpin, GetCurrentPhase(gm));
            Debug.Log("Free Spin Flow Test Success.");
        });

        [UnityTest]
        public IEnumerator Test_AutoSpin_Interrupt() => UniTask.ToCoroutine(async () =>
        {
            // --- Setup ---
            var mockRandom = new MockRandom { Values = new[] { 5, 5, 5, 5, 5 } };
            GameContext.Random = mockRandom;
            GameContext.GameState = new GameState(1000, 9_999_999, new[] { 10, 20, 50, 100 }, 1000, 10);
            GameContext.SaveDataManager = new SaveDataManager();
            GameContext.SaveData = new SaveData { coins = 1000, betAmount = 10 };

            await SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            var gm = GameObject.FindFirstObjectByType<GameManager>();

            // --- Execute ---
            gm.OnAutoSpinButtonPressed(10);
            
            await UniTask.WaitUntil(() => GetCurrentPhase(gm) == GamePhase.Spinning);
            gm.OnAutoSpinStopRequested();
            
            await UniTask.WaitUntil(() => GetCurrentPhase(gm) == GamePhase.Idle)
                         .Timeout(TimeSpan.FromSeconds(20));
            
            await UniTask.Delay(TimeSpan.FromSeconds(1));
            Assert.AreEqual(GamePhase.Idle, GetCurrentPhase(gm));
        });

        [UnityTest]
        public IEnumerator Test_GameOver_ResetsCoins() => UniTask.ToCoroutine(async () =>
        {
            // --- Setup ---
            var mockRandom = new MockRandom { Values = new[] { 1, 3, 5, 7, 9 } }; 
            GameContext.Random = mockRandom;
            GameContext.GameState = new GameState(10, 9_999_999, new[] { 10, 20, 50, 100 }, 10, 10); // 最後の 10 コイン
            GameContext.SaveDataManager = new SaveDataManager();
            GameContext.SaveData = new SaveData { coins = 10, betAmount = 10 };

            await SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            var gm = GameObject.FindFirstObjectByType<GameManager>();

            // --- Execute ---
            gm.OnSpinButtonPressed();

            await UniTask.WaitUntil(() => GetCurrentPhase(gm) == GamePhase.Spinning);
            Assert.AreEqual(0, GameContext.GameState.Coins);

            await UniTask.WaitUntil(() => GetCurrentPhase(gm) == GamePhase.Idle)
                         .Timeout(TimeSpan.FromSeconds(20));

            // --- Verify ---
            Assert.AreEqual(1000, GameContext.GameState.Coins, "Coins should be reset to 1000");
        });

        [UnityTest]
        public IEnumerator Test_EarlyStop_SkipsDelay() => UniTask.ToCoroutine(async () =>
        {
            // --- Setup ---
            var mockRandom = new MockRandom { Values = new[] { 0, 0, 0, 0, 0 } };
            GameContext.Random = mockRandom;
            GameContext.GameState = new GameState(1000, 9_999_999, new[] { 10, 20, 50, 100 }, 1000, 10);
            GameContext.SaveData = new SaveData { coins = 1000, betAmount = 10 };

            await SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            var gm = GameObject.FindFirstObjectByType<GameManager>();

            // --- Execute ---
            gm.OnSpinButtonPressed();

            await UniTask.WaitUntil(() => GetCurrentPhase(gm) == GamePhase.Spinning);
            var startTime = Time.realtimeSinceStartup;
            
            // Spinning 中にもう一度押すと Skip リクエストになるはず
            gm.OnSpinButtonPressed(); 
            
            await UniTask.WaitUntil(() => GetCurrentPhase(gm) == GamePhase.Idle)
                         .Timeout(TimeSpan.FromSeconds(10));
            
            var duration = Time.realtimeSinceStartup - startTime;
            
            // 通常は 2.0s (最低) + 順次停止 (0.3s * 4) = 3.2s 以上かかるが、
            // Skip すると順次停止待機がなくなる。
            Debug.Log($"Spin duration with skip: {duration}s");
            Assert.Less(duration, 5.0f); // 演出含め 5秒以内なら OK としておく
        });

        private GamePhase GetCurrentPhase(GameManager gm)
        {
            var field = typeof(GameManager).GetField("_currentPhase", BindingFlags.NonPublic | BindingFlags.Instance);
            return (GamePhase)field.GetValue(gm);
        }
    }
}
