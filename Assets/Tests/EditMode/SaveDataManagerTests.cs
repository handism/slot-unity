using System.IO;
using NUnit.Framework;
using SlotGame.Model;
using SlotGame.Utility;

namespace SlotGame.Tests.EditMode
{
    public class SaveDataManagerTests
    {
        private string _tempPath;

        [SetUp]
        public void SetUp()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), $"savedata_test_{System.Guid.NewGuid()}.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tempPath))      File.Delete(_tempPath);
            if (File.Exists(_tempPath + ".bak")) File.Delete(_tempPath + ".bak");
        }

        [Test]
        public void Load_FileNotExists_ReturnsDefault()
        {
            var mgr  = new SaveDataManager(_tempPath, null);
            var data = mgr.Load();

            Assert.AreEqual(1000,  data.coins);
            Assert.AreEqual(10,    data.betAmount);
            Assert.AreEqual("1.0", data.saveVersion);
        }

        [Test]
        public void Save_ThenLoad_RoundTrip()
        {
            var mgr  = new SaveDataManager(_tempPath, null);
            var save = new SaveData { coins = 5000, betAmount = 50, bgmVolume = 0.5f };
            mgr.Save(save);

            var loaded = mgr.Load();
            Assert.AreEqual(5000,  loaded.coins);
            Assert.AreEqual(50,    loaded.betAmount);
            Assert.AreEqual(0.5f,  loaded.bgmVolume, 0.001f);
        }

        [Test]
        public void Load_CorruptedJson_ReturnsDefaultAndCreatesBak()
        {
            File.WriteAllText(_tempPath, "{ invalid json !!!");
            var mgr  = new SaveDataManager(_tempPath, null);
            var data = mgr.Load();

            Assert.AreEqual(1000, data.coins);
            Assert.IsTrue(File.Exists(_tempPath + ".bak"));
        }

        [Test]
        public void Load_InvalidVersion_ReturnsDefault()
        {
            var bad = new SaveData { saveVersion = "9.9" };
            File.WriteAllText(_tempPath, UnityEngine.JsonUtility.ToJson(bad));
            var mgr  = new SaveDataManager(_tempPath, null);
            var data = mgr.Load();

            Assert.AreEqual(1000, data.coins);
        }

        [Test]
        public void Load_CoinsOutOfRange_ReturnsDefault()
        {
            var bad = new SaveData { coins = -100 };
            File.WriteAllText(_tempPath, UnityEngine.JsonUtility.ToJson(bad));
            var mgr  = new SaveDataManager(_tempPath, null);
            var data = mgr.Load();

            Assert.AreEqual(1000, data.coins);
        }

        [Test]
        public void Load_TamperedData_ReturnsDefault()
        {
            var mgr = new SaveDataManager(_tempPath, null);
            var save = new SaveData { coins = 5000 };
            mgr.Save(save);

            // Manual tampering
            string json = File.ReadAllText(_tempPath);
            json = json.Replace("5000", "999999");
            File.WriteAllText(_tempPath, json);

            var loaded = mgr.Load();
            Assert.AreEqual(1000, loaded.coins); // Back to default due to checksum failure
        }
    }
}
