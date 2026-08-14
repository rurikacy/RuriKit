using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuriKit
{
    /// <summary>
    ///     按事件类型注册、移除和触发全局事件监听器。
    /// </summary>
    public static class EventManager
    {
        private static readonly Dictionary<Type, Delegate> _listeners = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _listeners.Clear();
        }

        /// <summary>
        ///     触发指定事件类型下注册的所有监听器；单个监听器抛出的异常会被记录，且不影响其他监听器。
        /// </summary>
        /// <typeparam name="T">事件类型。</typeparam>
        /// <param name="eventData">要传递给监听器的事件数据。</param>
        public static void FireEvent<T>(T eventData)
        {
            if (_listeners.TryGetValue(typeof(T), out Delegate listeners))
            {
                foreach (Delegate listener in listeners.GetInvocationList())
                {
                    try
                    {
                        ((Action<T>)listener)(eventData);
                    }
                    catch (Exception exception)
                    {
                        RLog.LogException(exception);
                    }
                }
            }
        }

        /// <summary>
        ///     为指定事件类型添加一个监听器。
        /// </summary>
        /// <typeparam name="T">事件类型。</typeparam>
        /// <param name="action">事件触发时执行的回调。为 <c>null</c> 时不执行任何操作。</param>
        public static void AddListener<T>(Action<T> action)
        {
            if (action == null) return;

            Type type = typeof(T);
            if (_listeners.TryGetValue(type, out Delegate existing))
            {
                _listeners[type] = Delegate.Combine(existing, action);
            }
            else
            {
                _listeners[type] = action;
            }
        }

        /// <summary>
        ///     从指定事件类型中移除一个监听器。
        /// </summary>
        /// <typeparam name="T">事件类型。</typeparam>
        /// <param name="action">要移除的回调。为 <c>null</c> 时不执行任何操作。</param>
        public static void RemoveListener<T>(Action<T> action)
        {
            if (action == null) return;

            Type type = typeof(T);
            if (!_listeners.TryGetValue(type, out Delegate existing)) return;

            Delegate result = Delegate.Remove(existing, action);
            if (result == null)
            {
                _listeners.Remove(type);
            }
            else
            {
                _listeners[type] = result;
            }
        }
    }
}
