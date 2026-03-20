using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using SlotGame.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.View
{
    /// <summary>
    /// ボーナスラウンドの宝箱選択 UI。
    /// BonusRound.unity に配置し、BonusManager から WaitForSelection() で呼ばれる。
    /// </summary>
    public class BonusRoundView : MonoBehaviour
    {
        private const int ChestCount    = 9;
        private const int SelectCount   = 3;

        [SerializeField] private Button[]   chestButtons;       // 9 個の宝箱ボタン
        [SerializeField] private TMP_Text[] rewardTexts;        // 各宝箱の報酬表示
        [SerializeField] private TMP_Text   totalWinText;

        private UniTaskCompletionSource<int[]> _tcs;
        private List<int>                      _selectedRewards;
        private int                            _selectRemaining;
        private int[]                          _rewards;         // 事前に BonusManager が設定した報酬値

        private void Awake()
        {
            for (int i = 0; i < chestButtons.Length; i++)
            {
                int idx = i;
                chestButtons[i].onClick.AddListener(() => OnChestSelected(idx));
            }
        }

        /// <summary>
        /// 報酬値を設定して宝箱選択を開始し、選ばれた 3 個の報酬倍率配列を返す。
        /// </summary>
        public async UniTask<int[]> WaitForSelection(int[] presetRewards, CancellationToken ct)
        {
            _rewards         = presetRewards;
            _selectedRewards = new List<int>(SelectCount);
            _selectRemaining = SelectCount;
            _tcs             = new UniTaskCompletionSource<int[]>();

            // 全宝箱をリセット
            for (int i = 0; i < chestButtons.Length; i++)
            {
                chestButtons[i].interactable = true;
                rewardTexts[i].text          = "?";
                rewardTexts[i].gameObject.SetActive(false);
            }
            totalWinText.text = "0";

            // キャンセル時は tcs を cancel
            ct.Register(() => _tcs.TrySetCanceled());

            return await _tcs.Task;
        }

        // Inspector 用オーバーロード（rewards を外部から設定しない場合）
        public UniTask<int[]> WaitForSelection(CancellationToken ct)
            => WaitForSelection(_rewards, ct);

        private void OnChestSelected(int index)
        {
            if (_selectRemaining <= 0) return;
            chestButtons[index].interactable = false;

            int reward = _rewards[index];
            _selectedRewards.Add(reward);
            _selectRemaining--;

            // 開封アニメーション
            PlayOpenAnimation(index, reward).Forget();
        }

        private async UniTask PlayOpenAnimation(int index, int reward)
        {
            var rt = chestButtons[index].GetComponent<RectTransform>();

            await rt.DOScale(1.3f, 0.1f).SetEase(Ease.OutBack).ToUniTask();
            await rt.DOScale(1.0f, 0.1f).ToUniTask();
            await rt.DORotate(new Vector3(0, 0, 10f), 0.05f).ToUniTask();
            await rt.DORotate(Vector3.zero, 0.05f).ToUniTask();

            rewardTexts[index].text = $"×{reward}";
            rewardTexts[index].gameObject.SetActive(true);

            // 累計表示更新
            long total = 0;
            foreach (int r in _selectedRewards) total += r;
            totalWinText.text = total.ToString("N0");

            if (_selectRemaining <= 0)
            {
                // 未選択の宝箱を一括表示（参考表示）
                await UniTask.Delay(500);
                for (int i = 0; i < chestButtons.Length; i++)
                    if (chestButtons[i].interactable)
                    {
                        rewardTexts[i].text = $"×{_rewards[i]}";
                        rewardTexts[i].gameObject.SetActive(true);
                        chestButtons[i].interactable = false;
                    }

                await UniTask.Delay(1500);
                _tcs.TrySetResult(_selectedRewards.ToArray());
            }
        }
    }
}
