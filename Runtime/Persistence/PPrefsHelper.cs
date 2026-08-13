using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace RuriKit
{
    /// <summary>
    ///     提供基于 PlayerPrefs 的键值数据读写，并通过延迟冲刷减少磁盘写入次数。
    /// </summary>
    /// <remarks>
    ///     依赖 Newtonsoft.Json 包（package name：<c>com.unity.nuget.newtonsoft-json</c>）。
    ///     此类未在 RuriKit 内部引用，如不需要可删除。
    /// </remarks>
    public static class PPrefsHelper
    {
        private const float FLUSH_INTERVAL = 0.5f;
        private const float FULL_INTERVAL = 3f;

        private static readonly object _cacheLock = new();
        private static readonly object _flushLock = new();
        private static readonly Dictionary<string, string> _cache = new();
        private static bool _isDirty;
        private static TimerHandle _flushTimerHandle;
        private static TimerHandle _fullTimerHandle;

        static PPrefsHelper()
        {
            Application.quitting += Flush;
        }

        /// <summary>
        ///     读取并反序列化指定键对应的数据。
        /// </summary>
        /// <typeparam name="T">要读取的数据类型</typeparam>
        /// <param name="key">用于定位数据的键。不能为空、空字符串或仅包含空白字符。</param>
        /// <param name="defaultValue">数据不存在、读取失败或反序列化失败时返回的值</param>
        /// <returns>反序列化后的数据；读取失败时返回 <paramref name="defaultValue" />。</returns>
        /// <exception cref="ArgumentException"><paramref name="key" /> 为空、空字符串或仅包含空白字符。</exception>
        public static T Read<T>(string key, T defaultValue = default)
        {
            ValidateKey(key);

            if (!PlayerPrefs.HasKey(key)) return defaultValue;

            Type type = typeof(T);

            try
            {
                if (type == typeof(int))
                {
                    return (T)(object)PlayerPrefs.GetInt(key);
                }

                if (type == typeof(float))
                {
                    return (T)(object)PlayerPrefs.GetFloat(key);
                }

                if (type == typeof(string))
                {
                    return (T)(object)PlayerPrefs.GetString(key);
                }

                if (type == typeof(bool))
                {
                    return (T)(object)(PlayerPrefs.GetInt(key) != 0);
                }

                string json = PlayerPrefs.GetString(key);
                if (string.IsNullOrEmpty(json))
                {
                    return defaultValue;
                }

                T value = JsonConvert.DeserializeObject<T>(json);
                return value == null ? defaultValue : value;
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        /// <summary>
        ///     序列化指定数据并写入 PlayerPrefs 内存注册表，在缓冲时间结束后持久化到磁盘。
        /// </summary>
        /// <typeparam name="T">要写入的数据类型</typeparam>
        /// <param name="key">用于定位数据的键。不能为空、空字符串或仅包含空白字符。</param>
        /// <param name="value">要序列化并持久化的数据</param>
        /// <exception cref="ArgumentException"><paramref name="key" /> 为空、空字符串或仅包含空白字符。</exception>
        public static void Write<T>(string key, T value)
        {
            ValidateKey(key);

            Type type = typeof(T);
            string serialized = SerializeForCache(value, type);

            lock (_cacheLock)
            {
                if (_cache.TryGetValue(key, out string cached) && cached == serialized) return;

                _cache[key] = serialized;
                _isDirty = true;
            }

            CommitToPlayerPrefs(key, value, type, serialized);

            _flushTimerHandle?.Remove();
            _flushTimerHandle = TimerManager.Instance.AddTimer(FLUSH_INTERVAL, OnFlushTimer);

            if (_fullTimerHandle is not { IsActive: true })
            {
                _fullTimerHandle = TimerManager.Instance.AddTimer(FULL_INTERVAL, OnFullTimer);
            }
        }

        /// <summary>
        ///     检查指定键在 PlayerPrefs 中是否存在。
        /// </summary>
        /// <param name="key">要检查的键。不能为空、空字符串或仅包含空白字符。</param>
        /// <returns>键存在时返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        /// <exception cref="ArgumentException"><paramref name="key" /> 为空、空字符串或仅包含空白字符。</exception>
        public static bool HasKey(string key)
        {
            ValidateKey(key);
            return PlayerPrefs.HasKey(key);
        }

        /// <summary>
        ///     尝试从 PlayerPrefs 及内存缓存中删除指定键，返回是否实际执行了删除。
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
        ///     从 PlayerPrefs 及内存缓存中删除指定键。
        /// </summary>
        /// <param name="key">要删除的键。不能为空、空字符串或仅包含空白字符。</param>
        /// <exception cref="ArgumentException"><paramref name="key" /> 为空、空字符串或仅包含空白字符。</exception>
        public static void DeleteKey(string key)
        {
            ValidateKey(key);
            bool deleted = PlayerPrefs.HasKey(key);
            PlayerPrefs.DeleteKey(key);

            lock (_cacheLock)
            {
                _cache.Remove(key);
                _isDirty |= deleted;
            }
        }

        /// <summary>
        ///     删除 PlayerPrefs 中的所有键及内存缓存，并停止所有缓冲计时器。
        /// </summary>
        public static void DeleteAll()
        {
            PlayerPrefs.DeleteAll();

            lock (_cacheLock)
            {
                _cache.Clear();
                _isDirty = false;
            }

            _flushTimerHandle?.Remove();
            _flushTimerHandle = null;
            _fullTimerHandle?.Remove();
            _fullTimerHandle = null;
        }

        /// <summary>
        ///     立即将所有缓存的更改持久化到磁盘，并停止所有缓冲计时器。
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

            lock (_flushLock)
            {
                bool shouldSave;

                lock (_cacheLock)
                {
                    shouldSave = _isDirty;
                    _isDirty = false;
                }

                if (shouldSave)
                {
                    PlayerPrefs.Save();
                }
            }
        }

        private static void OnFlushTimer()
        {
            _flushTimerHandle = null;
            _fullTimerHandle?.Remove();
            _fullTimerHandle = null;
            Flush();
        }

        private static void OnFullTimer()
        {
            _fullTimerHandle = null;
            _flushTimerHandle?.Remove();
            _flushTimerHandle = null;
            Flush();
        }

        private static string SerializeForCache<T>(T value, Type type)
        {
            if (type == typeof(int))
            {
                return ((int)(object)value).ToString();
            }

            if (type == typeof(float))
            {
                return ((float)(object)value).ToString("R");
            }

            if (type == typeof(string))
            {
                return (string)(object)value;
            }

            if (type == typeof(bool))
            {
                return ((bool)(object)value).ToString();
            }

            return JsonConvert.SerializeObject(value, Formatting.None);
        }

        private static void CommitToPlayerPrefs<T>(string key, T value, Type type, string serialized)
        {
            if (type == typeof(int))
            {
                PlayerPrefs.SetInt(key, (int)(object)value);
                return;
            }

            if (type == typeof(float))
            {
                PlayerPrefs.SetFloat(key, (float)(object)value);
                return;
            }

            if (type == typeof(string))
            {
                PlayerPrefs.SetString(key, (string)(object)value);
                return;
            }

            if (type == typeof(bool))
            {
                PlayerPrefs.SetInt(key, (bool)(object)value ? 1 : 0);
                return;
            }

            PlayerPrefs.SetString(key, serialized);
        }

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("PlayerPrefs 键为空", nameof(key));
            }
        }
    }
}
