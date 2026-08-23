# Changelog

本项目的重要变更都会记录在此文件中。格式参考 Keep a Changelog，版本号遵循 Semantic Versioning。

## [Unreleased]

### Changed

- 配置文件改为支持注释和尾随逗号的 `config.jsonc`，默认模板内置字段说明。
- 优化窗口查找和启动状态管理，减少频繁使用时的临时分配，并及时释放进程与 GDI 资源。

### Removed

- 移除图形化「设置快捷键」窗口及托盘菜单入口，改为直接编辑配置文件。

## [1.0.4] - 2026-08-23

### Added

- 新增图形化快捷键设置窗口，支持添加应用、录制、移除和拖拽排序。
- 新增 GitHub Actions，自动构建安装包与便携版并发布 GitHub Release。
- 新增版本递增、Changelog 归档、发布包构建和 SHA-256 校验脚本。

## [1.0.3] - 2026-08-12

### Changed

- 配置文件改为 exe 同目录的 `config.json`。
- 托盘菜单显示应用版本号。
