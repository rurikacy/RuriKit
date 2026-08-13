using System;
using System.Collections.Generic;

namespace RuriKit
{
    /// <summary>
    ///     注册并控制场景中多个画布下的顶层 UI 视图。
    /// </summary>
    /// <remarks>
    ///     管理器只负责视图注册、查询和显隐控制；画布排序、动画、数据绑定和业务逻辑由各个 UI 自行处理。
    ///     每个具体的 <see cref="UIView" /> 类型只能注册一个实例，画布和视图的生命周期仍由所在场景管理。
    /// </remarks>
    public class UIManager : ManagerSingleton<UIManager>
    {
        private readonly Dictionary<Type, UIView> _views = new();
        private readonly Dictionary<UICanvas, List<UIView>> _canvasViews = new();

        protected override void OnSingletonDestroy()
        {
            foreach (UIView view in _views.Values)
            {
                if (view)
                {
                    view.ResetManager(this);
                }
            }

            _views.Clear();
            _canvasViews.Clear();
        }

        /// <summary>
        ///     显示指定类型的已注册 UI 视图。
        /// </summary>
        /// <typeparam name="T">要显示的具体 <see cref="UIView" /> 类型。</typeparam>
        /// <returns>成功找到并显示的视图；没有注册对应类型时返回 <c>null</c>。</returns>
        public T ShowView<T>() where T : UIView
        {
            if (!TryGetView(out T view))
            {
                RLog.LogWarning($"ShowView 失败：未注册 {typeof(T).Name}。");
                return null;
            }

            ShowView(view);
            return view;
        }

        /// <summary>
        ///     显示指定类型的已注册 UI 视图，并隐藏同一 <see cref="UICanvas" /> 下的其他视图。
        /// </summary>
        /// <remarks>
        ///     其他画布中的视图不受影响。
        /// </remarks>
        /// <typeparam name="T">要显示的具体 <see cref="UIView" /> 类型。</typeparam>
        /// <returns>成功找到并独占显示的视图；没有注册对应类型或所属画布需要刷新时返回 <c>null</c>。</returns>
        public T ShowViewOnly<T>() where T : UIView
        {
            if (!TryGetView(out T view))
            {
                RLog.LogWarning($"ShowViewOnly 失败：未注册 {typeof(T).Name}。");
                return null;
            }

            return ShowViewOnly(view) as T;
        }

        /// <summary>
        ///     显示指定的已注册 UI 视图，并隐藏同一 <see cref="UICanvas" /> 下的其他视图。
        /// </summary>
        /// <remarks>
        ///     其他画布中的视图不受影响。
        /// </remarks>
        /// <param name="view">要显示的已注册视图。</param>
        /// <returns>成功独占显示的视图；视图未注册或所属画布需要刷新时返回 <c>null</c>。</returns>
        public UIView ShowViewOnly(UIView view)
        {
            if (!CanControlView(view))
            {
                RLog.LogWarning("ShowViewOnly 失败：传入视图未注册。", view);
                return null;
            }

            UICanvas canvas = view.GetComponentInParent<UICanvas>(true);
            if (!canvas || !_canvasViews.TryGetValue(canvas, out List<UIView> views) || !views.Contains(view))
            {
                RLog.LogWarning($"ShowViewOnly 失败：{view.GetType().Name} 所属画布未注册或需要刷新。", view);
                return null;
            }

            for (int i = 0; i < views.Count; i++)
            {
                UIView otherView = views[i];
                if (otherView != view)
                {
                    HideView(otherView);
                }
            }

            ShowView(view);
            return view;
        }

        /// <summary>
        ///     隐藏指定类型的已注册 UI 视图。
        /// </summary>
        /// <typeparam name="T">要隐藏的具体 <see cref="UIView" /> 类型。</typeparam>
        public void HideView<T>() where T : UIView
        {
            if (!TryGetView(out T view))
            {
                RLog.LogWarning($"HideView 失败：未注册 {typeof(T).Name}。");
                return;
            }

            HideView(view);
        }

        /// <summary>
        ///     尝试获取指定类型的已注册 UI 视图。
        /// </summary>
        /// <typeparam name="T">要获取的具体 <see cref="UIView" /> 类型。</typeparam>
        /// <param name="view">已注册的视图；不存在时为 <c>null</c>。</param>
        /// <returns>存在可用视图时返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public bool TryGetView<T>(out T view) where T : UIView
        {
            Type viewType = typeof(T);
            if (_views.TryGetValue(viewType, out UIView registeredView) && registeredView)
            {
                view = (T)registeredView;
                return true;
            }

            _views.Remove(viewType);
            view = null;
            return false;
        }

        /// <summary>
        ///     尝试获取指定类型的已注册 UI 画布。
        /// </summary>
        /// <typeparam name="T">要获取的具体 <see cref="UICanvas" /> 类型。</typeparam>
        /// <param name="canvas">唯一匹配的已注册画布；不存在或存在多个匹配项时为 <c>null</c>。</param>
        /// <returns>存在唯一可用画布时返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public bool TryGetCanvas<T>(out T canvas) where T : UICanvas
        {
            canvas = null;

            foreach (UICanvas registeredCanvas in _canvasViews.Keys)
            {
                if (!registeredCanvas || registeredCanvas is not T matchedCanvas) continue;

                if (canvas)
                {
                    RLog.LogWarning($"TryGetCanvas 失败：存在多个匹配 {typeof(T).Name} 的已注册画布。");
                    canvas = null;
                    return false;
                }

                canvas = matchedCanvas;
            }

            return canvas;
        }

        /// <summary>
        ///     隐藏当前管理器中所有已注册的 UI 视图。
        /// </summary>
        public void HideAllViews()
        {
            List<UIView> views = new(_views.Values);
            for (int i = 0; i < views.Count; i++)
            {
                HideView(views[i]);
            }
        }

        internal void RegisterCanvas(UICanvas canvas)
        {
            if (!canvas || _canvasViews.ContainsKey(canvas)) return;

            UIView[] views = canvas.GetViews();
            List<UIView> registeredViews = new(views.Length);
            _canvasViews.Add(canvas, registeredViews);

            for (int i = 0; i < views.Length; i++)
            {
                UIView view = views[i];
                if (!view) continue;

                if (view.gameObject == canvas.gameObject)
                {
                    RLog.LogError("注册失败：UIView 不能与 UICanvas 位于同一游戏对象。", view);
                    continue;
                }

                Type viewType = view.GetType();
                if (_views.TryGetValue(viewType, out UIView registeredView))
                {
                    if (registeredView)
                    {
                        RLog.LogError($"注册 {viewType.Name} 失败：同类型 UIView 已存在。", view);
                        continue;
                    }

                    _views.Remove(viewType);
                }

                _views.Add(viewType, view);
                registeredViews.Add(view);
                view.Initialize(this);
            }
        }

        internal void UnregisterCanvas(UICanvas canvas)
        {
            if (!canvas || !_canvasViews.Remove(canvas, out List<UIView> views)) return;

            for (int i = 0; i < views.Count; i++)
            {
                UIView view = views[i];
                if (ReferenceEquals(view, null)) continue;

                Type viewType = view.GetType();
                if (_views.TryGetValue(viewType, out UIView registeredView) && registeredView == view)
                {
                    _views.Remove(viewType);
                }

                if (view)
                {
                    view.ResetManager(this);
                }
            }
        }

        internal void ShowView(UIView view)
        {
            if (!CanControlView(view)) return;
            view.SetVisible(true);
        }

        internal void HideView(UIView view)
        {
            if (!CanControlView(view)) return;
            view.SetVisible(false);
        }

        private bool CanControlView(UIView view)
        {
            if (!view || view._manager != this) return false;

            Type viewType = view.GetType();
            return _views.TryGetValue(viewType, out UIView registeredView) && registeredView == view;
        }
    }
}