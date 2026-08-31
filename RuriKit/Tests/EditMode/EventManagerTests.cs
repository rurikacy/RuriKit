using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RuriKit.Tests.EditMode
{
    /// <summary>
    ///     验证全局事件总线的公开订阅、触发、异常隔离与重置行为。
    /// </summary>
    public class EventManagerTests
    {
        /// <summary>
        ///     每个测试前清理静态监听器，避免测试顺序影响结果。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            EventManager.ResetStaticStateForTests();
        }

        /// <summary>
        ///     验证订阅后可收到发布的数据。
        /// </summary>
        [Test]
        public void FireEvent_WhenListenerIsSubscribed_ShouldPassEventData()
        {
            int received = 0;
            EventManager.AddListener<int>(value => received = value);

            EventManager.FireEvent(42);

            Assert.That(received, Is.EqualTo(42));
        }

        /// <summary>
        ///     验证多个监听器按订阅顺序运行。
        /// </summary>
        [Test]
        public void FireEvent_WhenMultipleListenersAreSubscribed_ShouldInvokeInSubscriptionOrder()
        {
            List<string> calls = new();
            EventManager.AddListener<int>(_ => calls.Add("first"));
            EventManager.AddListener<int>(_ => calls.Add("second"));

            EventManager.FireEvent(0);

            CollectionAssert.AreEqual(new[] { "first", "second" }, calls);
        }

        /// <summary>
        ///     验证移除订阅后该回调不再执行。
        /// </summary>
        [Test]
        public void RemoveListener_WhenListenerWasSubscribed_ShouldStopInvokingIt()
        {
            int calls = 0;
            Action<int> listener = _ => calls++;
            EventManager.AddListener(listener);
            EventManager.RemoveListener(listener);

            EventManager.FireEvent(0);

            Assert.That(calls, Is.Zero);
        }

        /// <summary>
        ///     验证重复订阅保留两个独立调用，重复取消只移除一个委托。
        /// </summary>
        [Test]
        public void AddListener_WhenSameListenerIsAddedTwice_ShouldInvokeOncePerSubscription()
        {
            int calls = 0;
            Action<int> listener = _ => calls++;
            EventManager.AddListener(listener);
            EventManager.AddListener(listener);
            EventManager.RemoveListener(listener);

            EventManager.FireEvent(0);

            Assert.That(calls, Is.EqualTo(1));
        }

        /// <summary>
        ///     验证不存在的监听器和空监听器可安全移除。
        /// </summary>
        [Test]
        public void RemoveListener_WhenListenerDoesNotExist_ShouldBeNoOp()
        {
            EventManager.RemoveListener<int>(null);
            EventManager.RemoveListener<int>(_ => { });

            Assert.DoesNotThrow(() => EventManager.FireEvent(0));
        }

        /// <summary>
        ///     验证没有监听器的事件可安全发布。
        /// </summary>
        [Test]
        public void FireEvent_WhenNoListenerExists_ShouldBeNoOp()
        {
            Assert.DoesNotThrow(() => EventManager.FireEvent("missing"));
        }

        /// <summary>
        ///     验证一个监听器抛异常不会阻止后续监听器。
        /// </summary>
        [Test]
        public void FireEvent_WhenListenerThrows_ShouldLogAndContinueOtherListeners()
        {
            int calls = 0;
            EventManager.AddListener<int>(_ => throw new InvalidOperationException("event listener failure"));
            EventManager.AddListener<int>(_ => calls++);
            LogAssert.Expect(LogType.Exception, new Regex("event listener failure"));

            EventManager.FireEvent(0);

            Assert.That(calls, Is.EqualTo(1));
        }

        /// <summary>
        ///     验证触发过程使用快照，回调内修改订阅只影响下一次触发。
        /// </summary>
        [Test]
        public void FireEvent_WhenListenerMutatesSubscriptions_ShouldApplyMutationNextDispatch()
        {
            List<string> calls = new();
            bool changed = false;
            Action<int> second = _ => calls.Add("second");
            Action<int> third = _ => calls.Add("third");

            EventManager.AddListener((Action<int>)First);
            EventManager.AddListener(second);

            EventManager.FireEvent(0);
            EventManager.FireEvent(0);

            CollectionAssert.AreEqual(new[] { "first", "second", "first", "third" }, calls);
            return;

            void First(int _)
            {
                calls.Add("first");
                if (changed) return;
                changed = true;
                EventManager.RemoveListener(second);
                EventManager.AddListener(third);
            }
        }

        /// <summary>
        ///     验证不同事件类型的监听器相互隔离。
        /// </summary>
        [Test]
        public void FireEvent_WhenEventTypesDiffer_ShouldOnlyInvokeMatchingType()
        {
            int ints = 0;
            int strings = 0;
            EventManager.AddListener<int>(_ => ints++);
            EventManager.AddListener<string>(_ => strings++);

            EventManager.FireEvent(1);

            Assert.That(ints, Is.EqualTo(1));
            Assert.That(strings, Is.Zero);
        }

        /// <summary>
        ///     验证子系统注册重置会删除残留静态监听器。
        /// </summary>
        [Test]
        public void ResetStaticState_WhenDomainIsReused_ShouldClearListeners()
        {
            int calls = 0;
            EventManager.AddListener<int>(_ => calls++);

            EventManager.ResetStaticStateForTests();
            EventManager.FireEvent(0);

            Assert.That(calls, Is.Zero);
        }
    }
}
