using System;
using System.IO;
using UnityEngine;
using SlotGame.Model;

namespace SlotGame.Utility
{
    /// <summary>
    /// JSON ファイルへのセーブデータ読み書きを担う。
    /// コンストラクタでパスを差し替えることでテスト可能。
    /// </summary>
    public class SaveDataManager
    {
        private readonly string      _savePath;
        private readonly SlotConfig? _config;

        public SaveDataManager(SlotConfig? config = null)
            : this(Path.Combine(Application.persistentDataPath, "savedata.json"), config) { }

        public SaveDataManager(string savePath, SlotConfig? config = null)
        {
            _savePath = savePath;
            _config   = config;
        }

        /// <summary>
        /// セーブデータを読み込む。
        /// ファイルが存在しない場合や破損している場合はデフォルト値を返す。
        /// 破損ファイルは .bak にリネームして保全する。
        /// </summary>
        public SaveData Load()
        {
            if (!File.Exists(_savePath))
                return new SaveData();

            try
            {
                string json = File.ReadAllText(_savePath);
                var data    = JsonUtility.FromJson<SaveData>(json);
                if (data == null || !Validate(data, _config))
                    return RecoverFromCorruption();

                // 移行戦略: チェックサムがない旧データの場合はバリデーションのみで通し、次回保存時にチェックサムを付与する
                if (string.IsNullOrEmpty(data.checksum))
                {
                    return data;
                }

                if (!VerifyChecksum(data))
                    return RecoverFromCorruption();

                return data;
            }
            catch (Exception)
            {
                return RecoverFromCorruption();
            }
        }

        /// <summary>セーブデータを JSON ファイルに書き込む（一時ファイルを用いたアトミック書き込み）。</summary>
        public void Save(SaveData data)
        {
            data.checksum = CalculateChecksum(data, _config);
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            string tempPath = _savePath + ".tmp";

            try
            {
                File.WriteAllText(tempPath, json);
                if (File.Exists(_savePath))
                {
                    File.Replace(tempPath, _savePath, null);
                }
                else
                {
                    File.Move(tempPath, _savePath);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveDataManager] Save failed: {e.Message}");
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        // ─── バリデーション ──────────────────────────────────────────────

        private static bool Validate(SaveData data, SlotConfig? config)
        {
            if (data.saveVersion != "1.0")                  return false;
            if (data.coins < 0)                             return false;
            if (config != null)
            {
                if (data.coins > config.MaxCoins) return false;
                if (System.Array.IndexOf(config.ValidBetAmounts, data.betAmount) < 0) return false;
            }
            if (data.bgmVolume < 0f || data.bgmVolume > 1f) return false;
            if (data.seVolume  < 0f || data.seVolume  > 1f) return false;
            if (data.totalSpins < 0 || data.maxWin < 0)     return false;
            return true;
        }

        private static string CalculateChecksum(SaveData data, SlotConfig? config)
        {
            string salt = config != null ? config.ChecksumSalt : "SALTY_SLOT_2026";
            string raw = $"{data.coins}:{data.betAmount}:{data.bgmVolume:F2}:{data.seVolume:F2}:{data.totalSpins}:{data.maxWin}:{data.saveVersion}:{salt}";
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
            return Convert.ToBase64String(bytes);
        }

        private bool VerifyChecksum(SaveData data)
        {
            string actual = data.checksum;
            string expected = CalculateChecksum(data, _config);
            return actual == expected;
        }

        private SaveData RecoverFromCorruption()
        {
            if (File.Exists(_savePath))
            {
                string bakPath = _savePath + ".bak";
                try { if (File.Exists(bakPath)) File.Delete(bakPath); File.Move(_savePath, bakPath); }
                catch (Exception) { /* バックアップ失敗は無視してデフォルト値を返す */ }
            }
            return new SaveData();
        }
    }
}
