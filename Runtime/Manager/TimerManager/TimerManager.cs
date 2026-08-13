using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuriKit
{
    /// <summary>
    ///     基于 Unity 帧更新管理延迟计时器、循环计时器和整秒/整分钟事件。
    /// </summary>
    /// <remarks>
    ///     计时器的新增、移除、暂停、恢复和生命周期清理由管理器集中执行。
    ///     计时器回调内可以安全地创建或控制其它计时器，结构变更会在当前帧计时遍历结束后统一整理。
    /// </remarks>
    public class TimerManager : ManagerSingleton<TimerManager>
    {
        private const string DEFAULT_TAG = "_Default_";

        private readonly List<TimerHandle> _activeTimers = new(64);
        private readonly List<TimerHandle> _pendingTimers = new(16);
        private bool _isUpdatingTimers;

        private int _lastRealMinute = -1;
        private int _lastRealSecond = -1;
        private int _lastScaledMinute = -1;
        private int _lastScaledSecond = -1;

        /// <summary>
        ///     当非缩放时间跨越新的整数秒时发生。若一帧跨越多秒，会按跨越次数重复触发。
        /// </summary>
        public event Action OnRealSecondChanged;

        /// <summary>
        ///     当非缩放时间跨越新的整数分钟时发生。若一帧跨越多分钟，会按跨越次数重复触发。
        /// </summary>
        public event Action OnRealMinuteChanged;

        /// <summary>
        ///     当受 <see cref="Time.timeScale" /> 影响的时间跨越新的整数秒时发生。若一帧跨越多秒，会按跨越次数重复触发。
        /// </summary>
        public event Action OnSecondChanged;

        /// <summary>
        ///     当受 <see cref="Time.timeScale" /> 影响的时间跨越新的整数分钟时发生。若一帧跨越多分钟，会按跨越次数重复触发。
        /// </summary>
        public event Action OnMinuteChanged;

        protected override void OnSingletonAwake()
        {
            InitializeTimeMarkers();
        }

        protected override void OnSingletonDestroy()
        {
            for (int i = _activeTimers.Count - 1; i >= 0; i--)
            {
                _activeTimers[i].Reset();
            }

            for (int i = _pendingTimers.Count - 1; i >= 0; i--)
            {
                _pendingTimers[i].Reset();
            }

            _activeTimers.Clear();
            _pendingTimers.Clear();

            OnRealSecondChanged = null;
            OnRealMinuteChanged = null;
            OnSecondChanged = null;
            OnMinuteChanged = null;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float unscaledDt = Time.unscaledDeltaTime;

            DetectTimeJumps();

            _isUpdatingTimers = true;
            try
            {
                for (int i = _activeTimers.Count - 1; i >= 0; i--)
                {
                    TickTimer(_activeTimers[i], dt, unscaledDt);
                }
            }
            finally
            {
                _isUpdatingTimers = false;
                FinalizeTimerCollections();
            }
        }

        /// <summary>
        ///     创建一个在指定延迟后执行一次回调的计时器。
        /// </summary>
        /// <param name="delay">执行回调前的延迟时间，单位为秒。负数按 0 处理；0 表示在下一次计时器更新时触发。</param>
        /// <param name="callback">计时结束时执行的回调，不能为 <c>null</c>。</param>
        /// <param name="useUnscaledTime">是否使用不受 <see cref="Time.timeScale" /> 影响的时间。</param>
        /// <param name="timerTag">用于批量管理计时器的标签。为 <c>null</c> 时使用默认标签；标签匹配为精确匹配。</param>
        /// <returns>用于查询和请求控制新计时器的句柄。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="callback" /> 为 <c>null</c>。</exception>
        public TimerHandle AddTimer(float delay, Action callback, bool useUnscaledTime = false, string timerTag = DEFAULT_TAG)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }
            if (delay < 0f)
            {
                delay = 0f;
            }

            TimerHandle timer = new();
            timer.Initialize(this, delay, delay, callback, useUnscaledTime, NormalizeTag(timerTag), false);
            AddManagedTimer(timer);
            return timer;
        }

        /// <summary>
        ///     创建一个在首次延迟后执行回调，并按指定间隔重复执行的计时器。
        /// </summary>
        /// <param name="delay">首次执行回调前的延迟时间，单位为秒。负数按 0 处理；0 表示在下一次计时器更新时触发。</param>
        /// <param name="interval">后续两次回调之间的间隔，单位为秒。负数按 0 处理；0 表示每次计时器更新最多触发一次。</param>
        /// <param name="callback">每次计时结束时执行的回调，不能为 <c>null</c>。</param>
        /// <param name="useUnscaledTime">是否使用不受 <see cref="Time.timeScale" /> 影响的时间。</param>
        /// <param name="timerTag">用于批量管理计时器的标签。为 <c>null</c> 时使用默认标签；标签匹配为精确匹配。</param>
        /// <returns>用于查询和请求控制新计时器的句柄。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="callback" /> 为 <c>null</c>。</exception>
        public TimerHandle AddLoopTimer(float delay, float interval, Action callback, bool useUnscaledTime = false, string timerTag = DEFAULT_TAG)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }
            if (delay < 0f)
            {
                delay = 0f;
            }
            if (interval < 0f)
            {
                interval = 0f;
            }

            TimerHandle timer = new();
            timer.Initialize(this, delay, interval, callback, useUnscaledTime, NormalizeTag(timerTag), true);
            AddManagedTimer(timer);
            return timer;
        }

        /// <summary>
        ///     移除当前由管理器维护的所有活动计时器，包括本帧回调中新建但尚未开始计时的计时器。
        /// </summary>
        public void RemoveAllTimers()
        {
            RemoveTimers(timer => timer._isActive);
        }

        /// <summary>
        ///     移除指定标签的活动计时器，包括本帧回调中新建但尚未开始计时的计时器。
        /// </summary>
        /// <param name="timerTag">要精确匹配的计时器标签；为 <c>null</c> 时匹配默认标签。</param>
        public void RemoveTimersByTag(string timerTag)
        {
            string normalizedTag = NormalizeTag(timerTag);
            RemoveTimers(timer => timer._tag == normalizedTag);
        }

        /// <summary>
        ///     暂停当前由管理器维护的所有活动计时器，包括本帧回调中新建但尚未开始计时的计时器。
        /// </summary>
        public void PauseAllTimers()
        {
            SetPauseState(timer => timer._isActive, true);
        }

        /// <summary>
        ///     暂停指定标签的活动计时器，包括本帧回调中新建但尚未开始计时的计时器。
        /// </summary>
        /// <param name="timerTag">要精确匹配的计时器标签；为 <c>null</c> 时匹配默认标签。</param>
        public void PauseTimersByTag(string timerTag)
        {
            string normalizedTag = NormalizeTag(timerTag);
            SetPauseState(timer => timer._tag == normalizedTag, true);
        }

        /// <summary>
        ///     恢复当前由管理器维护的所有活动计时器，包括本帧回调中新建但尚未开始计时的计时器。
        /// </summary>
        public void ResumeAllTimers()
        {
            SetPauseState(timer => timer._isActive, false);
        }

        /// <summary>
        ///     恢复指定标签的活动计时器，包括本帧回调中新建但尚未开始计时的计时器。
        /// </summary>
        /// <param name="timerTag">要精确匹配的计时器标签；为 <c>null</c> 时匹配默认标签。</param>
        public void ResumeTimersByTag(string timerTag)
        {
            string normalizedTag = NormalizeTag(timerTag);
            SetPauseState(timer => timer._tag == normalizedTag, false);
        }

        internal void RemoveTimer(TimerHandle timer)
        {
            if (!CanControl(timer)) return;

            DeactivateTimer(timer);

            if (!_isUpdatingTimers)
            {
                FinalizeTimerCollections();
            }
        }

        internal void PauseTimer(TimerHandle timer)
        {
            if (CanControl(timer))
            {
                timer._isPaused = true;
            }
        }

        internal void ResumeTimer(TimerHandle timer)
        {
            if (CanControl(timer))
            {
                timer._isPaused = false;
            }
        }

        private void InitializeTimeMarkers()
        {
            _lastRealSecond = Mathf.FloorToInt(Time.unscaledTime);
            _lastRealMinute = Mathf.FloorToInt(Time.unscaledTime / 60f);
            _lastScaledSecond = Mathf.FloorToInt(Time.time);
            _lastScaledMinute = Mathf.FloorToInt(Time.time / 60f);
        }

        private void AddManagedTimer(TimerHandle timer)
        {
            if (_isUpdatingTimers)
            {
                _pendingTimers.Add(timer);
            }
            else
            {
                _activeTimers.Add(timer);
            }
        }

        private void TickTimer(TimerHandle timer, float dt, float unscaledDt)
        {
            if (!CanTick(timer)) return;

            timer._elapsed += timer._useUnscaledTime ? unscaledDt : dt;

            if (timer._elapsed < timer._duration) return;

            Action callback = timer._callback;
            try
            {
                callback?.Invoke();
            }
            catch (Exception exception)
            {
                RLog.LogException(exception, this);
            }
            finally
            {
                if (CanControl(timer))
                {
                    if (timer._isLoop)
                    {
                        timer._elapsed -= timer._duration;
                        timer._duration = timer._interval;
                    }
                    else
                    {
                        DeactivateTimer(timer);
                    }
                }
            }
        }

        private bool CanTick(TimerHandle timer)
        {
            return timer is { _owner: not null, _isActive: true, _isPaused: false } && timer._owner == this;
        }

        private bool CanControl(TimerHandle timer)
        {
            return timer is { _owner: not null, _isActive: true } && timer._owner == this;
        }

        private void RemoveTimers(Predicate<TimerHandle> match)
        {
            DeactivateMatchingTimers(_activeTimers, match);
            DeactivateMatchingTimers(_pendingTimers, match);

            if (!_isUpdatingTimers)
            {
                FinalizeTimerCollections();
            }
        }

        private static void DeactivateMatchingTimers(List<TimerHandle> timers, Predicate<TimerHandle> match)
        {
            for (int i = timers.Count - 1; i >= 0; i--)
            {
                TimerHandle timer = timers[i];
                if (timer._isActive && match(timer))
                {
                    DeactivateTimer(timer);
                }
            }
        }

        private void SetPauseState(Predicate<TimerHandle> match, bool isPaused)
        {
            SetPauseState(_activeTimers, match, isPaused);
            SetPauseState(_pendingTimers, match, isPaused);
        }

        private static void SetPauseState(List<TimerHandle> timers, Predicate<TimerHandle> match, bool isPaused)
        {
            for (int i = timers.Count - 1; i >= 0; i--)
            {
                TimerHandle timer = timers[i];
                if (timer._isActive && match(timer))
                {
                    timer._isPaused = isPaused;
                }
            }
        }

        private static void DeactivateTimer(TimerHandle timer)
        {
            timer._isActive = false;
            timer._isPaused = false;
            timer._callback = null;
        }

        private void FinalizeTimerCollections()
        {
            CleanupInactiveTimers();
            ActivatePendingTimers();
        }

        private void CleanupInactiveTimers()
        {
            for (int i = _activeTimers.Count - 1; i >= 0; i--)
            {
                TimerHandle timer = _activeTimers[i];
                if (timer._owner != this || !timer._isActive)
                {
                    _activeTimers.RemoveAt(i);
                    timer.Reset();
                }
            }
        }

        private void ActivatePendingTimers()
        {
            if (_pendingTimers.Count == 0) return;

            for (int i = 0; i < _pendingTimers.Count; i++)
            {
                TimerHandle timer = _pendingTimers[i];
                if (timer._owner == this && timer._isActive)
                {
                    _activeTimers.Add(timer);
                }
                else
                {
                    timer.Reset();
                }
            }

            _pendingTimers.Clear();
        }

        private void DetectTimeJumps()
        {
            float realNow = Time.unscaledTime;
            int currentRealSecond = Mathf.FloorToInt(realNow);
            int currentRealMinute = Mathf.FloorToInt(realNow / 60f);

            InvokeElapsedEvents(currentRealSecond, ref _lastRealSecond, OnRealSecondChanged);
            InvokeElapsedEvents(currentRealMinute, ref _lastRealMinute, OnRealMinuteChanged);

            float scaledNow = Time.time;
            int currentScaledSecond = Mathf.FloorToInt(scaledNow);
            int currentScaledMinute = Mathf.FloorToInt(scaledNow / 60f);

            InvokeElapsedEvents(currentScaledSecond, ref _lastScaledSecond, OnSecondChanged);
            InvokeElapsedEvents(currentScaledMinute, ref _lastScaledMinute, OnMinuteChanged);
        }

        private static void InvokeElapsedEvents(int currentValue, ref int lastValue, Action repeatedEvent)
        {
            int elapsed = currentValue - lastValue;
            if (elapsed <= 0)
            {
                if (elapsed < 0)
                {
                    lastValue = currentValue;
                }
                return;
            }

            lastValue = currentValue;
            InvokeRepeated(repeatedEvent, elapsed);
        }

        private static void InvokeRepeated(Action callback, int count)
        {
            if (callback == null) return;

            for (int i = 0; i < count; i++)
            {
                callback.Invoke();
            }
        }

        private static string NormalizeTag(string timerTag)
        {
            return timerTag ?? DEFAULT_TAG;
        }
    }
}