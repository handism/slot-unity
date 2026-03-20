using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.View
{
    /// <summary>設定画面（BGM/SE ボリューム・コインリセット）の View。</summary>
    public class SettingsView : MonoBehaviour
    {
        [SerializeField] private Slider   bgmSlider;
        [SerializeField] private Slider   seSlider;
        [SerializeField] private TMP_Text bgmValueText;
        [SerializeField] private TMP_Text seValueText;
        [SerializeField] private Button   resetCoinsButton;
        [SerializeField] private Button   closeButton;

        public event System.Action<float> OnBGMVolumeChanged;
        public event System.Action<float> OnSEVolumeChanged;
        public event System.Action        OnResetCoinsRequested;
        public event System.Action        OnCloseRequested;

        private void Awake()
        {
            bgmSlider.onValueChanged.AddListener(v =>
            {
                bgmValueText.text = $"{(int)(v * 100)}%";
                OnBGMVolumeChanged?.Invoke(v);
            });

            seSlider.onValueChanged.AddListener(v =>
            {
                seValueText.text = $"{(int)(v * 100)}%";
                OnSEVolumeChanged?.Invoke(v);
            });

            resetCoinsButton.onClick.AddListener(() => OnResetCoinsRequested?.Invoke());
            closeButton.onClick.AddListener(()       => OnCloseRequested?.Invoke());
        }

        public void SetVolumes(float bgm, float se)
        {
            bgmSlider.SetValueWithoutNotify(bgm);
            seSlider.SetValueWithoutNotify(se);
            bgmValueText.text = $"{(int)(bgm * 100)}%";
            seValueText.text  = $"{(int)(se  * 100)}%";
        }
    }
}
