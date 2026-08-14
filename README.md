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

**RuriKit** 是一个面向中小型 Unity 项目的轻量级游戏服务框架，提供音频、事件、对象池、计时器、UI 与持久化六大基础服务模块，以及若干常用工具扩展。

项目由个人在日常维护的游戏项目中得来，设计目标是在保持低侵入、易上手的同时，让业务代码远离重复的基础设施工作。框架采用「服务定位器」架构（通过 `XxxManager.Instance` 获取服务），不强制约束业务代码的组织方式——可以整体接入，也可以只引用其中个别模块。

---

## 📥 安装

- 本项目为个人日常开发使用框架，**因此可能会进行无规律的更新，且有可能变更行为接口，建议通过下载源码后将目录复制到项目的 `Assets` 下引用**，也可以通过 Unity Package Manager 以 git URL 方式安装，但不建议更新：

1. 打开 `Window → Package Manager`，点击左上角 `+`，选择 `Add package from git URL...`；
2. 粘贴以下地址并确认：

```
https://github.com/rurikacy/RuriKit.git#main
```

安装完成后，RuriKit 会以 UPM 包形式出现在 **Packages** 区：

- **包名**：`com.rurikacy.rurikit`
- **源码路径**：`Library/PackageCache/com.rurikacy.rurikit@<提交哈希>`（Unity 逻辑路径为 `Packages/com.rurikacy.rurikit`）

在 Unity 的 Project 窗口把视图从 `Assets` 切换到 `Packages`，即可浏览全部源码。该区域只读；如需修改源码，请直接 clone 本仓库。依赖 `com.unity.nuget.newtonsoft-json` 会自动安装。

---

## ✨ 功能特性

### 管理器层（六大服务模块）

| 模块 | 功能说明 |
| --- | --- |
| **AudioManager** | 2D / 3D 音效与 BGM 播放、淡入淡出、AudioMixer 音量控制、AudioSource 池化复用 |
| **EventManager** | 全局事件总线，支持无参与强类型参数事件，域重载自动重置 |
| **PoolManager** | GameObject 对象池与纯 C# 对象池，支持预加载、延迟归还、池清理 |
| **TimerManager** | 延时 / 循环计时器，支持暂停恢复、标签批量管理、整秒 / 整分钟事件 |
| **UIManager** | UIView 注册与显隐控制，支持多画布管理与同画布独占显示 |
| **Persistence** | JsonHelper（JSON 文件存储，延迟刷写 + 失败重试）与 PPrefsHelper（PlayerPrefs 封装） |

### 基础设施

- `ManagerSingleton<T>` — 泛型单例基类：按需创建、重复实例处理、退出保护、域重载重置
- 扩展方法：UI Graphic 透明度、列表随机选取 / 洗牌、TMP 精灵数字等
- `RLog` 统一日志、`RMath` 数学工具、`RList` 集合工具
- 编辑器扩展：`AutoDisableRaycastTarget` 自动关闭非交互 UI 的射线检测

---

## 🚀 快速开始

1. 将 `RuriKit` 目录复制到项目的 `Assets` 下（或作为 UPM 包引用）；
2. 依赖包：`com.unity.nuget.newtonsoft-json`（3.2.2）、`com.unity.textmeshpro`（3.0.6）、`com.unity.ugui`（1.0.0）；
3. 运行时管理器自动创建，通过静态入口直接使用：

```csharp
// 播放音效
AudioManager.Instance.Play(clip);

// 1.5 秒后执行
TimerManager.Instance.AddTimer(1.5f, () => { /* ... */ });

// 从对象池获取
GameObject go = PoolManager.Instance.Get(prefab);
```

---

## 📌 说明

- 本框架已在个人多款游戏中稳定运行，且暂未出现过 Bug，但不保证不存在任何安全隐患或缺陷，使用前建议自行评估与测试，因使用本项目造成的一切后果由使用者自行承担。

---

<p align="center"><em>RuriKit · 轻量级 Unity 游戏服务框架</em></p>
