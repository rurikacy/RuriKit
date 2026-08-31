using System;
using UnityEngine;

namespace RuriKit
{
    /// <summary>
    ///     表示由 <see cref="TimerManager" /> 创建的计时器句柄。
    /// </summary>
    /// <remarks>
    ///     句柄只负责查询和发起控制请求，实际的暂停、恢复、移除和生命周期清理由 <see cref="TimerManager" /> 执行。
    ///     计时器结束或被移除后，句柄会失效；对失效句柄再次调用控制方法不会影响后续创建的计时器。
    /// </remarks>
    public class TimerHandle
    {
        internal Action _callback;
        internal float _duration;
        internal float _elapsed;
        internal float _interval;
        internal bool _isActive;
        internal bool _isLoop;
        internal bool _isPaused;
        internal TimerManager _owner;
        internal string _tag;
        internal bool _useUnscaledTime;

        /// <summary>
        ///     获取计时器是否仍处于活动状态。
        /// </summary>
        public bool IsActive => _owner && _isActive;

        /// <summary>
        ///     获取当前周期的剩余时间，单位为秒；句柄失效时返回 0。
        /// </summary>
        public float RemainingTime => IsActive ? Mathf.Max(0f, _duration - _elapsed) : 0f;

        /// <summary>
        ///     获取用于分组管理计时器的标签；句柄失效后可能为 <c>null</c>。
        /// </summary>
        public string Tag => _tag;

        /// <summary>
        ///     获取当前周期的总时长，单位为秒；句柄失效时返回 0。
        /// </summary>
        public float Duration => IsActive ? _duration : 0f;

        /// <summary>
        ///     获取当前周期的归一化进度，范围为 0 到 1；句柄失效时返回 1。
        /// </summary>
        public float Progress => IsActive && _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;

        /// <summary>
        ///     获取计时器是否处于暂停状态；句柄失效时返回 <c>false</c>。
        /// </summary>
        public bool IsPaused => IsActive && _isPaused;

        /// <summary>
        ///     获取计时器是否会重复执行回调；句柄失效时返回 <c>false</c>。
        /// </summary>
        public bool IsLoop => IsActive && _isLoop;

        /// <summary>
        ///     请求移除此计时器，使其不再计时或执行回调；句柄失效时不执行任何操作。
        /// </summary>
        public void Remove()
        {
            _owner?.RemoveTimer(this);
        }

        /// <summary>
        ///     请求暂停此计时器；句柄失效时不执行任何操作。
        /// </summary>
        public void Pause()
        {
            _owner?.PauseTimer(this);
        }

        /// <summary>
        ///     请求恢复此计时器；句柄失效时不执行任何操作。
        /// </summary>
        public void Resume()
        {
            _owner?.ResumeTimer(this);
        }

        internal void Initialize(TimerManager owner,
            float duration,
            float interval,
            Action callback,
            bool useUnscaledTime,
            string tag,
            bool isLoop)
        {
            _owner = owner;
            _elapsed = 0f;
            _duration = duration;
            _interval = interval;
            _callback = callback;
            _tag = tag;
            _useUnscaledTime = useUnscaledTime;
            _isLoop = isLoop;
            _isPaused = false;
            _isActive = true;
        }

        internal void Reset()
        {
            _elapsed = 0f;
            _duration = 0f;
            _interval = 0f;
            _callback = null;
            _owner = null;
            _tag = null;
            _useUnscaledTime = false;
            _isLoop = false;
            _isPaused = false;
            _isActive = false;
        }
    }
}
