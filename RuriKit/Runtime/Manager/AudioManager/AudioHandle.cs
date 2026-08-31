using System;
using UnityEngine;

namespace RuriKit
{
    /// <summary>
    ///     表示由 <see cref="AudioManager" /> 创建的一次音频播放句柄。
    /// </summary>
    /// <remarks>
    ///     句柄只负责查询状态和发起控制请求，实际的播放、暂停、恢复、停止、淡入淡出和资源回收由 <see cref="AudioManager" /> 执行。
    ///     播放自然结束或被停止后，句柄会失效；对失效句柄再次调用控制方法不会影响后续创建的音频播放。
    /// </remarks>
    public class AudioHandle
    {
        internal Coroutine _fadeCoroutine;
        internal bool _isBgm;
        internal AudioManager _manager;
        internal AudioSource _source;

        /// <summary>
        ///     获取当前播放的音量倍率；句柄失效后返回 1。
        /// </summary>
        public float Volume { get; internal set; } = 1f;

        /// <summary>
        ///     获取当前音频是否循环播放；句柄失效后返回 <c>false</c>。
        /// </summary>
        public bool Loop { get; internal set; }

        /// <summary>
        ///     获取当前播放位置，单位为秒；句柄失效或音频片段不可用时返回 0。
        /// </summary>
        public float Time => IsValid && _source.clip ? _source.time : 0f;

        /// <summary>
        ///     获取音频片段的总时长，单位为秒；句柄失效或音频片段不可用时返回 0。
        /// </summary>
        public float Duration => IsValid && _source.clip ? _source.clip.length : 0f;

        /// <summary>
        ///     获取音频当前是否正在播放；句柄失效后返回 <c>false</c>。
        /// </summary>
        public bool IsPlaying => IsValid && _source.isPlaying;

        /// <summary>
        ///     获取音频当前是否处于暂停状态；句柄失效后返回 <c>false</c>。
        /// </summary>
        public bool IsPaused { get; internal set; }

        /// <summary>
        ///     获取音频是否已经停止。句柄失效时视为已停止。
        /// </summary>
        public bool IsStopped => !IsValid || _isStopped;

        /// <summary>
        ///     获取本次播放是否因为自然播完而结束；手动停止不会置为 <c>true</c>。
        /// </summary>
        public bool IsCompleted => _isCompleted;

        /// <summary>
        ///     在本次播放自然播完时发生。手动停止不会触发此事件。
        /// </summary>
        public event Action<AudioHandle> Completed;

        /// <summary>
        ///     在本次播放停止时发生，包括自然播完和手动停止。
        /// </summary>
        public event Action<AudioHandle> Stopped;

        private bool IsValid => _manager && _source && !_isStopped;
        private bool _isCompleted;
        private bool _isStopped = true;

        /// <summary>
        ///     请求立即设置当前播放的音量倍率；句柄失效时不执行任何操作。
        /// </summary>
        /// <param name="volume">目标音量倍率。负数和 <see cref="float.NaN" /> 按 0 处理。</param>
        public void SetVolume(float volume)
        {
            _manager?.SetHandleVolume(this, volume);
        }

        /// <summary>
        ///     请求在指定时间内将当前播放的音量平滑过渡到目标倍率；句柄失效时不执行任何操作。
        /// </summary>
        /// <param name="targetVolume">目标音量倍率。负数和 <see cref="float.NaN" /> 按 0 处理。</param>
        /// <param name="duration">过渡时长，单位为秒。负数按 0 处理。</param>
        public void FadeTo(float targetVolume, float duration)
        {
            _manager?.FadeHandleTo(this, targetVolume, duration);
        }

        /// <summary>
        ///     请求暂停当前播放；句柄失效时不执行任何操作。
        /// </summary>
        public void Pause()
        {
            _manager?.PauseHandle(this);
        }

        /// <summary>
        ///     请求恢复当前播放；句柄失效时不执行任何操作。
        /// </summary>
        public void Resume()
        {
            _manager?.ResumeHandle(this);
        }

        /// <summary>
        ///     请求立即停止当前播放；句柄失效时不执行任何操作。
        /// </summary>
        public void Stop()
        {
            _manager?.StopHandle(this);
        }

        /// <summary>
        ///     请求在指定时间内淡出音量，然后停止当前播放；句柄失效时不执行任何操作。
        /// </summary>
        /// <param name="duration">淡出时长，单位为秒。负数按 0 处理。</param>
        public void Stop(float duration)
        {
            _manager?.StopHandle(this, duration);
        }

        /// <summary>
        ///     请求将播放位置跳转到指定时间；句柄失效或音频片段不可用时不执行任何操作。
        /// </summary>
        /// <param name="time">目标播放位置，单位为秒。该值会限制在音频片段的有效时长内。</param>
        public void Seek(float time)
        {
            _manager?.SeekHandle(this, time);
        }

        internal void Initialize(AudioSource source, AudioManager manager, bool loop, float volume, bool isBgm)
        {
            _source = source;
            _manager = manager;
            Loop = loop;
            Volume = volume;
            _isBgm = isBgm;
            IsPaused = false;
            _isStopped = false;
            _isCompleted = false;
            _fadeCoroutine = null;
        }

        internal void MarkStopped(bool completed)
        {
            _isStopped = true;
            _isCompleted = completed;
            IsPaused = false;
        }

        internal void RaiseCompleted()
        {
            Completed?.Invoke(this);
        }

        internal void RaiseStopped()
        {
            Stopped?.Invoke(this);
        }

        internal void Reset()
        {
            _fadeCoroutine = null;
            _source = null;
            _manager = null;
            Volume = 1f;
            Loop = false;
            _isBgm = false;
            IsPaused = false;
            _isStopped = true;
            _isCompleted = false;
            Completed = null;
            Stopped = null;
        }
    }
}
