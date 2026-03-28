using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace SlotGame.Audio
{
    public enum BGMType  { Normal, FreeSpin, BonusRound }
    public enum SEType
    {
        SpinStart, ReelStop, SmallWin, BigWin, MegaWin,
        ScatterAppear, FreeSpinStart, BonusStart,
        ChestSelect, ChestOpen, ButtonClick
    }

    /// <summary>BGM・SE 再生を管理する。</summary>
    public class AudioManager : MonoBehaviour
    {
        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource bgmSourceSub;  // クロスフェード用サブソース
        [SerializeField] private AudioSource seSource;

        [Header("BGM Clips")]
        [SerializeField] private AudioClip bgmNormal;
        [SerializeField] private AudioClip bgmFreeSpin;
        [SerializeField] private AudioClip bgmBonusRound;

        [Header("SE Clips")]
        [SerializeField] private AudioClip seSpinStart;
        [SerializeField] private AudioClip seReelStop;
        [SerializeField] private AudioClip seSmallWin;
        [SerializeField] private AudioClip seBigWin;
        [SerializeField] private AudioClip seMegaWin;
        [SerializeField] private AudioClip seScatterAppear;
        [SerializeField] private AudioClip seFreeSpinStart;
        [SerializeField] private AudioClip seBonusStart;
        [SerializeField] private AudioClip seChestSelect;
        [SerializeField] private AudioClip seChestOpen;
        [SerializeField] private AudioClip seButtonClick;

        private float _lastReelStopPlayTime = -1f;
        private float _preMuteBgmVolume = 0.8f;
        private float _preMuteSeVolume = 1.0f;
        private bool  _isMuted;
        private float _bgmTargetVolume = 1f;
        private AudioSource _activeBgm;
        private AudioSource _inactiveBgm;

        public bool IsMuted => _isMuted;

        private void Awake()
        {
            _activeBgm   = bgmSource;
            _inactiveBgm = bgmSourceSub;
            ValidateConfiguration();
            PreloadAssignedClips();
        }

        public void PlayBGM(BGMType type)
        {
            var clip = GetBGMClip(type);
            if (clip == null) return;

            _inactiveBgm.Stop();
            _activeBgm.clip   = clip;
            _activeBgm.loop   = true;
            _activeBgm.volume = _bgmTargetVolume;
            _activeBgm.Play();
        }

        /// <summary>現在のBGMをフェードアウトしながら次のBGMをフェードインする（クロスフェード）。</summary>
        public async UniTask CrossFadeBGM(BGMType type, float duration, CancellationToken ct)
        {
            var clip = GetBGMClip(type);
            if (clip == null) return;

            // 同じクリップへの切り替えは何もしない
            if (_activeBgm.clip == clip && _activeBgm.isPlaying) return;

            _inactiveBgm.clip   = clip;
            _inactiveBgm.loop   = true;
            _inactiveBgm.volume = 0f;
            _inactiveBgm.Play();

            float fromVolume = _activeBgm.volume;
            var fadeOut = DOTween.To(
                () => _activeBgm.volume,
                v  => _activeBgm.volume = v,
                0f, duration);
            var fadeIn = DOTween.To(
                () => _inactiveBgm.volume,
                v  => _inactiveBgm.volume = v,
                _bgmTargetVolume, duration);

            await UniTask.WhenAll(
                fadeOut.ToUniTask(cancellationToken: ct),
                fadeIn.ToUniTask(cancellationToken: ct));

            _activeBgm.Stop();
            _activeBgm.volume = fromVolume; // 次回フェード用に音量を戻す

            // アクティブソースを入れ替え
            (_activeBgm, _inactiveBgm) = (_inactiveBgm, _activeBgm);
        }

        public void PlaySE(SEType type)
        {
            if (type == SEType.ReelStop)
            {
                if (Time.time - _lastReelStopPlayTime < 0.05f) return;
                _lastReelStopPlayTime = Time.time;
            }

            var clip = type switch
            {
                SEType.SpinStart      => seSpinStart,
                SEType.ReelStop       => seReelStop,
                SEType.SmallWin       => seSmallWin,
                SEType.BigWin         => seBigWin,
                SEType.MegaWin        => seMegaWin,
                SEType.ScatterAppear  => seScatterAppear,
                SEType.FreeSpinStart  => seFreeSpinStart,
                SEType.BonusStart     => seBonusStart,
                SEType.ChestSelect    => seChestSelect,
                SEType.ChestOpen      => seChestOpen,
                SEType.ButtonClick    => seButtonClick,
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
            if (clip == null) return;
            seSource.PlayOneShot(clip);
        }

        public void SetBGMVolume(float volume)
        {
            float clamped = Mathf.Clamp01(volume);
            _bgmTargetVolume  = clamped;
            _preMuteBgmVolume = clamped;
            if (!_isMuted) _activeBgm.volume = clamped;
        }

        public void SetSEVolume(float volume)
        {
            float clamped = Mathf.Clamp01(volume);
            _preMuteSeVolume = clamped;
            if (!_isMuted) seSource.volume = clamped;
        }

        public void ToggleMute()
        {
            _isMuted = !_isMuted;
            if (_isMuted)
            {
                _preMuteBgmVolume = _activeBgm.volume;
                _preMuteSeVolume  = seSource.volume;
                _activeBgm.volume = 0;
                seSource.volume   = 0;
            }
            else
            {
                _activeBgm.volume = _preMuteBgmVolume;
                seSource.volume   = _preMuteSeVolume;
            }
        }

        /// <summary>BGM をフェードアウトして停止する。</summary>
        public async UniTask FadeOutBGM(float duration, CancellationToken ct)
        {
            float startVolume = _activeBgm.volume;
            await DOTween.To(() => _activeBgm.volume, v => _activeBgm.volume = v, 0f, duration)
                         .ToUniTask(cancellationToken: ct);
            _activeBgm.Stop();
            _activeBgm.volume = startVolume;
        }

        private AudioClip GetBGMClip(BGMType type) => type switch
        {
            BGMType.Normal     => bgmNormal,
            BGMType.FreeSpin   => bgmFreeSpin,
            BGMType.BonusRound => bgmBonusRound,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        private void ValidateConfiguration()
        {
            var missing = new List<string>();

            if (bgmSource == null)    missing.Add(nameof(bgmSource));
            if (bgmSourceSub == null) missing.Add(nameof(bgmSourceSub));
            if (seSource == null)     missing.Add(nameof(seSource));
            if (bgmNormal == null)    missing.Add(nameof(bgmNormal));
            if (bgmFreeSpin == null)  missing.Add(nameof(bgmFreeSpin));
            if (bgmBonusRound == null) missing.Add(nameof(bgmBonusRound));
            if (seSpinStart == null)  missing.Add(nameof(seSpinStart));
            if (seReelStop == null)   missing.Add(nameof(seReelStop));
            if (seSmallWin == null)   missing.Add(nameof(seSmallWin));
            if (seBigWin == null)     missing.Add(nameof(seBigWin));
            if (seMegaWin == null)    missing.Add(nameof(seMegaWin));
            if (seScatterAppear == null)  missing.Add(nameof(seScatterAppear));
            if (seFreeSpinStart == null)  missing.Add(nameof(seFreeSpinStart));
            if (seBonusStart == null)     missing.Add(nameof(seBonusStart));
            if (seChestSelect == null)    missing.Add(nameof(seChestSelect));
            if (seChestOpen == null)      missing.Add(nameof(seChestOpen));
            if (seButtonClick == null)    missing.Add(nameof(seButtonClick));

            if (missing.Count > 0)
            {
                Debug.LogWarning($"[AudioManager] Missing audio assignments: {string.Join(", ", missing)}", this);
            }
        }

        private void PreloadAssignedClips()
        {
            foreach (var clip in EnumerateAssignedClips())
            {
                if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
                {
                    clip.LoadAudioData();
                }
            }
        }

        private IEnumerable<AudioClip> EnumerateAssignedClips()
        {
            yield return bgmNormal;
            yield return bgmFreeSpin;
            yield return bgmBonusRound;
            yield return seSpinStart;
            yield return seReelStop;
            yield return seSmallWin;
            yield return seBigWin;
            yield return seMegaWin;
            yield return seScatterAppear;
            yield return seFreeSpinStart;
            yield return seBonusStart;
            yield return seChestSelect;
            yield return seChestOpen;
            yield return seButtonClick;
        }
    }
}
