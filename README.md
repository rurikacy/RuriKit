<p align="center">
  <img src="Documents/Icon.ico" alt="RuriKit" width="96" height="96" />
  <h1 align="center">RuriKit — 轻量级 Unity 游戏服务框架</h1>
  <p align="center">
    <img alt="Unity" src="https://img.shields.io/badge/Unity-2021.3+-000000" />
    <img alt="C#" src="https://img.shields.io/badge/C%23-9.0-512BD4" />
    <img alt="Newtonsoft" src="https://img.shields.io/badge/Newtonsoft.Json-3.2.2-6DB33F" />
    <img alt="Platform" src="https://img.shields.io/badge/Platform-Windows%20%7C%20Android-0F766E" />
  </p>
  <p align="center"><code>by Rurikacy</code></p>
  <p align="center"><a href="README_EN.md">English</a></p>
</p>

---

## 📖 简介

**RuriKit** 是一个面向中小型 Unity 项目的轻量级游戏服务框架，主要提供音频、事件、对象池、计时器、UI 与持久化六大基础服务模块。
项目由个人在日常维护的游戏项目中得来，设计目标是在保持低侵入、易上手的同时，让业务代码远离重复的基础设施工作。框架采用单例服务架构，不强制约束业务代码的组织方式——可以整体接入，也可以只引用其中个别模块。

---

## 📥 安装

1. 前往 [Releases](https://github.com/rurikacy/RuriKit/releases/latest) 页面，下载最新版本的 `.unitypackage` 文件。
2. 在 Unity 中双击下载的文件，或选择 `Assets → Import Package → Custom Package...`，选中该文件并完成导入。

---

## ✨ 功能特性

### 管理器层（六大服务模块）

| 模块 | 功能说明 |
| --- | --- |
| **AudioManager** | 2D / 3D 音效与 BGM 播放、淡入淡出、AudioMixer 音量控制、AudioSource 池化复用 |
| **EventManager** | 基于类型的全局事件总线 |
| **PoolManager** | GameObject 对象池与纯 C# 对象池，支持预加载、延迟归还、池清理 |
| **TimerManager** | 延时 / 循环计时器，支持暂停 / 恢复、标签批量管理、整秒 / 整分钟事件 |
| **UIManager** | UIView 注册与显隐控制，支持多画布管理与同画布独占显示 |
| **Persistence** | JsonHelper（JSON 文件存储，延迟刷写 + 失败重试）与 PPrefsHelper（PlayerPrefs 封装） |


### 依赖包

| 包名 | 依赖 |
| --- | --- |
| `com.unity.nuget.newtonsoft-json` | 用于 `JsonHelper`、`PPrefsHelper` 中的序列化和反序列化 |

---

## 📚 API 参考

以下列表覆盖 Runtime 中公开类的全部 public 方法。属性和事件请直接通过 IntelliSense 查看。

### ManagerSingleton&lt;T&gt;

| 方法签名 | 参数 | 返回值 | 用法 |
| --- | --- | --- | --- |
| `static bool TryGetInstance(out T manager)` | `manager`：输出当前实例 | `bool` | 尝试获取实例，不会自动创建。 |

### AudioManager

| 方法签名 | 参数 | 返回值 | 用法 |
| --- | --- | --- | --- |
| `AudioHandle Play(AudioClip clip, bool loop = false, float volume = 1f)` | 音频片段、循环、播放音量倍率 | `AudioHandle` | 以二维音效播放。 |
| `AudioHandle Play3D(AudioClip clip, Vector3 position, bool loop = false, float volume = 1f)` | 音频片段、世界坐标、循环、音量倍率 | `AudioHandle` | 在指定位置播放三维音效。 |
| `AudioHandle Play3D(AudioClip clip, Transform target, bool loop = false, float volume = 1f)` | 音频片段、跟随目标、循环、音量倍率 | `AudioHandle` | 播放跟随目标移动的三维音效。 |
| `AudioHandle PlayBgm(AudioClip clip, bool loop = true, float fadeInDuration = 0f, float volume = 1f)` | 音频片段、循环、淡入时长、音量倍率 | `AudioHandle` | 播放背景音乐并替换当前 BGM。 |
| `void PauseAll()` | 无 | `void` | 暂停所有活动音频。 |
| `void ResumeAll()` | 无 | `void` | 恢复所有已暂停音频。 |
| `void StopAll()` | 无 | `void` | 立即停止所有活动音频。 |

### AudioHandle

| 方法签名 | 参数 | 返回值 | 用法 |
| --- | --- | --- | --- |
| `void SetVolume(float volume)` | 目标音量倍率 | `void` | 立即设置本次播放音量。 |
| `void FadeTo(float targetVolume, float duration)` | 目标倍率、过渡秒数 | `void` | 平滑调整本次播放音量。 |
| `void Pause()` | 无 | `void` | 暂停本次播放。 |
| `void Resume()` | 无 | `void` | 从当前位置恢复播放。 |
| `void Stop()` | 无 | `void` | 立即停止本次播放。 |
| `void Stop(float duration)` | 淡出秒数 | `void` | 淡出后停止本次播放。 |
| `void Seek(float time)` | 目标秒数 | `void` | 跳转到指定播放位置。 |

### EventManager

| 方法签名 | 参数 | 返回值 | 用法 |
| --- | --- | --- | --- |
| `static void FireEvent<T>(T eventData)` | 事件数据 | `void` | 触发该类型的所有监听器。 |
| `static void AddListener<T>(Action<T> action)` | 类型回调 | `void` | 注册全局事件监听器。 |
| `static void RemoveListener<T>(Action<T> action)` | 已注册回调 | `void` | 移除全局事件监听器。 |

### PoolManager

| 方法签名 | 参数 | 返回值 | 用法 |
| --- | --- | --- | --- |
| `void Preload(GameObject prefab, int count)` | 预制体、数量 | `void` | 预热 GameObject 对象池。 |
| `GameObject Get(GameObject prefab)` | 预制体 | `GameObject` | 获取一个活动实例。 |
| `GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)` | 预制体、位置、旋转、父级 | `GameObject` | 获取并设置 Transform 后激活实例。 |
| `void Release(GameObject instance)` | 实例 | `void` | 立即归还实例。 |
| `void Release(GameObject instance, float delaySeconds)` | 实例、延迟秒数 | `void` | 延迟归还实例。 |
| `bool HasPool(GameObject prefab)` | 预制体 | `bool` | 检查 GameObject 对象池是否存在。 |
| `void ClearPool(GameObject prefab)` | 预制体 | `void` | 释放并删除指定 GameObject 对象池。 |
| `void Preload<T>(int count) where T : class, new()` | 数量 | `void` | 预热纯 C# 对象池。 |
| `T Get<T>() where T : class, new()` | 无 | `T` | 获取纯 C# 对象。 |
| `void Release<T>(T obj) where T : class, new()` | 对象 | `void` | 归还纯 C# 对象。 |
| `void ClearPool<T>() where T : class, new()` | 无 | `void` | 释放并删除指定类型对象池。 |
| `void ClearAllUnused()` | 无 | `void` | 清理所有池中未借出的对象。 |
| `void Shutdown()` | 无 | `void` | 停止延迟归还并释放全部对象池。 |

### CSPool&lt;T&gt;（内部类型）

| 方法签名 | 参数 | 返回值 | 用法 |
| --- | --- | --- | --- |
| `T Get()` | 无 | `T` | 从纯 C# 对象池借出对象。 |
| `bool Release(T obj)` | 对象 | `bool` | 归还对象并报告是否成功。 |
| `void Clear()` | 无 | `void` | 清理未借出对象。 |
| `void Dispose()` | 无 | `void` | 释放对象池。 |

### ReferenceEqualityComparer&lt;T&gt;（内部类型）

| 方法签名 | 参数 | 返回值 | 用法 |
| --- | --- | --- | --- |
| `bool Equals(T x, T y)` | 两个对象 | `bool` | 按引用判断相等。 |
| `int GetHashCode(T obj)` | 对象 | `int` | 获取基于对象引用的哈希值。 |

### TimerManager

| 方法签名 | 参数 | 返回值 | 用法 |
| --- | --- | --- | --- |
| `TimerHandle AddTimer(float delay, Action callback, bool useUnscaledTime = false, string timerTag = "_Default_")` | 延迟、回调、是否不受 timeScale、标签 | `TimerHandle` | 创建一次性计时器。 |
| `TimerHandle AddLoopTimer(float delay, float interval, Action callback, bool useUnscaledTime = false, string timerTag = "_Default_")` | 首次延迟、间隔、回调、时间模式、标签 | `TimerHandle` | 创建循环计时器。 |
| `void RemoveAllTimers()` | 无 | `void` | 移除所有活动计时器。 |
| `void RemoveTimersByTag(string timerTag)` | 标签 | `void` | 移除指定标签的计时器。 |
| `void PauseAllTimers()` | 无 | `void` | 暂停所有计时器。 |
| `void PauseTimersByTag(string timerTag)` | 标签 | `void` | 暂停指定标签的计时器。 |
| `void ResumeAllTimers()` | 无 | `void` | 恢复所有计时器。 |
| `void ResumeTimersByTag(string timerTag)` | 标签 | `void` | 恢复指定标签的计时器。 |

### TimerHandle

| 方法签名 | 参数 | 返回值 | 用法 |
| --- | --- | --- | --- |
| `void Remove()` | 无 | `void` | 移除此计时器。 |
| `void Pause()` | 无 | `void` | 暂停此计时器。 |
| `void Resume()` | 无 | `void` | 恢复此计时器。 |

### UIManager

| 方法签名 | 参数 | 返回值 | 用法 |
| --- | --- | --- | --- |
| `T ShowView<T>() where T : UIView` | 无 | `T` | 显示指定类型视图。 |
| `T ShowViewOnly<T>() where T : UIView` | 无 | `T` | 显示视图并隐藏同画布其他视图。 |
| `UIView ShowViewOnly(UIView view)` | 已注册视图 | `UIView` | 独占显示指定视图。 |
| `void HideView<T>() where T : UIView` | 无 | `void` | 隐藏指定类型视图。 |
| `bool TryGetView<T>(out T view) where T : UIView` | `view`：输出视图 | `bool` | 尝试获取已注册视图。 |
| `bool TryGetCanvas<T>(out T canvas) where T : UICanvas` | `canvas`：输出画布 | `bool` | 尝试获取唯一匹配画布。 |
| `void HideAllViews()` | 无 | `void` | 隐藏所有已注册视图。 |

### UICanvas / UIView

| 方法签名 | 参数 | 返回值 | 用法 |
| --- | --- | --- | --- |
| `void UICanvas.Refresh()` | 无 | `void` | 重新收集并同步画布下的视图。 |
| `void UIView.Show()` | 无 | `void` | 激活视图自身。 |
| `void UIView.Hide()` | 无 | `void` | 停用视图自身。 |

### JsonHelper / PPrefsHelper

| 方法签名 | 参数 | 返回值 | 用法 |
| --- | --- | --- | --- |
| `static T Read<T>(string key, T defaultValue = default)` | 键、默认值 | `T` | 读取并反序列化数据。 |
| `static void Write<T>(string key, T value)` | 键、数据 | `void` | 写入缓存并延迟持久化。 |
| `static bool HasKey(string key)` | 键 | `bool` | 检查键是否存在。 |
| `static bool TryDeleteKey(string key)` | 键 | `bool` | 尝试删除键并返回是否存在。 |
| `static void DeleteKey(string key)` | 键 | `void` | 删除指定键。 |
| `static void DeleteAll()` | 无 | `void` | 删除全部数据并清理缓存。 |
| `static void Save()` | 无 | `void` | 立即刷写缓存。 |

`JsonHelper` 使用持久化目录中的 JSON 文件；`PPrefsHelper` 使用 Unity `PlayerPrefs`。两者 API 相同。

---

## 📌 说明

- 本框架已在个人多款游戏中稳定运行，且暂未出现过 Bug，但不保证不存在任何安全隐患或缺陷，使用前建议自行评估与测试，因使用本项目造成的一切后果由使用者自行承担。

---

## 📄 许可证

本项目使用 [The Unlicense](LICENSE)，允许在法律允许范围内自由复制、修改、发布和商业使用。第三方依赖遵循各自许可证。

---

<p align="center"><em>RuriKit · 轻量级 Unity 游戏服务框架</em></p>
