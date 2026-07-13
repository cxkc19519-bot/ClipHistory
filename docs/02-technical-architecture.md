# 技术架构规范

## 建议技术基线

- 开发语言：C#
- 运行平台：Windows 10/11，64 位
- UI：.NET 8 + WPF
- 架构：MVVM，UI、业务逻辑和数据访问分离
- 本地数据库：SQLite
- SQLite 驱动：Microsoft.Data.Sqlite 8.0.28
- 日志：本地滚动日志，禁止记录完整剪贴板内容
- 测试：xUnit（业务逻辑和数据层）

选择 WPF 是因为其 Windows 集成成熟、离线运行简单，适合系统托盘、全局快捷键和剪贴板监听场景。

## 已验证开发环境

- 操作系统：Windows 10 Pro 22H2，64 位
- .NET SDK：8.0.422
- Windows Desktop Runtime：8.0.28
- MSBuild：17.11.48
- Git：2.54.0.windows.1
- WPF 验证：最小 `net8.0-windows` 项目 Release 构建通过，0 警告、0 错误
- 生命周期验证：通过 `dotnet` 主机启动后，主窗口成功创建并可正常关闭，退出码为 0
- 发布验证：win-x64 自包含单文件 exe 已生成，并通过自动启动/正常退出 smoke-test（退出码 0）
- 安装验证：Inno Setup 当前用户安装包已完成隔离安装、已安装程序启动和静默卸载测试，三项退出码均为 0

仓库根目录已通过 `global.json` 固定 .NET 8 SDK 系列，避免不同开发环境意外使用不兼容的 SDK。

当前 SDK 由官方脚本安装在用户目录，未完成系统级运行时注册，因此开发时使用 `dotnet` 主机运行。发布版本必须使用正式安装程序或自带运行时的发布方式，确保普通用户无需配置 `DOTNET_ROOT`。

## 建议项目结构

```text
src/
  ClipHistory.App/             WPF 界面和应用生命周期
  ClipHistory.Core/            领域模型与业务规则
  ClipHistory.Infrastructure/  SQLite、文件存储和 Windows 集成
tests/
  ClipHistory.Core.Tests/
  ClipHistory.Infrastructure.Tests/
docs/
dev-logs/
scripts/
```

## 模块边界

- Clipboard Monitor：接收 Windows 剪贴板变化通知，不使用高频轮询。
- Content Normalizer：读取并规范化文字、图片和文件路径。
- Deduplication：生成内容指纹，判断重复并更新时间。
- History Service：负责查询、置顶、删除、撤销和过期清理。
- Storage：SQLite 保存元数据；图片使用应用数据目录中的独立文件保存。
- Windows Integration：托盘、全局快捷键和开机启动。
- Presentation：ViewModel、筛选、搜索、本地化和界面状态。

## 数据模型基线

历史记录至少包含：

- `Id`：唯一标识
- `ContentType`：Text、Image、Files
- `TextContent`：文字内容，可空
- `ImageRelativePath`：应用私有图片路径，可空
- `FilePaths`：文件路径集合，可空
- `ContentHash`：去重指纹
- `CreatedAtUtc`：首次记录时间
- `LastCopiedAtUtc`：最近复制时间
- `IsPinned`：是否置顶
- `RetentionBaseAtUtc`：过期计算起点

数据库保存 UTC 时间，界面按本地时区显示。

### 置顶状态规则

- `Pin` 和 `Unpin` 是幂等操作；重复执行相同状态不会产生额外更新时间。
- 置顶时保留原保存期限起点，因为置顶记录不参与过期判断。
- 只有真正从置顶变为普通记录时，才把保存期限起点重置为取消置顶的 UTC 时间。
- 取消置顶时间不得早于该记录最近一次复制时间。

### 重复复制与展示排序

- 再次复制同一内容时更新 `LastCopiedAtUtc`，不创建新实体。
- 普通记录再次复制时，同时把 `RetentionBaseAtUtc` 更新为本次复制时间。
- 置顶记录再次复制时保留原期限起点，因为它不参与过期清理。
- 展示顺序首先按是否置顶分组，置顶项始终在普通项之前。
- 每组按最近复制时间倒序；时间相同时依次按创建时间倒序和 ID 升序，避免列表顺序抖动。

### 保存期限边界

- 普通记录的到期时间为 `RetentionBaseAtUtc + RetentionPeriod`。
- 当前 UTC 时间等于或晚于到期时间时，记录视为过期；到期前最后一个 tick 仍未过期。
- 置顶记录永不过期，但仍验证保存期限配置只能是 1、3、5 天。
- 核心层只负责判断，不直接删除数据库记录或图片文件。

## 关键实现规则

- 使用 Windows `AddClipboardFormatListener` 监听变化。
- 程序自己将历史内容放回剪贴板时，必须避免产生错误的重复处理或监听循环。
- 文字指纹基于完整、原样文字；不得擅自去除空格或改变大小写。
- 图片指纹基于规范化后的像素数据或稳定编码结果。
- 文件指纹基于规范化路径集合；路径比较遵循 Windows 不区分大小写的规则。
- 自动清理仅删除应用数据库记录及应用自己的图片文件，绝不删除原始文件。
- SQLite 迁移必须可追踪；不得在升级时直接丢弃用户数据库。

### 内容指纹 V1

- 使用 SHA-256，并为文字、图片和文件使用不同的指纹域，避免跨类型碰撞。
- 文字按 UTF-8 原样计算；大小写、空格和换行差异都视为不同内容。
- 图片进入核心层前必须转换为紧密排列的 BGRA32 像素；指纹同时包含宽度、高度和像素数据。
- 文件路径必须是完整 Windows 路径。计算前统一斜杠、忽略大小写、去除非根目录末尾分隔符、去重并排序。
- 多文件选择顺序不同但路径集合相同时，视为相同内容。
- 指纹只用于本地去重，不作为密码学身份或安全认证依据。

## 数据位置

生产数据应放在 `%LOCALAPPDATA%/ClipHistory/`，而不是安装目录。建议包含：

```text
ClipHistory/
  data/history.db
  images/
  logs/
  settings.json
```

## SQLite 初始结构（版本 1）

- `SchemaInfo`：单行保存当前数据库版本，初始化时拒绝未知版本。
- `HistoryItems`：保存历史记录元数据、文字内容和应用私有图片相对路径。
- `HistoryItemFiles`：按位置保存文件路径，通过外键关联历史记录。
- `(ContentType, ContentHash)` 唯一索引从数据库层阻止重复记录。
- 展示顺序和期限清理字段分别建立索引。
- 删除历史记录时，关联文件路径行通过外键级联删除；这只删除路径记录，不删除原始文件。
- 初始建表在单个事务中执行，并允许同一连接安全地重复初始化。
- SQLite 时间值统一保存为带 UTC 偏移的往返格式文本，避免本地时区歧义。

### 仓储写入与读取规则

- 历史记录主行与多文件路径必须在同一事务中写入，任一文件路径失败则整体回滚。
- 所有值使用 SQL 参数，不拼接用户复制的文字或文件路径。
- `Guid` 使用标准 `D` 格式，UTC 时间使用往返 `O` 格式。
- 文件路径按 `Position` 保存和读取，保持用户复制时的原始顺序。
- 从数据库读取后重新构造 `HistoryItem`，再次执行核心层数据约束；异常数据不得静默进入应用。
- 图片阶段只保存应用私有相对路径，实际图片文件写入在后续独立步骤完成。
- 重复内容使用 `(ContentType, ContentHash)` 查找，两个字段必须同时匹配。
- 状态更新只允许修改 `LastCopiedAtUtc`、`RetentionBaseAtUtc` 和 `IsPinned`，不得改变内容载荷或指纹。
- 更新不存在的 ID 时返回“未更新”，不自动插入新记录，避免调用错误掩盖数据状态。
- 全量历史查询固定使用两次数据库读取：一次读取已排序主记录，一次批量读取全部文件路径，避免随记录数量增加产生 N+1 查询。
- 全量查询顺序与核心规则一致：置顶优先、最近复制时间倒序、创建时间倒序、ID 升序。
- 多文件路径按 `Position` 批量组装；缺失或损坏的文件载荷会在重新构造核心模型时被拒绝。
- 删除只针对 `HistoryItems` 主行；SQLite 外键约束始终启用，并自动清理关联的 `HistoryItemFiles` 路径行。
- 数据库删除绝不调用文件系统删除用户原始文件，也不在此步骤删除应用图片文件。
- 单条删除前，界面/应用层保留完整 `HistoryItem` 快照；撤销时用同一快照重新写入，恢复 ID、时间、置顶和载荷。
- 删除不存在的 ID 返回 false，便于调用方准确判断结果。
