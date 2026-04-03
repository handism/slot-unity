using UnityEngine.SceneManagement;

namespace SlotGame.Core
{
    /// <summary>
    /// 実際の <see cref="UnityEngine.SceneManagement.SceneManager"/> に委譲する
    /// <see cref="ISceneLoader"/> の実装。
    /// </summary>
    public class UnitySceneLoader : ISceneLoader
    {
        public void LoadSceneAsync(string sceneName, LoadSceneMode mode)
        {
            SceneManager.LoadSceneAsync(sceneName, mode);
        }
    }
}
