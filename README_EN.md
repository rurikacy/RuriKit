<p align="center">
  <img src="Documents/Icon.ico" alt="RuriKit" width="96" height="96" />
  <h1 align="center">RuriKit — Lightweight Unity Game Service Framework</h1>
  <p align="center">
    <img alt="Unity" src="https://img.shields.io/badge/Unity-2021.3+-000000" />
    <img alt="C#" src="https://img.shields.io/badge/C%23-9.0-512BD4" />
    <img alt="Newtonsoft" src="https://img.shields.io/badge/Newtonsoft.Json-3.2.2-6DB33F" />
    <img alt="Platform" src="https://img.shields.io/badge/Platform-Windows%20%7C%20Android-0F766E" />
  </p>
  <p align="center"><code>by Rurikacy</code></p>
  <p align="center"><a href="README.md">简体中文</a></p>
</p>

---

## 📖 Introduction

**RuriKit** is a lightweight game-service framework for small and medium-sized Unity projects. It provides six basic service modules: audio, events, object pooling, timers, UI, and persistence.
The project grew out of a personal game codebase. Its goal is to keep integration low-impact and easy to learn while removing repetitive infrastructure work from gameplay code. The framework uses singleton services without imposing a business-code architecture, so you can adopt the whole package or only the modules you need.

---

## 📥 Installation

- Copy the source directory into your project's `Assets` folder, or install it through Unity Package Manager using a git URL:

1. Open `Window → Package Manager`, click `+`, and select `Add package from git URL...`.
2. Paste the following URL and confirm:

```
https://github.com/rurikacy/RuriKit.git#main
```

---

## ✨ Features

### Manager layer (six service modules)

| Module | Description |
| --- | --- |
| **AudioManager** | 2D/3D SFX and BGM playback, fades, AudioMixer volume control, and AudioSource pooling. |
| **EventManager** | A type-based global event bus. |
| **PoolManager** | GameObject and pure C# object pools with preloading, delayed release, and cleanup. |
| **TimerManager** | One-shot and looping timers with pause/resume, tag management, and second/minute events. |
| **UIManager** | UIView registration and visibility control across multiple canvases, including per-canvas exclusive display. |
| **Persistence** | JsonHelper (JSON file storage with delayed flush and retry) and PPrefsHelper (PlayerPrefs wrapper). |

### Dependency

| Package | Purpose |
| --- | --- |
| `com.unity.nuget.newtonsoft-json` | Serialization and deserialization for `JsonHelper` and `PPrefsHelper`. |

---

## 📚 API Reference

The following tables cover every public method in the Runtime public classes. Inspect properties and events through IntelliSense.

### ManagerSingleton&lt;T&gt;

| Signature | Parameters | Returns | Usage |
| --- | --- | --- | --- |
| `static bool TryGetInstance(out T manager)` | `manager`: output instance | `bool` | Tries to get the current instance without creating one. |

### AudioManager

| Signature | Parameters | Returns | Usage |
| --- | --- | --- | --- |
| `AudioHandle Play(AudioClip clip, bool loop = false, float volume = 1f)` | Clip, loop flag, volume gain | `AudioHandle` | Plays a 2D sound effect. |
| `AudioHandle Play3D(AudioClip clip, Vector3 position, bool loop = false, float volume = 1f)` | Clip, world position, loop flag, volume gain | `AudioHandle` | Plays a 3D sound at a position. |
| `AudioHandle Play3D(AudioClip clip, Transform target, bool loop = false, float volume = 1f)` | Clip, follow target, loop flag, volume gain | `AudioHandle` | Plays a 3D sound that follows a target. |
| `AudioHandle PlayBgm(AudioClip clip, bool loop = true, float fadeInDuration = 0f, float volume = 1f)` | Clip, loop flag, fade-in seconds, volume gain | `AudioHandle` | Plays BGM and replaces the current track. |
| `void PauseAll()` | None | `void` | Pauses all active audio. |
| `void ResumeAll()` | None | `void` | Resumes all paused audio. |
| `void StopAll()` | None | `void` | Immediately stops all active audio. |

### AudioHandle

| Signature | Parameters | Returns | Usage |
| --- | --- | --- | --- |
| `void SetVolume(float volume)` | Target volume gain | `void` | Sets this playback's volume immediately. |
| `void FadeTo(float targetVolume, float duration)` | Target gain, transition seconds | `void` | Smoothly changes this playback's volume. |
| `void Pause()` | None | `void` | Pauses this playback. |
| `void Resume()` | None | `void` | Resumes from the current position. |
| `void Stop()` | None | `void` | Immediately stops this playback. |
| `void Stop(float duration)` | Fade-out seconds | `void` | Fades out and then stops this playback. |
| `void Seek(float time)` | Target seconds | `void` | Seeks to a playback position. |

### EventManager

| Signature | Parameters | Returns | Usage |
| --- | --- | --- | --- |
| `static void FireEvent<T>(T eventData)` | Event data | `void` | Invokes all listeners registered for `T`. |
| `static void AddListener<T>(Action<T> action)` | Typed callback | `void` | Registers a global listener. |
| `static void RemoveListener<T>(Action<T> action)` | Registered callback | `void` | Removes a global listener. |

### PoolManager

| Signature | Parameters | Returns | Usage |
| --- | --- | --- | --- |
| `void Preload(GameObject prefab, int count)` | Prefab, count | `void` | Warms a GameObject pool. |
| `GameObject Get(GameObject prefab)` | Prefab | `GameObject` | Gets an active instance. |
| `GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)` | Prefab, position, rotation, parent | `GameObject` | Gets an instance, applies its transform, and activates it. |
| `void Release(GameObject instance)` | Instance | `void` | Returns an instance immediately. |
| `void Release(GameObject instance, float delaySeconds)` | Instance, delay seconds | `void` | Returns an instance after a delay. |
| `bool HasPool(GameObject prefab)` | Prefab | `bool` | Checks whether a GameObject pool exists. |
| `void ClearPool(GameObject prefab)` | Prefab | `void` | Disposes and removes a GameObject pool. |
| `void Preload<T>(int count) where T : class, new()` | Count | `void` | Warms a pure C# object pool. |
| `T Get<T>() where T : class, new()` | None | `T` | Gets a pure C# object. |
| `void Release<T>(T obj) where T : class, new()` | Object | `void` | Returns a pure C# object. |
| `void ClearPool<T>() where T : class, new()` | None | `void` | Disposes and removes a typed object pool. |
| `void ClearAllUnused()` | None | `void` | Clears unused objects from every pool. |
| `void Shutdown()` | None | `void` | Stops delayed releases and disposes all pools. |

### CSPool&lt;T&gt; (internal)

| Signature | Parameters | Returns | Usage |
| --- | --- | --- | --- |
| `T Get()` | None | `T` | Borrows an object from a pure C# pool. |
| `bool Release(T obj)` | Object | `bool` | Returns an object and reports success. |
| `void Clear()` | None | `void` | Clears unused objects. |
| `void Dispose()` | None | `void` | Disposes the object pool. |

### ReferenceEqualityComparer&lt;T&gt; (internal)

| Signature | Parameters | Returns | Usage |
| --- | --- | --- | --- |
| `bool Equals(T x, T y)` | Two objects | `bool` | Compares objects by reference. |
| `int GetHashCode(T obj)` | Object | `int` | Gets a reference-based hash code. |

### TimerManager

| Signature | Parameters | Returns | Usage |
| --- | --- | --- | --- |
| `TimerHandle AddTimer(float delay, Action callback, bool useUnscaledTime = false, string timerTag = "_Default_")` | Delay, callback, unscaled-time flag, tag | `TimerHandle` | Creates a one-shot timer. |
| `TimerHandle AddLoopTimer(float delay, float interval, Action callback, bool useUnscaledTime = false, string timerTag = "_Default_")` | Initial delay, interval, callback, time mode, tag | `TimerHandle` | Creates a looping timer. |
| `void RemoveAllTimers()` | None | `void` | Removes all active timers. |
| `void RemoveTimersByTag(string timerTag)` | Tag | `void` | Removes timers with an exact tag match. |
| `void PauseAllTimers()` | None | `void` | Pauses all timers. |
| `void PauseTimersByTag(string timerTag)` | Tag | `void` | Pauses timers with an exact tag match. |
| `void ResumeAllTimers()` | None | `void` | Resumes all timers. |
| `void ResumeTimersByTag(string timerTag)` | Tag | `void` | Resumes timers with an exact tag match. |

### TimerHandle

| Signature | Parameters | Returns | Usage |
| --- | --- | --- | --- |
| `void Remove()` | None | `void` | Removes this timer. |
| `void Pause()` | None | `void` | Pauses this timer. |
| `void Resume()` | None | `void` | Resumes this timer. |

### UIManager

| Signature | Parameters | Returns | Usage |
| --- | --- | --- | --- |
| `T ShowView<T>() where T : UIView` | None | `T` | Shows the registered view of type `T`. |
| `T ShowViewOnly<T>() where T : UIView` | None | `T` | Shows `T` and hides other views on the same canvas. |
| `UIView ShowViewOnly(UIView view)` | Registered view | `UIView` | Exclusively shows the specified view. |
| `void HideView<T>() where T : UIView` | None | `void` | Hides the registered view of type `T`. |
| `bool TryGetView<T>(out T view) where T : UIView` | `view`: output view | `bool` | Tries to get a registered view. |
| `bool TryGetCanvas<T>(out T canvas) where T : UICanvas` | `canvas`: output canvas | `bool` | Tries to get the unique matching canvas. |
| `void HideAllViews()` | None | `void` | Hides all registered views. |

### UICanvas / UIView

| Signature | Parameters | Returns | Usage |
| --- | --- | --- | --- |
| `void UICanvas.Refresh()` | None | `void` | Recollects and synchronizes views under the canvas. |
| `void UIView.Show()` | None | `void` | Activates the view itself. |
| `void UIView.Hide()` | None | `void` | Deactivates the view itself. |

### JsonHelper / PPrefsHelper

| Signature | Parameters | Returns | Usage |
| --- | --- | --- | --- |
| `static T Read<T>(string key, T defaultValue = default)` | Key, default value | `T` | Reads and deserializes data. |
| `static void Write<T>(string key, T value)` | Key, value | `void` | Writes to the cache and flushes later. |
| `static bool HasKey(string key)` | Key | `bool` | Checks whether a key exists. |
| `static bool TryDeleteKey(string key)` | Key | `bool` | Deletes a key if present and reports whether it existed. |
| `static void DeleteKey(string key)` | Key | `void` | Deletes a key. |
| `static void DeleteAll()` | None | `void` | Deletes all data and clears the cache. |
| `static void Save()` | None | `void` | Flushes cached changes immediately. |

`JsonHelper` stores JSON files under the persistent-data directory; `PPrefsHelper` uses Unity `PlayerPrefs`. Both expose the same API.

---

## 📌 Notes

- The framework has been stable in several personal games, but no software is guaranteed to be free of security issues or defects. Evaluate and test it yourself before use. The user assumes all risks arising from use of this project.

---

## 📄 License

This project uses [The Unlicense](LICENSE), allowing copying, modification, distribution, and commercial use to the extent permitted by law. Third-party dependencies retain their own licenses.

---

<p align="center"><em>RuriKit · Lightweight Unity Game Service Framework</em></p>
