using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RuriKit.Tests.PlayMode
{
    /// <summary>
    ///     验证 JSON 与 PlayerPrefs 持久化的公开读写、缓存、损坏数据和隔离行为。
    /// </summary>
    public class PersistenceTests
    {
        private string _jsonDirectory;
        private string _playerPrefsPrefix;

        /// <summary>
        ///     为每个测试配置独立 JSON 目录与 PlayerPrefs 键前缀。
        /// </summary>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _jsonDirectory = Path.Combine(Path.GetTempPath(), "RuriKitTests", Guid.NewGuid().ToString("N"));
            _playerPrefsPrefix = $"RuriKitTests.{Guid.NewGuid():N}";
            JsonHelper.DataDirectoryPathOverride = _jsonDirectory;
            JsonHelper.ResetCacheForTests();
            PPrefsHelper.KeyPrefixForTests = _playerPrefsPrefix;
            PPrefsHelper.ResetCacheForTests();
            yield return null;
        }

        /// <summary>
        ///     清理临时目录、静态缓存和本测试创建的 PlayerPrefs 键。
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            JsonHelper.DeleteAll();
            JsonHelper.ResetCacheForTests();
            JsonHelper.DataDirectoryPathOverride = null;
            PPrefsHelper.DeleteAll();
            PPrefsHelper.ResetCacheForTests();
            PPrefsHelper.KeyPrefixForTests = null;
            if (Directory.Exists(_jsonDirectory))
            {
                Directory.Delete(_jsonDirectory, true);
            }

            yield return null;
        }

        /// <summary>
        ///     验证 JsonHelper 读写基础类型、对象、集合及覆盖写入。
        /// </summary>
        [Test]
        public void JsonHelper_WhenValuesAreWrittenAndSaved_ShouldRoundTripSupportedData()
        {
            JsonHelper.Write("number", 7);
            JsonHelper.Write("text", "你好");
            JsonHelper.Write("profile", new PersistenceProbe { Name = "Ruri", Level = 3 });
            JsonHelper.Write("list", new List<int> { 1, 2, 3 });
            JsonHelper.Write("map", new Dictionary<string, int> { ["a"] = 1 });
            JsonHelper.Write("number", 9);
            JsonHelper.Save();
            JsonHelper.ResetCacheForTests();

            Assert.That(JsonHelper.Read("number", -1), Is.EqualTo(9));
            Assert.That(JsonHelper.Read("text", "missing"), Is.EqualTo("你好"));
            Assert.That(JsonHelper.Read<PersistenceProbe>("profile").Level, Is.EqualTo(3));
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, JsonHelper.Read<List<int>>("list"));
            Assert.That(JsonHelper.Read<Dictionary<string, int>>("map")["a"], Is.EqualTo(1));
        }

        /// <summary>
        ///     验证 JSON 缓存写入、快速覆盖、删除及清空行为。
        /// </summary>
        [Test]
        public void JsonHelper_WhenCacheChanges_ShouldExposeCurrentValueAndDeleteCorrectly()
        {
            JsonHelper.Write("rapid", "first");
            JsonHelper.Write("rapid", "second");

            Assert.That(JsonHelper.Read("rapid", "missing"), Is.EqualTo("second"));
            Assert.That(JsonHelper.HasKey("rapid"), Is.True);
            Assert.That(JsonHelper.TryDeleteKey("rapid"), Is.True);
            Assert.That(JsonHelper.TryDeleteKey("rapid"), Is.False);
            Assert.That(JsonHelper.Read("rapid", "fallback"), Is.EqualTo("fallback"));

            JsonHelper.Write("other", 1);
            JsonHelper.DeleteAll();
            Assert.That(JsonHelper.HasKey("other"), Is.False);
        }

        /// <summary>
        ///     验证 JSON 缺失、null 和损坏内容都会按文档返回默认值。
        /// </summary>
        [Test]
        public void JsonHelper_WhenFileIsMissingNullOrInvalid_ShouldReturnDefaultValue()
        {
            Assert.That(JsonHelper.Read("missing", 123), Is.EqualTo(123));
            JsonHelper.Write<string>("null", null);
            JsonHelper.Save();
            JsonHelper.ResetCacheForTests();
            Assert.That(JsonHelper.Read("null", "fallback"), Is.EqualTo("fallback"));

            WriteRawJson("broken", "{not json");
            JsonHelper.ResetCacheForTests();
            Assert.That(JsonHelper.Read("broken", new PersistenceProbe { Name = "fallback" }).Name, Is.EqualTo("fallback"));
        }

        /// <summary>
        ///     验证特殊、Unicode 和长键写入后使用哈希文件名而非原始键名。
        /// </summary>
        [Test]
        public void JsonHelper_WhenKeyContainsSpecialOrUnicodeCharacters_ShouldPersistUsingHashFileName()
        {
            string key = "用户/配置:*?" + new string('k', 512);
            JsonHelper.Write(key, "value");
            JsonHelper.Save();

            string expectedPath = Path.Combine(_jsonDirectory, HashKey(key) + ".json");
            Assert.That(File.Exists(expectedPath), Is.True);
            Assert.That(Path.GetFileName(expectedPath), Does.Not.Contain("用户"));
            JsonHelper.ResetCacheForTests();
            Assert.That(JsonHelper.Read(key, "missing"), Is.EqualTo("value"));
        }

        /// <summary>
        ///     验证 PlayerPrefs 基础类型、对象和覆盖写入。
        /// </summary>
        [Test]
        public void PPrefsHelper_WhenValuesAreWritten_ShouldRoundTripSupportedData()
        {
            PPrefsHelper.Write("integer", 3);
            PPrefsHelper.Write("float", 1.25f);
            PPrefsHelper.Write("bool", true);
            PPrefsHelper.Write("text", "中文");
            PPrefsHelper.Write("profile", new PersistenceProbe { Name = "Ruri", Level = 4 });
            PPrefsHelper.Write("integer", 5);
            PPrefsHelper.Save();
            PPrefsHelper.ResetCacheForTests();

            Assert.That(PPrefsHelper.Read("integer", -1), Is.EqualTo(5));
            Assert.That(PPrefsHelper.Read("float", 0f), Is.EqualTo(1.25f).Within(0.0001f));
            Assert.That(PPrefsHelper.Read("bool", false), Is.True);
            Assert.That(PPrefsHelper.Read("text", "missing"), Is.EqualTo("中文"));
            Assert.That(PPrefsHelper.Read<PersistenceProbe>("profile").Level, Is.EqualTo(4));
        }

        /// <summary>
        ///     验证 PlayerPrefs 缺失、损坏、删除与清空均遵循公开契约。
        /// </summary>
        [Test]
        public void PPrefsHelper_WhenDataIsMissingInvalidOrDeleted_ShouldReturnDefaultValue()
        {
            Assert.That(PPrefsHelper.Read("missing", 11), Is.EqualTo(11));
            string corruptKey = $"{_playerPrefsPrefix}:corrupt";
            PlayerPrefs.SetString(corruptKey, "{bad json");
            Assert.That(PPrefsHelper.Read("corrupt", new PersistenceProbe { Name = "fallback" }).Name, Is.EqualTo("fallback"));
            PlayerPrefs.DeleteKey(corruptKey);

            PPrefsHelper.Write("delete", "value");
            Assert.That(PPrefsHelper.TryDeleteKey("delete"), Is.True);
            Assert.That(PPrefsHelper.HasKey("delete"), Is.False);
            PPrefsHelper.DeleteAll();
            Assert.That(PPrefsHelper.HasKey("delete"), Is.False);
        }

        /// <summary>
        ///     验证无效键统一抛出参数异常，防止产生不可寻址的数据。
        /// </summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void PersistenceHelpers_WhenKeyIsBlank_ShouldThrowArgumentException(string key)
        {
            Assert.Throws<ArgumentException>(() => JsonHelper.Read(key, 0));
            Assert.Throws<ArgumentException>(() => PPrefsHelper.Read(key, 0));
        }

        /// <summary>
        ///     向临时目录写入可复现的损坏 JSON 文件。
        /// </summary>
        private void WriteRawJson(string key, string content)
        {
            Directory.CreateDirectory(_jsonDirectory);
            File.WriteAllText(Path.Combine(_jsonDirectory, HashKey(key) + ".json"), content, Encoding.UTF8);
        }

        /// <summary>
        ///     复现 JsonHelper 对键的稳定 SHA-256 文件名映射。
        /// </summary>
        private static string HashKey(string key)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
            StringBuilder result = new(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) result.Append(hash[i].ToString("x2"));
            return result.ToString();
        }

        /// <summary>
        ///     用于 JSON 序列化验证的简单数据对象。
        /// </summary>
        [Serializable]
        public class PersistenceProbe
        {
            /// <summary>
            ///     名称字段。
            /// </summary>
            public string Name;

            /// <summary>
            ///     等级字段。
            /// </summary>
            public int Level;
        }
    }
}
