using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace RuriKit
{
    /// <summary>
    ///     基于 Unity <see cref="AudioSource" /> 管理背景音乐、音效播放、淡入淡出和音频源复用。
    /// </summary>
    /// <remarks>
    ///     音频源、活动列表、淡入淡出协程和回收流程都由管理器集中执行。
    ///     <see cref="AudioHandle" /> 只作为播放句柄发起控制请求，播放结束或停止后不会影响后续复用的音频源。
    /// </remarks>
    public class AudioManager : ManagerSingleton<AudioManager>
    {
        private const float MIN_DB = -80f;
        private const float MIN_LINEAR = 0.0001f;
        private const int POOL_CAPACITY = 16;

        private const string BGM_GROUP_NAME = "Bgm";
        private const string BGM_VOLUME_PARAM = "BgmVolume";
        private const string MASTER_GROUP_NAME = "Master";
        private const string MASTER_VOLUME_PARAM = "MasterVolume";
        private const string SFX_GROUP_NAME = "Sfx";
        private const string SFX_VOLUME_PARAM = "SfxVolume";

        [SerializeField] private AudioMixer _mixer;

        private readonly List<AudioHandle> _activeHandles = new(32);
        private readonly List<AudioHandle> _pendingHandles = new(8);
        private readonly Stack<AudioSource> _sourcePool = new(POOL_CAPACITY);
        private AudioMixerGroup _bgmGroup;
        private float _bgmVolume = 1f;
        private int _handleIterationDepth;
        private AudioMixerGroup _masterGroup;
        private float _masterVolume = 1f;
        private bool _muted;
        private AudioMixerGroup _sfxGroup;
        private float _sfxVolume = 1f;
        private GameObject _sourceRoot;

        /// <summary>
        ///     获取或设置所有音频的主音量，取值范围为 0 到 1；内部会转换为分贝后应用到 AudioMixer。
        /// </summary>
        public float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = Mathf.Clamp01(value);
                if (!_muted)
                {
                    ApplyVolumeSetting(MASTER_VOLUME_PARAM, _masterVolume);
                }
            }
        }

        /// <summary>
        ///     获取或设置是否静音。静音时所有音频输出静默，取消静音时恢复之前的主音量。
        /// </summary>
        public bool Muted
        {
            get => _muted;
            set
            {
                if (_muted == value) return;
                _muted = value;

                if (_mixer)
                {
                    if (_muted)
                    {
                        _mixer.SetFloat(MASTER_VOLUME_PARAM, MIN_DB);
                    }
                    else
                    {
                        ApplyMixerVolume(MASTER_VOLUME_PARAM, _masterVolume);
                    }
                }
                else
                {
                    RefreshAllSourceVolumes();
                }
            }
        }

        /// <summary>
        ///     获取或设置背景音乐音量，取值范围为 0 到 1；内部会转换为分贝后应用到 AudioMixer。
        /// </summary>
        public float BgmVolume
        {
            get => _bgmVolume;
            set
            {
                _bgmVolume = Mathf.Clamp01(value);
                ApplyVolumeSetting(BGM_VOLUME_PARAM, _bgmVolume);
            }
        }

        /// <summary>
        ///     获取或设置音效音量，取值范围为 0 到 1；内部会转换为分贝后应用到 AudioMixer。通过 <see cref="Play" /> 和 <see cref="Play3D(AudioClip, Vector3, bool, float)" /> 播放的音频使用此音量。
        /// </summary>
        public float SfxVolume
        {
            get => _sfxVolume;
            set
            {
                _sfxVolume = Mathf.Clamp01(value);
                ApplyVolumeSetting(SFX_VOLUME_PARAM, _sfxVolume);
            }
        }

        /// <summary>
        ///     获取当前背景音乐的播放句柄；没有背景音乐时为 <c>null</c>。
        /// </summary>
        public AudioHandle CurrentBgm { get; private set; }

        protected override void OnSingletonAwake()
        {
            Initialize();
        }

        protected override void OnSingletonDestroy()
        {
            StopAllImmediate();
        }

        private void Update()
        {
            BeginHandleIteration();
            try
            {
                for (int i = _activeHandles.Count - 1; i >= 0; i--)
                {
                    AudioHandle handle = _activeHandles[i];
                    if (!CanControl(handle)) continue;

                    bool isPausedByListener = AudioListener.pause && !handle._source.ignoreListenerPause;
                    if (!handle.Loop && !handle.IsPaused && !isPausedByListener &&
                        !handle._source.isPlaying && handle._source.time > 0f)
                    {
                        CompleteHandle(handle);
                    }
                }
            }
            finally
            {
                EndHandleIteration();
            }
        }

        /// <summary>
        ///     以二维音效形式播放指定音频片段。
        /// </summary>
        /// <param name="clip">要播放的音频片段。如果为 <c>null</c>，则不播放并返回 <c>null</c>。</param>
        /// <param name="loop">是否循环播放。</param>
        /// <param name="volume">本次播放的音量倍率，只作用于当前 AudioSource。全局主音量和音效音量由 AudioMixer 控制；负数和 <see cref="float.NaN" /> 按 0 处理。</param>
        /// <returns>用于查询和请求控制本次播放的句柄；播放失败时返回 <c>null</c>。</returns>
        public AudioHandle Play(AudioClip clip, bool loop = false, float volume = 1f)
        {
            return PlayInternal(clip, loop, volume, false);
        }

        /// <summary>
        ///     在指定世界坐标以三维音效形式播放音频片段。
        /// </summary>
        /// <param name="clip">要播放的音频片段。如果为 <c>null</c>，则不播放并返回 <c>null</c>。</param>
        /// <param name="position">音频源的世界坐标。</param>
        /// <param name="loop">是否循环播放。</param>
        /// <param name="volume">本次播放的音量倍率，只作用于当前 AudioSource。全局主音量和音效音量由 AudioMixer 控制；负数和 <see cref="float.NaN" /> 按 0 处理。</param>
        /// <returns>用于查询和请求控制本次播放的句柄；播放失败时返回 <c>null</c>。</returns>
        public AudioHandle Play3D(AudioClip clip, Vector3 position, bool loop = false, float volume = 1f)
        {
            return PlayInternal(clip, loop, volume, false, position);
        }

        /// <summary>
        ///     以三维音效形式播放音频片段，并使音频源跟随指定目标。
        /// </summary>
        /// <param name="clip">要播放的音频片段。如果为 <c>null</c>，则不播放并返回 <c>null</c>。</param>
        /// <param name="target">音频源要跟随的目标。为 <c>null</c> 时回退到二维播放。</param>
        /// <param name="loop">是否循环播放。</param>
        /// <param name="volume">本次播放的音量倍率，只作用于当前 AudioSource。全局主音量和音效音量由 AudioMixer 控制；负数和 <see cref="float.NaN" /> 按 0 处理。</param>
        /// <returns>用于查询和请求控制本次播放的句柄；播放失败时返回 <c>null</c>。</returns>
        public AudioHandle Play3D(AudioClip clip, Transform target, bool loop = false, float volume = 1f)
        {
            if (!target)
            {
                RLog.LogWarning("Play3D target 为 null，回退到 2D 播放。");
                return Play(clip, loop, volume);
            }

            return PlayInternal(clip, loop, volume, false, target: target);
        }

        /// <summary>
        ///     播放背景音乐，并停止或淡出当前背景音乐。
        /// </summary>
        /// <param name="clip">要播放的音频片段。如果为 <c>null</c>，则保留当前背景音乐并返回 <c>null</c>。</param>
        /// <param name="loop">是否循环播放。</param>
        /// <param name="fadeInDuration">新背景音乐的淡入时长，单位为秒；同时用作旧背景音乐的淡出时长。负数按 0 处理。</param>
        /// <param name="volume">本次播放的音量倍率，只作用于当前 AudioSource。全局主音量和背景音乐音量由 AudioMixer 控制；负数和 <see cref="float.NaN" /> 按 0 处理。</param>
        /// <returns>用于查询和请求控制新背景音乐的句柄；播放失败时返回 <c>null</c>。</returns>
        public AudioHandle PlayBgm(AudioClip clip, bool loop = true, float fadeInDuration = 0f, float volume = 1f)
        {
            if (!clip)
            {
                RLog.LogWarning("AudioClip 为空。");
                return null;
            }

            fadeInDuration = Mathf.Max(0f, fadeInDuration);
            if (CanControl(CurrentBgm))
            {
                AudioHandle oldBgm = CurrentBgm;
                CurrentBgm = null;

                if (fadeInDuration > 0f)
                {
                    StopHandle(oldBgm, fadeInDuration);
                }
                else
                {
                    StopHandle(oldBgm);
                }
            }

            volume = NormalizeVolumeGain(volume);
            float startVolume = fadeInDuration > 0f ? 0f : volume;
            AudioHandle handle = PlayInternal(clip, loop, startVolume, true);
            CurrentBgm = handle;

            if (fadeInDuration > 0f && handle != null)
            {
                FadeHandleTo(handle, volume, fadeInDuration);
            }

            return handle;
        }

        /// <summary>
        ///     暂停所有活动的音频播放。
        /// </summary>
        public void PauseAll()
        {
            BeginHandleIteration();
            try
            {
                for (int i = _activeHandles.Count - 1; i >= 0; i--)
                {
                    PauseHandle(_activeHandles[i]);
                }

                for (int i = _pendingHandles.Count - 1; i >= 0; i--)
                {
                    PauseHandle(_pendingHandles[i]);
                }
            }
            finally
            {
                EndHandleIteration();
            }
        }

        /// <summary>
        ///     恢复所有已暂停的活动音频播放。
        /// </summary>
        public void ResumeAll()
        {
            BeginHandleIteration();
            try
            {
                for (int i = _activeHandles.Count - 1; i >= 0; i--)
                {
                    ResumeHandle(_activeHandles[i]);
                }

                for (int i = _pendingHandles.Count - 1; i >= 0; i--)
                {
                    ResumeHandle(_pendingHandles[i]);
                }
            }
            finally
            {
                EndHandleIteration();
            }
        }

        /// <summary>
        ///     立即停止所有活动的音频播放。手动停止只触发 <see cref="AudioHandle.Stopped" />，不会触发 <see cref="AudioHandle.Completed" />。
        /// </summary>
        public void StopAll()
        {
            BeginHandleIteration();
            try
            {
                for (int i = _activeHandles.Count - 1; i >= 0; i--)
                {
                    StopHandle(_activeHandles[i]);
                }

                for (int i = _pendingHandles.Count - 1; i >= 0; i--)
                {
                    StopHandle(_pendingHandles[i]);
                }
            }
            finally
            {
                EndHandleIteration();
            }
        }

        internal void SetHandleVolume(AudioHandle handle, float volume)
        {
            if (!CanControl(handle)) return;

            handle.Volume = NormalizeVolumeGain(volume);
            ApplySourceVolume(handle);
        }

        internal void FadeHandleTo(AudioHandle handle, float targetVolume, float duration)
        {
            if (!CanControl(handle)) return;

            targetVolume = NormalizeVolumeGain(targetVolume);
            duration = Mathf.Max(0f, duration);
            StopFadeCoroutine(handle);

            if (duration <= 0f)
            {
                SetHandleVolume(handle, targetVolume);
                return;
            }

            handle._fadeCoroutine = StartCoroutine(FadeRoutine(handle, targetVolume, duration));
        }

        internal void PauseHandle(AudioHandle handle)
        {
            if (!CanControl(handle) || handle.IsPaused) return;

            handle._source.Pause();
            handle.IsPaused = true;
        }

        internal void ResumeHandle(AudioHandle handle)
        {
            if (!CanControl(handle) || !handle.IsPaused) return;

            handle._source.UnPause();
            handle.IsPaused = false;
        }

        internal void StopHandle(AudioHandle handle)
        {
            if (!CanControl(handle)) return;
            StopHandleInternal(handle, false);
        }

        internal void StopHandle(AudioHandle handle, float duration)
        {
            if (!CanControl(handle)) return;

            duration = Mathf.Max(0f, duration);
            StopFadeCoroutine(handle);

            if (duration <= 0f)
            {
                StopHandleInternal(handle, false);
                return;
            }

            handle._fadeCoroutine = StartCoroutine(StopFadeRoutine(handle, duration));
        }

        internal void SeekHandle(AudioHandle handle, float time)
        {
            if (!CanControl(handle) || !handle._source.clip) return;

            handle._source.time = Mathf.Clamp(time, 0f, handle._source.clip.length);
        }

        private void Initialize()
        {
            if (_sourceRoot) return;

            _sourceRoot = new GameObject("AudioSources");
            _sourceRoot.transform.SetParent(transform);
            _sourceRoot.transform.localPosition = Vector3.zero;

            FindMixerGroups();
            ApplyMixerVolume(MASTER_VOLUME_PARAM, _masterVolume);
            ApplyMixerVolume(BGM_VOLUME_PARAM, _bgmVolume);
            ApplyMixerVolume(SFX_VOLUME_PARAM, _sfxVolume);
        }

        private AudioHandle PlayInternal(AudioClip clip, bool loop, float volume, bool isBgm, Vector3? position = null, Transform target = null)
        {
            if (!clip)
            {
                RLog.LogWarning("AudioClip 为空。");
                return null;
            }

            AudioSource source = GetSource();
            source.clip = clip;
            source.loop = loop;
            source.outputAudioMixerGroup = GetOutputGroup(isBgm);

            if (target)
            {
                source.spatialBlend = 1f;
                source.transform.SetParent(target);
                source.transform.localPosition = Vector3.zero;
            }
            else if (position.HasValue)
            {
                source.spatialBlend = 1f;
                source.transform.SetParent(null);
                source.transform.position = position.Value;
            }
            else
            {
                source.spatialBlend = 0f;
                source.transform.SetParent(_sourceRoot.transform);
                source.transform.localPosition = Vector3.zero;
            }

            AudioHandle handle = new();
            handle.Initialize(source, this, loop, NormalizeVolumeGain(volume), isBgm);
            ApplySourceVolume(handle);

            source.Play();
            AddManagedHandle(handle);
            return handle;
        }

        private void AddManagedHandle(AudioHandle handle)
        {
            if (IsIteratingHandles)
            {
                _pendingHandles.Add(handle);
            }
            else
            {
                _activeHandles.Add(handle);
            }
        }

        private AudioSource GetSource()
        {
            AudioSource source;
            if (_sourcePool.Count > 0)
            {
                source = _sourcePool.Pop();
                source.gameObject.SetActive(true);
            }
            else
            {
                GameObject go = new("AudioSource");
                go.transform.SetParent(_sourceRoot.transform);
                source = go.AddComponent<AudioSource>();
            }

            source.volume = 1f;
            source.loop = false;
            source.time = 0f;
            source.spatialBlend = 0f;
            source.playOnAwake = false;
            return source;
        }

        private void ReturnSource(AudioSource source)
        {
            if (!source) return;

            source.Stop();
            source.clip = null;
            source.loop = false;
            source.volume = 1f;
            source.time = 0f;
            source.spatialBlend = 0f;
            source.playOnAwake = false;
            source.outputAudioMixerGroup = null;

            if (_sourceRoot)
            {
                source.transform.SetParent(_sourceRoot.transform);
                source.transform.localPosition = Vector3.zero;
            }
            else
            {
                source.transform.SetParent(null);
            }

            source.gameObject.SetActive(false);

            if (_sourcePool.Count < POOL_CAPACITY * 2)
            {
                _sourcePool.Push(source);
            }
            else
            {
                Destroy(source.gameObject);
            }
        }

        private void CompleteHandle(AudioHandle handle)
        {
            if (!CanControl(handle)) return;
            StopHandleInternal(handle, true);
        }

        private void StopHandleInternal(AudioHandle handle, bool completed)
        {
            if (!CanControl(handle)) return;

            StopFadeCoroutine(handle);
            handle.MarkStopped(completed);

            if (handle._source)
            {
                handle._source.Stop();
            }

            try
            {
                if (completed)
                {
                    handle.RaiseCompleted();
                }
            }
            catch (Exception exception)
            {
                RLog.LogException(exception, this);
            }

            try
            {
                handle.RaiseStopped();
            }
            catch (Exception exception)
            {
                RLog.LogException(exception, this);
            }
            finally
            {
                ReleaseHandle(handle);
            }
        }

        private void ReleaseHandle(AudioHandle handle)
        {
            if (handle == null) return;

            if (handle == CurrentBgm)
                CurrentBgm = null;

            AudioSource source = handle._source;
            handle.Reset();

            if (source)
            {
                ReturnSource(source);
            }

            if (!IsIteratingHandles)
            {
                RemoveInactiveHandles();
            }
        }

        private void StopFadeCoroutine(AudioHandle handle)
        {
            if (handle?._fadeCoroutine == null) return;

            StopCoroutine(handle._fadeCoroutine);
            handle._fadeCoroutine = null;
        }

        private IEnumerator FadeRoutine(AudioHandle handle, float targetVolume, float duration)
        {
            float startVolume = handle.Volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (!CanControl(handle)) yield break;

                elapsed += Time.deltaTime;
                float t = duration > 0f ? elapsed / duration : 1f;
                SetHandleVolume(handle, Mathf.Lerp(startVolume, targetVolume, t));
                yield return null;
            }

            if (CanControl(handle))
            {
                SetHandleVolume(handle, targetVolume);
                handle._fadeCoroutine = null;
            }
        }

        private IEnumerator StopFadeRoutine(AudioHandle handle, float duration)
        {
            float startVolume = handle.Volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (!CanControl(handle)) yield break;

                elapsed += Time.deltaTime;
                float t = duration > 0f ? elapsed / duration : 1f;
                SetHandleVolume(handle, Mathf.Lerp(startVolume, 0f, t));
                yield return null;
            }

            if (CanControl(handle))
            {
                handle._fadeCoroutine = null;
                StopHandleInternal(handle, false);
            }
        }

        private void ApplySourceVolume(AudioHandle handle)
        {
            if (!CanControl(handle)) return;

            float masterScale = _muted ? 0f : _masterVolume;
            handle._source.volume = _mixer ? handle.Volume : handle.Volume * masterScale * (handle._isBgm ? _bgmVolume : _sfxVolume);
        }

        private void RefreshAllSourceVolumes()
        {
            for (int i = _activeHandles.Count - 1; i >= 0; i--)
            {
                AudioHandle handle = _activeHandles[i];
                if (CanControl(handle))
                {
                    ApplySourceVolume(handle);
                }
            }

            for (int i = _pendingHandles.Count - 1; i >= 0; i--)
            {
                AudioHandle handle = _pendingHandles[i];
                if (CanControl(handle))
                {
                    ApplySourceVolume(handle);
                }
            }
        }

        private void FindMixerGroups()
        {
            if (!_mixer) return;

            _masterGroup = FindMixerGroup(MASTER_GROUP_NAME);
            _bgmGroup = FindMixerGroup(BGM_GROUP_NAME) ?? _masterGroup;
            _sfxGroup = FindMixerGroup(SFX_GROUP_NAME) ?? _masterGroup;
        }

        private AudioMixerGroup FindMixerGroup(string groupName)
        {
            if (!_mixer) return null;

            AudioMixerGroup[] groups = _mixer.FindMatchingGroups(groupName);
            return groups is { Length: > 0 } ? groups[0] : null;
        }

        private AudioMixerGroup GetOutputGroup(bool isBgm)
        {
            if (!_mixer) return null;
            return isBgm ? _bgmGroup : _sfxGroup;
        }

        private void ApplyMixerVolume(string paramName, float linear)
        {
            if (!_mixer) return;

            _mixer.SetFloat(paramName, LinearToDecibel(linear));
        }

        private void ApplyVolumeSetting(string paramName, float linear)
        {
            if (_mixer)
            {
                ApplyMixerVolume(paramName, linear);
            }
            else
            {
                RefreshAllSourceVolumes();
            }
        }

        private static float LinearToDecibel(float linear)
        {
            return linear <= MIN_LINEAR ? MIN_DB : 20f * Mathf.Log10(linear);
        }

        private bool CanControl(AudioHandle handle)
        {
            return handle is { _manager: not null, _source: not null } && handle._manager == this && !handle.IsStopped;
        }

        private void BeginHandleIteration()
        {
            _handleIterationDepth++;
        }

        private void EndHandleIteration()
        {
            _handleIterationDepth--;
            if (_handleIterationDepth == 0)
            {
                FinalizeHandleCollections();
            }
        }

        private bool IsIteratingHandles => _handleIterationDepth > 0;

        private void FinalizeHandleCollections()
        {
            RemoveInactiveHandles();
            ActivatePendingHandles();
        }

        private void RemoveInactiveHandles()
        {
            for (int i = _activeHandles.Count - 1; i >= 0; i--)
            {
                AudioHandle handle = _activeHandles[i];
                if (handle == null || handle._manager != this || handle.IsStopped)
                {
                    _activeHandles.RemoveAt(i);
                }
            }
        }

        private void ActivatePendingHandles()
        {
            if (_pendingHandles.Count == 0) return;

            for (int i = 0; i < _pendingHandles.Count; i++)
            {
                AudioHandle handle = _pendingHandles[i];
                if (CanControl(handle))
                {
                    _activeHandles.Add(handle);
                }
                else
                {
                    handle?.Reset();
                }
            }

            _pendingHandles.Clear();
        }

        private void StopAllImmediate()
        {
            for (int i = _activeHandles.Count - 1; i >= 0; i--)
            {
                AudioHandle handle = _activeHandles[i];
                if (handle == null) continue;

                StopFadeCoroutine(handle);
                AudioSource source = handle._source;
                handle.MarkStopped(false);
                handle.Reset();

                if (source)
                {
                    source.Stop();
                    ReturnSource(source);
                }
            }

            for (int i = _pendingHandles.Count - 1; i >= 0; i--)
            {
                AudioHandle handle = _pendingHandles[i];
                if (handle == null) continue;

                StopFadeCoroutine(handle);
                AudioSource source = handle._source;
                handle.MarkStopped(false);
                handle.Reset();

                if (source)
                {
                    source.Stop();
                    ReturnSource(source);
                }
            }

            _activeHandles.Clear();
            _pendingHandles.Clear();
            _sourcePool.Clear();
            CurrentBgm = null;
        }

        private static float NormalizeVolumeGain(float volume)
        {
            return float.IsNaN(volume) ? 0f : Mathf.Max(0f, volume);
        }
    }
}