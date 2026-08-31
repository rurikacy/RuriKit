using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RuriKit.Tests.PlayMode
{
    /// <summary>
    ///     验证画布扫描、视图注册、刷新、显隐与注销生命周期。
    /// </summary>
    public class UIManagerTests
    {
        private GameObject _canvasObject;
        private UIManager _manager;

        /// <summary>
        ///     清理前序 UI 管理器并建立空白测试环境。
        /// </summary>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (UIManager.TryGetInstance(out UIManager existing))
            {
                Object.Destroy(existing.gameObject);
                yield return null;
            }

            _manager = UIManager.Instance;
            yield return null;
        }

        /// <summary>
        ///     清理场景对象和管理器。
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_canvasObject) Object.Destroy(_canvasObject);
            if (_manager) Object.Destroy(_manager.gameObject);
            yield return null;
        }

        /// <summary>
        ///     验证画布启用会扫描、注册子视图，并可查询与显示。
        /// </summary>
        [UnityTest]
        public IEnumerator RegisterCanvas_WhenViewsExist_ShouldRegisterAndShowRequestedView()
        {
            ProbeViewA view = CreateCanvasWithViews(out _);
            yield return null;

            Assert.That(view.IsRegistered, Is.True);
            Assert.That(_manager.TryGetView(out ProbeViewA found), Is.True);
            Assert.That(found, Is.SameAs(view));
            _manager.ShowView<ProbeViewA>();

            Assert.That(view.IsActiveSelf, Is.True);
            Assert.That(view.IsVisible, Is.True);
        }

        /// <summary>
        ///     验证独占显示只隐藏同画布视图，不影响其他画布。
        /// </summary>
        [UnityTest]
        public IEnumerator ShowViewOnly_WhenViewsShareCanvas_ShouldHideOnlySiblingViews()
        {
            ProbeViewA first = CreateCanvasWithViews(out ProbeViewB second);
            GameObject otherCanvasObject = new("OtherCanvas");
            otherCanvasObject.AddComponent<Canvas>();
            ProbeCanvas otherCanvas = otherCanvasObject.AddComponent<ProbeCanvas>();
            GameObject otherViewObject = new("OtherView");
            otherViewObject.transform.SetParent(otherCanvasObject.transform);
            ProbeViewC other = otherViewObject.AddComponent<ProbeViewC>();
            otherCanvas.Refresh();
            yield return null;

            first.Show();
            second.Show();
            _manager.ShowViewOnly(first);

            Assert.That(first.IsActiveSelf, Is.True);
            Assert.That(second.IsActiveSelf, Is.False);
            Assert.That(other.IsActiveSelf, Is.True);
            Object.Destroy(otherCanvasObject);
        }

        /// <summary>
        ///     验证相同视图类型仅注册第一个实例，后续实例会保留未注册状态。
        /// </summary>
        [UnityTest]
        public IEnumerator RegisterCanvas_WhenSameViewTypeAppearsTwice_ShouldKeepFirstRegistrationOnly()
        {
            ProbeViewA first = CreateCanvasWithViews(out _);
            GameObject duplicateObject = new("DuplicateView");
            duplicateObject.transform.SetParent(_canvasObject.transform);
            ProbeViewA duplicate = duplicateObject.AddComponent<ProbeViewA>();
            LogAssert.Expect(LogType.Error, "注册 ProbeViewA 失败：同类型 UIView 已存在。");
            _canvasObject.GetComponent<ProbeCanvas>().Refresh();
            yield return null;

            Assert.That(_manager.TryGetView(out ProbeViewA registered), Is.True);
            Assert.That(registered, Is.SameAs(first));
            Assert.That(duplicate.IsRegistered, Is.False);
        }

        /// <summary>
        ///     验证刷新后可注册运行时新增视图，删除后移除旧注册。
        /// </summary>
        [UnityTest]
        public IEnumerator Refresh_WhenViewsAreAddedOrDestroyed_ShouldSynchronizeRegistration()
        {
            CreateCanvasWithViews(out _, false);
            ProbeCanvas canvas = _canvasObject.GetComponent<ProbeCanvas>();
            GameObject dynamic = new("DynamicView");
            dynamic.transform.SetParent(_canvasObject.transform);
            ProbeViewB dynamicView = dynamic.AddComponent<ProbeViewB>();
            canvas.Refresh();
            yield return null;

            Assert.That(dynamicView.IsRegistered, Is.True);
            Assert.That(_manager.TryGetView(out ProbeViewB registered), Is.True);
            Assert.That(registered, Is.SameAs(dynamicView));
            Object.Destroy(dynamic);
            yield return null;
            canvas.Refresh();

            Assert.That(_manager.TryGetView(out ProbeViewB _), Is.False);
        }

        /// <summary>
        ///     验证隐藏全部和不可用类型查询的公开行为。
        /// </summary>
        [UnityTest]
        public IEnumerator HideAllViews_WhenRegisteredViewsExist_ShouldHideAllAndReturnNullForMissingType()
        {
            ProbeViewA first = CreateCanvasWithViews(out ProbeViewB second);
            yield return null;
            first.Show();
            second.Show();

            _manager.HideAllViews();

            Assert.That(first.IsActiveSelf, Is.False);
            Assert.That(second.IsActiveSelf, Is.False);
            LogAssert.Expect(LogType.Warning, "ShowView 失败：未注册 ProbeMissingView。");
            Assert.That(_manager.ShowView<ProbeMissingView>(), Is.Null);
        }

        /// <summary>
        ///     验证禁用画布会注销视图，注销后视图显隐请求为安全空操作。
        /// </summary>
        [UnityTest]
        public IEnumerator OnDisable_WhenCanvasIsDisabled_ShouldUnregisterViews()
        {
            ProbeViewA view = CreateCanvasWithViews(out _);
            yield return null;
            _canvasObject.SetActive(false);
            yield return null;

            Assert.That(view.IsRegistered, Is.False);
            Assert.That(_manager.TryGetView(out ProbeViewA _), Is.False);
            Assert.DoesNotThrow(view.Show);
        }

        /// <summary>
        ///     验证同类型画布存在多个注册实例时查询会明确失败。
        /// </summary>
        [UnityTest]
        public IEnumerator TryGetCanvas_WhenMultipleCanvasesMatch_ShouldReturnFalse()
        {
            CreateCanvasWithViews(out _);
            GameObject secondCanvasObject = new("SecondCanvas");
            secondCanvasObject.AddComponent<Canvas>();
            secondCanvasObject.AddComponent<ProbeCanvas>();
            yield return null;

            LogAssert.Expect(LogType.Warning, "TryGetCanvas 失败：存在多个匹配 ProbeCanvas 的已注册画布。");
            Assert.That(_manager.TryGetCanvas(out ProbeCanvas _), Is.False);
            Object.Destroy(secondCanvasObject);
        }

        /// <summary>
        ///     验证管理器销毁会解除视图的管理器引用。
        /// </summary>
        [UnityTest]
        public IEnumerator OnDestroy_WhenManagerIsDestroyed_ShouldUnregisterViews()
        {
            ProbeViewA view = CreateCanvasWithViews(out _);
            yield return null;
            Object.Destroy(_manager.gameObject);
            yield return null;

            Assert.That(view.IsRegistered, Is.False);
            _manager = null;
        }

        /// <summary>
        ///     创建一个带两个不同视图类型的已启用测试画布。
        /// </summary>
        private ProbeViewA CreateCanvasWithViews(out ProbeViewB second, bool includeSecond = true)
        {
            _canvasObject = new GameObject("Canvas");
            _canvasObject.AddComponent<Canvas>();
            _canvasObject.AddComponent<ProbeCanvas>();

            GameObject firstObject = new("FirstView");
            firstObject.transform.SetParent(_canvasObject.transform);
            ProbeViewA first = firstObject.AddComponent<ProbeViewA>();
            if (includeSecond)
            {
                GameObject secondObject = new("SecondView");
                secondObject.transform.SetParent(_canvasObject.transform);
                second = secondObject.AddComponent<ProbeViewB>();
            }
            else
            {
                second = null;
            }
            _canvasObject.GetComponent<ProbeCanvas>().Refresh();
            return first;
        }

        /// <summary>
        ///     未注册视图类型，用于验证查找失败。
        /// </summary>
        private class ProbeMissingView : UIView
        {
        }
    }
}
