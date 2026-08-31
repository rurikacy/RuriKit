using UnityEngine;

namespace RuriKit
{
    /// <summary>
    ///     表示一个可由 <see cref="UIManager" /> 查询和控制显隐的顶层 UI 视图。
    /// </summary>
    /// <remarks>
    ///     视图只负责查询状态和发起显隐请求，实际控制由注册此视图的 <see cref="UIManager" /> 执行。
    ///     视图必须位于 <see cref="UICanvas" /> 的子级，不能与画布组件位于同一游戏对象。
    ///     所在的 <see cref="UICanvas" /> 注销后，视图会失效；对失效视图调用控制方法不会执行任何操作。
    /// </remarks>
    [DisallowMultipleComponent]
    public abstract class UIView : MonoBehaviour
    {
        internal UIManager _manager;

        /// <summary>
        ///     获取当前视图是否已经注册到可用的 <see cref="UIManager" />。
        /// </summary>
        public bool IsRegistered => _manager;

        /// <summary>
        ///     获取当前视图所在游戏对象自身是否处于活动状态，不考虑父级状态。
        /// </summary>
        public bool IsActiveSelf => gameObject.activeSelf;

        /// <summary>
        ///     获取当前视图是否在活动层级中可见。
        /// </summary>
        public bool IsVisible => gameObject.activeInHierarchy;

        /// <summary>
        ///     请求激活当前视图自身；父级未激活时视图仍不会实际可见，视图未注册时不执行任何操作。
        /// </summary>
        public void Show()
        {
            _manager?.ShowView(this);
        }

        /// <summary>
        ///     请求停用当前视图自身；视图未注册时不执行任何操作。
        /// </summary>
        public void Hide()
        {
            _manager?.HideView(this);
        }

        internal void Initialize(UIManager manager)
        {
            _manager = manager;
        }

        internal void ResetManager(UIManager manager)
        {
            if (_manager == manager)
            {
                _manager = null;
            }
        }

        internal void SetVisible(bool visible)
        {
            if (gameObject.activeSelf == visible) return;
            gameObject.SetActive(visible);
        }
    }
}
