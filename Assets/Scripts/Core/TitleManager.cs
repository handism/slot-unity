using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlotGame.Core
{
    /// <summary>
    /// タイトルシーンの表示と遷移を担当するマネージャー。
    /// </summary>
    public class TitleManager : MonoBehaviour
    {
        /// <summary>
        /// シーンローダー。テスト時にモックと差し替えられる。
        /// </summary>
        internal ISceneLoader SceneLoader { get; set; } = new UnitySceneLoader();

        public void StartGame()
        {
            // 非同期ロードを使用し、BootManager でセットされた GameContext が
            // GameManager の Awake() で正しく参照されるようにする。
            SceneLoader.LoadSceneAsync("Main", LoadSceneMode.Single);
        }
    }
}
