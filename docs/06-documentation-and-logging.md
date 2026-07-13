# 文档与开发日志规范

## 文档维护

- 产品行为变化：更新 `01-product-requirements.md`。
- 技术选型、模块或数据结构变化：更新 `02-technical-architecture.md`。
- 视觉或交互变化：更新 `03-ui-ux-guidelines.md`。
- 阶段顺序或完成状态变化：更新 `04-development-plan.md`。
- 测试、安全规则变化：更新 `05-quality-security-testing.md`。
- 文档修改必须在当天日志中说明原因。

## 每日日志

- 路径：`dev-logs/YYYY-MM-DD.md`。
- 只有当天发生实际项目工作时才创建，不使用系统计划任务制造空日志。
- 开发开始时由脚本自动创建标准模板。
- 开发结束前记录完成事项、验证结果、问题和下一步待办。
- 日志只记录项目事实，不记录密码、令牌或剪贴板隐私内容。

## 日志自动化

使用 PowerShell：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/Update-DevLog.ps1 -Start
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/Update-DevLog.ps1 -Completed "完成事项" -Todo "下一步事项"
```

`ExecutionPolicy Bypass` 仅应用于这个 PowerShell 子进程，不修改电脑的全局执行策略。脚本按本机日期选择日志文件；如果文件已存在则追加，不覆盖已有记录。Codex 每次开发会话必须遵循 `AGENTS.md` 中的日志流程。
