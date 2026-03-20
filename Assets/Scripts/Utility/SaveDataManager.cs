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
        private readonly string _savePath;

        public SaveDataManager()
            : this(Path.Combine(Application.persistentDataPath, "savedata.json")) { }

        public SaveDataManager(string savePath)
        {
            _savePath = savePath;
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
                if (data == null || !Validate(data))
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

        private static bool Validate(SaveData data)
        {
            if (data.saveVersion != "1.0")                  return false;
            if (data.coins < 0 || data.coins > GameState.MaxCoins) return false;
            if (Array.IndexOf(GameState.ValidBetAmounts, data.betAmount) < 0) return false;
            if (data.bgmVolume < 0f || data.bgmVolume > 1f) return false;
            if (data.seVolume  < 0f || data.seVolume  > 1f) return false;
            if (data.totalSpins < 0 || data.maxWin < 0)     return false;
            return true;
        }

        private SaveData RecoverFromCorruption()
        {
            if (File.Exists(_savePath))
            {
                string bakPath = _savePath + ".bak";
                try { File.Move(_savePath, bakPath, overwrite: true); }
                catch (Exception) { /* バックアップ失敗は無視してデフォルト値を返す */ }
            }
            return new SaveData();
        }
    }
}
