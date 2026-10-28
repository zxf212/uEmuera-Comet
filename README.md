# uEmuera-Comet

Android 上运行 **Emuera（era 系列游戏）** 的运行时。

基于 [xerysherry/uEmuera](https://github.com/xerysherry/uEmuera) **v0.2.9d**（Apache-2.0），引擎内核为 **Emuera 1824v15**，修复时对照 **Emuera 1824 + v24** 源码。

> ## ⚠️ 请先读这一段
>
> **0.1 版本的源码已经丢失。** 原 Unity 工程已从磁盘删除，无 git、无备份、无云副本，IL2CPP 产物也无法反编译出方法体。
>
> **本仓库的 0.2 是重建版**，由三部分拼成：上游 GitHub 仓库快照（2026-05-02）+ Trae 编辑器本地历史还原的 18 个文件（05-02 ~ 05-05）+ 1 个反推文件（`IDrawContext.cs`），再叠加 0.2 的修复。
>
> 它不是当年那个工程的逐字节副本。还原过程、APK 逆向取证、永久丢失的部分，见 **[docs/源码恢复说明.md](docs/源码恢复说明.md)**。

---

## 状态

| 项 | 值 |
|---|---|
| 版本 | 0.2（2026-10-27，重建版） |
| 引擎 | uEmuera 0.2.9d / Emuera 1824v15 |
| 参考实现 | Emuera 1824 + v24 |
| Unity | 2022.3.62f3c1，IL2CPP，ARMv7 + ARM64，Release |
| APK | 33.6 MB（0.1 为 66.3 MB 的 Development Build） |
| 实测 | MuMu 12（Android 12，6 GB）+ Android 真机；eraTW 系两个发行包 |

---

## 0.2 修复清单

| # | 现象 | 根因 | 提交 |
|---|---|---|---|
| 1 | 资源 CSV 全失败，图片不显示 | 后台线程读 `Texture.width` 触发 Unity 主线程检查 | `47314f8` |
| 2 | 启动即退出（`ERHの読み込み中にエラー`） | Android 小写重扫导致 ERH 加载两次 → `#DIM` 重复 → Lv2 致命 | `f9e7a48` |
| 3 | 6 GB 设备被系统 OOM 杀掉 | 启动解码全部 2540 张图，纹理 + 缓存 ≈ 10.4 GB | `dc2bce1` |
| 4 | eraTW 类游戏满屏「解釈できない識別子」 | 读不到 `emuera.config` → 不搜子目录 → 子目录脚本未加载 | `34a2b04` |
| 5 | 满屏 `警告Lv2:定義していないキャラクタ変数` | 角色文件为 `Chara0.csv`，严格 `CHARA*.CSV` 在大小写敏感 FS 上命中 0 个 | `c71caf7` |
| 6 | GCREATE 画布不可用 / GSAVE 存空图 | 画布未置脏、像素缓冲未上传、未每帧刷新 | `cae93e5` `1986600` `452f8bc` |

每项的完整证据（日志行、内存数字、文件计数）见 [CHANGELOG.md](CHANGELOG.md) 与 [docs/验证记录.md](docs/验证记录.md)。

---

## 参考 Emuera 1824 + v24 的地方

0.2 的修复不是凭经验猜的，关键几处都对着 **Emuera 1824 + v24** 的源码改：

| 位置 | v24 的写法 | 本项目的处理 |
|---|---|---|
| `HeaderFileLoader.cs` | 只有 `GetFiles(headerDir, "*.ERH")`，**没有** Android 小写重扫 | 删掉 5 处 `#if (UNITY_ANDROID \|\| UNITY_IOS)` 重扫，与 v24 一致 |
| `Config.getFiles()` | Windows 下模式匹配天然忽略大小写 | Android/Linux 大小写敏感，改为单次枚举 + 忽略大小写匹配，**语义等同 Windows/v24** |
| `GraphicsImage` / `GCREATE` | 创建后画布即一张有效空图 | 创建即置脏，保证 `ApplyPixelBuffer` 执行 |
| `ConstImage` 像素 | 只持一份 `Bitmap` | 去掉常驻 `pixelCache`，改按需构建，对齐「单份像素」语义 |
| `SearchSubdirectory` | 默认 `NO`，由 `emuera.config` 决定 | 默认仍为 `NO`；仅当配置文件缺失/读取失败时兜底为 `YES`（手机无法方便编辑） |
| `GSAVE` | 先把像素缓冲落到 Bitmap | 补 `ApplyPixelBuffer()` 再 `Bitmap.Save()` |

> 说明：v24 是 Windows 端的 Emuera 分支，本项目只对齐它的**行为语义**，不复制其 GDI+ 实现（颜色矩阵、双三次插值仍缺，见「已知问题」）。

---

## 与 0.1 的差异

体积减半，功能未削减。

| 项目 | 0.1 | 0.2 |
|---|---|---|
| `libunity.so`（双 ABI） | 16.29 MB | 8.98 MB（0.1 为 Development Build） |
| `libil2cpp.so`（双 ABI） | 14.28 MB | 12.82 MB |
| `assets/bin/Data` | 13.02 MB | 9.78 MB |
| 条目间全零死空间 | **20.43 MB** | **0 MB** |
| APK 总计 | 66.3 MB | 33.6 MB |

`BitmapTexture.ReadDimensions`：0.1 的 APK 有、源码缺；本项目用 `TextureInfo` 缓存宽高实现等价功能。

---

## 已知问题

| 项 | 状态 |
|---|---|
| 颜色矩阵 `cm`、双三次插值 | 未实现（**0.1 也没有**，属 Comet 分支历史欠账） |
| `UnityDrawContext` | 已实现 `IDrawContext` 但无调用方，死代码 |
| 首次绘制延迟 | 按需解码，首次显示某图有几十毫秒延迟 |
| 长时游玩稳定性 | 未压测（纹理 60/120/300 秒无引用即回收） |
| 游戏兼容性 | 仅 2 个发行包回归，其他 era 游戏未测 |
| App 内改配置 | 继承上游，不支持 |

未验证项清单见 [docs/验证记录.md](docs/验证记录.md)。

---

## 构建

- Unity **2022.3.62f3c1**（含 Android Build Support / IL2CPP / NDK）、Android SDK、JDK 17
- **工程路径不能含非 ASCII 字符**，否则报 `Invalid project path ... non-ASCII characters`

```bash
Unity.exe -batchmode -quit -projectPath "<工程路径>" -buildTarget Android \
  -executeMethod CometBuild.BuildAndroid -logFile "<日志路径>"
```

产物：`<工程根目录>/Builds/uEmuera-Comet-0.1.apk`，日志含 `[CometBuild] 结果=Succeeded` 即成功。
编辑器内构建：`File → Build Settings → Android → Build`。

---

## 运行

1. 游戏脚本（`*.csv` / `*.ERB` / `*.ERH`）必须是 **UTF-8**；
2. 首次启动允许「文件访问」权限；
3. 游戏文件夹放到 `storage/emulated/0/emuera/`（也支持 `/1/`、`/2/`）；
4. 若有 `emuera.config`，「サブディレクトリを検索する」建议为 `YES`；文件缺失时本版本自动开启。

---

## 目录

```
Assets/Editor/CometBuild.cs          命令行构建入口（0.2 新增）
Assets/Scripts/SpriteManager.cs      纹理：按需解码、像素缓存、回收
Assets/Scripts/uEmuera/ImageHeader.cs  图片头解析（0.2 新增）
Assets/Scripts/Emuera/Config/Config.cs 文件枚举（忽略大小写匹配）
docs/源码恢复说明.md                   重建过程与存疑点
docs/验证记录.md                       实测数据与未验证项
CHANGELOG.md
```

---

## 授权

- 基于 **xerysherry/uEmuera v0.2.9d**（**Apache-2.0**）修改，上游为 emuera1824v15。作者：**xerysherry**；Emuera 解释器作者：**MinorShift** 等。
- Apache-2.0 已授予修改与再分发权利。Emuera 原版授权（`Assets/StreamingAssets/license@emuera`）要求：不得虚示来源、修改必须明示、不得删除声明 —— 本版本按此三项执行。
- 若权利人认为本版本不妥，请提 Issue，我会立即处理。

## 禁止商用

仅限个人学习、研究与娱乐使用。禁止打包售卖、作为商业平台组成部分或借此间接盈利。

## 免责

本修改与原作者无关，使用本版本导致的任何问题由使用者自行承担。

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
