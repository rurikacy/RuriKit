using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RuriKit.Tests.PlayMode
{
    /// <summary>
    ///     验证音频播放句柄、BGM 替换、音量和 AudioSource 复用的确定性状态。
    /// </summary>
    public class AudioManagerTests
    {
        private AudioClip _clip;
        private AudioManager _manager;

        /// <summary>
        ///     创建独立音频管理器与内存音频片段。
        /// </summary>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (AudioManager.TryGetInstance(out AudioManager existing))
            {
                Object.Destroy(existing.gameObject);
                yield return null;
            }

            _manager = AudioManager.Instance;
            AudioListener.pause = false;
            if (!Object.FindObjectOfType<AudioListener>()) _manager.gameObject.AddComponent<AudioListener>();
            _clip = AudioClip.Create("TestClip", 4410, 1, 44100, false);
            yield return null;
        }

        /// <summary>
        ///     清理管理器、内存片段和全局监听器暂停状态。
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_manager) Object.Destroy(_manager.gameObject);
            if (_clip) Object.Destroy(_clip);
            AudioListener.pause = false;
            yield return null;
        }

        /// <summary>
        ///     验证空片段不创建句柄。
        /// </summary>
        [Test]
        public void Play_WhenClipIsNull_ShouldReturnNull()
        {
            LogAssert.Expect(LogType.Warning, "AudioClip 为空。");

            Assert.That(_manager.Play(null), Is.Null);
        }

        /// <summary>
        ///     验证播放返回可控制句柄，音量会规范化且停止回调只触发一次。
        /// </summary>
        [Test]
        public void Play_WhenHandleIsStopped_ShouldInvokeStoppedAndInvalidateHandle()
        {
            int stoppedCalls = 0;
            AudioHandle handle = _manager.Play(_clip, true, -1f);
            handle.Stopped += _ => stoppedCalls++;

            Assert.That(handle.Loop, Is.True);
            Assert.That(handle.Volume, Is.Zero);
            handle.SetVolume(float.NaN);
            Assert.That(handle.Volume, Is.Zero);
            handle.Stop();
            handle.Stop();

            Assert.That(stoppedCalls, Is.EqualTo(1));
            Assert.That(handle.IsStopped, Is.True);
            Assert.That(handle.IsPlaying, Is.False);
        }

        /// <summary>
        ///     验证暂停、恢复、批量控制和无效句柄操作均可安全执行。
        /// </summary>
        [Test]
        public void PauseResumeAndStopAll_WhenHandlesAreActive_ShouldUpdateHandleState()
        {
            AudioHandle first = _manager.Play(_clip, true);
            AudioHandle second = _manager.Play(_clip, true);

            _manager.PauseAll();
            Assert.That(first.IsPaused, Is.True);
            Assert.That(second.IsPaused, Is.True);
            _manager.ResumeAll();
            Assert.That(first.IsPaused, Is.False);
            Assert.That(second.IsPaused, Is.False);
            _manager.StopAll();

            Assert.That(first.IsStopped, Is.True);
            Assert.That(second.IsStopped, Is.True);
            Assert.DoesNotThrow(() => first.Seek(1f));
        }

        /// <summary>
        ///     验证替换 BGM 会停止旧句柄并设置新句柄为当前 BGM。
        /// </summary>
        [Test]
        public void PlayBgm_WhenExistingBgmIsReplaced_ShouldStopOldAndExposeNewHandle()
        {
            AudioHandle oldBgm = _manager.PlayBgm(_clip, true);
            AudioHandle newBgm = _manager.PlayBgm(_clip, true, 0f, 0.5f);

            Assert.That(oldBgm.IsStopped, Is.True);
            Assert.That(_manager.CurrentBgm, Is.SameAs(newBgm));
            Assert.That(newBgm.Volume, Is.EqualTo(0.5f));
        }

        /// <summary>
        ///     验证音量属性被限制在有效范围，静音状态可切换。
        /// </summary>
        [Test]
        public void VolumeSettings_WhenOutsideRange_ShouldClampAndRetainMuteState()
        {
            _manager.MasterVolume = 2f;
            _manager.BgmVolume = -1f;
            _manager.SfxVolume = 0.4f;
            _manager.Muted = true;

            Assert.That(_manager.MasterVolume, Is.EqualTo(1f));
            Assert.That(_manager.BgmVolume, Is.Zero);
            Assert.That(_manager.SfxVolume, Is.EqualTo(0.4f));
            Assert.That(_manager.Muted, Is.True);
        }

        /// <summary>
        ///     验证停止后 AudioSource 被重置并再次播放时复用，而非持续新建对象。
        /// </summary>
        [Test]
        public void Play_WhenPreviousHandleStopped_ShouldReuseResetAudioSource()
        {
            AudioHandle first = _manager.Play(_clip, true);
            first.Stop();
            AudioSource[] sourcesAfterStop = _manager.GetComponentsInChildren<AudioSource>(true);
            Assert.That(sourcesAfterStop.Length, Is.EqualTo(1));
            Assert.That(sourcesAfterStop[0].gameObject.activeSelf, Is.False);
            Assert.That(sourcesAfterStop[0].clip, Is.Null);

            _manager.Play(_clip, true);

            Assert.That(_manager.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(1));
        }

        /// <summary>
        ///     验证自然结束后回调只触发一次、句柄完成状态保留且音源能够再次复用。
        /// </summary>
        [UnityTest]
        public IEnumerator NaturalEnd_ShouldCompleteAndReuseSource()
        {
            AudioHandle handle = _manager.Play(_clip);
            int completed = 0;
            int stopped = 0;
            handle.Completed += _ => completed++;
            handle.Stopped += _ => stopped++;
            Assert.That(_manager.AliveHandleCount, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(0.3f);

            Assert.That(handle.IsCompleted, Is.True);
            Assert.That(handle.IsStopped, Is.True);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(stopped, Is.EqualTo(1));
            Assert.That(_manager.AliveHandleCount, Is.Zero);
            AudioSource source = _manager.GetComponentInChildren<AudioSource>(true);
            Assert.That(source.gameObject.activeSelf, Is.False);
            Assert.That(source.clip, Is.Null);
            AudioHandle next = _manager.Play(_clip, true);
            Assert.That(_manager.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(1));
            handle.Stop();
            Assert.That(next.IsStopped, Is.False);
        }

        /// <summary>
        ///     验证短音效在两次管理器更新之间完整播完且时间归零时仍能回收。
        /// </summary>
        [UnityTest]
        public IEnumerator NaturalEnd_BetweenUpdates_ShouldRecycleZeroTimeSource()
        {
            _manager.enabled = false;
            AudioHandle handle = _manager.Play(_clip);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(handle._source.isPlaying, Is.False);
            Assert.That(handle._source.time, Is.Zero);
            _manager.enabled = true;
            yield return null;
            yield return null;
            Assert.That(handle.IsCompleted, Is.True);
            Assert.That(_manager.AliveHandleCount, Is.Zero);
        }

        /// <summary>
        ///     验证句柄暂停、全局监听器暂停和应用挂起都不会被误判为自然结束。
        /// </summary>
        [UnityTest]
        public IEnumerator PausedAudio_ShouldStayAliveUntilResumed()
        {
            AudioHandle handle = _manager.Play(_clip);
            handle.Pause();
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(_manager.AliveHandleCount, Is.EqualTo(1));
            Assert.That(handle.IsCompleted, Is.False);
            AudioListener.pause = true;
            handle.Resume();
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(handle.IsStopped, Is.False);
            _manager.SendMessage("OnApplicationPause", true);
            AudioListener.pause = false;
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(handle.IsStopped, Is.False);
            _manager.SendMessage("OnApplicationPause", false);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(handle.IsCompleted, Is.True);
        }

        /// <summary>
        ///     验证重复播放短音效后存活数量归零，音源数量不会随播放轮数增长。
        /// </summary>
        [UnityTest]
        public IEnumerator RepeatedOneShots_ShouldKeepSourceCountBounded()
        {
            for (int round = 0; round < 10; round++)
            {
                for (int i = 0; i < 12; i++) _manager.Play(_clip);
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.That(_manager.AliveHandleCount, Is.Zero, $"第 {round} 轮未回收");
                Assert.That(_manager.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(12));
            }
        }

        /// <summary>
        ///     验证回调中新建但尚未加入活动列表的句柄也会纳入存活计数。
        /// </summary>
        [UnityTest]
        public IEnumerator CompletedCallback_ShouldCountPendingHandleAndPreserveIt()
        {
            AudioHandle next = null;
            int countInCallback = -1;
            AudioHandle first = _manager.Play(_clip);
            first.Completed += _ =>
            {
                next = _manager.Play(_clip, true);
                countInCallback = _manager.AliveHandleCount;
            };
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(countInCallback, Is.EqualTo(1));
            Assert.That(_manager.AliveHandleCount, Is.EqualTo(1));
            Assert.That(next.IsStopped, Is.False);
            _manager.StopAll();
            Assert.That(_manager.AliveHandleCount, Is.Zero);
        }

        /// <summary>
        ///     验证三维音源随目标销毁时会释放句柄引用和滤镜记录。
        /// </summary>
        [UnityTest]
        public IEnumerator DestroyedAudioTarget_ShouldReleaseHandleAndFilterRecord()
        {
            GameObject target = new("AudioTarget");
            AudioHandle handle = _manager.Play3D(_clip, target.transform, true);
            Object.Destroy(target);
            yield return null;
            yield return null;
            Assert.That(handle._manager, Is.Null);
            Assert.That(_manager.AliveHandleCount, Is.Zero);
            var filters = (IDictionary)typeof(AudioManager)
                .GetField("_gainFilters", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_manager);
            Assert.That(filters.Count, Is.Zero);
        }

        /// <summary>
        ///     验证管理器销毁后活动句柄失效。
        /// </summary>
        [UnityTest]
        public IEnumerator OnDestroy_WhenHandleIsActive_ShouldInvalidateHandle()
        {
            AudioHandle handle = _manager.Play(_clip, true);
            Object.Destroy(_manager.gameObject);
            yield return null;

            Assert.That(handle.IsStopped, Is.True);
            _manager = null;
        }
    }
}
