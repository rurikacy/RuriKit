using UnityEngine;

namespace RuriKit
{
    /// <summary>
    ///     为单个 <see cref="AudioSource" /> 提供不受 <see cref="AudioSource.volume" /> 上限限制的线性 DSP 增益。
    /// </summary>
    internal sealed class AudioGainFilter : MonoBehaviour
    {
        private volatile float _gain = 1f;

        /// <summary>
        ///     音频增益倍率（按线性倍率增益）。
        /// </summary>
        public float Gain
        {
            set => _gain = value;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float gain = _gain;
            if (Mathf.Approximately(gain, 1f)) return;

            for (int i = 0; i < data.Length; i++)
            {
                data[i] *= gain;
            }
        }
    }
}