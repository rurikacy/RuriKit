using System;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RuriKit
{
    /// <summary>
    ///     提供统一的日志输出，并自动添加调用文件名作为类名前缀。
    /// </summary>
    public static class RLog
    {
        /// <summary>
        ///     输出普通日志。
        /// </summary>
        /// <param name="message">要输出的日志内容。</param>
        /// <param name="context">用于定位 Unity 对象的上下文。</param>
        /// <param name="callerFilePath">由编译器填充的调用文件路径。</param>
        public static void Log(string message, Object context = null, [CallerFilePath] string callerFilePath = "")
        {
            Debug.Log(Format(message, callerFilePath), context);
        }

        /// <summary>
        ///     输出警告日志。
        /// </summary>
        /// <param name="message">要输出的警告内容。</param>
        /// <param name="context">用于定位 Unity 对象的上下文。</param>
        /// <param name="callerFilePath">由编译器填充的调用文件路径。</param>
        public static void LogWarning(string message, Object context = null, [CallerFilePath] string callerFilePath = "")
        {
            Debug.LogWarning(Format(message, callerFilePath), context);
        }

        /// <summary>
        ///     输出错误日志。
        /// </summary>
        /// <param name="message">要输出的错误内容。</param>
        /// <param name="context">用于定位 Unity 对象的上下文。</param>
        /// <param name="callerFilePath">由编译器填充的调用文件路径。</param>
        public static void LogError(string message, Object context = null, [CallerFilePath] string callerFilePath = "")
        {
            Debug.LogError(Format(message, callerFilePath), context);
        }

        /// <summary>
        ///     输出异常内容。
        /// </summary>
        /// <param name="exception">要输出的异常。</param>
        /// <param name="context">用于定位 Unity 对象的上下文。</param>
        /// <param name="callerFilePath">由编译器填充的调用文件路径。</param>
        public static void LogException(Exception exception, Object context = null, [CallerFilePath] string callerFilePath = "")
        {
            if (exception == null) return;
            Debug.LogError(Format(exception.ToString(), callerFilePath), context);
        }

        private static string Format(string message, string callerFilePath)
        {
            string className = Path.GetFileNameWithoutExtension(callerFilePath);
            return string.IsNullOrEmpty(className) ? message : $"[{className}] {message}";
        }
    }
}