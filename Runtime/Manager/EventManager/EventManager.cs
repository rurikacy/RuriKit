using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuriKit
{
    /// <summary>
    ///     按事件标识注册、移除和触发无参数或强类型参数的全局事件监听器。
    /// </summary>
    public static class EventManager
    {
        private static readonly Dictionary<GameEvents, Action> _listeners = new();
        private static readonly Dictionary<GameEvents, Dictionary<Type, Delegate>> _genericListeners = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _listeners.Clear();
            _genericListeners.Clear();
        }

        /// <summary>
        ///     触发指定事件标识下注册的所有无参数监听器。
        /// </summary>
        /// <param name="gameEvent">要触发的事件标识。</param>
        public static void FireEvent(GameEvents gameEvent)
        {
            if (_listeners.TryGetValue(gameEvent, out Action action))
            {
                action();
            }
        }

        /// <summary>
        ///     触发指定事件标识和参数类型下注册的所有监听器。
        /// </summary>
        /// <typeparam name="T">事件参数的类型。</typeparam>
        /// <param name="gameEvent">要触发的事件标识。</param>
        /// <param name="arg">传递给监听器的事件参数。</param>
        public static void FireEvent<T>(GameEvents gameEvent, T arg)
        {
            if (!_genericListeners.TryGetValue(gameEvent, out Dictionary<Type, Delegate> typeDict)) return;
            if (typeDict.TryGetValue(typeof(T), out Delegate del))
            {
                ((Action<T>)del)(arg);
            }
        }

        /// <summary>
        ///     为指定事件标识添加一个无参数监听器。
        /// </summary>
        /// <param name="gameEvent">要监听的事件标识。</param>
        /// <param name="action">事件触发时执行的回调。为 <c>null</c> 时不执行任何操作。</param>
        public static void AddListener(GameEvents gameEvent, Action action)
        {
            if (action == null) return;

            if (_listeners.TryGetValue(gameEvent, out Action existing))
            {
                _listeners[gameEvent] = (Action)Delegate.Combine(existing, action);
            }
            else
            {
                _listeners[gameEvent] = action;
            }
        }

        /// <summary>
        ///     为指定事件标识和参数类型添加一个监听器。
        /// </summary>
        /// <typeparam name="T">事件参数的类型。</typeparam>
        /// <param name="gameEvent">要监听的事件标识。</param>
        /// <param name="action">事件触发时执行的回调。为 <c>null</c> 时不执行任何操作。</param>
        public static void AddListener<T>(GameEvents gameEvent, Action<T> action)
        {
            if (action == null) return;

            if (!_genericListeners.TryGetValue(gameEvent, out Dictionary<Type, Delegate> typeDict))
            {
                typeDict = new Dictionary<Type, Delegate>();
                _genericListeners[gameEvent] = typeDict;
            }

            Type type = typeof(T);
            if (typeDict.TryGetValue(type, out Delegate existing))
            {
                typeDict[type] = Delegate.Combine(existing, action);
            }
            else
            {
                typeDict[type] = action;
            }
        }

        /// <summary>
        ///     从指定事件标识中移除一个无参数监听器。
        /// </summary>
        /// <param name="gameEvent">要停止监听的事件标识。</param>
        /// <param name="action">要移除的回调。为 <c>null</c> 时不执行任何操作。</param>
        public static void RemoveListener(GameEvents gameEvent, Action action)
        {
            if (action == null) return;
            if (!_listeners.TryGetValue(gameEvent, out Action existing)) return;

            Action result = (Action)Delegate.Remove(existing, action);
            if (result == null)
            {
                _listeners.Remove(gameEvent);
            }
            else
            {
                _listeners[gameEvent] = result;
            }
        }

        /// <summary>
        ///     从指定事件标识和参数类型中移除一个监听器。
        /// </summary>
        /// <typeparam name="T">事件参数的类型。</typeparam>
        /// <param name="gameEvent">要停止监听的事件标识。</param>
        /// <param name="action">要移除的回调。为 <c>null</c> 时不执行任何操作。</param>
        public static void RemoveListener<T>(GameEvents gameEvent, Action<T> action)
        {
            if (action == null) return;
            if (!_genericListeners.TryGetValue(gameEvent, out Dictionary<Type, Delegate> typeDict)) return;

            Type type = typeof(T);
            if (!typeDict.TryGetValue(type, out Delegate existing)) return;

            Delegate result = Delegate.Remove(existing, action);
            if (result == null)
            {
                typeDict.Remove(type);
                if (typeDict.Count == 0)
                {
                    _genericListeners.Remove(gameEvent);
                }
            }
            else
            {
                typeDict[type] = result;
            }
        }
    }
}