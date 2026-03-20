using System;
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
            bgmSource.volume = Mathf.Clamp01(volume);
        }

        public void SetSEVolume(float volume)
        {
            seSource.volume = Mathf.Clamp01(volume);
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
    }
}
