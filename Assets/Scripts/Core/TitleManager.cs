using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlotGame.Core
{
    /// <summary>
    /// タイトルシーンの表示と遷移を担当するマネージャー。
    /// </summary>
    public class TitleManager : MonoBehaviour
    {
        public void StartGame()
        {
            // 非同期ロードを使用し、BootManager でセットされた GameContext が
            // GameManager の Awake() で正しく参照されるようにする。
            SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single).Forget();
        }
    }
}
