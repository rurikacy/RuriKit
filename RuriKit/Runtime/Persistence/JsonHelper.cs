using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace RuriKit
{
    /// <summary>
    ///     提供基于 JSON 文件的键值数据读写，并通过内存缓存延迟写入持久化目录。
    /// </summary>
    /// <remarks>
    ///     依赖 Newtonsoft.Json 包（package name：<c>com.unity.nuget.newtonsoft-json</c>）。
    ///     此类未在 RuriKit 内部引用，如不需要可删除。
    /// </remarks>
    public static class JsonHelper
    {
        private const string DATA_DIRECTORY = "JsonData";
        private const string FILE_EXTENSION = ".json";
        private const float FLUSH_INTERVAL = 0.5f;
        private const float FULL_INTERVAL = 3f;

        private static readonly object _cacheLock = new();
        private static readonly object _flushLock = new();
        private static readonly Dictionary<string, string> _cache = new();
        private static readonly HashSet<string> _dirtyKeys = new();
        private static readonly HashSet<string> _legacyKeys = new();
        private static readonly string _dataDirectoryPath;
        private static TimerHandle _flushTimerHandle;
        private static TimerHandle _fullTimerHandle;

        static JsonHelper()
        {
            _dataDirectoryPath = Path.Combine(Application.persistentDataPath, DATA_DIRECTORY);
            Application.quitting += Flush;
        }

        /// <summary>
        ///     供测试隔离 JSON 数据目录；为 <c>null</c> 时使用正式持久化目录。
        /// </summary>
        internal static string DataDirectoryPathOverride { get; set; }

        /// <summary>
        ///     读取并反序列化指定键对应的 JSON 数据。
        /// </summary>
        /// <typeparam name="T">要读取的数据类型</typeparam>
        /// <param name="key">用于定位数据文件的键。不能为空、空字符串或仅包含空白字符。</param>
        /// <param name="defaultValue">数据不存在、文件读取失败或反序列化失败时返回的值</param>
        /// <returns>反序列化后的数据；读取失败时返回 <paramref name="defaultValue" />。</returns>
        /// <exception cref="ArgumentException"><paramref name="key" /> 为空、空字符串或仅包含空白字符。</exception>
        public static T Read<T>(string key, T defaultValue = default)
        {
            ValidateKey(key);
            EnsureCached(key);

            string json;
            lock (_cacheLock)
            {
                json = _cache[key];
            }

            if (string.IsNullOrEmpty(json))
            {
                return defaultValue;
            }

            try
            {
                T value = JsonConvert.DeserializeObject<T>(json);
                return value == null ? defaultValue : value;
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        /// <summary>
        ///     序列化指定数据并写入内存缓存，在缓冲时间结束后保存到持久化目录。
        /// </summary>
        /// <typeparam name="T">要写入的数据类型</typeparam>
        /// <param name="key">用于定位数据文件的键。不能为空、空字符串或仅包含空白字符。</param>
        /// <param name="value">要序列化并持久化的数据</param>
        /// <exception cref="ArgumentException"><paramref name="key" /> 为空、空字符串或仅包含空白字符。</exception>
        public static void Write<T>(string key, T value)
        {
            ValidateKey(key);
            EnsureCached(key);

            string json = JsonConvert.SerializeObject(value, Formatting.None);

            lock (_cacheLock)
            {
                bool needsMigration = _legacyKeys.Remove(key);
                if (_cache[key] == json && !needsMigration) return;

                _cache[key] = json;
                _dirtyKeys.Add(key);
            }

            _flushTimerHandle?.Remove();
            _flushTimerHandle = TimerManager.Instance.AddTimer(FLUSH_INTERVAL, OnFlushTimer);

            if (_fullTimerHandle is not { IsActive: true })
            {
                _fullTimerHandle = TimerManager.Instance.AddTimer(FULL_INTERVAL, OnFullTimer);
            }
        }

        /// <summary>
        ///     检查指定键对应的 JSON 数据是否存在。
        /// </summary>
        /// <param name="key">要检查的键。不能为空、空字符串或仅包含空白字符。</param>
        /// <returns>键存在时返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        /// <exception cref="ArgumentException"><paramref name="key" /> 为空、空字符串或仅包含空白字符。</exception>
        public static bool HasKey(string key)
        {
            ValidateKey(key);

            lock (_cacheLock)
            {
                if (_cache.TryGetValue(key, out string json))
                {
                    return json != null;
                }
            }

            return File.Exists(GetPath(key)) || File.Exists(GetLegacyPath(key));
        }

        /// <summary>
        ///     尝试删除指定键对应的 JSON 数据，返回是否实际执行了删除。
        /// </summary>
        /// <param name="key">要删除的键。不能为空、空字符串或仅包含空白字符。</param>
        /// <returns>键存在并成功删除时返回 <c>true</c>，键原本就不存在时返回 <c>false</c>。</returns>
        /// <exception cref="ArgumentException"><paramref name="key" /> 为空、空字符串或仅包含空白字符。</exception>
        public static bool TryDeleteKey(string key)
        {
            if (!HasKey(key)) return false;
            DeleteKey(key);
            return true;
        }

        /// <summary>
        ///     删除指定 JSON 键对应的内存缓存和磁盘文件。
        /// </summary>
        /// <param name="key">用于定位数据文件的键。不能为空、空字符串或仅包含空白字符。</param>
        /// <exception cref="ArgumentException"><paramref name="key" /> 为空、空字符串或仅包含空白字符。</exception>
        public static void DeleteKey(string key)
        {
            ValidateKey(key);

            lock (_flushLock)
            {
                lock (_cacheLock)
                {
                    _cache[key] = null;
                    _dirtyKeys.Remove(key);
                    _legacyKeys.Remove(key);
                }

                try
                {
                    string path = GetPath(key);
                    string legacyPath = GetLegacyPath(key);
                    if (File.Exists(path)) File.Delete(path);
                    if (File.Exists(legacyPath)) File.Delete(legacyPath);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Json 数据删除失败，原因如下：{exception.Message}");
                }
            }
        }

        /// <summary>
        ///     删除所有 JSON 键及内存缓存，并停止所有缓冲计时器。
        /// </summary>
        public static void DeleteAll()
        {
            _flushTimerHandle?.Remove();
            _flushTimerHandle = null;
            _fullTimerHandle?.Remove();
            _fullTimerHandle = null;

            lock (_flushLock)
            {
                lock (_cacheLock)
                {
                    _cache.Clear();
                    _dirtyKeys.Clear();
                    _legacyKeys.Clear();
                }

                if (!Directory.Exists(DataDirectoryPath)) return;

                try
                {
                    Directory.Delete(DataDirectoryPath, true);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Json 数据清空失败，原因如下：{exception.Message}");
                }
            }
        }

        /// <summary>
        ///     立即将所有缓存的 JSON 数据持久化到磁盘。
        /// </summary>
        public static void Save()
        {
            Flush();
        }

        private static void Flush()
        {
            _flushTimerHandle?.Remove();
            _flushTimerHandle = null;
            _fullTimerHandle?.Remove();
            _fullTimerHandle = null;
            FlushDirtyData();
        }

        private static void OnFlushTimer()
        {
            _flushTimerHandle = null;
            _fullTimerHandle?.Remove();
            _fullTimerHandle = null;
            FlushDirtyData();
        }

        private static void OnFullTimer()
        {
            _fullTimerHandle = null;
            _flushTimerHandle?.Remove();
            _flushTimerHandle = null;
            FlushDirtyData();
        }

        private static void EnsureCached(string key)
        {
            lock (_cacheLock)
            {
                if (_cache.ContainsKey(key)) return;
            }

            string json = LoadJson(key, out bool loadedFromLegacy);

            lock (_cacheLock)
            {
                if (_cache.TryAdd(key, json) && loadedFromLegacy)
                {
                    _legacyKeys.Add(key);
                }
            }
        }

        private static string LoadJson(string key, out bool loadedFromLegacy)
        {
            loadedFromLegacy = false;
            string path = GetPath(key);
            if (!File.Exists(path))
            {
                path = GetLegacyPath(key);
                if (!File.Exists(path)) return null;
                loadedFromLegacy = true;
            }

            try
            {
                return File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void FlushDirtyData()
        {
            lock (_flushLock)
            {
                List<KeyValuePair<string, string>> writeList = GetDirtySnapshot();
                if (writeList.Count == 0) return;

                try
                {
                    Directory.CreateDirectory(DataDirectoryPath);
                }
                catch (Exception)
                {
                    MarkAllDirtyIfUnchanged(writeList);
                    ScheduleRetry();
                    return;
                }

                bool hasWriteFailure = false;
                foreach (KeyValuePair<string, string> item in writeList)
                {
                    try
                    {
                        File.WriteAllText(GetPath(item.Key), item.Value, Encoding.UTF8);
                    }
                    catch (Exception)
                    {
                        MarkDirtyIfUnchanged(item.Key, item.Value);
                        hasWriteFailure = true;
                    }
                }

                if (hasWriteFailure)
                {
                    ScheduleRetry();
                }
            }
        }

        private static List<KeyValuePair<string, string>> GetDirtySnapshot()
        {
            List<KeyValuePair<string, string>> writeList = new();

            lock (_cacheLock)
            {
                foreach (string key in _dirtyKeys)
                {
                    writeList.Add(new KeyValuePair<string, string>(key, _cache[key]));
                }
                _dirtyKeys.Clear();
            }

            return writeList;
        }

        private static void MarkAllDirtyIfUnchanged(List<KeyValuePair<string, string>> writeList)
        {
            foreach (KeyValuePair<string, string> item in writeList)
            {
                MarkDirtyIfUnchanged(item.Key, item.Value);
            }
        }

        private static void MarkDirtyIfUnchanged(string key, string json)
        {
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(key, out string currentJson) && currentJson == json)
                {
                    _dirtyKeys.Add(key);
                }
            }
        }

        private static string GetPath(string key)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
            StringBuilder fileName = new(hash.Length * 2 + FILE_EXTENSION.Length);

            for (int i = 0; i < hash.Length; i++)
            {
                fileName.Append(hash[i].ToString("x2"));
            }

            fileName.Append(FILE_EXTENSION);
            return Path.Combine(DataDirectoryPath, fileName.ToString());
        }

        private static string GetLegacyPath(string key)
        {
            string safeKey = key.Replace('\\', '_').Replace('/', '_').Replace(':', '_')
                .Replace('*', '_').Replace('?', '_').Replace('"', '_')
                .Replace('<', '_').Replace('>', '_').Replace('|', '_');
            return Path.Combine(DataDirectoryPath, safeKey + FILE_EXTENSION);
        }

        private static void ScheduleRetry()
        {
            if (_flushTimerHandle is { IsActive: true }) return;

            TimerManager manager = TimerManager.Instance;
            if (!manager) return;

            _flushTimerHandle = manager.AddTimer(FULL_INTERVAL, OnFlushTimer);
        }

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Json 键为空", nameof(key));
            }
        }

        /// <summary>
        ///     供测试模拟重新加载时清除进程内缓存，不会修改磁盘数据。
        /// </summary>
        internal static void ResetCacheForTests()
        {
            _flushTimerHandle?.Remove();
            _flushTimerHandle = null;
            _fullTimerHandle?.Remove();
            _fullTimerHandle = null;

            lock (_cacheLock)
            {
                _cache.Clear();
                _dirtyKeys.Clear();
                _legacyKeys.Clear();
            }
        }

        private static string DataDirectoryPath => DataDirectoryPathOverride ?? _dataDirectoryPath;
    }
}
