<p align="center">
  <img src="Documents/Icon.ico" alt="UniHacker" width="96" height="96" />
  <h1 align="center">RuriKit — 轻量级 Unity 游戏服务框架</h1>
  <p align="center">
    <img alt="Unity" src="https://img.shields.io/badge/Unity-2021.3+-000000" />
    <img alt="C#" src="https://img.shields.io/badge/C%23-9.0-512BD4" />
    <img alt="Newtonsoft" src="https://img.shields.io/badge/Newtonsoft.Json-3.2.2-6DB33F" />
    <img alt="Platform" src="https://img.shields.io/badge/Platform-Windows%20%7C%20Android-0F766E" />
  </p>
  <p align="center"><code>by Rurikacy</code></p>
</p>

---

## 📖 简介

**RuriKit** 是一个面向中小型 Unity 项目的轻量级游戏服务框架，主要提供音频、事件、对象池、计时器、UI 与持久化六大基础服务模块，以及若干常用工具扩展。

项目由个人在日常维护的游戏项目中得来，设计目标是在保持低侵入、易上手的同时，让业务代码远离重复的基础设施工作。框架采用单例服务架构，不强制约束业务代码的组织方式——可以整体接入，也可以只引用其中个别模块。

---

## 📥 安装

- **本项目为个人日常开发使用框架，因此可能会进行无规律的更新，且有可能变更行为接口，建议通过下载源码后将目录复制到项目的 `Assets` 下引用**。

- 也可以通过 Unity Package Manager 以 git URL 方式安装，但不建议接入后二次更新：

1. 打开 `Window → Package Manager`，点击左上角 `+`，选择 `Add package from git URL...`；
2. 粘贴以下地址并确认：

```
https://github.com/rurikacy/RuriKit.git#main
```

---

## ✨ 功能特性

### 管理器层（六大服务模块）

| 模块 | 功能说明 |
| --- | --- |
| **AudioManager** | 2D / 3D 音效与 BGM 播放、淡入淡出、AudioMixer 音量控制、AudioSource 池化复用 |
| **EventManager** | 基于类型的全局事件总线 |
| **PoolManager** | GameObject 对象池与纯 C# 对象池，支持预加载、延迟归还、池清理 |
| **TimerManager** | 延时 / 循环计时器，支持暂停恢复、标签批量管理、整秒 / 整分钟事件 |
| **UIManager** | UIView 注册与显隐控制，支持多画布管理与同画布独占显示 |
| **Persistence** | JsonHelper（JSON 文件存储，延迟刷写 + 失败重试）与 PPrefsHelper（PlayerPrefs 封装） |

### 基础设施

- 针对常用类型的常用扩展方法及工具类、`RLog` 统一日志输出类等
- 部分常用的编辑器扩展，如自动关闭非交互 UI 的射线检测等

### 依赖包

| 包名 | 依赖 |
| --- | --- |
| `com.unity.nuget.newtonsoft-json` | 用于 `JsonHelper`、`PPrefsHelper` 中的序列化和反序列化 |
| `com.unity.textmeshpro` | 用于 `TextMeshPro` 等类型的拓展方法 |
| `com.unity.ugui` | 用于 `Graphic` 等类型的拓展方法 |

---

## 📌 说明

- 本框架已在个人多款游戏中稳定运行，且暂未出现过 Bug，但不保证不存在任何安全隐患或缺陷，使用前建议自行评估与测试，因使用本项目造成的一切后果由使用者自行承担。

---

<p align="center"><em>RuriKit · 轻量级 Unity 游戏服务框架</em></p>
