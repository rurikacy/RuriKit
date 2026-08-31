namespace RuriKit.Tests.PlayMode
{
    /// <summary>
    ///     用于验证泛型单例生命周期的独立测试管理器。
    /// </summary>
    public class SingletonProbeManager : ManagerSingleton<SingletonProbeManager>
    {
        /// <summary>
        ///     初始化次数。
        /// </summary>
        public static int AwakeCount { get; private set; }

        /// <summary>
        ///     销毁次数。
        /// </summary>
        private static int DestroyCount { get; set; }

        /// <summary>
        ///     清理测试可观察状态。
        /// </summary>
        public static void ResetProbe()
        {
            AwakeCount = 0;
            DestroyCount = 0;
        }

        /// <summary>
        ///     记录单例首次初始化。
        /// </summary>
        protected override void OnSingletonAwake()
        {
            AwakeCount++;
        }

        /// <summary>
        ///     记录单例销毁。
        /// </summary>
        protected override void OnSingletonDestroy()
        {
            DestroyCount++;
        }
    }

    /// <summary>
    ///     用于验证 UI 注册、刷新与显隐的第一种视图类型。
    /// </summary>
    public class ProbeViewA : UIView
    {
    }

    /// <summary>
    ///     用于验证 UI 注册、刷新与显隐的第二种视图类型。
    /// </summary>
    public class ProbeViewB : UIView
    {
    }

    /// <summary>
    ///     用于验证跨画布独占显示隔离的第三种视图类型。
    /// </summary>
    public class ProbeViewC : UIView
    {
    }

    /// <summary>
    ///     用于验证同类型画布查找歧义的测试画布类型。
    /// </summary>
    public class ProbeCanvas : UICanvas
    {
    }

    /// <summary>
    ///     用于验证纯 C# 对象池重用的简单引用类型。
    /// </summary>
    public class PoolProbe
    {
        /// <summary>
        ///     可被测试写入的状态。
        /// </summary>
        public int Value;
    }
}
