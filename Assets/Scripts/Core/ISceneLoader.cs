using UnityEngine.SceneManagement;

namespace SlotGame.Core
{
    /// <summary>
    /// シーンの非同期ロードを抽象化するインターフェース。
    /// テスト時にモックと差し替えられるようにするために導入する。
    /// </summary>
    public interface ISceneLoader
    {
        void LoadSceneAsync(string sceneName, LoadSceneMode mode);
    }
}
