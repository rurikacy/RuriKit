using UnityEngine;

namespace RuriKit
{
    /// <summary>
    ///     使所在的游戏对象在加载新场景时不被自动销毁。
    /// </summary>
    public class DontDestroyOnLoad : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
