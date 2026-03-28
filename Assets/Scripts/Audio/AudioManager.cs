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
        SpinStart, ReelStop, SmallWin, BigWin, MegaWin, EpicWin,
        ScatterAppear, FreeSpinStart, BonusStart,
        ChestSelect, ChestOpen, ButtonClick
    }

    /// <summary>BGM・SE 再生を管理する。</summary>
    public class AudioManager : MonoBehaviour
    {
        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
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
        [SerializeField] private AudioClip seEpicWin;
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

        public bool IsMuted => _isMuted;

        private void Awake()
        {
            ValidateConfiguration();
            PreloadAssignedClips();
        }

        public void PlayBGM(BGMType type)
        {
            var clip = type switch
            {
                BGMType.Normal     => bgmNormal,
                BGMType.FreeSpin   => bgmFreeSpin,
                BGMType.BonusRound => bgmBonusRound,
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
            if (clip == null) return;
            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.Play();
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
                SEType.EpicWin        => seEpicWin,
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
            _preMuteBgmVolume = clamped;
            if (!_isMuted) bgmSource.volume = clamped;
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
                _preMuteBgmVolume = bgmSource.volume;
                _preMuteSeVolume = seSource.volume;
                bgmSource.volume = 0;
                seSource.volume = 0;
            }
            else
            {
                bgmSource.volume = _preMuteBgmVolume;
                seSource.volume = _preMuteSeVolume;
            }
        }

        /// <summary>BGM をフェードアウトして停止する。</summary>
        public async UniTask FadeOutBGM(float duration, CancellationToken ct)
        {
            float startVolume = bgmSource.volume;
            await DOTween.To(() => bgmSource.volume, v => bgmSource.volume = v, 0f, duration)
                         .ToUniTask(cancellationToken: ct);
            bgmSource.Stop();
            bgmSource.volume = startVolume;
        }

        /// <summary>現在の BGM をフェードアウトし、新しい BGM をフェードインする。</summary>
        public async UniTask CrossFadeBGM(BGMType type, float duration, CancellationToken ct)
        {
            await FadeOutBGM(duration * 0.5f, ct);
            PlayBGM(type);
            bgmSource.volume = 0f;
            await DOTween.To(() => bgmSource.volume, v => bgmSource.volume = v, _preMuteBgmVolume, duration * 0.5f)
                         .ToUniTask(cancellationToken: ct);
        }

        private void ValidateConfiguration()
        {
            var missing = new List<string>();

            if (bgmSource == null) missing.Add(nameof(bgmSource));
            if (seSource == null) missing.Add(nameof(seSource));
            if (bgmNormal == null) missing.Add(nameof(bgmNormal));
            if (bgmFreeSpin == null) missing.Add(nameof(bgmFreeSpin));
            if (bgmBonusRound == null) missing.Add(nameof(bgmBonusRound));
            if (seSpinStart == null) missing.Add(nameof(seSpinStart));
            if (seReelStop == null) missing.Add(nameof(seReelStop));
            if (seSmallWin == null) missing.Add(nameof(seSmallWin));
            if (seBigWin == null) missing.Add(nameof(seBigWin));
            if (seMegaWin == null) missing.Add(nameof(seMegaWin));
            if (seEpicWin == null) missing.Add(nameof(seEpicWin));
            if (seScatterAppear == null) missing.Add(nameof(seScatterAppear));
            if (seFreeSpinStart == null) missing.Add(nameof(seFreeSpinStart));
            if (seBonusStart == null) missing.Add(nameof(seBonusStart));
            if (seChestSelect == null) missing.Add(nameof(seChestSelect));
            if (seChestOpen == null) missing.Add(nameof(seChestOpen));
            if (seButtonClick == null) missing.Add(nameof(seButtonClick));

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
            yield return seEpicWin;
            yield return seScatterAppear;
            yield return seFreeSpinStart;
            yield return seBonusStart;
            yield return seChestSelect;
            yield return seChestOpen;
            yield return seButtonClick;
        }
    }
}
