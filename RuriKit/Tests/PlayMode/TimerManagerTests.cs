using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RuriKit.Tests.PlayMode
{
    /// <summary>
    ///     验证计时器的边界、暂停、移除、重入与管理器销毁清理。
    /// </summary>
    public class TimerManagerTests
    {
        private TimerManager _manager;

        /// <summary>
        ///     为每个测试创建独立计时器管理器。
        /// </summary>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (TimerManager.TryGetInstance(out TimerManager existing))
            {
                Object.Destroy(existing.gameObject);
                yield return null;
            }

            _manager = TimerManager.Instance;
            yield return null;
        }

        /// <summary>
        ///     移除计时器并销毁管理器，防止静态状态影响其他测试。
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_manager)
            {
                _manager.RemoveAllTimers();
                Object.Destroy(_manager.gameObject);
            }

            Time.timeScale = 1f;
            yield return null;
        }

        /// <summary>
        ///     验证一次性计时器在累计时间达到延迟后仅触发一次并失效。
        /// </summary>
        [Test]
        public void AddTimer_WhenDelayElapses_ShouldInvokeOnceAndDeactivate()
        {
            int calls = 0;
            TimerHandle timer = _manager.AddTimer(1f, () => calls++);

            _manager.TickForTests(0.4f, 0.4f);
            Assert.That(calls, Is.Zero);
            Assert.That(timer.RemainingTime, Is.EqualTo(0.6f).Within(0.0001f));
            _manager.TickForTests(0.6f, 0.6f);

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(timer.IsActive, Is.False);
            Assert.That(timer.Progress, Is.EqualTo(1f));
        }

        /// <summary>
        ///     验证循环计时器每帧最多触发一次，即使单帧跨越多个间隔。
        /// </summary>
        [Test]
        public void AddLoopTimer_WhenFrameExceedsSeveralIntervals_ShouldInvokeAtMostOncePerTick()
        {
            int calls = 0;
            TimerHandle timer = _manager.AddLoopTimer(0.1f, 0.1f, () => calls++);

            _manager.TickForTests(0.35f, 0.35f);
            _manager.TickForTests(0f, 0f);

            Assert.That(calls, Is.EqualTo(2));
            Assert.That(timer.IsActive, Is.True);
        }

        /// <summary>
        ///     验证暂停后不累计时间，恢复后继续计时。
        /// </summary>
        [Test]
        public void PauseAndResume_WhenTimerIsPaused_ShouldFreezeThenContinue()
        {
            int calls = 0;
            TimerHandle timer = _manager.AddTimer(1f, () => calls++);
            _manager.TickForTests(0.4f, 0.4f);
            timer.Pause();
            _manager.TickForTests(10f, 10f);

            Assert.That(timer.IsPaused, Is.True);
            Assert.That(calls, Is.Zero);
            timer.Resume();
            _manager.TickForTests(0.6f, 0.6f);

            Assert.That(calls, Is.EqualTo(1));
        }

        /// <summary>
        ///     验证按标签暂停、恢复和移除只影响匹配计时器。
        /// </summary>
        [Test]
        public void TimerTagOperations_WhenTagsDiffer_ShouldOnlyAffectMatchingTimers()
        {
            int firstCalls = 0;
            int secondCalls = 0;
            TimerHandle first = _manager.AddTimer(1f, () => firstCalls++, timerTag: "first");
            TimerHandle second = _manager.AddTimer(1f, () => secondCalls++, timerTag: "second");
            _manager.PauseTimersByTag("first");
            _manager.TickForTests(1f, 1f);

            Assert.That(firstCalls, Is.Zero);
            Assert.That(secondCalls, Is.EqualTo(1));
            _manager.ResumeTimersByTag("first");
            _manager.RemoveTimersByTag("first");

            Assert.That(first.IsActive, Is.False);
            Assert.That(second.IsActive, Is.False);
        }

        /// <summary>
        ///     验证回调中的异常被记录且不会阻断其余计时器。
        /// </summary>
        [Test]
        public void Tick_WhenTimerCallbackThrows_ShouldLogAndContinueOtherTimers()
        {
            int calls = 0;
            _manager.AddTimer(0f, () => throw new InvalidOperationException("timer callback failure"));
            _manager.AddTimer(0f, () => calls++);
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: timer callback failure");

            _manager.TickForTests(0f, 0f);

            Assert.That(calls, Is.EqualTo(1));
        }

        /// <summary>
        ///     验证回调中新建的计时器在下一次推进才开始计时。
        /// </summary>
        [Test]
        public void Tick_WhenCallbackAddsTimer_ShouldDeferNewTimerUntilNextTick()
        {
            int calls = 0;
            _manager.AddTimer(0f, () => _manager.AddTimer(0f, () => calls++));

            _manager.TickForTests(0f, 0f);
            Assert.That(calls, Is.Zero);
            _manager.TickForTests(0f, 0f);

            Assert.That(calls, Is.EqualTo(1));
        }

        /// <summary>
        ///     验证回调可安全移除当前循环计时器。
        /// </summary>
        [Test]
        public void Tick_WhenCallbackRemovesCurrentTimer_ShouldNotRunItAgain()
        {
            int calls = 0;
            TimerHandle timer = null;
            timer = _manager.AddLoopTimer(0f, 0f, () =>
            {
                calls++;
                timer.Remove();
            });

            _manager.TickForTests(0f, 0f);
            _manager.TickForTests(0f, 0f);

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(timer.IsActive, Is.False);
        }

        /// <summary>
        ///     验证回调可删除尚未被遍历到的其他计时器。
        /// </summary>
        [Test]
        public void Tick_WhenCallbackRemovesOtherTimer_ShouldPreventOtherCallback()
        {
            int calls = 0;
            TimerHandle removed = _manager.AddTimer(0f, () => calls++);
            _manager.AddTimer(0f, removed.Remove);

            _manager.TickForTests(0f, 0f);

            Assert.That(calls, Is.Zero);
            Assert.That(removed.IsActive, Is.False);
        }

        /// <summary>
        ///     验证负数延迟和零间隔会在下一次推进触发且不会同帧无限循环。
        /// </summary>
        [Test]
        public void AddLoopTimer_WhenDelayOrIntervalIsNegative_ShouldClampToZeroAndRunOncePerTick()
        {
            int calls = 0;
            TimerHandle timer = _manager.AddLoopTimer(-1f, -1f, () => calls++);

            _manager.TickForTests(0f, 0f);
            _manager.TickForTests(0f, 0f);

            Assert.That(calls, Is.EqualTo(2));
            Assert.That(timer.IsActive, Is.True);
        }

        /// <summary>
        ///     验证缩放时间暂停时，非缩放计时器仍会推进而普通计时器不推进。
        /// </summary>
        [Test]
        public void Tick_WhenScaledDeltaIsZero_ShouldOnlyAdvanceUnscaledTimer()
        {
            int scaledCalls = 0;
            int unscaledCalls = 0;
            _manager.AddTimer(1f, () => scaledCalls++);
            _manager.AddTimer(1f, () => unscaledCalls++, true);

            _manager.TickForTests(0f, 1f);

            Assert.That(scaledCalls, Is.Zero);
            Assert.That(unscaledCalls, Is.EqualTo(1));
        }

        /// <summary>
        ///     验证空回调会在创建边界抛出参数异常。
        /// </summary>
        [Test]
        public void AddTimer_WhenCallbackIsNull_ShouldThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.AddTimer(1f, null));
            Assert.Throws<ArgumentNullException>(() => _manager.AddLoopTimer(1f, 1f, null));
        }

        /// <summary>
        ///     验证管理器销毁时会使未完成计时器失效。
        /// </summary>
        [UnityTest]
        public IEnumerator OnDestroy_WhenTimersRemain_ShouldInvalidateTheirHandles()
        {
            TimerHandle timer = _manager.AddTimer(5f, () => { });
            Object.Destroy(_manager.gameObject);
            yield return null;

            Assert.That(timer.IsActive, Is.False);
            _manager = null;
        }
    }
}
