# Changelog

本项目遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/) 风格，版本号与 APK 的 `versionName` 对应。

> 说明：本仓库不是原始 APK 的逐字节源码副本，而是由编辑器历史快照 + 上游仓库重建而来。重建过程见 [docs/源码恢复说明.md](docs/源码恢复说明.md)。

---

## [0.2] - 2026-10-27

**主题：让 Android 上真正跑得起来。** 全部修复均基于实机 `logcat` 与 `emuera.log` 定位，每条都有可复现的证据。

### 修复

#### 1. 资源加载线程违规 —— 31 个 CSV 全失败、图片全部不显示

- **现象**：`[LoadContents] done: 31 CSVs, 0 sprites, 31 failed`，logcat 中 33 次 `GetDataWidth can only be called from the main thread`。
- **根因**：`BitmapTexture` 构造函数运行在 Emuera 的后台加载线程上，却直接读 `textureinfo.texture.width/height`。该属性最终调用 Unity 的 `GetDataWidth/GetDataHeight`，带主线程检查，在 worker 线程上抛异常，导致整个资源 CSV 加载中止。
- **改动**：改读 `TextureInfo` 在主线程构造时缓存下来的 `_w/_h`（`Assets/Scripts/uEmuera/Drawing.cs`）。
- **证据**：修复后 `[LoadContents] done: 31 CSVs, 10142 sprites, 0 failed`。
- 提交：`47314f8`

#### 2. ERH 双重加载 —— 启动即终止

- **现象**：`emuera.log` 中 ERH 被加载 106 次（实际 90 个文件），`警告Lv2` 报 `DT_VAGINA` / `CHAR_PER_LINE` 等 `#DIM` 重复定义，最后输出 `ERHの読み込み中にエラーが発生したため処理を終了しました`。
- **根因**：上游为 Android 追加的「小写扩展名重扫」在大小写不敏感的文件系统上会把同一批文件命中两次；`analyzeSharpDimLines()` 中重复的 `#DIM` 触发 `CodeEE`，`Warn(..., 2)` 使 `noError = false`，`LoadHeaderFiles` 返回 false → 进程退出。
- **改动**：删除 5 处 `#if (UNITY_ANDROID || UNITY_IOS)` 小写重扫（`HeaderFileLoader.cs`、`ErbLoader.cs`、`ConstantData.cs`、`AppContents.cs`、`Program.cs`），与 v24 的写法对齐。共 1 增 26 删。
- 提交：`f9e7a48`

> 注：早期曾尝试「保留重扫 + 路径去重」（`e7a5e20`），实测只挡住 74/90，仍有 16 个文件重复，已整体回退（`2a98d85`）。

#### 3. 内存溢出 —— 6 GB 设备被系统杀掉

- **现象**：启动约 45 秒后 `lowmemorykiller: Kill 'com.uemuera.comet' ... to free 3652428kB rss, 1768392kB swap`；设备 `MemTotal` 仅 4 GB / 6 GB，进程峰值需求约 5.4 GB。
- **根因**：`LoadContents` 一次性把 2540 张图（13.9 亿像素）全部解码为 RGBA32，纹理 5.18 GB + `pixelCache` 5.18 GB ≈ 10.4 GB。降采样也无法解决（上限 640 px 仍为 2.38 GB/份）。
- **改动**：
  - 新增 `Assets/Scripts/uEmuera/ImageHeader.cs`：仅解析 PNG / WebP（VP8X / VP8L / VP8）/ JPEG / BMP / GIF 文件头取宽高，零像素内存；完整解码推迟到首次绘制。
  - `TextureInfo` 去掉构造时的 `GetPixels32()`，改为 `EnsurePixelCache()` 按需构建（内部派发到主线程），空闲 20 秒自动释放。
  - 纹理回收阈值 120/300/600 秒 → 60/120/300 秒，扫描间隔 15 秒 → 5 秒。
  - 关闭逐图 `Debug.Log`（原为每图 5 行，万级资源会灌满 logcat）。
- **证据**：Native Heap 3.65 GB + 1.77 GB swap → 22~42 MB；进程存活 90 秒以上；2540 张图文件头解析 0 失败。
- 提交：`dc2bce1`

#### 4. 配置缺失导致子目录不被搜索 —— eraTW 类游戏满屏「解釈できない識別子」

- **现象**：`ERBの解析中に解釈不可能な行があるためEmueraを終了します`，报 `DATE_HITOGOMI` / `TRAP_POINT` / `ANIMALNAME` 等未定义。
- **根因**：`サブディレクトリを検索する` 的来源只有游戏目录下的 `emuera.config`，v15/v24 的默认值均为 **NO**。该类游戏顶层只有 3/94 个 ERH、23/3903 个 ERB，其余全在子目录；手机用户无法方便地编辑该文件。
- **改动**：仅在 `emuera.config` **不存在或读取失败**时，把 `SearchSubdirectory` 兜底为 `true`；文件存在且可解析时行为与 v24 完全一致，不覆盖用户设置。同时输出 `[Config]` 诊断日志。
- **证据**：模拟器上移除 `emuera.config` 后，`[Config] exists=False loaded=False SearchSubdirectory=True`，加载通过、无致命错误；恢复文件后行为不变。
- 提交：`34a2b04`

#### 5. 文件名大小写导致角色未定义 —— 满屏 `警告Lv2:定義していないキャラクタ変数`

- **现象**：进入标题画面后持续刷 `PRINTL %C%CSTR:(129:0)%` 一类警告，角色编号 129/130/133/134/139/140/142/151/154/160… 全部未定义。
- **根因**：游戏包的角色文件名为 `Chara0.csv`、`Chara1 霊夢.csv`（**C 大写、csv 小写**，共 163 个）。代码按 `CHARA*.CSV` 严格匹配，在**大小写敏感**的 Android 文件系统上命中 **0 个**；模拟器因文件系统大小写不敏感（casefold）而恰好通过，掩盖了问题。
- **改动**：`Config.getFiles()` 改为「单次 `Directory.GetFiles(dir, "*")` 枚举 + 自写忽略大小写正则匹配」，与 Windows/v24 语义等价，且结构上不可能重复命中（所以第 2 条删掉的重扫不需要回来）。同时给目录/文件枚举加 `try/catch`，个别子目录读取失败不再中止整个扫描；新增 `[GetFiles] root/pattern/count` 日志。
- **证据**：`find . -type f -name "CHARA*.CSV"` 在游戏包中返回 **0**，`-iname` 返回 163。
- 提交：`c71caf7`

#### 6. GXX 画布管线（0.1 遗留）

- `GCREATE` 后画布未标脏 → `ApplyPixelBuffer` 不执行 → `Bitmap` 为 null → `SPRITECREATE` 解析失败。改为创建即置脏。`cae93e5`
- `GSAVE` 前未上传像素缓冲 → 存出空图。补 `ApplyPixelBuffer()`。`1986600`
- 脏画布未在每帧刷新 → 显示不更新。`EmueraContent.Update()` 开头调用 `FlushDirtyGraphics()`。`452f8bc`

#### 7. 构建与图标

- 新增 `Assets/Editor/CometBuild.cs` 命令行构建入口，自动注册 `Assets/Main.unity`。`307c639`
- 从 APK 还原彗星图标（192×192）到 `Assets/splash/icon.png`（原始 512×512 已丢失）。`e1e9ab3`
- `bundleVersion` 设为 `0.1`（包名 `com.uemuera.comet` / versionCode 16 已正确）。`8295784`

### 变更（行为层面）

| 项 | 0.1 | 0.2 |
|---|---|---|
| 图片解码时机 | 启动时全部解码 | 首次绘制时按需解码 |
| 像素缓存 | 常驻 | 按需构建，空闲 20 秒释放 |
| 纹理回收 | 120/300/600 秒 | 60/120/300 秒 |
| 无 `emuera.config` 时 | 不搜索子目录 | 搜索子目录 |
| 文件名匹配 | 依赖文件系统大小写行为 | 忽略大小写（全平台一致） |
| 逐图调试日志 | 每图 5 行 | 关闭 |
| 包类型 | Development Build | Release |

### 已知缺口

- `ColorMatrix` / `SetColorMatrix` / `InterpolationMode` / `HighQualityBicubic`：**0.1 与本版本都缺**，属 Comet 分支历史欠账。
- `BitmapTexture.ReadDimensions`：0.1 的 APK 有该成员、源码缺失，本仓库用 `TextureInfo` 缓存宽高实现等价功能。
- `UnityDrawContext` 实现了 `IDrawContext` 但无调用方，为未接线的实验性后端。
- 仅 2 个游戏包做过回归；长时游玩（>1 小时）内存稳定性未压测。

---

## [0.1] - 2026-05

首个 Comet 修改版（原始工程已丢失，以下内容依据 Trae 历史快照整理）。

### 新增

1. **GXX 全指令软件光栅化**：`GCREATE` / `GCLEAR` / `GFILLRECTANGLE` / `GDRAWSPRITE` / `GDRAWG` / `GDRAWGWITHMASK` / `GSETCOLOR` / `GGETCOLOR` 由空实现改为基于 CPU 端 `Color32` 缓冲，经 `ApplyPixelBuffer()` 上传 `Texture2D`。
2. **线程化纹理加载**：新增主线程调度（`IsMainThread` / `RunOnMainThread` / `ProcessMainThreadActions`），异步加载同步原语由 `Mutex` 改为 `ManualResetEvent`。
3. **WebP 直接解码为像素**：新增 `DecodeWebPToPixels`，并提供可读纹理副本 `MakeReadable`。
4. **BitmapPixelBuffer 直挂寻址**：`GCREATE` 产生的画布以 `:GRAPHICS:{ID}` 直接注册进纹理字典，不再走文件名查找。
5. **按钮位图像素缓存**：新增 `CacheButtonMapPixels` / `GetButtonMapPixel`，CBG 点击判定不再逐像素回读 GPU。

### 未实现

1. 颜色矩阵参数 `cm` 接收后丢弃；
2. 缩放为最近邻采样（上游 v24 为双三次插值）；
3. `UnityDrawContext` 未接线（见上）。
