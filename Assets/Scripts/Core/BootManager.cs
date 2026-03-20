using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SlotGame.Core
{
    /// <summary>Boot シーンの初期化処理。DOTween 初期化後に Main シーンへ遷移する。</summary>
    public class BootManager : MonoBehaviour
    {
        [SerializeField] private Slider progressBar;

        private async void Start()
        {
            DOTween.Init(recycleAllByDefault: true, useSafeMode: true, logBehaviour: LogBehaviour.ErrorsOnly);
            DOTween.defaultAutoPlay = AutoPlay.All;

            if (progressBar != null) progressBar.value = 0;

            var op = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                if (progressBar != null) progressBar.value = op.progress;
                await UniTask.Yield();
            }

            if (progressBar != null) progressBar.value = 1f;
            await UniTask.Delay(200);

            op.allowSceneActivation = true;
        }
    }
}
