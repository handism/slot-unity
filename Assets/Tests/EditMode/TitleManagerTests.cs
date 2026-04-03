using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using SlotGame.Core;

namespace SlotGame.Tests.EditMode
{
    /// <summary>
    /// <see cref="TitleManager"/> のユニットテスト。
    /// <see cref="ISceneLoader"/> をモックに差し替えることで Edit Mode で検証する。
    /// </summary>
    public class TitleManagerTests
    {
        private class MockSceneLoader : ISceneLoader
        {
            public string LoadedSceneName { get; private set; }
            public LoadSceneMode LoadedSceneMode { get; private set; }
            public int CallCount { get; private set; }

            public void LoadSceneAsync(string sceneName, LoadSceneMode mode)
            {
                LoadedSceneName = sceneName;
                LoadedSceneMode = mode;
                CallCount++;
            }
        }

        private GameObject _go;
        private TitleManager _titleManager;
        private MockSceneLoader _mockLoader;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject();
            _titleManager = _go.AddComponent<TitleManager>();
            _mockLoader = new MockSceneLoader();
            _titleManager.SceneLoader = _mockLoader;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void StartGame_LoadsMainScene()
        {
            _titleManager.StartGame();

            Assert.AreEqual("Main", _mockLoader.LoadedSceneName);
        }

        [Test]
        public void StartGame_UsesSingleMode()
        {
            _titleManager.StartGame();

            Assert.AreEqual(LoadSceneMode.Single, _mockLoader.LoadedSceneMode);
        }

        [Test]
        public void StartGame_CallsLoadSceneAsyncExactlyOnce()
        {
            _titleManager.StartGame();

            Assert.AreEqual(1, _mockLoader.CallCount);
        }

        [Test]
        public void StartGame_CalledTwice_CallsLoadSceneAsyncTwice()
        {
            _titleManager.StartGame();
            _titleManager.StartGame();

            Assert.AreEqual(2, _mockLoader.CallCount);
        }
    }
}
