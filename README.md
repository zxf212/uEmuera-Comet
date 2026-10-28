# uEmuera-Comet

uEmuera 的第三方修改版 —— 在 Android 手机上运行 **Emuera（era 系列游戏）** 脚本的运行时。

本项目基于 [xerysherry/uEmuera](https://github.com/xerysherry/uEmuera) **v0.2.9d** 修改，引擎内核源自 **Emuera 1824v15**（作者 MinorShift 等）。

> **0.2 版重点**：修复 Android 上「资源全部加载失败 / 图片变文本 / 一启动就退出 / 被系统 OOM 杀掉」四类致命问题。这些修复全部基于实机日志定位，不是猜测。

---

## 项目状态

| 项 | 值 |
|---|---|
| 当前版本 | 0.2（2026-10-27） |
| 引擎基线 | uEmuera 0.2.9d / Emuera 1824v15 |
| Unity 版本 | 2022.3.62f3c1 |
| 构建配置 | IL2CPP，ARMv7 + ARM64，Release |
| APK 体积 | **33.6 MB**（0.1 为 66.3 MB 的 Development Build） |
| 实测环境 | MuMu 12（Android 12，x86_64，6 GB 内存）；Android 真机 |
| 实测游戏 | eraTW 系两个发行包（kagerou 250930 及 eratw 蛇250930） |

---

## 0.2 修复了什么

每一项都有对应的实机日志证据，详见 [CHANGELOG.md](CHANGELOG.md) 与 [docs/验证记录.md](docs/验证记录.md)。

| # | 现象 | 根因 | 提交 |
|---|---|---|---|
| 1 | 31 个资源 CSV 全部失败，图片全不显示 | 后台线程读 `Texture.width/height` 触发 Unity 主线程检查异常 | `47314f8` |
| 2 | 启动即退出，日志 `ERHの読み込み中にエラーが発生…` | Android 专属的小写扩展名重扫导致 ERH 被加载两次 → `#DIM` 重复注册 → Lv2 致命 | `f9e7a48` |
| 3 | 6 GB 设备玩几分钟被系统杀掉 | 启动时解码全部 2540 张图：纹理 5.2 GB + 像素缓存 5.2 GB ≈ 10.4 GB | `dc2bce1` |
| 4 | 手机端改不了配置，eraTW 类游戏满屏「解釈できない識別子」 | 读不到 `emuera.config` 时「搜索子目录」默认为 NO，子目录脚本全没加载 | `34a2b04` |
| 5 | 进游戏后满屏 `警告Lv2:定義していないキャラクタ変数` | 角色文件名为 `Chara0.csv`（大小写混排），严格 `CHARA*.CSV` 在大小写敏感的文件系统上命中 **0 个** | `c71caf7` |
| 6 | GCREATE 之后的画布无法当精灵使用 / GSAVE 存出空图 | 画布初始未标脏、像素缓冲未上传 | `cae93e5` `1986600` `452f8bc` |

---

## 与 0.1（原始 APK）的差异

### 体积从 66.3 MB 降到 33.6 MB，功能没有削减

| 项目 | 0.1 APK | 0.2 APK | 说明 |
|---|---|---|---|
| `libunity.so`（双 ABI） | 16.29 MB | 8.98 MB | 0.1 是 Development Build，含调试运行时与断言 |
| `libil2cpp.so`（双 ABI） | 14.28 MB | 12.82 MB | 调试符号减少 |
| `assets/bin/Data` | 13.02 MB | 9.78 MB | 0.1 为单个 12.7 MB 未压缩 `data.unity3d` |
| **条目间全零死空间** | **20.43 MB** | **0 MB** | 0.1 的 zip 内有 6 段空洞，抽样 480 KB 非零字节占比 0.0142% |
| 合计 | 66.3 MB | 33.6 MB | — |

### 功能等价性

| 项 | 说明 |
|---|---|
| `BitmapTexture.ReadDimensions` | 0.1 的 APK 里有此成员，源码中缺失；本仓库用 `TextureInfo` 缓存的宽高实现同一功能，行为等价 |
| `ColorMatrix` / `SetColorMatrix` / 双三次插值 | **0.1 与本项目都没有**。这是 Comet 分支的历史缺口，不是 0.2 引入的 |
| GXX 软件光栅化、WebP 解码、按钮位图缓存 | 与 0.1 一致，未改动 |

---

## 已知问题（如实标注）

| 项 | 状态 | 影响 |
|---|---|---|
| 颜色矩阵参数 `cm` 被忽略 | 未实现 | 使用该参数的游戏效果等同不传 |
| 缩放为最近邻采样 | 未实现 | 放大时可见锯齿（上游 v24 为双三次插值） |
| `UnityDrawContext` 无调用方 | 未接线 | 该绘制后端目前是死代码，不影响现有渲染路径 |
| 首次绘制延迟 | 0.2 引入 | 图片改为「用到才解码」，首次显示某张图有几十毫秒延迟 |
| 长时游玩内存稳定性 | 未压测 | 纹理 60/120/300 秒无引用即回收，回收后再用会重新解码 |
| 兼容性覆盖 | 仅 2 个游戏包 | 其他 era 游戏未做回归测试 |
| App 内无法修改游戏配置 | 继承上游 | 需要在 PC 上编辑 `emuera.config` |

完整证据与未验证项见 [docs/验证记录.md](docs/验证记录.md)。

---

## 源码来源与可信度（重要，务必阅读）

**本仓库不等于原始 APK 的逐字节源码副本。** 原始 Unity 工程已从磁盘删除且无备份，本仓库是重建产物：

```
GitHub 仓库快照（2026-05-02，128 个 .cs）
  + Trae 编辑器本地历史还原的 18 个文件（2026-05-02 ~ 05-05，字节级原样）
  + 1 个反推文件（IDrawContext.cs）
  + 0.2 的 6 项修复
= 现在的 130 个 .cs
```

| 结论 | 依据 |
|---|---|
| 加载链、绘制链的关键改动**已复原** | APK 的 `global-metadata.dat` 成员级比对，只剩 1 个真实差异（`ReadDimensions`） |
| 源码**不是**原工程的完整副本 | 05-04 之后的改动没有被任何编辑器历史记录 |
| 是否可编译、可运行 | 是。2026-10-27 的 APK 即由本仓库直接构建，并在模拟器与真机验证通过 |

完整还原过程、18 个文件清单、APK 逆向取证方法、仍然存疑的 4 处，见 **[docs/源码恢复说明.md](docs/源码恢复说明.md)**。

---

## 构建

### 前置条件

- Unity **2022.3.62f3c1**（含 Android Build Support / IL2CPP / NDK）
- Android SDK 与 JDK 17

### 重要限制

**工程路径不能包含非 ASCII 字符**，否则 Unity 构建直接报 `Invalid project path ... non-ASCII characters`。请把工程放在纯英文路径下再构建。

### 命令行构建（推荐）

```bash
Unity.exe -batchmode -quit \
  -projectPath "<工程路径>" \
  -buildTarget Android \
  -executeMethod CometBuild.BuildAndroid \
  -logFile "<日志路径>"
```

产物输出到 `<工程根目录>/Builds/uEmuera-Comet-0.1.apk`（文件名固定，见 `Assets/Editor/CometBuild.cs`），日志中有 `[CometBuild] 结果=Succeeded` 一行可判断成败。

### 编辑器内构建

`File → Build Settings → Android → Build`。`Assets/Editor/CometBuild.cs` 会自动把 `Assets/Main.unity` 设为唯一场景。

---

## 运行（玩家向）

1. era 游戏脚本（`*.csv` / `*.ERB` / `*.ERH`）必须是 **UTF-8** 编码；
2. 首次启动需允许「文件访问」权限；
3. 把游戏文件夹放到 `storage/emulated/0/emuera/`（也支持 `/1/`、`/2/`）；
4. 文件夹内若有 `emuera.config`，其中的「サブディレクトリを検索する」建议为 `YES`。若该文件缺失，本版本会自动开启子目录搜索。

---

## 目录结构

```
Assets/
  Editor/CometBuild.cs              命令行构建入口（0.2 新增）
  Scripts/
    SpriteManager.cs                纹理管理：按需解码、像素缓存、回收
    EmueraContent.cs                每帧刷新脏画布
    uEmuera/
      Drawing.cs                    Bitmap / BitmapTexture / BitmapPixelBuffer
      ImageHeader.cs                图片头解析（0.2 新增，仅读宽高不占像素内存）
      Utils.cs                      资源 CSV 尺寸修复
    Emuera/
      Config/Config.cs              文件枚举：忽略大小写的模式匹配
      Content/GraphicsImage.cs      GXX 指令的软件光栅化
      Content/AppContents.cs        资源加载
      GameProc/HeaderFileLoader.cs  ERH 加载
      GameProc/ErbLoader.cs         ERB 加载
docs/
  源码恢复说明.md                    工程重建全过程与存疑点
  验证记录.md                        实测数据与证据
CHANGELOG.md
```

---

## 原作者与授权

- 本项目基于 **xerysherry/uEmuera v0.2.9d**（**Apache-2.0**）修改，上游为 emuera1824v15 源码。原作者：**xerysherry**；Emuera 解释器作者：**MinorShift** 等。
- Apache-2.0 已授予修改与再分发权利。Emuera 原版授权（见 `Assets/StreamingAssets/license@emuera`）要求：不得虚示来源、修改必须明示、不得删除声明 —— 本版本按此三项执行，未删除或改动任何原授权文件。
- 若原作者或相关权利人认为本版本不妥，请提 Issue，我会立即处理。

## 禁止商用

本修改版仅限个人学习、研究与娱乐使用，**禁止任何形式的商业用途**，包括但不限于打包售卖、作为商业平台组成部分、或借此间接盈利。

## 致谢与免责

感谢原作者的杰出工作。本修改与原作者无关，因使用本版本导致的任何问题，责任由使用者自行承担。

---

## 截图

开始界面
![Screenshot1](Screenshot/screenshot1.png)

游戏运行界面
![Screenshot2](Screenshot/screenshot2.png)

快捷按钮
![Screenshot3](Screenshot/screenshot3.png)

指令输入
![Screenshot4](Screenshot/screenshot4.png)

缩放控制
![Screenshot5](Screenshot/screenshot5.png)
