using System.Collections.Generic;
using UnityEngine;

namespace RuriKit
{
    /// <summary>
    ///     将一个画布及其直接管理的 <see cref="UIView" /> 注册到 <see cref="UIManager" />。
    /// </summary>
    /// <remarks>
    ///     组件所在游戏对象应保持活动，具体 UI 的显隐由其子级中的 <see cref="UIView" /> 控制。
    ///     <see cref="UIView" /> 不能与本组件位于同一游戏对象，否则隐藏视图会同时注销画布，导致视图无法再次显示。
    ///     嵌套画布下的视图由距离它最近的 UICanvas 管理，不会被外层画布重复注册。
    ///     运行时新增、销毁或移动视图后，需要调用 <see cref="Refresh" /> 同步注册状态。
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public class UICanvas : MonoBehaviour
    {
        private UIView[] _views;

        protected virtual void OnEnable()
        {
            Refresh();
        }

        protected virtual void OnDisable()
        {
            if (UIManager.TryGetInstance(out UIManager manager))
            {
                manager.UnregisterCanvas(this);
            }
        }

        /// <summary>
        ///     重新收集当前画布直接管理的视图，并将最新结果同步到 <see cref="UIManager" />。
        /// </summary>
        /// <remarks>
        ///     运行时新增、销毁或移动 <see cref="UIView" /> 后调用此方法。禁用状态下只更新本地快照，不会创建或注册管理器。
        /// </remarks>
        public void Refresh()
        {
            if (UIManager.TryGetInstance(out UIManager currentManager))
            {
                currentManager.UnregisterCanvas(this);
            }

            _views = CollectViews();
            if (!isActiveAndEnabled) return;

            UIManager manager = UIManager.Instance;
            if (manager)
            {
                manager.RegisterCanvas(this);
            }
        }

        internal UIView[] GetViews()
        {
            return _views;
        }

        private UIView[] CollectViews()
        {
            UIView[] childViews = GetComponentsInChildren<UIView>(true);
            List<UIView> ownedViews = new(childViews.Length);

            for (int i = 0; i < childViews.Length; i++)
            {
                UIView view = childViews[i];
                if (view.GetComponentInParent<UICanvas>(true) == this)
                {
                    ownedViews.Add(view);
                }
            }

            return ownedViews.ToArray();
        }
    }
}
