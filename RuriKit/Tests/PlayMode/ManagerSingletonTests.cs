using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RuriKit.Tests.PlayMode
{
    /// <summary>
    ///     验证单例创建、重复实例处理、销毁与静态重置。
    /// </summary>
    public class ManagerSingletonTests
    {
        /// <summary>
        ///     清理测试单例，避免跨测试的持久对象和静态状态泄漏。
        /// </summary>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return DestroyProbeInstances();
            ManagerSingletonRuntime.ResetStaticStateForTests();
            SingletonProbeManager.ResetProbe();
        }

        /// <summary>
        ///     清理测试单例。
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return DestroyProbeInstances();
            ManagerSingletonRuntime.ResetStaticStateForTests();
        }

        /// <summary>
        ///     验证首次访问会创建持久实例且只初始化一次。
        /// </summary>
        [UnityTest]
        public IEnumerator Instance_WhenFirstAccessed_ShouldCreateAndInitializeOnce()
        {
            SingletonProbeManager manager = SingletonProbeManager.Instance;
            yield return null;

            Assert.That(manager, Is.Not.Null);
            Assert.That(SingletonProbeManager.HasInstance, Is.True);
            Assert.That(SingletonProbeManager.AwakeCount, Is.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<SingletonProbeManager>().Length, Is.EqualTo(1));
        }

        /// <summary>
        ///     验证场景已存在实例时访问不会额外创建对象。
        /// </summary>
        [UnityTest]
        public IEnumerator Instance_WhenSceneAlreadyContainsInstance_ShouldReuseIt()
        {
            SingletonProbeManager existing = new GameObject("existing").AddComponent<SingletonProbeManager>();
            yield return null;

            Assert.That(SingletonProbeManager.Instance, Is.SameAs(existing));
            Assert.That(Object.FindObjectsOfType<SingletonProbeManager>().Length, Is.EqualTo(1));
        }

        /// <summary>
        ///     验证重复实例会销毁后创建者，并保留先初始化的实例。
        /// </summary>
        [UnityTest]
        public IEnumerator Awake_WhenDuplicateInstanceExists_ShouldDestroyDuplicate()
        {
            SingletonProbeManager first = new GameObject("first").AddComponent<SingletonProbeManager>();
            SingletonProbeManager duplicate = new GameObject("duplicate").AddComponent<SingletonProbeManager>();
            yield return null;

            Assert.That(SingletonProbeManager.Instance, Is.SameAs(first));
            Assert.IsTrue(duplicate == null);
            Assert.That(Object.FindObjectsOfType<SingletonProbeManager>().Length, Is.EqualTo(1));
        }

        /// <summary>
        ///     验证销毁后静态实例失效，下次访问会创建新实例。
        /// </summary>
        [UnityTest]
        public IEnumerator Instance_WhenCurrentInstanceIsDestroyed_ShouldCreateReplacement()
        {
            SingletonProbeManager first = SingletonProbeManager.Instance;
            Object.Destroy(first.gameObject);
            yield return null;

            Assert.That(SingletonProbeManager.HasInstance, Is.False);
            SingletonProbeManager replacement = SingletonProbeManager.Instance;
            yield return null;

            Assert.That(replacement, Is.Not.SameAs(first));
            Assert.That(SingletonProbeManager.AwakeCount, Is.EqualTo(2));
        }

        /// <summary>
        ///     验证子系统注册重置在禁用 Domain Reload 时不会复用失效静态引用。
        /// </summary>
        [UnityTest]
        public IEnumerator ResetStaticState_WhenStaticReferenceSurvives_ShouldFindExistingSceneInstance()
        {
            SingletonProbeManager first = SingletonProbeManager.Instance;
            ManagerSingletonRuntime.ResetStaticStateForTests();

            Assert.That(SingletonProbeManager.HasInstance, Is.False);
            Assert.That(SingletonProbeManager.Instance, Is.SameAs(first));
            yield return null;
            Assert.That(Object.FindObjectsOfType<SingletonProbeManager>().Length, Is.EqualTo(1));
        }

        /// <summary>
        ///     验证退出标记存在时不会按需创建新的单例实例。
        /// </summary>
        [Test]
        public void Instance_WhenApplicationIsQuitting_ShouldNotCreateManager()
        {
            ManagerSingletonRuntime.MarkApplicationQuitting();
            LogAssert.Expect(LogType.Warning, "应用程序正在退出时，调用了 SingletonProbeManager 单例实例。");

            Assert.That(SingletonProbeManager.Instance, Is.Null);
            Assert.That(Object.FindObjectsOfType<SingletonProbeManager>().Length, Is.Zero);
        }

        /// <summary>
        ///     Unity Test Framework 无法切换 Enter Play Mode 设置；此处只验证运行时重置入口。
        /// </summary>
        private static IEnumerator DestroyProbeInstances()
        {
            SingletonProbeManager[] managers = Object.FindObjectsOfType<SingletonProbeManager>();
            for (int i = 0; i < managers.Length; i++)
            {
                Object.Destroy(managers[i].gameObject);
            }

            yield return null;
        }
    }
}
