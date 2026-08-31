using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RuriKit.Tests.PlayMode
{
    /// <summary>
    ///     验证 GameObject 与纯 C# 对象池的借还、复用、延迟归还和销毁行为。
    /// </summary>
    public class PoolManagerTests
    {
        private GameObject _prefab;
        private PoolManager _manager;

        /// <summary>
        ///     创建隔离对象池管理器和源对象。
        /// </summary>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (PoolManager.TryGetInstance(out PoolManager existing))
            {
                Object.Destroy(existing.gameObject);
                yield return null;
            }

            _manager = PoolManager.Instance;
            _prefab = new GameObject("PoolPrefab");
            _prefab.SetActive(false);
            yield return null;
        }

        /// <summary>
        ///     销毁管理器及源对象，清理所有借出实例。
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_manager) Object.Destroy(_manager.gameObject);
            if (_prefab) Object.Destroy(_prefab);
            yield return null;
        }

        /// <summary>
        ///     验证预热创建池，获取实例会激活，归还后会停用且可复用。
        /// </summary>
        [Test]
        public void GetAndRelease_WhenPrefabPoolExists_ShouldActivateDeactivateAndReuseInstance()
        {
            _manager.Preload(_prefab, 2);
            Assert.That(_manager.HasPool(_prefab), Is.True);

            GameObject first = _manager.Get(_prefab);
            Assert.That(first.activeSelf, Is.True);
            _manager.Release(first);
            Assert.That(first.activeSelf, Is.False);
            GameObject reused = _manager.Get(_prefab);

            Assert.That(reused, Is.SameAs(first));
        }

        /// <summary>
        ///     验证获取重载会应用位置、旋转、父级并激活实例。
        /// </summary>
        [Test]
        public void Get_WhenTransformArgumentsAreProvided_ShouldApplyThemBeforeActivation()
        {
            GameObject parent = new("Parent");
            Vector3 position = new(1f, 2f, 3f);
            Quaternion rotation = Quaternion.Euler(10f, 20f, 30f);

            GameObject instance = _manager.Get(_prefab, position, rotation, parent.transform);

            Assert.That(instance.transform.parent, Is.SameAs(parent.transform));
            Assert.That(instance.transform.position, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(instance.transform.rotation, rotation), Is.LessThan(0.001f));
            Object.DestroyImmediate(parent);
        }

        /// <summary>
        ///     验证空实例和重复归还均不会破坏池状态。
        /// </summary>
        [Test]
        public void Release_WhenInstanceIsNullOrAlreadyReturned_ShouldBeNoOp()
        {
            _manager.Release(null);
            GameObject instance = _manager.Get(_prefab);
            _manager.Release(instance);

            Assert.DoesNotThrow(() => _manager.Release(instance));
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.activeSelf, Is.False);
        }

        /// <summary>
        ///     固定当前 API 行为：归还非池对象会销毁该对象。
        /// </summary>
        [UnityTest]
        public IEnumerator Release_WhenObjectDoesNotBelongToPool_ShouldDestroyObject()
        {
            GameObject foreign = new("Foreign");
            LogAssert.Expect(LogType.Warning, "Release 失败：实例 'Foreign' 不属于任何对象池，直接销毁。");

            _manager.Release(foreign);
            yield return null;

            Assert.IsTrue(foreign == null);
        }

        /// <summary>
        ///     验证延迟归还期间同一预制体再次获取会借出另一个实例，延迟后原实例被归还。
        /// </summary>
        [UnityTest]
        public IEnumerator Release_WhenDelayed_ShouldKeepOriginalBorrowedUntilDelayElapses()
        {
            GameObject delayed = _manager.Get(_prefab);
            _manager.Release(delayed, 0.02f);
            GameObject second = _manager.Get(_prefab);

            Assert.That(second, Is.Not.SameAs(delayed));
            Assert.That(delayed.activeSelf, Is.True);
            yield return new WaitForSeconds(0.05f);

            Assert.That(delayed.activeSelf, Is.False);
        }

        /// <summary>
        ///     验证立即归还会取消已有延迟归还，后续不会再次操作该对象。
        /// </summary>
        [UnityTest]
        public IEnumerator Release_WhenImmediateReturnFollowsDelayedReturn_ShouldCancelDelayedCoroutine()
        {
            GameObject instance = _manager.Get(_prefab);
            _manager.Release(instance, 0.02f);
            _manager.Release(instance);
            yield return new WaitForSeconds(0.05f);

            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.activeSelf, Is.False);
        }

        /// <summary>
        ///     验证清理指定池会销毁池追踪的空闲和借出实例并移除池。
        /// </summary>
        [UnityTest]
        public IEnumerator ClearPool_WhenInstancesAreBorrowed_ShouldDestroyTrackedInstances()
        {
            GameObject borrowed = _manager.Get(_prefab);
            GameObject returned = _manager.Get(_prefab);
            _manager.Release(returned);

            _manager.ClearPool(_prefab);
            yield return null;

            Assert.That(_manager.HasPool(_prefab), Is.False);
            Assert.IsTrue(borrowed == null);
            Assert.IsTrue(returned == null);
        }

        /// <summary>
        ///     验证不同源对象的 GameObject 池互不复用。
        /// </summary>
        [Test]
        public void Get_WhenPrefabsDiffer_ShouldUseIndependentPools()
        {
            GameObject otherPrefab = new("OtherPrefab");
            otherPrefab.SetActive(false);
            GameObject first = _manager.Get(_prefab);
            GameObject second = _manager.Get(otherPrefab);

            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(_manager.HasPool(_prefab), Is.True);
            Assert.That(_manager.HasPool(otherPrefab), Is.True);
            Object.DestroyImmediate(otherPrefab);
        }

        /// <summary>
        ///     验证清理未借出对象后，下一次获取会创建新的实例。
        /// </summary>
        [Test]
        public void ClearAllUnused_WhenInstanceWasReturned_ShouldDiscardThatUnusedInstance()
        {
            GameObject returned = _manager.Get(_prefab);
            _manager.Release(returned);

            _manager.ClearAllUnused();
            GameObject replacement = _manager.Get(_prefab);

            Assert.That(replacement, Is.Not.SameAs(returned));
        }

        /// <summary>
        ///     验证已销毁的预制体不再创建对象池实例。
        /// </summary>
        [UnityTest]
        public IEnumerator Get_WhenPrefabWasDestroyed_ShouldReturnNull()
        {
            Object.Destroy(_prefab);
            yield return null;
            _prefab = null;
            LogAssert.Expect(LogType.Warning, "Get 失败：prefab 为 null。");

            Assert.That(_manager.Get(_prefab), Is.Null);
        }

        /// <summary>
        ///     验证纯 C# 对象会复用上次归还的引用，并保留其状态。
        /// </summary>
        [Test]
        public void CsPool_WhenObjectIsReleased_ShouldReuseSameReferenceAndPreserveState()
        {
            PoolProbe first = _manager.Get<PoolProbe>();
            first.Value = 9;
            _manager.Release(first);
            PoolProbe reused = _manager.Get<PoolProbe>();

            Assert.That(reused, Is.SameAs(first));
            Assert.That(reused.Value, Is.EqualTo(9));
        }

        /// <summary>
        ///     验证纯 C# 对象的重复归还和无池对象归还不会损坏池。
        /// </summary>
        [Test]
        public void CsPool_WhenObjectIsReturnedTwiceOrForeign_ShouldIgnoreInvalidReturn()
        {
            PoolProbe borrowed = _manager.Get<PoolProbe>();
            _manager.Release(borrowed);
            LogAssert.Expect(LogType.Warning, "Release<PoolProbe> 失败：对象不属于当前借出集合，忽略归还。");
            _manager.Release(borrowed);

            LogAssert.Expect(LogType.Warning, "Release<PoolProbe> 失败：对象不属于当前借出集合，忽略归还。");
            Assert.DoesNotThrow(() => _manager.Release(new PoolProbe()));
        }

        /// <summary>
        ///     验证管理器销毁会销毁仍借出的 GameObject 实例。
        /// </summary>
        [UnityTest]
        public IEnumerator OnDestroy_WhenGameObjectInstancesAreBorrowed_ShouldDestroyThem()
        {
            GameObject borrowed = _manager.Get(_prefab);
            Object.Destroy(_manager.gameObject);
            yield return null;

            Assert.IsTrue(borrowed == null);
            _manager = null;
        }
    }
}
