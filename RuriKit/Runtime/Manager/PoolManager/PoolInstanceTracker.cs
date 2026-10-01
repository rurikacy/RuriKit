using UnityEngine;

namespace RuriKit
{
    /// <summary>
    ///     在池实例销毁时解除管理器持有的记录，不要求业务代码必须通过对象池销毁实例。
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class PoolInstanceTracker : MonoBehaviour
    {
        private PoolManager _owner;
        private int _instanceId;

        /// <summary>
        ///     记录实例所属对象池管理器和实例编号。
        /// </summary>
        /// <param name="owner">所属对象池管理器。</param>
        /// <param name="instanceId">当前实例编号。</param>
        internal void Initialize(PoolManager owner, int instanceId)
        {
            _owner = owner;
            _instanceId = instanceId;
        }

        /// <summary>
        ///     销毁时通知仍存活的管理器，避免重新创建单例。
        /// </summary>
        private void OnDestroy()
        {
            if (_owner) _owner.NotifyInstanceDestroyed(_instanceId);
            _owner = null;
        }
    }
}
