/*
 Ginkgo 数据库初始化脚本（MySQL 5.7+ 版 - Snowflake ID）

 - 本脚本用于初始化 Ginkgo 框架的数据库结构、索引、约束以及基础数据
 - 兼容 MySQL 5.7+，使用 utf8mb4 字符集和 utf8mb4_unicode_ci 排序规则
 - 所有主键和外键使用 BIGINT 类型（Snowflake ID）
 - 管理员账户不会在此脚本中预置：安装向导将单独创建初始管理员
 - 若需 SQL Server 版本，请使用 resource/mssql_install_snowflake.sql
 - 请在执行前备份现有数据，生产环境建议走变更评审流程
 
 Snowflake ID 说明：
 - 64位整数，索引性能最优
 - 时间有序，B+树插入效率高
 - 支持每毫秒 4096 个 ID
 - 支持 1024 个节点
 - 基础时间：2024-01-01 00:00:00 UTC
 
 MySQL 5.7 兼容性说明：
 - 使用 JSON 类型存储 JSON 数据（MySQL 5.7 原生支持）
 - 移除所有 DATETIME 字段的 DEFAULT CURRENT_TIMESTAMP 表达式，由应用层生成
 - 移除所有 CHECK 约束（MySQL 5.7 不支持）
 - 对超过 191 字符的 VARCHAR 字段使用前缀索引

 生成日期：2026-04-18
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;



-- ============================================================================
-- Module Management Tables
-- ============================================================================

-- ----------------------------
-- Table structure for ginkgo_Client_ModuleStatus
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Client_ModuleStatus`;
CREATE TABLE `ginkgo_Client_ModuleStatus` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `ClientId` VARCHAR(64) NOT NULL COMMENT '客户端唯一标识（Agent 上报）',
  `ModuleName` VARCHAR(128) NOT NULL COMMENT '模块名称',
  `Version` VARCHAR(32) NOT NULL COMMENT '模块版本（semver）',
  `Status` VARCHAR(32) NOT NULL COMMENT '状态：pending/installed/failed',
  `UpdatedAt` DATETIME(6) NOT NULL COMMENT '状态更新时间（UTC,应用层生成）',
  `Error` VARCHAR(1000) NULL COMMENT '错误信息（当失败时）',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='客户端模块状态';


-- ----------------------------
-- Table structure for ginkgo_Mod_Migrations
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Mod_Migrations`;
CREATE TABLE `ginkgo_Mod_Migrations` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `ModuleName` VARCHAR(128) NOT NULL COMMENT '模块名称',
  `ScriptName` VARCHAR(200) NOT NULL COMMENT '脚本文件名',
  `Hash` VARCHAR(64) NOT NULL COMMENT '脚本内容哈希（避免重复执行）',
  `ExecutedAt` DATETIME(6) NOT NULL COMMENT '执行时间（UTC,应用层生成）',
  `ExecutedBy` BIGINT NULL COMMENT '执行人用户ID(Snowflake ID)',
  `Success` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否成功',
  `Message` VARCHAR(1000) NULL COMMENT '执行消息（失败原因等）',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='模块 SQL 执行记录';


-- ----------------------------
-- Table structure for ginkgo_Modules
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Modules`;
CREATE TABLE `ginkgo_Modules` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `Name` VARCHAR(128) NOT NULL COMMENT '模块名称',
  `Version` VARCHAR(32) NOT NULL COMMENT '模块版本（semver）',
  `HasClient` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否包含客户端组件',
  `HasPages` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否包含页面/路由',
  `Enabled` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否启用（软禁用时为0）',
  `InstalledAt` DATETIME(6) NOT NULL COMMENT '安装时间（UTC,应用层生成）',
  `InstalledBy` BIGINT NULL COMMENT '安装人用户ID(Snowflake ID)',
  `ExtraJson` JSON NULL COMMENT '扩展信息（JSON）',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='已安装模块清单';


-- ----------------------------
-- Table structure for ginkgo_Modules_Installed
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Modules_Installed`;
CREATE TABLE `ginkgo_Modules_Installed` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `ModuleId` VARCHAR(200) NOT NULL COMMENT '模块标识',
  `Name` VARCHAR(200) NOT NULL COMMENT '模块名称',
  `Version` VARCHAR(50) NOT NULL COMMENT '模块版本',
  `HasClient` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否包含客户端',
  `Publisher` VARCHAR(200) NULL COMMENT '发布者',
  `Homepage` VARCHAR(500) NULL COMMENT '主页URL',
  `InstalledAtUtc` DATETIME(6) NOT NULL COMMENT '安装时间（UTC,应用层生成）',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间（UTC,应用层生成）',
  `CreatedBy` BIGINT NULL COMMENT '创建人ID',
  `ModifiedAt` DATETIME(6) NULL COMMENT '修改时间（UTC,应用层生成）',
  `ModifiedBy` BIGINT NULL COMMENT '修改人ID',
  `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否删除',
  `UpdatedAt` DATETIME(6) NULL COMMENT '更新时间（UTC,应用层生成）',
  `UpdatedBy` BIGINT NULL COMMENT '更新人ID',
  `Enabled` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否启用',
  `MenuRootCode` VARCHAR(200) NULL COMMENT '插件菜单根编码',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='已安装模块记录';



-- ============================================================================
-- Core System Tables (ginkgo_Sys_*) - Part 1
-- ============================================================================

-- ----------------------------
-- Table structure for ginkgo_Sys_Department
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_Department`;
CREATE TABLE `ginkgo_Sys_Department` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `ParentId` BIGINT NULL COMMENT '父部门ID',
  `Name` VARCHAR(128) NOT NULL COMMENT '部门名称',
  `Code` VARCHAR(64) NULL COMMENT '部门编码',
  `LeaderUserId` BIGINT NULL COMMENT '负责人用户ID',
  `OrderNo` INT NOT NULL DEFAULT 0 COMMENT '排序号',
  `Enabled` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否启用',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间（UTC,应用层生成）',
  `CreatedBy` BIGINT NULL COMMENT '创建人ID',
  `UpdatedAt` DATETIME(6) NULL COMMENT '更新时间（UTC,应用层生成）',
  `UpdatedBy` BIGINT NULL COMMENT '更新人ID',
  `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否删除',
  `DeletedAt` DATETIME(6) NULL COMMENT '删除时间',
  `DeletedBy` BIGINT NULL COMMENT '删除人ID',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='部门表';


-- ----------------------------
-- Table structure for ginkgo_Sys_DepartmentClosure
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_DepartmentClosure`;
CREATE TABLE `ginkgo_Sys_DepartmentClosure` (
  `AncestorId` BIGINT NOT NULL COMMENT '祖先部门ID',
  `DescendantId` BIGINT NOT NULL COMMENT '后代部门ID',
  `Depth` INT NOT NULL COMMENT '深度',
  PRIMARY KEY (`AncestorId`, `DescendantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='部门闭包表';


-- ----------------------------
-- Table structure for ginkgo_Sys_Dictionary
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_Dictionary`;
CREATE TABLE `ginkgo_Sys_Dictionary` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `Module` VARCHAR(64) NOT NULL DEFAULT 'sys' COMMENT '所属模块标识（sys=系统级，其他为插件ModuleId）',
  `Code` VARCHAR(50) NOT NULL COMMENT '字典编码',
  `Name` VARCHAR(100) NOT NULL COMMENT '字典名称',
  `NameI18n` JSON NULL COMMENT '字典名称-多语言',
  `Description` VARCHAR(500) NULL COMMENT '描述',
  `DescriptionI18n` JSON NULL COMMENT '字典描述-多语言',
  `Category` VARCHAR(50) NOT NULL DEFAULT 'STATIC' COMMENT '分类',
  `SourceType` VARCHAR(50) NOT NULL DEFAULT 'Static' COMMENT '来源类型',
  `ExtraJson` JSON NULL COMMENT '扩展JSON',
  `IsSystem` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否系统字典',
  `CreatedAt` DATETIME NOT NULL COMMENT '创建时间（UTC,应用层生成）',
  `UpdatedAt` DATETIME(6) NULL COMMENT '更新时间（UTC,应用层生成）',
  `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否删除',
  `CreatedBy` BIGINT NULL COMMENT '创建人ID',
  `UpdatedBy` BIGINT NULL COMMENT '更新人ID',
  `DeletedAt` DATETIME(6) NULL COMMENT '删除时间',
  `DeletedBy` BIGINT NULL COMMENT '删除人ID',
  `Enabled` TINYINT(1) DEFAULT 1 COMMENT '是否启用',
  PRIMARY KEY (`Id`),
  KEY `IX_Dictionary_Module` (`Module`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='字典类型/分类表';


-- ----------------------------
-- Table structure for ginkgo_Sys_DictionaryItem
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_DictionaryItem`;
CREATE TABLE `ginkgo_Sys_DictionaryItem` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `Module` VARCHAR(64) NOT NULL DEFAULT 'sys' COMMENT '所属模块标识（sys=系统级，其他为插件ModuleId）',
  `DictId` BIGINT NOT NULL COMMENT '字典ID',
  `ParentId` BIGINT NULL COMMENT '父项ID',
  `Code` VARCHAR(50) NOT NULL COMMENT '项编码',
  `Value` VARCHAR(200) NOT NULL COMMENT '项值',
  `ValueI18n` JSON NULL COMMENT '条目值-多语言',
  `SortOrder` INT NOT NULL DEFAULT 0 COMMENT '排序号',
  `IsActive` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否激活',
  `ExtraJson` JSON NULL COMMENT '扩展JSON',
  `CreatedAt` DATETIME NOT NULL COMMENT '创建时间（UTC,应用层生成）',
  `UpdatedAt` DATETIME(6) NULL COMMENT '更新时间（UTC,应用层生成）',
  `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否删除',
  `CreatedBy` BIGINT NULL COMMENT '创建人ID',
  `UpdatedBy` BIGINT NULL COMMENT '更新人ID',
  `DeletedAt` DATETIME(6) NULL COMMENT '删除时间',
  `DeletedBy` BIGINT NULL COMMENT '删除人ID',
  PRIMARY KEY (`Id`),
  KEY `IX_DictionaryItem_Module` (`Module`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='字典项表';


-- ----------------------------
-- Table structure for ginkgo_Sys_File
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_File`;
CREATE TABLE `ginkgo_Sys_File` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `FileName` VARCHAR(260) NOT NULL COMMENT '文件名',
  `ContentType` VARCHAR(128) NULL COMMENT '内容类型',
  `Size` BIGINT NOT NULL COMMENT '文件大小',
  `Hash` VARCHAR(128) NULL COMMENT '文件哈希',
  `StorageProvider` VARCHAR(64) NOT NULL COMMENT '存储提供者',
  `StoragePath` VARCHAR(512) NOT NULL COMMENT '存储路径',
  `Url` VARCHAR(1024) NULL COMMENT '访问URL',
  `OwnerId` BIGINT NULL COMMENT '所有者ID',
  `Tags` VARCHAR(256) NULL COMMENT '标签',
  `Version` INT NOT NULL DEFAULT 1 COMMENT '版本号',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间（UTC,应用层生成）',
  `CreatedBy` BIGINT NULL COMMENT '创建人ID',
  `UpdatedAt` DATETIME(6) NULL COMMENT '更新时间（UTC,应用层生成）',
  `UpdatedBy` BIGINT NULL COMMENT '更新人ID',
  `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否删除',
  `DepartmentId` BIGINT NULL COMMENT '部门ID',
  `type` VARCHAR(255) NULL COMMENT '文件类型',
  `DeletedAt` DATETIME(6) NULL COMMENT '删除时间',
  `DeletedBy` BIGINT NULL COMMENT '删除人ID',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='文件表';


-- ----------------------------
-- Table structure for ginkgo_Sys_LoginLog
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_LoginLog`;
CREATE TABLE `ginkgo_Sys_LoginLog` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `UserId` BIGINT NULL COMMENT '用户ID',
  `UserName` VARCHAR(64) NOT NULL COMMENT '用户名',
  `Success` TINYINT(1) NOT NULL COMMENT '是否成功',
  `Reason` VARCHAR(256) NULL COMMENT '原因',
  `Ip` VARCHAR(64) NULL COMMENT 'IP地址',
  `UserAgent` VARCHAR(256) NULL COMMENT '用户代理',
  `At` DATETIME(6) NOT NULL COMMENT '登录时间（UTC,应用层生成）',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间（UTC,应用层生成）',
  `CreatedBy` BIGINT NULL COMMENT '创建人ID',
  `UpdatedAt` DATETIME(6) NULL COMMENT '更新时间（UTC,应用层生成）',
  `UpdatedBy` BIGINT NULL COMMENT '更新人ID',
  `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否删除',
  `DeletedAt` DATETIME(6) NULL COMMENT '删除时间',
  `DeletedBy` BIGINT NULL COMMENT '删除人ID',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='登录日志';



-- ============================================================================
-- Menu System Tables
-- ============================================================================

-- ----------------------------
-- Table structure for ginkgo_Sys_Menu
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_Menu`;
CREATE TABLE `ginkgo_Sys_Menu` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `Module` VARCHAR(64) NOT NULL DEFAULT 'sys' COMMENT '所属模块标识（sys=系统级，其他为插件ModuleId）',
  `ParentId` BIGINT NULL COMMENT '父菜单ID',
  `Name` VARCHAR(128) NOT NULL COMMENT '菜单名称',
  `NameI18n` JSON NULL COMMENT '菜单名称-多语言 {"zh-CN":"系统管理","en":"System"}',
  `Route` VARCHAR(256) NULL COMMENT '路由',
  `Icon` VARCHAR(64) NULL COMMENT '图标',
  `OrderNo` INT NOT NULL DEFAULT 0 COMMENT '排序号',
  `Visible` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否可见',
  `PermissionCode` VARCHAR(128) NULL COMMENT '权限编码',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间（UTC,应用层生成）',
  `CreatedBy` BIGINT NULL COMMENT '创建人ID',
  `UpdatedAt` DATETIME(6) NULL COMMENT '更新时间（UTC,应用层生成）',
  `UpdatedBy` BIGINT NULL COMMENT '更新人ID',
  `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否删除',
  `Type` VARCHAR(16) NOT NULL DEFAULT 'Directory' COMMENT '类型：Directory/Menu/Button',
  `ItemMode` VARCHAR(16) NULL COMMENT '项模式',
  `Url` VARCHAR(512) NULL COMMENT '外部URL',
  `Resource` VARCHAR(512) NULL COMMENT '资源标识',
  `Method` VARCHAR(16) NULL COMMENT 'HTTP方法',
  `Code` VARCHAR(256) NULL COMMENT '菜单编码',
  `SupportedClients` VARCHAR(100) NULL COMMENT '支持的客户端',
  `WebUrl` VARCHAR(500) NULL COMMENT 'Web URL',
  `MobileUrl` VARCHAR(500) NULL COMMENT '移动端URL',
  `WpfRouteUrl` VARCHAR(500) NULL COMMENT 'WPF路由URL',
  `WebRouteUrl` VARCHAR(500) NULL COMMENT 'Web路由URL',
  `MobileRouteUrl` VARCHAR(500) NULL COMMENT '移动端路由URL',
  `WpfDisplayMode` VARCHAR(20) NULL COMMENT 'WPF显示模式',
  `WebDisplayMode` VARCHAR(20) NULL COMMENT 'Web显示模式',
  `MobileDisplayMode` VARCHAR(20) NULL COMMENT '移动端显示模式',
  `DeletedAt` DATETIME(6) NULL COMMENT '删除时间',
  `DeletedBy` BIGINT NULL COMMENT '删除人ID',
  PRIMARY KEY (`Id`),
  KEY `IX_Menu_Module` (`Module`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='菜单表';


-- ----------------------------
-- Table structure for ginkgo_Sys_MenuGroup
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_MenuGroup`;
CREATE TABLE `ginkgo_Sys_MenuGroup` (
  `Id` BIGINT NOT NULL COMMENT '主键（Snowflake ID）',
  `Name` VARCHAR(64) NOT NULL COMMENT '菜单组名称',
  `Slug` VARCHAR(64) NOT NULL COMMENT '唯一标识（程序调用用，如 frontend-nav）',
  `Description` VARCHAR(256) NULL COMMENT '描述说明',
  `Location` VARCHAR(64) NULL COMMENT '展示位置标识（site-header/mobile-tabbar/site-footer）',
  `ClientType` VARCHAR(64) NULL COMMENT '适用终端（WEB_ADMIN/WEB_PORTAL/WPF/UNIAPP，逗号分隔）',
  `IsSystem` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否系统内置（不可删除）',
  `Enabled` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否启用',
  `MaxDepth` INT NOT NULL DEFAULT 3 COMMENT '最大嵌套层级（0=不限）',
  `Version` VARCHAR(32) NULL COMMENT '版本标识（v1/v2/beta，同Location可多版本）',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间',
  `CreatedBy` BIGINT NULL COMMENT '创建人',
  `UpdatedAt` DATETIME(6) NULL COMMENT '更新时间',
  `UpdatedBy` BIGINT NULL COMMENT '更新人',
  `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '软删除',
  `DeletedAt` DATETIME(6) NULL COMMENT '删除时间',
  `DeletedBy` BIGINT NULL COMMENT '删除人',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='菜单组定义表';


-- ----------------------------
-- Table structure for ginkgo_Sys_MenuGroupItem
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_MenuGroupItem`;
CREATE TABLE `ginkgo_Sys_MenuGroupItem` (
  `Id` BIGINT NOT NULL COMMENT '主键（Snowflake ID）',
  `MenuGroupId` BIGINT NOT NULL COMMENT '所属菜单组Id',
  `ParentId` BIGINT NULL COMMENT '父级菜单项Id（树形）',
  `Title` VARCHAR(128) NOT NULL COMMENT '显示标题',
  `TitleI18n` JSON NULL COMMENT '多语言标题',
  `Subtitle` VARCHAR(256) NULL COMMENT '副标题',
  `Icon` VARCHAR(64) NULL COMMENT '图标',
  `Image` VARCHAR(512) NULL COMMENT '图片地址',
  `LinkType` VARCHAR(16) NOT NULL DEFAULT 'Custom' COMMENT '链接类型：Custom/SystemMenu/External',
  `Url` VARCHAR(512) NULL COMMENT '链接地址',
  `Target` VARCHAR(16) NOT NULL DEFAULT '_self' COMMENT '打开方式：_self/_blank',
  `RefMenuId` BIGINT NULL COMMENT '关联系统菜单Id',
  `PermissionCode` VARCHAR(128) NULL COMMENT '权限编码（与系统菜单同体系）',
  `CssClass` VARCHAR(128) NULL COMMENT '自定义CSS类',
  `Badge` VARCHAR(32) NULL COMMENT '角标文字',
  `BadgeType` VARCHAR(16) NULL COMMENT '角标类型',
  `ExtraData` JSON NULL COMMENT '扩展数据',
  `OrderNo` INT NOT NULL DEFAULT 0 COMMENT '排序号',
  `Enabled` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否启用',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间',
  `CreatedBy` BIGINT NULL COMMENT '创建人',
  `UpdatedAt` DATETIME(6) NULL COMMENT '更新时间',
  `UpdatedBy` BIGINT NULL COMMENT '更新人',
  `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '软删除',
  `DeletedAt` DATETIME(6) NULL COMMENT '删除时间',
  `DeletedBy` BIGINT NULL COMMENT '删除人',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='菜单组项表';


-- ----------------------------
-- Table structure for ginkgo_Sys_MenuLog
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_MenuLog`;
CREATE TABLE `ginkgo_Sys_MenuLog` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `Action` VARCHAR(32) NOT NULL COMMENT '动作：scan/install/upgrade/uninstall/refresh',
  `MenuCode` VARCHAR(200) NULL COMMENT '受影响的菜单编码',
  `FromModule` VARCHAR(128) NULL COMMENT '变更来源模块',
  `SqlFile` VARCHAR(255) NULL COMMENT '执行的 SQL 文件名',
  `DeltaJson` JSON NULL COMMENT '变更详情（JSON）',
  `By` BIGINT NULL COMMENT '执行人用户ID',
  `At` DATETIME(6) NOT NULL COMMENT '执行时间（UTC,应用层生成）',
  `Result` VARCHAR(32) NULL COMMENT '执行结果：success/failed',
  `Message` VARCHAR(1000) NULL COMMENT '执行消息/错误信息',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='菜单变更日志';


-- ----------------------------
-- Table structure for ginkgo_Sys_RoleMenuGroup
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_RoleMenuGroup`;
CREATE TABLE `ginkgo_Sys_RoleMenuGroup` (
  `Id` BIGINT NOT NULL COMMENT '主键（Snowflake ID）',
  `RoleId` BIGINT NOT NULL COMMENT '角色Id',
  `MenuGroupId` BIGINT NOT NULL COMMENT '菜单组Id',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间',
  `CreatedBy` BIGINT NULL COMMENT '创建人',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='角色菜单组权限表';



-- ============================================================================
-- Notification System Tables (ginkgo_Sys_Notify*)
-- ============================================================================

-- ----------------------------
-- Table structure for ginkgo_Sys_NotifyMessage
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_NotifyMessage`;
CREATE TABLE `ginkgo_Sys_NotifyMessage` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `Title` VARCHAR(200) NOT NULL COMMENT '标题',
  `TitleI18n` JSON NULL COMMENT '标题-多语言',
  `ContentType` TINYINT NOT NULL DEFAULT 1 COMMENT '内容类型',
  `ContentText` LONGTEXT NULL COMMENT '纯文本内容',
  `ContentHtml` LONGTEXT NULL COMMENT 'HTML内容',
  `IsImportant` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否重要',
  `Priority` TINYINT NOT NULL DEFAULT 1 COMMENT '优先级',
  `SenderId` BIGINT NULL COMMENT '发送人ID',
  `SenderName` VARCHAR(100) NULL COMMENT '发送人名称',
  `ScheduledAt` DATETIME(6) NULL COMMENT '计划发送时间',
  `PublishedAt` DATETIME(6) NULL COMMENT '发布时间',
  `Status` TINYINT NOT NULL DEFAULT 0 COMMENT '状态',
  `TotalRecipients` INT NOT NULL DEFAULT 0 COMMENT '总接收人数',
  `ReadCount` INT NOT NULL DEFAULT 0 COMMENT '已读数',
  `ClickCount` INT NOT NULL DEFAULT 0 COMMENT '点击数',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间（UTC,应用层生成）',
  `CreatedBy` BIGINT NULL COMMENT '创建人ID',
  `UpdatedAt` DATETIME(6) NULL COMMENT '更新时间（UTC,应用层生成）',
  `UpdatedBy` BIGINT NULL COMMENT '更新人ID',
  `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否删除',
  `DeletedAt` DATETIME(6) NULL COMMENT '删除时间',
  `DeletedBy` BIGINT NULL COMMENT '删除人ID',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='系统通知消息';


-- ----------------------------
-- Table structure for ginkgo_Sys_NotifyAttachment
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_NotifyAttachment`;
CREATE TABLE `ginkgo_Sys_NotifyAttachment` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `NotifyId` BIGINT NOT NULL COMMENT '通知ID',
  `FileId` BIGINT NOT NULL COMMENT '文件ID',
  `Name` VARCHAR(200) NULL COMMENT '附件名称',
  `ContentType` VARCHAR(128) NULL COMMENT '内容类型',
  `Size` BIGINT NULL COMMENT '文件大小',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间（UTC,应用层生成）',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='系统通知附件';


-- ----------------------------
-- Table structure for ginkgo_Sys_NotifyAudience
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_NotifyAudience`;
CREATE TABLE `ginkgo_Sys_NotifyAudience` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `NotifyId` BIGINT NOT NULL COMMENT '通知ID',
  `UserId` BIGINT NOT NULL COMMENT '用户ID',
  `UserName` VARCHAR(100) NULL COMMENT '用户名',
  `DeptId` BIGINT NULL COMMENT '部门ID',
  `RoleId` BIGINT NULL COMMENT '角色ID',
  `DeliveryStatus` TINYINT NOT NULL DEFAULT 0 COMMENT '投递状态',
  `DeliveredAt` DATETIME(6) NULL COMMENT '投递时间',
  `ReadAt` DATETIME(6) NULL COMMENT '阅读时间',
  `ClickAt` DATETIME(6) NULL COMMENT '点击时间',
  `LastError` VARCHAR(500) NULL COMMENT '最后错误',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间（UTC,应用层生成）',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='系统通知受众';


-- ----------------------------
-- Table structure for ginkgo_Sys_NotifyDispatch
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_NotifyDispatch`;
CREATE TABLE `ginkgo_Sys_NotifyDispatch` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `NotifyId` BIGINT NOT NULL COMMENT '通知ID',
  `UserId` BIGINT NOT NULL COMMENT '用户ID',
  `Attempt` SMALLINT NOT NULL DEFAULT 0 COMMENT '尝试次数',
  `NextTryAt` DATETIME(6) NOT NULL COMMENT '下次尝试时间',
  `State` TINYINT NOT NULL DEFAULT 0 COMMENT '状态',
  `LastError` VARCHAR(500) NULL COMMENT '最后错误',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间（UTC,应用层生成）',
  `UpdatedAt` DATETIME(6) NULL COMMENT '更新时间（UTC,应用层生成）',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='系统通知分发';



-- ============================================================================
-- Message System Tables
-- ============================================================================

-- ----------------------------
-- Table structure for ginkgo_Sys_Message
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_Message`;
CREATE TABLE `ginkgo_Sys_Message` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `UserId` BIGINT NOT NULL COMMENT '接收用户ID',
  `Title` VARCHAR(200) NOT NULL COMMENT '消息标题',
  `Summary` VARCHAR(500) NULL COMMENT '消息摘要',
  `Content` TEXT NULL COMMENT '消息正文',
  `Type` VARCHAR(50) NOT NULL DEFAULT 'system' COMMENT '消息类型: system/task/notice',
  `IsRead` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否已读',
  `ReadAt` DATETIME(6) NULL COMMENT '阅读时间',
  `DeliveryRole` VARCHAR(20) NOT NULL DEFAULT 'primary' COMMENT '送达角色: primary/cc',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间(UTC,应用层生成)',
  `CreatedBy` BIGINT NULL COMMENT '创建人ID',
  `UpdatedAt` DATETIME(6) NULL COMMENT '更新时间(UTC,应用层生成)',
  `UpdatedBy` BIGINT NULL COMMENT '更新人ID',
  `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否删除(软删)',
  `DeletedAt` DATETIME(6) NULL COMMENT '删除时间',
  `DeletedBy` BIGINT NULL COMMENT '删除人ID',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='系统消息通知表';


-- ----------------------------
-- Table structure for ginkgo_Sys_MessageAttachment
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_MessageAttachment`;
CREATE TABLE `ginkgo_Sys_MessageAttachment` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `MessageId` BIGINT NOT NULL COMMENT '关联消息ID',
  `FileId` BIGINT NOT NULL COMMENT '关联文件ID（SysFile）',
  `FileName` VARCHAR(300) NOT NULL COMMENT '文件名',
  `FileSize` BIGINT NOT NULL DEFAULT 0 COMMENT '文件大小（字节）',
  `AttachmentType` VARCHAR(20) NOT NULL DEFAULT 'file' COMMENT '附件类型: image/file',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间(UTC,应用层生成)',
  `CreatedBy` BIGINT NULL COMMENT '创建人ID',
  `UpdatedAt` DATETIME(6) NULL COMMENT '更新时间(UTC,应用层生成)',
  `UpdatedBy` BIGINT NULL COMMENT '更新人ID',
  `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否删除(软删)',
  `DeletedAt` DATETIME(6) NULL COMMENT '删除时间',
  `DeletedBy` BIGINT NULL COMMENT '删除人ID',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='消息附件表';


-- ----------------------------
-- Table structure for ginkgo_Sys_MessageLink
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_MessageLink`;
CREATE TABLE `ginkgo_Sys_MessageLink` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `MessageId` BIGINT NOT NULL COMMENT '关联消息ID',
  `Title` VARCHAR(200) NOT NULL COMMENT '链接标题',
  `Platform` VARCHAR(20) NOT NULL COMMENT '目标平台: web/wpf/uniapp',
  `Url` VARCHAR(1000) NOT NULL COMMENT '跳转URL（含路径和参数）',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间(UTC,应用层生成)',
  `CreatedBy` BIGINT NULL COMMENT '创建人ID',
  `UpdatedAt` DATETIME(6) NULL COMMENT '更新时间(UTC,应用层生成)',
  `UpdatedBy` BIGINT NULL COMMENT '更新人ID',
  `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否删除(软删)',
  `DeletedAt` DATETIME(6) NULL COMMENT '删除时间',
  `DeletedBy` BIGINT NULL COMMENT '删除人ID',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='消息链接表';



-- ============================================================================
-- User / Role / Auth Core Tables
-- ============================================================================

-- ----------------------------
-- Table structure for ginkgo_Sys_User
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_User`;
CREATE TABLE `ginkgo_Sys_User` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `UserName` VARCHAR(64) NOT NULL COMMENT '用户名',
  `DisplayName` VARCHAR(128) NULL COMMENT '显示名称',
  `PasswordHash` VARCHAR(256) NOT NULL COMMENT '密码哈希',
  `Salt` VARCHAR(128) NULL COMMENT '盐值',
  `Email` VARCHAR(256) NULL COMMENT '邮箱',
  `Phone` VARCHAR(32) NULL COMMENT '电话',
  `Enabled` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否启用',
  `LastLoginAt` DATETIME(6) NULL COMMENT '最后登录时间',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间（UTC,应用层生成）',
  `CreatedBy` BIGINT NULL COMMENT '创建人ID',
  `UpdatedAt` DATETIME(6) NULL COMMENT '更新时间（UTC,应用层生成）',
  `UpdatedBy` BIGINT NULL COMMENT '更新人ID',
  `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否删除',
  `Avatar` VARCHAR(500) NULL COMMENT '头像URL',
  `Introduction` VARCHAR(1000) NULL COMMENT '个人简介',
  `DeletedAt` DATETIME(6) NULL COMMENT '删除时间',
  `DeletedBy` BIGINT NULL COMMENT '删除人ID',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='用户表';


-- ----------------------------
-- Table structure for ginkgo_Sys_Role
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_Role`;
CREATE TABLE `ginkgo_Sys_Role` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `Name` VARCHAR(128) NOT NULL COMMENT '角色名称',
  `Code` VARCHAR(64) NOT NULL COMMENT '角色编码',
  `DataScope` VARCHAR(32) NOT NULL DEFAULT 'All' COMMENT '数据范围',
  `Enabled` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否启用',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间（UTC,应用层生成）',
  `CreatedBy` BIGINT NULL COMMENT '创建人ID',
  `UpdatedAt` DATETIME(6) NULL COMMENT '更新时间（UTC,应用层生成）',
  `UpdatedBy` BIGINT NULL COMMENT '更新人ID',
  `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否删除',
  `ParentId` BIGINT NULL COMMENT '父角色ID',
  `DeletedAt` DATETIME(6) NULL COMMENT '删除时间',
  `DeletedBy` BIGINT NULL COMMENT '删除人ID',
  `AllowedClients` VARCHAR(256) NULL COMMENT '允许登录的客户端列表(逗号分隔: WEB_ADMIN,WEB_PORTAL,WPF,UNIAPP)，NULL=不限制',
  `IsSuperAdmin` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否超级管理员 0-否 1-是',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='角色表';


-- ----------------------------
-- Table structure for ginkgo_Sys_UserDepartment
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_UserDepartment`;
CREATE TABLE `ginkgo_Sys_UserDepartment` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `UserId` BIGINT NOT NULL COMMENT '用户ID',
  `DepartmentId` BIGINT NOT NULL COMMENT '部门ID',
  `IsPrimary` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否主部门',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间（UTC,应用层生成）',
  `IsManager` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否管理者',
  `CreatedBy` BIGINT NULL COMMENT '创建人ID',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='用户部门关联';


-- ----------------------------
-- Table structure for ginkgo_Sys_UserRole
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_UserRole`;
CREATE TABLE `ginkgo_Sys_UserRole` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `UserId` BIGINT NOT NULL COMMENT '用户ID',
  `RoleId` BIGINT NOT NULL COMMENT '角色ID',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间（UTC,应用层生成）',
  `CreatedBy` BIGINT NULL COMMENT '创建人ID',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='用户角色关联';


-- ----------------------------
-- Table structure for ginkgo_Sys_RolePermission
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_RolePermission`;
CREATE TABLE `ginkgo_Sys_RolePermission` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `RoleId` BIGINT NOT NULL COMMENT '角色ID',
  `PermissionId` BIGINT NOT NULL COMMENT '权限ID(菜单ID)',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间（UTC,应用层生成）',
  `CreatedBy` BIGINT NULL COMMENT '创建人ID',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='角色权限';


-- ----------------------------
-- Table structure for ginkgo_Sys_RoleResource
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_RoleResource`;
CREATE TABLE `ginkgo_Sys_RoleResource` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `RoleId` BIGINT NOT NULL COMMENT '角色ID',
  `ResourceId` BIGINT NOT NULL COMMENT '资源ID(菜单ID)',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间（UTC,应用层生成）',
  `CreatedBy` BIGINT NULL COMMENT '创建人ID',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='角色资源';


-- ----------------------------
-- Table structure for ginkgo_Sys_RoleDataScopeDept
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_RoleDataScopeDept`;
CREATE TABLE `ginkgo_Sys_RoleDataScopeDept` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `RoleId` BIGINT NOT NULL COMMENT '角色ID',
  `DepartmentId` BIGINT NOT NULL COMMENT '部门ID',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='角色数据范围部门';


-- ----------------------------
-- Table structure for ginkgo_Sys_RefreshToken
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_RefreshToken`;
CREATE TABLE `ginkgo_Sys_RefreshToken` (
  `Id` BIGINT NOT NULL COMMENT '实体主键Id（Snowflake ID）',
  `Token` VARCHAR(128) NOT NULL COMMENT '刷新令牌值',
  `UserId` BIGINT NOT NULL COMMENT '关联用户Id',
  `ExpiresAt` DATETIME NOT NULL COMMENT '过期时间',
  `IsRevoked` TINYINT(1) NOT NULL COMMENT '是否已吊销',
  `RevokedAt` DATETIME NULL COMMENT '吊销时间',
  `CreatedByIp` VARCHAR(64) NULL COMMENT '创建时客户端IP',
  `ReplacedByToken` VARCHAR(128) NULL COMMENT '替代令牌（轮换链）',
  `CreatedAt` DATETIME NOT NULL COMMENT '创建时间',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='刷新令牌表';



-- ============================================================================
-- Plugin / OpLog / Settings Tables
-- ============================================================================

-- ----------------------------
-- Table structure for ginkgo_Sys_Plugin
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_Plugin`;
CREATE TABLE `ginkgo_Sys_Plugin` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `Name` VARCHAR(128) NOT NULL COMMENT '插件名称',
  `Code` VARCHAR(64) NOT NULL COMMENT '插件编码',
  `Version` VARCHAR(32) NOT NULL COMMENT '版本',
  `EntryType` VARCHAR(256) NOT NULL COMMENT '入口类型',
  `Scope` VARCHAR(16) NOT NULL COMMENT '作用域',
  `Enabled` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否启用',
  `ManifestJson` JSON NULL COMMENT '清单JSON',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间（UTC,应用层生成）',
  `CreatedBy` BIGINT NULL COMMENT '创建人ID',
  `UpdatedAt` DATETIME(6) NULL COMMENT '更新时间（UTC,应用层生成）',
  `UpdatedBy` BIGINT NULL COMMENT '更新人ID',
  `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否删除',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='插件表';


-- ----------------------------
-- Table structure for ginkgo_Sys_PluginEvent
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_PluginEvent`;
CREATE TABLE `ginkgo_Sys_PluginEvent` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `PluginId` BIGINT NOT NULL COMMENT '插件ID',
  `Event` VARCHAR(64) NOT NULL COMMENT '事件名称',
  `At` DATETIME(6) NOT NULL COMMENT '事件时间（UTC,应用层生成）',
  `DataJson` JSON NULL COMMENT '数据JSON',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='插件事件';


-- ----------------------------
-- Table structure for ginkgo_Sys_OpLog
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_OpLog`;
CREATE TABLE `ginkgo_Sys_OpLog` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `UserId` BIGINT NULL COMMENT '用户ID',
  `Action` VARCHAR(128) NOT NULL COMMENT '操作动作',
  `Resource` VARCHAR(256) NOT NULL COMMENT '资源标识',
  `Ip` VARCHAR(64) NULL COMMENT 'IP地址',
  `UserAgent` VARCHAR(256) NULL COMMENT '用户代理',
  `Result` VARCHAR(32) NOT NULL COMMENT '结果',
  `ElapsedMs` INT NULL COMMENT '耗时(毫秒)',
  `DataJson` JSON NULL COMMENT '数据JSON',
  `At` DATETIME(6) NOT NULL COMMENT '操作时间（UTC,应用层生成）',
  `ModuleCN` VARCHAR(64) NULL COMMENT '模块中文名',
  `FeatureCN` VARCHAR(64) NULL COMMENT '功能中文名',
  `DepartmentId` BIGINT NULL COMMENT '部门ID',
  `ReviewCN` VARCHAR(200) NULL COMMENT '审核中文名',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间（UTC,应用层生成）',
  `CreatedBy` BIGINT NULL COMMENT '创建人ID',
  `UpdatedAt` DATETIME(6) NULL COMMENT '更新时间（UTC,应用层生成）',
  `UpdatedBy` BIGINT NULL COMMENT '更新人ID',
  `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否删除',
  `DeletedAt` DATETIME(6) NULL COMMENT '删除时间',
  `DeletedBy` BIGINT NULL COMMENT '删除人ID',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='操作日志';


-- ----------------------------
-- Table structure for ginkgo_Sys_Settings
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_Settings`;
CREATE TABLE `ginkgo_Sys_Settings` (
  `Key` VARCHAR(200) NOT NULL COMMENT '配置键',
  `Module` VARCHAR(64) NOT NULL DEFAULT 'sys' COMMENT '所属模块标识（sys=系统级，其他为插件ModuleId）',
  `Value` LONGTEXT NULL COMMENT '配置值',
  `Type` VARCHAR(50) NULL COMMENT '类型',
  `Description` VARCHAR(500) NULL COMMENT '描述',
  `DescriptionI18n` JSON NULL COMMENT '描述-多语言',
  `Version` INT NOT NULL DEFAULT 1 COMMENT '版本号',
  `UpdatedAt` DATETIME(6) NOT NULL COMMENT '更新时间（UTC,应用层生成）',
  `UpdatedBy` BIGINT NULL COMMENT '更新人ID',
  `class` VARCHAR(255) NULL COMMENT '分类',
  `RowVersion` BIGINT NULL COMMENT '行版本',
  `Id` BIGINT NOT NULL COMMENT 'ID(Snowflake ID)',
  PRIMARY KEY (`Key`),
  KEY `IX_Settings_Module` (`Module`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='系统配置键值表';


-- ----------------------------
-- Table structure for ginkgo_Sys_Settings_History
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_Settings_History`;
CREATE TABLE `ginkgo_Sys_Settings_History` (
  `Id` BIGINT AUTO_INCREMENT NOT NULL COMMENT '主键(自增)',
  `Key` VARCHAR(200) NOT NULL COMMENT '配置键',
  `OldValue` LONGTEXT NULL COMMENT '旧值',
  `NewValue` LONGTEXT NULL COMMENT '新值',
  `Type` VARCHAR(50) NULL COMMENT '类型',
  `Version` INT NOT NULL COMMENT '版本号',
  `ChangedAt` DATETIME(6) NOT NULL COMMENT '变更时间（UTC,应用层生成）',
  `ChangedBy` BIGINT NULL COMMENT '变更人ID',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='系统配置变更历史';



-- ============================================================================
-- Verification Code / Template Tables
-- ============================================================================

-- ----------------------------
-- Table structure for ginkgo_Sys_VerificationCode
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_VerificationCode`;
CREATE TABLE `ginkgo_Sys_VerificationCode` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `UserId` BIGINT NULL COMMENT '用户ID（注册场景下可为空）',
  `Target` VARCHAR(256) NOT NULL COMMENT '验证目标（邮箱、手机号等）',
  `Channel` TINYINT NOT NULL DEFAULT 0 COMMENT '通道类型(0=Email, 1=SMS)',
  `Purpose` TINYINT NOT NULL DEFAULT 0 COMMENT '用途(0=ResetPassword,1=Login,2=Register,3=BindEmail,4=BindPhone,10=DangerousAction)',
  `PurposeLabel` VARCHAR(128) NULL COMMENT '用途标签（用于前端展示）',
  `CodeHash` VARCHAR(128) NOT NULL COMMENT '验证码哈希',
  `ExpiresAt` DATETIME(6) NOT NULL COMMENT '过期时间',
  `VerifiedAt` DATETIME(6) NULL COMMENT '验证时间',
  `Attempts` INT NOT NULL DEFAULT 0 COMMENT '尝试次数',
  `MaxAttempts` INT NOT NULL DEFAULT 5 COMMENT '最大尝试次数',
  `Ip` VARCHAR(64) NULL COMMENT '请求IP',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='验证码表';


-- ----------------------------
-- Table structure for ginkgo_Sys_VerificationTemplate
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_VerificationTemplate`;
CREATE TABLE `ginkgo_Sys_VerificationTemplate` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `Purpose` SMALLINT NOT NULL DEFAULT 0 COMMENT '用途(0=ResetPassword,1=Login,2=Register等)',
  `Channel` SMALLINT NOT NULL DEFAULT 0 COMMENT '通道(0=Email, 1=SMS)',
  `Name` VARCHAR(64) NOT NULL COMMENT '模板名称',
  `Subject` VARCHAR(256) NULL COMMENT '邮件主题',
  `BodyTemplate` TEXT NULL COMMENT '模板正文（支持占位符）',
  `IsHtml` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否HTML',
  `IsDefault` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否默认模板',
  `Enabled` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否启用',
  `SortOrder` INT NOT NULL DEFAULT 0 COMMENT '排序号',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间',
  `UpdatedAt` DATETIME(6) NULL COMMENT '更新时间',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='验证模板表';



-- ============================================================================
-- External Auth Tables (Reserved for Ginkgo.Module.third plugin)
-- 第三方登录与 TOTP 双因素认证表已移至对应模块的安装脚本中
-- 基础安装不再包含此部分
-- ============================================================================



-- ============================================================================
-- Scheduled Task Tables
-- ============================================================================

-- ----------------------------
-- Table structure for ginkgo_Sys_ScheduledTask
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_ScheduledTask`;
CREATE TABLE `ginkgo_Sys_ScheduledTask` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `TaskKey` VARCHAR(128) NOT NULL COMMENT '任务标识(唯一)',
  `DisplayName` VARCHAR(128) NOT NULL COMMENT '任务显示名称',
  `Group` VARCHAR(64) NULL COMMENT '任务组',
  `CronExpression` VARCHAR(64) NULL COMMENT 'Cron 表达式',
  `IsEnabled` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否启用',
  `LastRunAt` DATETIME NULL COMMENT '最后执行时间',
  `NextRunAt` DATETIME NULL COMMENT '下次执行时间',
  `LastResult` VARCHAR(32) NULL COMMENT '最后执行结果',
  `LastElapsedMs` INT NULL COMMENT '最后执行耗时(毫秒)',
  `Description` VARCHAR(500) NULL COMMENT '描述',
  `Source` VARCHAR(128) NULL COMMENT '来源',
  `CreatedAt` DATETIME NOT NULL COMMENT '创建时间',
  `UpdatedAt` DATETIME NULL COMMENT '更新时间',
  `ExecutionType` VARCHAR(64) NULL COMMENT '执行类型',
  `ExecutionTarget` VARCHAR(500) NULL COMMENT '执行目标',
  `DefinitionType` VARCHAR(32) NULL COMMENT '定义类型',
  `ExecutionSource` VARCHAR(32) NULL COMMENT '执行来源',
  `ActionKey` VARCHAR(128) NULL COMMENT '动作标识',
  `ConfigJson` TEXT NULL COMMENT '配置JSON',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='定时任务配置表';


-- ----------------------------
-- Table structure for ginkgo_Sys_ScheduledTaskLog
-- ----------------------------
DROP TABLE IF EXISTS `ginkgo_Sys_ScheduledTaskLog`;
CREATE TABLE `ginkgo_Sys_ScheduledTaskLog` (
  `Id` BIGINT NOT NULL COMMENT '主键(Snowflake ID)',
  `TaskKey` VARCHAR(128) NOT NULL COMMENT '任务标识',
  `StartedAt` DATETIME NOT NULL COMMENT '开始时间',
  `FinishedAt` DATETIME NULL COMMENT '结束时间',
  `Success` TINYINT(1) NOT NULL COMMENT '是否成功',
  `ErrorMessage` VARCHAR(2000) NULL COMMENT '错误信息',
  `ElapsedMs` INT NULL COMMENT '耗时(毫秒)',
  `TriggerType` VARCHAR(32) NULL COMMENT '触发方式',
  `DetailsJson` TEXT NULL COMMENT '详情JSON',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='定时任务执行日志';



-- ============================================================================
-- Code Designer Tables (Reserved for Ginkgo.Module.CodeDesigner plugin)
-- 代码生成器相关表已移至对应模块的安装脚本中
-- 基础安装不再包含此部分
-- ============================================================================



-- ============================================================================
-- Views
-- ============================================================================

-- ----------------------------
-- View structure for vw_NotifyStats
-- 通知统计视图（引用 ginkgo_Sys_NotifyMessage / ginkgo_Sys_NotifyAudience）
-- ----------------------------
DROP VIEW IF EXISTS `vw_NotifyStats`;
CREATE VIEW `vw_NotifyStats` AS 
SELECT n.Id AS NotifyId,
       n.Title,
       n.PublishedAt,
       COUNT(a.Id) AS TotalRecipients,
       SUM(CASE WHEN a.ReadAt IS NOT NULL THEN 1 ELSE 0 END) AS ReadCount,
       CAST(CASE WHEN COUNT(a.Id)=0 THEN 0 ELSE (SUM(CASE WHEN a.ReadAt IS NOT NULL THEN 1 ELSE 0 END)*100.0/COUNT(a.Id)) END AS DECIMAL(6,2)) AS ReadRate
FROM `ginkgo_Sys_NotifyMessage` n
LEFT JOIN `ginkgo_Sys_NotifyAudience` a ON a.NotifyId = n.Id
GROUP BY n.Id, n.Title, n.PublishedAt;


-- ----------------------------
-- View structure for vw_UserManagedDepartments
-- 用户管理的部门视图（基于闭包表向下展开）
-- ----------------------------
DROP VIEW IF EXISTS `vw_UserManagedDepartments`;
CREATE VIEW `vw_UserManagedDepartments` AS 
SELECT ud.UserId,
       dc.DescendantId AS DepartmentId
FROM ginkgo_Sys_UserDepartment ud
JOIN ginkgo_Sys_DepartmentClosure dc
  ON dc.AncestorId = ud.DepartmentId
WHERE ud.IsManager = 1;


-- ----------------------------
-- View structure for vw_UserManageableUsers
-- 用户可管理的用户视图
-- ----------------------------
DROP VIEW IF EXISTS `vw_UserManageableUsers`;
CREATE VIEW `vw_UserManageableUsers` AS 
SELECT m.UserId AS ManagerUserId,
       u.UserId AS TargetUserId,
       u.DepartmentId
FROM vw_UserManagedDepartments m
JOIN ginkgo_Sys_UserDepartment u
  ON u.DepartmentId = m.DepartmentId;



-- ============================================================================
-- Indexes
-- ============================================================================

-- ginkgo_Client_ModuleStatus
CREATE UNIQUE INDEX `IX_ginkgo_Client_ModuleStatus_UQ` ON `ginkgo_Client_ModuleStatus` (`ClientId`, `ModuleName`);

-- ginkgo_Mod_Migrations
CREATE UNIQUE INDEX `IX_ginkgo_Mod_Migrations_UQ` ON `ginkgo_Mod_Migrations` (`ModuleName`, `ScriptName`(191), `Hash`);

-- ginkgo_Modules
CREATE UNIQUE INDEX `UQ_ginkgo_Modules_Name_Version` ON `ginkgo_Modules` (`Name`, `Version`);

-- ginkgo_Modules_Installed
CREATE UNIQUE INDEX `UX_ginkgo_Modules_Installed_ModuleId` ON `ginkgo_Modules_Installed` (`ModuleId`(191));

-- ginkgo_Sys_Department
CREATE INDEX `IX_Sys_Department_Parent` ON `ginkgo_Sys_Department` (`ParentId`);
CREATE UNIQUE INDEX `UX_Sys_Department_Code` ON `ginkgo_Sys_Department` (`Code`);

-- ginkgo_Sys_DepartmentClosure
CREATE INDEX `IX_DepartmentClosure_Ancestor` ON `ginkgo_Sys_DepartmentClosure` (`AncestorId`);
CREATE INDEX `IX_DepartmentClosure_Descendant` ON `ginkgo_Sys_DepartmentClosure` (`DescendantId`);

-- ginkgo_Sys_Dictionary
CREATE UNIQUE INDEX `UX_ginkgo_Sys_Dictionary_Code` ON `ginkgo_Sys_Dictionary` (`Code`);

-- ginkgo_Sys_DictionaryItem
CREATE UNIQUE INDEX `UX_ginkgo_DictItem_Dict_Code` ON `ginkgo_Sys_DictionaryItem` (`DictId`, `Code`);
CREATE INDEX `IX_ginkgo_DictItem_Parent` ON `ginkgo_Sys_DictionaryItem` (`ParentId`);
CREATE INDEX `IX_ginkgo_DictItem_Active` ON `ginkgo_Sys_DictionaryItem` (`IsActive`);
CREATE INDEX `IX_ginkgo_DictItem_Sort` ON `ginkgo_Sys_DictionaryItem` (`DictId`, `SortOrder`);

-- ginkgo_Sys_File
CREATE INDEX `IX_Sys_File_Owner` ON `ginkgo_Sys_File` (`OwnerId`);
CREATE INDEX `IX_Sys_File_CreatedAt` ON `ginkgo_Sys_File` (`CreatedAt`);

-- ginkgo_Sys_LoginLog
CREATE INDEX `IX_Sys_LoginLog_At` ON `ginkgo_Sys_LoginLog` (`At`);
CREATE INDEX `IX_Sys_LoginLog_User_At` ON `ginkgo_Sys_LoginLog` (`UserId`, `At`);

-- ginkgo_Sys_Menu (前缀索引: Route > 191 chars)
CREATE INDEX `IX_Sys_Menu_Parent` ON `ginkgo_Sys_Menu` (`ParentId`);
CREATE UNIQUE INDEX `UX_Sys_Menu_Route` ON `ginkgo_Sys_Menu` (`Route`(191));
CREATE INDEX `IX_Sys_Menu_PermissionCode` ON `ginkgo_Sys_Menu` (`PermissionCode`);
CREATE INDEX `IX_Sys_Menu_Type` ON `ginkgo_Sys_Menu` (`Type`);
CREATE INDEX `IX_Sys_Menu_Code` ON `ginkgo_Sys_Menu` (`Code`(191));

-- ginkgo_Sys_MenuGroup
CREATE UNIQUE INDEX `UX_ginkgo_Sys_MenuGroup_Slug` ON `ginkgo_Sys_MenuGroup` (`Slug`);
CREATE INDEX `IX_ginkgo_Sys_MenuGroup_Location` ON `ginkgo_Sys_MenuGroup` (`Location`);
CREATE INDEX `IX_ginkgo_Sys_MenuGroup_ClientType` ON `ginkgo_Sys_MenuGroup` (`ClientType`);
CREATE INDEX `IX_ginkgo_Sys_MenuGroup_Enabled_IsDeleted` ON `ginkgo_Sys_MenuGroup` (`Enabled`, `IsDeleted`);

-- ginkgo_Sys_MenuGroupItem
CREATE INDEX `IX_ginkgo_Sys_MenuGroupItem_Group_Parent_Order` ON `ginkgo_Sys_MenuGroupItem` (`MenuGroupId`, `ParentId`, `OrderNo`);
CREATE INDEX `IX_ginkgo_Sys_MenuGroupItem_RefMenuId` ON `ginkgo_Sys_MenuGroupItem` (`RefMenuId`);
CREATE INDEX `IX_ginkgo_Sys_MenuGroupItem_PermissionCode` ON `ginkgo_Sys_MenuGroupItem` (`PermissionCode`);
CREATE INDEX `IX_ginkgo_Sys_MenuGroupItem_Enabled_IsDeleted` ON `ginkgo_Sys_MenuGroupItem` (`Enabled`, `IsDeleted`);

-- ginkgo_Sys_MenuLog
CREATE INDEX `IX_ginkgo_Sys_MenuLog_At` ON `ginkgo_Sys_MenuLog` (`At`);

-- ginkgo_Sys_RoleMenuGroup
CREATE UNIQUE INDEX `UX_ginkgo_Sys_RoleMenuGroup` ON `ginkgo_Sys_RoleMenuGroup` (`RoleId`, `MenuGroupId`);

-- ginkgo_Sys_NotifyMessage (无额外索引，主键即可)

-- ginkgo_Sys_NotifyAttachment
CREATE INDEX `IX_ginkgo_Sys_NotifyAttachment_NotifyId` ON `ginkgo_Sys_NotifyAttachment` (`NotifyId`);

-- ginkgo_Sys_NotifyAudience
CREATE INDEX `IX_ginkgo_Sys_NotifyAudience_NotifyId` ON `ginkgo_Sys_NotifyAudience` (`NotifyId`);
CREATE INDEX `IX_ginkgo_Sys_NotifyAudience_UserId` ON `ginkgo_Sys_NotifyAudience` (`UserId`);

-- ginkgo_Sys_NotifyDispatch
CREATE INDEX `IX_ginkgo_Sys_NotifyDispatch_NotifyId` ON `ginkgo_Sys_NotifyDispatch` (`NotifyId`);
CREATE INDEX `IX_ginkgo_Sys_NotifyDispatch_UserId` ON `ginkgo_Sys_NotifyDispatch` (`UserId`);

-- ginkgo_Sys_Message
CREATE INDEX `IX_Sys_Message_UserId_IsRead` ON `ginkgo_Sys_Message` (`UserId`, `IsRead`);
CREATE INDEX `IX_Sys_Message_UserId_CreatedAt` ON `ginkgo_Sys_Message` (`UserId`, `CreatedAt`);

-- ginkgo_Sys_MessageAttachment
CREATE INDEX `IX_Sys_MessageAttachment_MessageId` ON `ginkgo_Sys_MessageAttachment` (`MessageId`);

-- ginkgo_Sys_MessageLink
CREATE INDEX `IX_Sys_MessageLink_MessageId` ON `ginkgo_Sys_MessageLink` (`MessageId`);

-- ginkgo_Sys_User (前缀索引: Email > 191 chars)
CREATE UNIQUE INDEX `UX_Sys_User_UserName` ON `ginkgo_Sys_User` (`UserName`);
CREATE INDEX `IX_Sys_User_Enabled` ON `ginkgo_Sys_User` (`Enabled`);
CREATE INDEX `IX_Sys_User_Email` ON `ginkgo_Sys_User` (`Email`(191));
CREATE INDEX `IX_Sys_User_Phone` ON `ginkgo_Sys_User` (`Phone`);
CREATE INDEX `IX_ginkgo_Sys_User_Enabled_CreatedAt` ON `ginkgo_Sys_User` (`Enabled`, `CreatedAt` DESC);
CREATE INDEX `IX_ginkgo_Sys_User_UserName_Enabled` ON `ginkgo_Sys_User` (`UserName`, `Enabled`);

-- ginkgo_Sys_Role
CREATE UNIQUE INDEX `UX_Sys_Role_Code` ON `ginkgo_Sys_Role` (`Code`);
CREATE INDEX `IX_Sys_Role_Parent` ON `ginkgo_Sys_Role` (`ParentId`);

-- ginkgo_Sys_UserDepartment
CREATE UNIQUE INDEX `UX_Sys_UserDepartment` ON `ginkgo_Sys_UserDepartment` (`UserId`, `DepartmentId`);
CREATE INDEX `IX_UserDept_Manager_User` ON `ginkgo_Sys_UserDepartment` (`UserId`, `IsManager`);

-- ginkgo_Sys_UserRole
CREATE UNIQUE INDEX `UX_Sys_UserRole` ON `ginkgo_Sys_UserRole` (`UserId`, `RoleId`);

-- ginkgo_Sys_RolePermission
CREATE UNIQUE INDEX `UX_Sys_RolePermission` ON `ginkgo_Sys_RolePermission` (`RoleId`, `PermissionId`);

-- ginkgo_Sys_RoleResource
CREATE UNIQUE INDEX `UX_Sys_RoleResource` ON `ginkgo_Sys_RoleResource` (`RoleId`, `ResourceId`);

-- ginkgo_Sys_RoleDataScopeDept
CREATE UNIQUE INDEX `UX_Sys_RoleDataScopeDept` ON `ginkgo_Sys_RoleDataScopeDept` (`RoleId`, `DepartmentId`);

-- ginkgo_Sys_RefreshToken
CREATE UNIQUE INDEX `UX_Sys_RefreshToken_Token` ON `ginkgo_Sys_RefreshToken` (`Token`);
CREATE INDEX `IX_Sys_RefreshToken_UserId` ON `ginkgo_Sys_RefreshToken` (`UserId`);

-- ginkgo_Sys_Plugin
CREATE UNIQUE INDEX `UX_Sys_Plugin_Code` ON `ginkgo_Sys_Plugin` (`Code`);

-- ginkgo_Sys_PluginEvent
CREATE INDEX `IX_Sys_PluginEvent_Plugin_At` ON `ginkgo_Sys_PluginEvent` (`PluginId`, `At`);

-- ginkgo_Sys_OpLog (前缀索引: Resource > 191 chars)
CREATE INDEX `IX_Sys_OpLog_At` ON `ginkgo_Sys_OpLog` (`At`);
CREATE INDEX `IX_Sys_OpLog_User_At` ON `ginkgo_Sys_OpLog` (`UserId`, `At`);
CREATE INDEX `IX_Sys_OpLog_Resource_At` ON `ginkgo_Sys_OpLog` (`Resource`(191), `At`);
CREATE INDEX `IX_ginkgo_Sys_OpLog_DepartmentId` ON `ginkgo_Sys_OpLog` (`DepartmentId`);

-- ginkgo_Sys_Settings
CREATE INDEX `IX_ginkgo_Sys_Settings_UpdatedAt` ON `ginkgo_Sys_Settings` (`UpdatedAt`);
CREATE INDEX `IX_ginkgo_Sys_Settings_Class_Key` ON `ginkgo_Sys_Settings` (`class`, `Key`(191));

-- ginkgo_Sys_VerificationCode
CREATE INDEX `IX_ginkgo_Sys_VerificationCode_Target_Purpose_CreatedAt` ON `ginkgo_Sys_VerificationCode` (`Target`(128), `Purpose`, `CreatedAt`);
CREATE INDEX `IX_ginkgo_Sys_VerificationCode_UserId_CreatedAt` ON `ginkgo_Sys_VerificationCode` (`UserId`, `CreatedAt`);
CREATE INDEX `IX_ginkgo_Sys_VerificationCode_ExpiresAt` ON `ginkgo_Sys_VerificationCode` (`ExpiresAt`);

-- ginkgo_Sys_VerificationTemplate
CREATE UNIQUE INDEX `UX_ginkgo_Sys_VerificationTemplate_Purpose_Channel_IsDefault` ON `ginkgo_Sys_VerificationTemplate` (`Purpose`, `Channel`, `IsDefault`);

-- ginkgo_Sys_ScheduledTask
CREATE UNIQUE INDEX `UX_ginkgo_Sys_ScheduledTask_TaskKey` ON `ginkgo_Sys_ScheduledTask` (`TaskKey`);

-- ginkgo_Sys_ScheduledTaskLog
CREATE INDEX `IX_ginkgo_Sys_ScheduledTaskLog_TaskKey_StartedAt` ON `ginkgo_Sys_ScheduledTaskLog` (`TaskKey`, `StartedAt`);




-- ============================================================================
-- Foreign Keys
-- ============================================================================

-- ginkgo_Sys_DictionaryItem
ALTER TABLE `ginkgo_Sys_DictionaryItem` ADD CONSTRAINT `FK_ginkgo_DictItem_Dict` 
  FOREIGN KEY (`DictId`) REFERENCES `ginkgo_Sys_Dictionary` (`Id`) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE `ginkgo_Sys_DictionaryItem` ADD CONSTRAINT `FK_ginkgo_DictItem_Parent` 
  FOREIGN KEY (`ParentId`) REFERENCES `ginkgo_Sys_DictionaryItem` (`Id`) ON DELETE NO ACTION ON UPDATE NO ACTION;

-- ginkgo_Sys_MenuGroupItem
ALTER TABLE `ginkgo_Sys_MenuGroupItem` ADD CONSTRAINT `FK_MenuGroupItem_MenuGroup` 
  FOREIGN KEY (`MenuGroupId`) REFERENCES `ginkgo_Sys_MenuGroup` (`Id`) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE `ginkgo_Sys_MenuGroupItem` ADD CONSTRAINT `FK_MenuGroupItem_RefMenu` 
  FOREIGN KEY (`RefMenuId`) REFERENCES `ginkgo_Sys_Menu` (`Id`) ON DELETE SET NULL ON UPDATE NO ACTION;

-- ginkgo_Sys_RoleMenuGroup
ALTER TABLE `ginkgo_Sys_RoleMenuGroup` ADD CONSTRAINT `FK_RoleMenuGroup_Role` 
  FOREIGN KEY (`RoleId`) REFERENCES `ginkgo_Sys_Role` (`Id`) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE `ginkgo_Sys_RoleMenuGroup` ADD CONSTRAINT `FK_RoleMenuGroup_MenuGroup` 
  FOREIGN KEY (`MenuGroupId`) REFERENCES `ginkgo_Sys_MenuGroup` (`Id`) ON DELETE CASCADE ON UPDATE NO ACTION;

-- ginkgo_Sys_PluginEvent
ALTER TABLE `ginkgo_Sys_PluginEvent` ADD CONSTRAINT `FK_PluginEvent_Plugin` 
  FOREIGN KEY (`PluginId`) REFERENCES `ginkgo_Sys_Plugin` (`Id`) ON DELETE NO ACTION ON UPDATE NO ACTION;

-- ginkgo_Sys_RoleDataScopeDept
ALTER TABLE `ginkgo_Sys_RoleDataScopeDept` ADD CONSTRAINT `FK_RoleDataScope_Role` 
  FOREIGN KEY (`RoleId`) REFERENCES `ginkgo_Sys_Role` (`Id`) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE `ginkgo_Sys_RoleDataScopeDept` ADD CONSTRAINT `FK_RoleDataScope_Dept` 
  FOREIGN KEY (`DepartmentId`) REFERENCES `ginkgo_Sys_Department` (`Id`) ON DELETE NO ACTION ON UPDATE NO ACTION;

-- ginkgo_Sys_RolePermission
ALTER TABLE `ginkgo_Sys_RolePermission` ADD CONSTRAINT `FK_RolePermission_Role` 
  FOREIGN KEY (`RoleId`) REFERENCES `ginkgo_Sys_Role` (`Id`) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE `ginkgo_Sys_RolePermission` ADD CONSTRAINT `FK_RolePermission_Permission` 
  FOREIGN KEY (`PermissionId`) REFERENCES `ginkgo_Sys_Menu` (`Id`) ON DELETE NO ACTION ON UPDATE NO ACTION;

-- ginkgo_Sys_RoleResource
ALTER TABLE `ginkgo_Sys_RoleResource` ADD CONSTRAINT `FK_RoleResource_Role` 
  FOREIGN KEY (`RoleId`) REFERENCES `ginkgo_Sys_Role` (`Id`) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE `ginkgo_Sys_RoleResource` ADD CONSTRAINT `FK_RoleResource_Resource` 
  FOREIGN KEY (`ResourceId`) REFERENCES `ginkgo_Sys_Menu` (`Id`) ON DELETE NO ACTION ON UPDATE NO ACTION;

-- ginkgo_Sys_UserDepartment
ALTER TABLE `ginkgo_Sys_UserDepartment` ADD CONSTRAINT `FK_UserDept_User` 
  FOREIGN KEY (`UserId`) REFERENCES `ginkgo_Sys_User` (`Id`) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE `ginkgo_Sys_UserDepartment` ADD CONSTRAINT `FK_UserDept_Dept` 
  FOREIGN KEY (`DepartmentId`) REFERENCES `ginkgo_Sys_Department` (`Id`) ON DELETE NO ACTION ON UPDATE NO ACTION;

-- ginkgo_Sys_UserRole
ALTER TABLE `ginkgo_Sys_UserRole` ADD CONSTRAINT `FK_UserRole_User` 
  FOREIGN KEY (`UserId`) REFERENCES `ginkgo_Sys_User` (`Id`) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE `ginkgo_Sys_UserRole` ADD CONSTRAINT `FK_UserRole_Role` 
  FOREIGN KEY (`RoleId`) REFERENCES `ginkgo_Sys_Role` (`Id`) ON DELETE NO ACTION ON UPDATE NO ACTION;



-- ============================================================================
-- Initial Data: Departments & DepartmentClosure
-- ============================================================================

-- ----------------------------
-- Records of ginkgo_Sys_Department
-- ----------------------------
INSERT INTO `ginkgo_Sys_Department` (`Id`, `ParentId`, `Name`, `Code`, `LeaderUserId`, `OrderNo`, `Enabled`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `IsDeleted`) 
VALUES (100000000000001, NULL, '总部', 'HQ', NULL, 0, 1, '2025-08-08 12:44:43.999877', NULL, NULL, NULL, 0);

INSERT INTO `ginkgo_Sys_Department` (`Id`, `ParentId`, `Name`, `Code`, `LeaderUserId`, `OrderNo`, `Enabled`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `IsDeleted`) 
VALUES (100000000000002, 100000000000001, '云南分公司', 'YN', NULL, 0, 1, '2025-08-09 14:36:54.225269', NULL, NULL, NULL, 0);

INSERT INTO `ginkgo_Sys_Department` (`Id`, `ParentId`, `Name`, `Code`, `LeaderUserId`, `OrderNo`, `Enabled`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `IsDeleted`) 
VALUES (100000000000003, 100000000000002, '红河分公司', 'HH', NULL, 0, 1, '2025-08-09 14:37:27.741931', NULL, NULL, NULL, 0);

INSERT INTO `ginkgo_Sys_Department` (`Id`, `ParentId`, `Name`, `Code`, `LeaderUserId`, `OrderNo`, `Enabled`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `IsDeleted`) 
VALUES (100000000000004, 100000000000001, '默认注册', 'DEFAULT_REG', NULL, 0, 1, '2025-08-09 14:37:27.741931', NULL, NULL, NULL, 0);


-- ----------------------------
-- Records of ginkgo_Sys_DepartmentClosure
-- ----------------------------
INSERT INTO `ginkgo_Sys_DepartmentClosure` (`AncestorId`, `DescendantId`, `Depth`) VALUES (100000000000001, 100000000000001, 0);
INSERT INTO `ginkgo_Sys_DepartmentClosure` (`AncestorId`, `DescendantId`, `Depth`) VALUES (100000000000001, 100000000000002, 1);
INSERT INTO `ginkgo_Sys_DepartmentClosure` (`AncestorId`, `DescendantId`, `Depth`) VALUES (100000000000001, 100000000000003, 2);
INSERT INTO `ginkgo_Sys_DepartmentClosure` (`AncestorId`, `DescendantId`, `Depth`) VALUES (100000000000001, 100000000000004, 1);
INSERT INTO `ginkgo_Sys_DepartmentClosure` (`AncestorId`, `DescendantId`, `Depth`) VALUES (100000000000002, 100000000000002, 0);
INSERT INTO `ginkgo_Sys_DepartmentClosure` (`AncestorId`, `DescendantId`, `Depth`) VALUES (100000000000002, 100000000000003, 1);
INSERT INTO `ginkgo_Sys_DepartmentClosure` (`AncestorId`, `DescendantId`, `Depth`) VALUES (100000000000003, 100000000000003, 0);
INSERT INTO `ginkgo_Sys_DepartmentClosure` (`AncestorId`, `DescendantId`, `Depth`) VALUES (100000000000004, 100000000000004, 0);



-- ============================================================================
-- Initial Data: Roles
-- ============================================================================

-- ----------------------------
-- Records of ginkgo_Sys_Role
-- ----------------------------
INSERT INTO `ginkgo_Sys_Role` (`Id`, `Name`, `Code`, `DataScope`, `Enabled`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `IsDeleted`, `ParentId`, `IsSuperAdmin`, `AllowedClients`) 
VALUES (200000000000001, '管理员', 'ADMIN', 'All', 1, '2025-08-08 12:44:44.012888', NULL, NULL, NULL, 0, NULL, 1, 'WEB_ADMIN,WEB_PORTAL,WPF,UNIAPP');

INSERT INTO `ginkgo_Sys_Role` (`Id`, `Name`, `Code`, `DataScope`, `Enabled`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `IsDeleted`, `ParentId`, `IsSuperAdmin`, `AllowedClients`) 
VALUES (200000000000002, '内容编辑', 'EDITOR', 'OwnOnly', 0, '2025-08-09 04:48:43.246133', NULL, NULL, NULL, 0, 200000000000001, 0, 'WEB_ADMIN');

INSERT INTO `ginkgo_Sys_Role` (`Id`, `Name`, `Code`, `DataScope`, `Enabled`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `IsDeleted`, `ParentId`, `IsSuperAdmin`, `AllowedClients`) 
VALUES (200000000000003, '医务人员', 'MEDICAL', 'OwnOnly', 1, '2025-08-09 06:45:12.514393', NULL, NULL, NULL, 0, 200000000000001, 0, 'WEB_ADMIN,UNIAPP');

INSERT INTO `ginkgo_Sys_Role` (`Id`, `Name`, `Code`, `DataScope`, `Enabled`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `IsDeleted`, `ParentId`, `IsSuperAdmin`, `AllowedClients`) 
VALUES (200000000000004, '注册用户', 'REGUSER', 'OwnOnly', 1, '2025-08-09 06:45:12.514393', NULL, NULL, NULL, 0, NULL, 0, 'WEB_PORTAL,UNIAPP');



-- ============================================================================
-- Initial Data: Dictionaries & Dictionary Items
-- ============================================================================

-- ----------------------------
-- Records of ginkgo_Sys_Dictionary (13 条)
-- ----------------------------
INSERT INTO `ginkgo_Sys_Dictionary` (`Id`, `Code`, `Name`, `Description`, `Category`, `SourceType`, `ExtraJson`, `IsSystem`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`, `Enabled`) 
VALUES (300000000000001, 'city', '城市', NULL, 'HIERARCHY', '', NULL, 1, '2025-08-10 15:29:26', NULL, 0, NULL, NULL, 1);

INSERT INTO `ginkgo_Sys_Dictionary` (`Id`, `Code`, `Name`, `Description`, `Category`, `SourceType`, `ExtraJson`, `IsSystem`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`, `Enabled`) 
VALUES (300000000000002, 'sysconfig', '系统配置', NULL, 'STATIC', '', NULL, 1, '2025-08-11 06:31:26', NULL, 0, NULL, NULL, 1);

INSERT INTO `ginkgo_Sys_Dictionary` (`Id`, `Code`, `Name`, `Description`, `Category`, `SourceType`, `ExtraJson`, `IsSystem`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`, `Enabled`) 
VALUES (300000000000003, 'file', '文件类型', NULL, 'STATIC', '', NULL, 1, '2025-08-10 17:09:27', NULL, 0, NULL, NULL, 1);

INSERT INTO `ginkgo_Sys_Dictionary` (`Id`, `Code`, `Name`, `Description`, `Category`, `SourceType`, `ExtraJson`, `IsSystem`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`, `Enabled`) 
VALUES (300000000000004, 'SYS_LANGUAGES', '系统语言', NULL, 'STATIC', '', NULL, 1, '2025-08-10 17:09:27', NULL, 0, NULL, NULL, 1);

INSERT INTO `ginkgo_Sys_Dictionary` (`Id`, `Code`, `Name`, `Description`, `Category`, `SourceType`, `ExtraJson`, `IsSystem`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`, `Enabled`) 
VALUES (1000000000000001, 'gender', '性别', '用户性别枚举', 'STATIC', 'Static', NULL, 1, '2026-04-16 11:24:14', '2026-04-16 11:24:14', 0, NULL, NULL, 1);

INSERT INTO `ginkgo_Sys_Dictionary` (`Id`, `Code`, `Name`, `Description`, `Category`, `SourceType`, `ExtraJson`, `IsSystem`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`, `Enabled`) 
VALUES (1000000000000002, 'enabled_status', '启用状态', '通用启用/禁用状态', 'STATIC', 'Static', NULL, 1, '2026-04-16 11:24:14', '2026-04-16 11:24:14', 0, NULL, NULL, 1);

INSERT INTO `ginkgo_Sys_Dictionary` (`Id`, `Code`, `Name`, `Description`, `Category`, `SourceType`, `ExtraJson`, `IsSystem`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`, `Enabled`) 
VALUES (1000000000000003, 'priority_level', '优先级', '任务/工单优先级', 'STATIC', 'Static', NULL, 0, '2026-04-16 11:24:14', '2026-04-16 11:24:14', 0, NULL, NULL, 1);

INSERT INTO `ginkgo_Sys_Dictionary` (`Id`, `Code`, `Name`, `Description`, `Category`, `SourceType`, `ExtraJson`, `IsSystem`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`, `Enabled`) 
VALUES (1000000000000004, 'order_status', '订单状态', '电商/业务订单状态映射', 'MAPPING', 'Static', NULL, 0, '2026-04-16 11:24:14', '2026-04-16 11:24:14', 0, NULL, NULL, 1);

INSERT INTO `ginkgo_Sys_Dictionary` (`Id`, `Code`, `Name`, `Description`, `Category`, `SourceType`, `ExtraJson`, `IsSystem`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`, `Enabled`) 
VALUES (1000000000000005, 'http_method', 'HTTP方法', 'RESTful HTTP 请求方法', 'MAPPING', 'Static', NULL, 1, '2026-04-16 11:24:14', '2026-04-16 11:24:14', 0, NULL, NULL, 1);

INSERT INTO `ginkgo_Sys_Dictionary` (`Id`, `Code`, `Name`, `Description`, `Category`, `SourceType`, `ExtraJson`, `IsSystem`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`, `Enabled`) 
VALUES (1000000000000006, 'log_level', '日志级别', '系统日志级别', 'STATIC', 'Static', NULL, 1, '2026-04-16 11:24:14', '2026-04-16 11:24:14', 0, NULL, NULL, 1);

INSERT INTO `ginkgo_Sys_Dictionary` (`Id`, `Code`, `Name`, `Description`, `Category`, `SourceType`, `ExtraJson`, `IsSystem`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`, `Enabled`) 
VALUES (1000000000000008, 'notification_type', '通知类型', '系统通知消息类型', 'STATIC', 'Static', NULL, 0, '2026-04-16 11:24:14', '2026-04-16 11:24:14', 0, NULL, NULL, 1);

INSERT INTO `ginkgo_Sys_Dictionary` (`Id`, `Code`, `Name`, `Description`, `Category`, `SourceType`, `ExtraJson`, `IsSystem`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`, `Enabled`) 
VALUES (1000000000000009, 'sys_config', '系统配置', '全局系统配置项', 'CONFIG', 'Static', NULL, 1, '2026-04-16 11:24:14', '2026-04-16 11:24:14', 0, NULL, NULL, 1);

INSERT INTO `ginkgo_Sys_Dictionary` (`Id`, `Code`, `Name`, `Description`, `Category`, `SourceType`, `ExtraJson`, `IsSystem`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`, `Enabled`) 
VALUES (1000000000000010, 'app_theme', '主题风格', '应用界面主题', 'STATIC', 'Static', NULL, 0, '2026-04-16 11:24:14', '2026-04-16 11:24:14', 0, NULL, NULL, 1);


-- ----------------------------
-- Records of ginkgo_Sys_DictionaryItem (47 条，排除用户测试数据)
-- ----------------------------

-- 城市 (city) - 层级字典
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (400000000000001, 300000000000001, NULL, '087', '云南', 0, 1, NULL, '2025-08-10 15:37:25', NULL, 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (400000000000002, 300000000000001, 400000000000001, '0871', '昆明', 0, 1, NULL, '2025-08-10 15:37:32', NULL, 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (400000000000003, 300000000000001, 400000000000001, '0872', '玉溪市', 0, 1, NULL, '2025-08-10 15:45:31', NULL, 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (400000000000004, 300000000000001, 400000000000001, '0873', '红河州', 0, 1, NULL, '2025-08-10 15:38:03', NULL, 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (400000000000005, 300000000000001, 400000000000001, '曲靖', '0877', 0, 1, NULL, '2025-08-10 15:38:03', NULL, 0, NULL, NULL);

-- 文件类型 (file) - 排除用户创建的"user/用户附件"(ID=303101644998967300)
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (400000000000006, 300000000000003, NULL, 'default', '默认附件', 0, 1, NULL, '2025-08-10 17:10:28', NULL, 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (400000000000007, 300000000000003, NULL, 'system', '系统附件', 0, 1, NULL, '2025-08-10 17:10:40', NULL, 0, NULL, NULL);

-- 系统语言 (SYS_LANGUAGES)
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (400000000000008, 300000000000004, NULL, 'zh-CN', '简体中文', 1, 1, NULL, '2025-08-10 17:10:40', NULL, 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (400000000000009, 300000000000004, NULL, 'en-US', 'English', 2, 1, NULL, '2025-08-10 17:10:40', NULL, 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (400000000000010, 300000000000004, NULL, 'ja-JP', '日本語', 3, 1, NULL, '2025-08-10 17:10:40', NULL, 0, NULL, NULL);

-- 性别 (gender)
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010001, 1000000000000001, NULL, 'male', '男', 1, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010002, 1000000000000001, NULL, 'female', '女', 2, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010003, 1000000000000001, NULL, 'unknown', '未知', 3, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);

-- 启用状态 (enabled_status)
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010011, 1000000000000002, NULL, '1', '启用', 1, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010012, 1000000000000002, NULL, '0', '禁用', 2, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);

-- 优先级 (priority_level)
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010021, 1000000000000003, NULL, 'low', '低', 1, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010022, 1000000000000003, NULL, 'medium', '中', 2, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010023, 1000000000000003, NULL, 'high', '高', 3, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010024, 1000000000000003, NULL, 'critical', '紧急', 4, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);

-- 订单状态 (order_status)
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010031, 1000000000000004, NULL, '0', '待付款', 1, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010032, 1000000000000004, NULL, '10', '已付款', 2, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010033, 1000000000000004, NULL, '20', '已发货', 3, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010034, 1000000000000004, NULL, '30', '已完成', 4, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010035, 1000000000000004, NULL, '40', '已取消', 5, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010036, 1000000000000004, NULL, '50', '已退款', 6, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);

-- HTTP方法 (http_method)
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010041, 1000000000000005, NULL, 'GET', 'GET', 1, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010042, 1000000000000005, NULL, 'POST', 'POST', 2, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010043, 1000000000000005, NULL, 'PUT', 'PUT', 3, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010044, 1000000000000005, NULL, 'DELETE', 'DELETE', 4, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010045, 1000000000000005, NULL, 'PATCH', 'PATCH', 5, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);

-- 日志级别 (log_level)
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010051, 1000000000000006, NULL, 'trace', 'Trace', 1, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010052, 1000000000000006, NULL, 'debug', 'Debug', 2, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010053, 1000000000000006, NULL, 'info', 'Info', 3, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010054, 1000000000000006, NULL, 'warn', 'Warning', 4, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010055, 1000000000000006, NULL, 'error', 'Error', 5, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010056, 1000000000000006, NULL, 'fatal', 'Fatal', 6, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);

-- 通知类型 (notification_type)
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010101, 1000000000000008, NULL, 'system', '系统通知', 1, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010102, 1000000000000008, NULL, 'alert', '告警通知', 2, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010103, 1000000000000008, NULL, 'promotion', '营销通知', 3, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010104, 1000000000000008, NULL, 'task', '任务通知', 4, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);

-- 系统配置 (sys_config)
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010111, 1000000000000009, NULL, 'site_name', 'Ginkgo Admin', 1, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010112, 1000000000000009, NULL, 'max_upload_size', '10485760', 2, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010113, 1000000000000009, NULL, 'session_timeout', '1800', 3, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010114, 1000000000000009, NULL, 'enable_register', 'true', 4, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);

-- 主题风格 (app_theme)
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010121, 1000000000000010, NULL, 'light', '浅色模式', 1, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010122, 1000000000000010, NULL, 'dark', '深色模式', 2, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (1000000000010123, 1000000000000010, NULL, 'auto', '跟随系统', 3, 1, NULL, '2026-04-16 11:24:30', '2026-04-16 11:24:30', 0, NULL, NULL);



-- ============================================================================
-- Initial Data: System Settings (60 条，敏感值使用占位符)
-- 排除用户测试数据 GZ.key
-- ============================================================================

-- ----------------------------
-- Records of ginkgo_Sys_Settings
-- ----------------------------

-- Site 站点配置
INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Site.Name', 'GinkgoAdmin', 'String', '站点名称', 1, '2025-08-10 08:45:05.679973', NULL, 'Site', 500000000000001);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Site.BaseUrl', 'http://localhost:5288', 'String', '站点基础URL', 1, '2025-08-10 08:45:05.700552', NULL, 'Site', 500000000000002);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Site.DefaultLanguage', 'zh-CN', 'String', '默认语言', 1, '2025-08-10 08:45:05.745875', NULL, 'Site', 500000000000003);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Site.TimeZone', 'Asia/Shanghai', 'String', '时区', 1, '2025-08-10 08:45:05.766512', NULL, 'Site', 500000000000004);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Site.Logo', '/uploads/logo/logo128.png', 'String', '站点LOGO URL', 1, '2025-08-10 08:45:05.766512', NULL, 'Site', 279178454597894140);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Site.Branding.Favicon', '', 'String', '站点Favicon', 1, '2025-08-10 08:45:05.766512', NULL, NULL, 279178454635642880);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Site.Theme.PrimaryColor', '#3b82f6', 'String', '主题主色', 1, '2025-08-10 08:45:05.766512', NULL, NULL, 279178454660808700);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Site.Theme.SecondaryColor', '#2563eb', 'String', '主题辅色', 1, '2025-08-10 08:45:05.766512', NULL, NULL, 279178454685974530);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Site.Maintenance.Enabled', 'false', 'Bool', '是否启用维护模式', 1, '2025-08-10 08:45:05.766512', NULL, NULL, 279178454715334660);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Site.Footer.Text', '', 'String', '页脚文字', 1, '2025-08-10 08:45:05.766512', NULL, NULL, 279178454807609340);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Site.Subtitle', '欢迎使用GinkgoAdmin', 'String', '站点副标题', 1, '2025-08-10 08:45:05.766512', NULL, NULL, 279178454836969470);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Site.Login.WelcomeText', '', 'String', '登录欢迎语', 1, '2025-08-10 08:45:05.766512', NULL, NULL, 279178454862135300);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Site.Login.LeftPanelBackground', '', 'String', '登录页左侧面板背景', 1, '2025-08-10 08:45:05.766512', NULL, NULL, 279178454887301120);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Site.Animation.Enabled', 'true', 'Bool', '是否启用动画', 1, '2025-08-10 08:45:05.766512', NULL, NULL, 279178454912466940);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Site.Animation.Intensity', 'medium', 'String', '动画强度', 1, '2025-08-10 08:45:05.766512', NULL, NULL, 279178454933438460);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Site.ICP', '', 'String', 'ICP备案号', 1, '2025-08-10 08:45:05.766512', NULL, 'Site', 294179549183213600);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Site.PoliceICP', '', 'String', '公安备案号', 1, '2025-08-10 08:45:05.766512', NULL, 'Site', 294179549225156600);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Site.Cors.AllowedOrigins', '', 'String', 'CORS允许的来源', 1, '2025-08-10 08:45:05.766512', NULL, NULL, 279178455415783420);

-- Auth 认证配置
INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Auth.Jwt.AccessTokenMinutes', '120', 'Number', '访问令牌有效分钟数', 1, '2025-08-10 08:45:05.823963', NULL, NULL, 500000000000005);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Auth.Jwt.RefreshTokenDays', '7', 'Number', '刷新令牌有效天数', 1, '2025-08-10 08:45:05.828027', NULL, NULL, 500000000000006);

-- Security 安全配置
INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Security.Login.MaxFailedAttempts', '5', 'Number', '登录最大失败次数(触发锁定)', 1, '2025-08-10 08:45:05.811749', NULL, NULL, 500000000000007);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Security.Login.LockoutMinutes', '15', 'Number', '登录锁定分钟数', 1, '2025-08-10 08:45:05.815818', NULL, NULL, 500000000000008);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Security.PasswordPolicy', '{"minLen":8,"upper":true,"lower":true,"digit":true,"special":false}', 'Json', '密码策略', 1, '2025-08-10 08:45:05.815818', NULL, NULL, 500000000000009);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Security.BlockedIPs', '[]', 'Json', '封禁IP列表', 1, '2025-08-10 08:45:05.815818', NULL, NULL, 279178455059267600);

-- Mail 邮件配置（敏感值使用占位符）
INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Mail.Smtp.Host', '', 'String', 'SMTP服务器地址', 1, '2025-08-10 08:45:05.856743', NULL, NULL, 500000000000010);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Mail.Smtp.Port', '465', 'Number', 'SMTP端口', 1, '2025-08-10 08:45:05.881520', NULL, NULL, 500000000000011);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Mail.Ssl.Enable', 'true', 'Bool', '启用SSL', 1, '2025-08-10 08:45:05.889927', NULL, NULL, 500000000000012);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Mail.From.Address', '', 'String', '发件人地址', 1, '2025-08-10 08:45:05.926896', NULL, NULL, 500000000000013);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Mail.From.DisplayName', 'GinkgoAdmin', 'String', '发件人显示名', 1, '2025-08-10 08:45:05.935055', NULL, NULL, 500000000000014);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Mail.Smtp.UserName', '', 'String', 'SMTP用户名', 1, '2025-08-10 08:45:05.935055', NULL, NULL, 279178455268982800);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Mail.Smtp.Password', '', 'String', 'SMTP密码', 1, '2025-08-10 08:45:05.935055', NULL, NULL, 279178455298342900);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Mail.Smtp.AuthType', 'Login', 'String', 'SMTP认证类型', 1, '2025-08-10 08:45:05.935055', NULL, NULL, 279178455319314430);

-- Storage 存储配置
INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Storage.Provider', 'Local', 'String', '存储提供者：Local/S3/OSS', 1, '2025-08-10 08:45:05.844291', NULL, NULL, 500000000000015);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Storage.Local.BasePath', 'uploads', 'String', '本地存储根路径', 1, '2025-08-10 08:45:05.844291', NULL, NULL, 500000000000016);

-- Upload 上传配置
INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Upload.MaxSizeMB', '20', 'Number', '上传文件最大大小(MB)', 1, '2025-08-10 08:45:05.963768', NULL, NULL, 500000000000017);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Upload.AllowedExtensions', '.jpg,.png,.pdf,.xlsx,.mp3,.mp4,.zip,.rar,.doc,.docx,.webm,.m4a,.wav,.pem', 'String', '允许上传的文件扩展名', 1, '2025-08-10 08:45:05.971987', NULL, NULL, 500000000000018);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Upload.BasePath', '/uploads', 'String', '默认上传目录', 1, '2025-08-10 08:45:05.971987', NULL, NULL, 500000000000019);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Upload.ImageCompress.Enabled', 'false', 'Bool', '上传图片时是否启用后端压缩', 1, '2025-08-10 08:45:05.971987', NULL, NULL, 301262667660656640);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Upload.ImageCompress.Quality', '75', 'Number', '图片压缩质量（10-100）', 1, '2025-08-10 08:45:05.971987', NULL, NULL, 301262667698405400);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Upload.ImageCompress.KeepOriginal', 'false', 'Bool', '压缩后是否保留原图', 1, '2025-08-10 08:45:05.971987', NULL, NULL, 301262667727765500);

-- Logging & Audit
INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Logging.Level', 'Information', 'String', '日志级别：Trace/Debug/Information/Warning/Error/Critical', 1, '2025-08-10 08:45:05.848414', NULL, NULL, 500000000000019);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Audit.RetentionDays', '90', 'Number', '审计/操作日志保留天数', 1, '2025-08-10 08:45:05.848414', NULL, NULL, 500000000000020);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Menus.CacheMinutes', '10', 'Number', '菜单缓存分钟数(客户端可用)', 1, '2025-08-10 08:45:05.848414', NULL, NULL, 500000000000021);

-- Feature Flags
INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('FeatureFlags.Captcha', 'true', 'Bool', '启用图形验证码', 1, '2025-08-10 08:45:05.852482', NULL, NULL, 500000000000022);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('FeatureFlags.ConfigCenter', 'true', 'Bool', '启用系统配置中心页面', 1, '2025-08-10 08:45:05.852482', NULL, NULL, 500000000000023);

-- Registration 注册配置
INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Registration.Enabled', 'true', 'Bool', '启用用户注册', 1, '2025-08-10 08:45:05.787186', NULL, NULL, 500000000000024);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Registration.RequireCaptcha', 'true', 'Bool', '注册需要验证码', 1, '2025-08-10 08:45:05.795353', NULL, NULL, 500000000000025);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Registration.Mode', 'free', 'String', '注册模式', 1, '2025-08-10 08:45:05.795353', NULL, NULL, 302799073730101250);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Registration.LoginCaptcha', 'true', 'Bool', '登录验证码', 1, '2025-08-10 08:45:05.795353', NULL, NULL, 302799073914650600);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Registration.LoginMethods', '["password","email_code"]', 'Json', '允许的登录方式', 1, '2025-08-10 08:45:05.795353', NULL, NULL, 302799073977565200);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Registration.DefaultDepartmentId', '100000000000004', 'String', '注册用户默认部门ID', 1, '2025-08-10 08:45:05.795353', NULL, NULL, 279178455013130240);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Registration.DefaultRoleIds', '["200000000000004"]', 'Json', '注册用户默认角色ID列表', 1, '2025-08-10 08:45:05.795353', NULL, NULL, 279178455038296060);

-- DataPermission 数据权限
INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('DataPermission.DefaultScope', 'Self', 'String', '默认数据权限范围', 1, '2025-08-10 08:45:05.819828', NULL, NULL, 500000000000026);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('DataPermission.AllowCrossLevel', 'false', 'Bool', '允许跨级数据访问', 1, '2025-08-10 08:45:05.827997', NULL, NULL, 500000000000027);

-- UI
INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('UI.Theme', 'Light', 'String', '主题：Light/Dark', 1, '2025-08-10 08:45:05.844291', NULL, NULL, 500000000000028);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('System.AdminEmail', 'admin@example.com', 'String', '系统管理员通知邮箱', 1, '2025-08-10 08:45:05.803590', NULL, NULL, 500000000000029);

-- Language 多语言配置
INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Language.MultiLang.Enabled', 'false', 'Bool', '是否启用多语言', 1, '2025-08-10 08:45:05.803590', NULL, 'Language', 292218005469790200);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Language.MultiLang.Default', 'zh-CN', 'String', '默认语言代码', 1, '2025-08-10 08:45:05.803590', NULL, 'Language', 292218005499150340);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Language.MultiLang.Languages', '[{"code":"zh-CN","urlCode":"zh","label":"中文","flag":"🇨🇳","required":true},{"code":"en","urlCode":"en","label":"English","flag":"🇺🇸","required":false},{"code":"ja","urlCode":"ja","label":"日本語","flag":"🇯🇵","required":false}]', 'Json', '可用语言列表', 1, '2025-08-10 08:45:05.803590', NULL, 'Language', 292218005520121860);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('Language.MultiLang.PluginOverrides', '{}', 'Json', '插件多语言覆盖配置', 1, '2025-08-10 08:45:05.803590', NULL, 'Language', 292218005549482000);

-- Mobile 移动端（UNIAPP）配置
INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`) 
VALUES (400000000000099, 300000000000002, NULL, 'Mobile', '移动端', 90, 1, NULL, '2026-06-04 00:00:00', NULL, 0, NULL, NULL);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('App.HomePlugin', '', 'String', 'UNIAPP端首页替换插件ID', 1, '2026-06-04 00:00:00', NULL, 'Mobile', 500000000000080);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('App.Privacy.ShowPopup', 'true', 'Bool', '首次启动弹出隐私政策', 1, '2026-06-04 00:00:00', NULL, 'Mobile', 500000000000081);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('App.Privacy.PolicyVersion', '1.0.0', 'String', '隐私政策版本号', 1, '2026-06-04 00:00:00', NULL, 'Mobile', 500000000000082);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('App.Privacy.PolicyContent', '<h2>隐私政策</h2><p><strong>更新日期：</strong>2026年1月1日&nbsp;|&nbsp;<strong>生效日期：</strong>2026年1月1日</p><p>感谢您使用本应用（以下简称「我们」）。我们深知个人信息对您的重要性，并会尽全力保护您的个人信息安全。请在注册、登录或使用本应用前，仔细阅读并充分理解本政策。</p><h3>一、我们如何收集和使用个人信息</h3><p>为向您提供账户注册、登录验证、消息通知、业务办理等基础功能，我们可能收集以下信息：</p><ul><li><strong>帐号信息：</strong>用户名、昵称、密码（加密存储）</li><li><strong>联系信息：</strong>手机号码、电子邮箱（由您自愿填写）</li><li><strong>头像与简介：</strong>用于个人资料展示（可选）</li><li><strong>设备信息：</strong>设备型号、操作系统版本、应用版本号</li><li><strong>日志信息：</strong>登录时间、操作记录（用于安全审计与故障排查）</li></ul><h3>二、我们如何使用 Cookie 和同类技术</h3><p>为保障登录状态与安全，我们可能在本地存储必要的令牌与偏好设置（如字体大小、隐私同意记录），不会用于与提供服务无关的目的。</p><h3>三、信息的存储与保护</h3><p>您的个人信息存储于中华人民共和国境内服务器。我们采取加密传输、权限隔离、访问日志审计等安全措施保护您的数据。</p><h3>四、您的权利</h3><p>您依法享有以下权利，可在应用<strong>「我的」→「隐私与合规」</strong>中操作：</p><ul><li>查阅、更正您的个人信息</li><li>删除非必要的个人信息（邮箱、手机、头像、个人简介等）</li><li>注销用户帐号</li><li>撤回对本隐私政策的同意</li></ul><h3>五、未成年人保护</h3><p>若您未满 18 周岁，请在监护人陪同下阅读本政策，并在取得监护人同意后再使用本应用。</p><h3>六、政策更新</h3><p>我们可能适时修订本政策。重大变更将以应用内弹窗或公告方式通知您；若您继续使用，即视为同意修订后的政策。</p><h3>七、联系我们</h3><p>如对本政策有任何疑问、意见或投诉，请通过应用内「帮助中心」或联系系统管理员，我们将在合理期限内回复。</p>', 'RichText', '隐私政策内容（示例，可在后台「系统配置-移动端」修改）', 1, '2026-06-04 00:00:00', NULL, 'Mobile', 500000000000083);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('App.Privacy.UserAgreementContent', '<h2>用户服务协议</h2><p><strong>更新日期：</strong>2026年1月1日&nbsp;|&nbsp;<strong>生效日期：</strong>2026年1月1日</p><p>欢迎您使用本应用。本协议是您与运营方之间关于使用本应用服务所订立的协议，请您仔细阅读。</p><h3>一、服务说明</h3><p>本应用提供企业级移动办公、业务管理与消息通知等能力。您完成注册并登录，即表示您已阅读、理解并同意受本协议及《隐私政策》约束。</p><h3>二、帐号注册与安全</h3><p>您应提供真实、准确、完整的信息完成注册，并妥善保管帐号与密码。因帐号保管不善导致的损失，由您自行承担相应责任。</p><h3>三、用户行为规范</h3><p>您在使用本应用时，不得从事以下行为：</p><ul><li>违反法律法规、公序良俗或本协议约定</li><li>传播违法、虚假、侵权或骚扰性信息</li><li>以任何方式干扰、破坏系统或他人正常使用</li><li>未经授权访问、抓取或篡改系统数据</li></ul><h3>四、隐私保护</h3><p>我们重视您的隐私保护，个人信息处理规则详见《隐私政策》。使用本应用即表示您同时同意《隐私政策》。</p><h3>五、知识产权</h3><p>本应用的界面设计、程序代码、文档资料等知识产权归运营方或相关权利人所有。未经授权，不得复制、修改、传播或用于商业用途。</p><h3>六、免责声明</h3><p>因不可抗力、网络故障、第三方服务异常等非我们可控原因导致的服务中断，我们将在合理范围内协助恢复，但不承担由此产生的间接损失。</p><h3>七、协议变更与终止</h3><p>我们有权根据业务需要更新本协议，更新后将通过应用内公告或弹窗告知。您可随时通过「隐私与合规」申请注销帐号以终止服务。</p><h3>八、联系我们</h3><p>如对本协议有任何疑问，请通过应用内「帮助中心」联系我们。</p>', 'RichText', '用户协议内容（示例，可在后台「系统配置-移动端」修改）', 1, '2026-06-04 00:00:00', NULL, 'Mobile', 500000000000084);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('App.Privacy.EnableCorrectInfo', 'true', 'Bool', '开启更正/删除个人信息', 1, '2026-06-04 00:00:00', NULL, 'Mobile', 500000000000085);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('App.Privacy.EnableDeleteAccount', 'true', 'Bool', '开启注销用户帐号', 1, '2026-06-04 00:00:00', NULL, 'Mobile', 500000000000086);

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`) 
VALUES ('App.Privacy.EnableWithdrawConsent', 'true', 'Bool', '开启撤回同意隐私协议', 1, '2026-06-04 00:00:00', NULL, 'Mobile', 500000000000087);



-- ============================================================================
-- Initial Data: Verification Templates (6 条邮件模板)
-- ============================================================================

-- ----------------------------
-- Records of ginkgo_Sys_VerificationTemplate
-- ----------------------------

-- 模板1: 找回密码邮件模板 (Purpose=0 密码重置, Channel=0 邮件)
INSERT INTO `ginkgo_Sys_VerificationTemplate` (`Id`, `Name`, `Purpose`, `Channel`, `Subject`, `BodyTemplate`, `IsHtml`, `IsDefault`, `Enabled`, `SortOrder`, `CreatedAt`, `UpdatedAt`) 
VALUES (1, '找回密码邮件模板', 0, 0, '{appName} {purpose} - 验证码', 
'<div style="background-color:#f8fafc;padding:50px 20px;font-family:-apple-system,BlinkMacSystemFont,''Segoe UI'',Roboto,''Helvetica Neue'',Arial,sans-serif;">\n  <table align="center" border="0" cellpadding="0" cellspacing="0" width="100%" style="max-width:600px;background-color:#ffffff;border-radius:16px;box-shadow:0 4px 20px rgba(0,0,0,0.03);overflow:hidden;margin:0 auto;border:1px solid #f1f5f9;">\n    <tr><td style="height:8px;background:linear-gradient(90deg, #6366f1 0%, #8b5cf6 100%);"></td></tr>\n    <tr>\n      <td style="padding:48px 48px 32px;text-align:center;">\n        <div style="margin-bottom:32px;"><span style="font-size:28px;font-weight:800;color:#0f172a;letter-spacing:-0.5px;">{appName}</span></div>\n        <h2 style="margin:0 0 16px;font-size:20px;font-weight:600;color:#1e293b;">{purpose}验证</h2>\n        <p style="margin:0 0 32px;font-size:15px;line-height:1.6;color:#64748b;">您收到此邮件是因为我们收到了您的 <strong>{purpose}</strong> 请求。如果这是您本人的操作，请使用下方的高安全验证码进行验证。</p>\n        <div style="background-color:#f8fafc;border:2px dashed #cbd5e1;border-radius:12px;padding:32px 20px;margin:0 auto 32px;">\n          <span style="display:block;font-size:12px;text-transform:uppercase;letter-spacing:1px;color:#64748b;margin-bottom:12px;font-weight:600;">您的专属验证码</span>\n          <div style="font-size:46px;font-weight:700;letter-spacing:10px;color:#4f46e5;font-family:''SF Mono'',ui-monospace,Menlo,Monaco,Consolas,monospace;">{code}</div>\n        </div>\n        <table border="0" cellpadding="0" cellspacing="0" width="100%" style="margin-bottom:24px;">\n          <tr><td align="center" style="padding-bottom:8px;"><span style="font-size:14px;color:#64748b;">⏳ 验证码有效期：<strong style="color:#0f172a;">{minutes} 分钟</strong></span></td></tr>\n          <tr><td align="center"><span style="font-size:14px;color:#64748b;">🛡️ 提示：请勿将验证码泄露给他人</span></td></tr>\n        </table>\n      </td>\n    </tr>\n    <tr>\n      <td style="background-color:#f8fafc;padding:24px 48px;text-align:center;border-top:1px solid #f1f5f9;">\n        <p style="margin:0 0 8px;font-size:13px;color:#94a3b8;">如非本人操作，请立刻忽略此邮件或联系管理员。</p>\n        <p style="margin:0;font-size:12px;color:#cbd5e1;">&copy; {appName} 自动发送服务</p>\n      </td>\n    </tr>\n  </table>\n</div>', 
1, 1, 1, 0, '2025-08-10 12:15:16', NULL);

-- 模板2: 登录验证邮件模板 (Purpose=1 登录验证, Channel=0 邮件)
INSERT INTO `ginkgo_Sys_VerificationTemplate` (`Id`, `Name`, `Purpose`, `Channel`, `Subject`, `BodyTemplate`, `IsHtml`, `IsDefault`, `Enabled`, `SortOrder`, `CreatedAt`, `UpdatedAt`) 
VALUES (2, '登录验证邮件模板', 1, 0, '{appName} 登录验证码', 
'<div style="background-color:#f8fafc;padding:50px 20px;font-family:-apple-system,BlinkMacSystemFont,''Segoe UI'',Roboto,''Helvetica Neue'',Arial,sans-serif;">\n  <table align="center" border="0" cellpadding="0" cellspacing="0" width="100%" style="max-width:600px;background-color:#ffffff;border-radius:16px;box-shadow:0 4px 20px rgba(0,0,0,0.03);overflow:hidden;margin:0 auto;border:1px solid #f1f5f9;">\n    <tr><td style="height:8px;background:linear-gradient(90deg, #3b82f6 0%, #06b6d4 100%);"></td></tr>\n    <tr>\n      <td style="padding:48px 48px 32px;text-align:center;">\n        <div style="margin-bottom:32px;"><span style="font-size:28px;font-weight:800;color:#0f172a;letter-spacing:-0.5px;">{appName}</span></div>\n        <h2 style="margin:0 0 16px;font-size:20px;font-weight:600;color:#1e293b;">{purpose}</h2>\n        <p style="margin:0 0 32px;font-size:15px;line-height:1.6;color:#64748b;">您好，欢迎回来！为保障您的账户登录安全，请输入以下验证码完成身份校验。</p>\n        <div style="background-color:#f0f9ff;border:2px dashed #bae6fd;border-radius:12px;padding:32px 20px;margin:0 auto 32px;">\n          <span style="display:block;font-size:12px;text-transform:uppercase;letter-spacing:1px;color:#0284c7;margin-bottom:12px;font-weight:600;">登录安全验证码</span>\n          <div style="font-size:46px;font-weight:700;letter-spacing:10px;color:#0284c7;font-family:''SF Mono'',ui-monospace,Menlo,Monaco,Consolas,monospace;">{code}</div>\n        </div>\n        <table border="0" cellpadding="0" cellspacing="0" width="100%" style="margin-bottom:24px;">\n          <tr><td align="center" style="padding-bottom:8px;"><span style="font-size:14px;color:#64748b;">⏳ 该验证码将在 <strong style="color:#0f172a;">{minutes} 分钟</strong> 后失效</span></td></tr>\n        </table>\n      </td>\n    </tr>\n    <tr>\n      <td style="background-color:#f8fafc;padding:24px 48px;text-align:center;border-top:1px solid #f1f5f9;">\n        <p style="margin:0 0 8px;font-size:13px;color:#94a3b8;">如果这不是您的登录操作，表明您的密码可能已经泄露，请立即修改。</p>\n        <p style="margin:0;font-size:12px;color:#cbd5e1;">&copy; {appName} 安全中心</p>\n      </td>\n    </tr>\n  </table>\n</div>', 
1, 1, 1, 0, '2025-08-10 12:15:16', NULL);

-- 模板3: 危险操作确认邮件模板 (Purpose=10 危险操作, Channel=0 邮件)
INSERT INTO `ginkgo_Sys_VerificationTemplate` (`Id`, `Name`, `Purpose`, `Channel`, `Subject`, `BodyTemplate`, `IsHtml`, `IsDefault`, `Enabled`, `SortOrder`, `CreatedAt`, `UpdatedAt`) 
VALUES (3, '危险操作确认邮件模板', 10, 0, '{appName} 操作确认验证码', 
'<div style="background-color:#fef2f2;padding:50px 20px;font-family:-apple-system,BlinkMacSystemFont,''Segoe UI'',Roboto,''Helvetica Neue'',Arial,sans-serif;">\n  <table align="center" border="0" cellpadding="0" cellspacing="0" width="100%" style="max-width:600px;background-color:#ffffff;border-radius:16px;box-shadow:0 8px 30px rgba(239,68,68,0.08);overflow:hidden;margin:0 auto;border:1px solid #fee2e2;">\n    <tr><td style="height:8px;background:linear-gradient(90deg, #ef4444 0%, #f97316 100%);"></td></tr>\n    <tr>\n      <td style="padding:48px 48px 32px;text-align:center;">\n        <div style="margin-bottom:32px;"><span style="font-size:28px;font-weight:800;color:#0f172a;letter-spacing:-0.5px;">{appName}</span></div>\n        <h2 style="margin:0 0 16px;font-size:20px;font-weight:600;color:#ef4444;">操作安全确认</h2>\n        <p style="margin:0 0 32px;font-size:15px;line-height:1.6;color:#64748b;">系统检测到您正在请求执行 <strong>{purpose}</strong>。此类操作具有一定风险，请务必确认是您本人的意愿，并输入以下验证码继续：</p>\n        <div style="background-color:#fff5f5;border:2px dashed #fca5a5;border-radius:12px;padding:32px 20px;margin:0 auto 32px;">\n          <span style="display:block;font-size:12px;text-transform:uppercase;letter-spacing:1px;color:#dc2626;margin-bottom:12px;font-weight:600;">高风险操作验证码</span>\n          <div style="font-size:46px;font-weight:700;letter-spacing:10px;color:#dc2626;font-family:''SF Mono'',ui-monospace,Menlo,Monaco,Consolas,monospace;">{code}</div>\n        </div>\n        <table border="0" cellpadding="0" cellspacing="0" width="100%" style="margin-bottom:24px;">\n          <tr><td align="center" style="padding-bottom:8px;"><span style="font-size:14px;color:#64748b;">⏳ 有效时间还剩：<strong style="color:#0f172a;">{minutes} 分钟</strong></span></td></tr>\n        </table>\n      </td>\n    </tr>\n    <tr>\n      <td style="background-color:#fef2f2;padding:24px 48px;text-align:center;border-top:1px solid #fee2e2;">\n        <p style="margin:0 0 8px;font-size:13px;color:#ef4444;font-weight:500;">🚨 严正警告：绝不要将此验证码发送给任何人！遇到索要验证码的均是诈骗！</p>\n        <p style="margin:0;font-size:12px;color:#fca5a5;">&copy; {appName} 安全中心拦截</p>\n      </td>\n    </tr>\n  </table>\n</div>', 
1, 1, 1, 0, '2025-08-10 12:15:16', NULL);

-- 模板4: 注册验证码邮件模板 (Purpose=2 注册, Channel=0 邮件)
INSERT INTO `ginkgo_Sys_VerificationTemplate` (`Id`, `Name`, `Purpose`, `Channel`, `Subject`, `BodyTemplate`, `IsHtml`, `IsDefault`, `Enabled`, `SortOrder`, `CreatedAt`, `UpdatedAt`) 
VALUES (1913600000100001, '注册验证码邮件模板', 2, 0, '{appName} - 注册验证码', 
'<div style="margin:0;padding:0;background:#eef2ff;font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,Helvetica Neue,Arial,sans-serif"><table align="center" border="0" cellpadding="0" cellspacing="0" width="100%" style="max-width:600px;margin:40px auto;background:#fff;border-radius:20px;box-shadow:0 8px 40px rgba(99,102,241,.12);overflow:hidden"><tr><td style="height:6px;background:linear-gradient(135deg,#6366f1,#3b82f6,#06b6d4)"></td></tr><tr><td style="padding:48px 48px 16px;text-align:center"><h1 style="margin:0 0 8px;font-size:24px;font-weight:700;color:#1e293b">欢迎加入 {appName}</h1><p style="margin:0 0 32px;font-size:15px;color:#64748b;line-height:1.6">您正在注册账户，请使用以下验证码完成验证。</p></td></tr><tr><td style="padding:0 48px"><div style="background:linear-gradient(135deg,#eef2ff,#e0e7ff);border:2px solid #c7d2fe;border-radius:16px;padding:32px 20px;text-align:center"><div style="font-size:11px;text-transform:uppercase;letter-spacing:2px;color:#6366f1;font-weight:700;margin-bottom:12px">验证码</div><div style="font-size:48px;font-weight:800;letter-spacing:12px;color:#4338ca;font-family:ui-monospace,Menlo,Consolas,monospace">{code}</div></div></td></tr><tr><td style="padding:24px 48px 40px;text-align:center"><p style="margin:0 0 8px;font-size:13px;color:#475569">有效期 <strong>{minutes} 分钟</strong></p><p style="margin:0;font-size:13px;color:#94a3b8">请勿将验证码分享给任何人</p></td></tr><tr><td style="background:#f8fafc;padding:20px 48px;text-align:center;border-top:1px solid #f1f5f9"><p style="margin:0 0 4px;font-size:12px;color:#94a3b8">如非本人操作请忽略此邮件</p><p style="margin:0;font-size:11px;color:#cbd5e1">&copy; {appName} 高质量交付底座</p></td></tr></table></div>', 
1, 1, 1, 0, '2025-08-10 14:44:33', NULL);

-- 模板5: 绑定邮箱验证码模板 (Purpose=3 绑定邮箱, Channel=0 邮件)
INSERT INTO `ginkgo_Sys_VerificationTemplate` (`Id`, `Name`, `Purpose`, `Channel`, `Subject`, `BodyTemplate`, `IsHtml`, `IsDefault`, `Enabled`, `SortOrder`, `CreatedAt`, `UpdatedAt`) 
VALUES (1913600000100002, '绑定邮箱验证码模板', 3, 0, '{appName} - 绑定邮箱验证码', 
'<div style="margin:0;padding:0;background:#f0f9ff;font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,Helvetica Neue,Arial,sans-serif"><table align="center" border="0" cellpadding="0" cellspacing="0" width="100%" style="max-width:600px;margin:40px auto;background:#fff;border-radius:20px;box-shadow:0 6px 30px rgba(14,165,233,.1);overflow:hidden"><tr><td style="height:6px;background:linear-gradient(90deg,#0ea5e9,#06b6d4,#14b8a6)"></td></tr><tr><td style="padding:48px 48px 16px;text-align:center"><h1 style="margin:0 0 8px;font-size:22px;font-weight:700;color:#0c4a6e">绑定邮箱验证</h1><p style="margin:0 0 32px;font-size:15px;color:#64748b;line-height:1.6">您正在 {appName} 绑定邮箱，请使用以下验证码完成验证。</p></td></tr><tr><td style="padding:0 48px"><div style="background:#f0f9ff;border:2px solid #bae6fd;border-radius:16px;padding:32px 20px;text-align:center"><div style="font-size:11px;text-transform:uppercase;letter-spacing:2px;color:#0284c7;font-weight:700;margin-bottom:12px">验证码</div><div style="font-size:48px;font-weight:800;letter-spacing:12px;color:#0369a1;font-family:ui-monospace,Menlo,Consolas,monospace">{code}</div></div></td></tr><tr><td style="padding:24px 48px 40px;text-align:center"><p style="margin:0 0 8px;font-size:13px;color:#475569">有效期 <strong>{minutes} 分钟</strong></p><p style="margin:0;font-size:13px;color:#94a3b8">请勿将验证码分享给任何人</p></td></tr><tr><td style="background:#f0f9ff;padding:20px 48px;text-align:center;border-top:1px solid #e0f2fe"><p style="margin:0 0 4px;font-size:12px;color:#94a3b8">如非本人操作请忽略此邮件</p><p style="margin:0;font-size:11px;color:#cbd5e1">&copy; {appName}</p></td></tr></table></div>', 
1, 1, 1, 0, '2025-08-10 14:44:33', NULL);

-- 模板6: 绑定手机验证码模板 (Purpose=4 绑定手机, Channel=0 邮件)
INSERT INTO `ginkgo_Sys_VerificationTemplate` (`Id`, `Name`, `Purpose`, `Channel`, `Subject`, `BodyTemplate`, `IsHtml`, `IsDefault`, `Enabled`, `SortOrder`, `CreatedAt`, `UpdatedAt`) 
VALUES (1913600000100003, '绑定手机验证码模板', 4, 0, '{appName} - 绑定手机验证码', 
'<div style="margin:0;padding:0;background:#fefce8;font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,Helvetica Neue,Arial,sans-serif"><table align="center" border="0" cellpadding="0" cellspacing="0" width="100%" style="max-width:600px;margin:40px auto;background:#fff;border-radius:20px;box-shadow:0 6px 30px rgba(234,179,8,.08);overflow:hidden"><tr><td style="height:6px;background:linear-gradient(90deg,#eab308,#f59e0b,#f97316)"></td></tr><tr><td style="padding:48px 48px 16px;text-align:center"><h1 style="margin:0 0 8px;font-size:22px;font-weight:700;color:#713f12">绑定手机验证</h1><p style="margin:0 0 32px;font-size:15px;color:#64748b;line-height:1.6">您正在 {appName} 绑定手机号，请使用以下验证码完成验证。</p></td></tr><tr><td style="padding:0 48px"><div style="background:#fefce8;border:2px solid #fde68a;border-radius:16px;padding:32px 20px;text-align:center"><div style="font-size:11px;text-transform:uppercase;letter-spacing:2px;color:#ca8a04;font-weight:700;margin-bottom:12px">验证码</div><div style="font-size:48px;font-weight:800;letter-spacing:12px;color:#a16207;font-family:ui-monospace,Menlo,Consolas,monospace">{code}</div></div></td></tr><tr><td style="padding:24px 48px 40px;text-align:center"><p style="margin:0 0 8px;font-size:13px;color:#475569">有效期 <strong>{minutes} 分钟</strong></p><p style="margin:0;font-size:13px;color:#94a3b8">请勿将验证码分享给任何人</p></td></tr><tr><td style="background:#fefce8;padding:20px 48px;text-align:center;border-top:1px solid #fef9c3"><p style="margin:0 0 4px;font-size:12px;color:#94a3b8">如非本人操作请忽略此邮件</p><p style="margin:0;font-size:11px;color:#cbd5e1">&copy; {appName}</p></td></tr></table></div>', 
1, 1, 1, 0, '2025-08-10 14:44:33', NULL);



-- ============================================================================
-- Initial Data: System Files (LOGO 等预置文件)
-- ============================================================================

-- ----------------------------
-- Records of ginkgo_Sys_File
-- ----------------------------

-- 系统 LOGO 文件记录
INSERT INTO `ginkgo_Sys_File` (`Id`, `FileName`, `ContentType`, `Size`, `Hash`, `StorageProvider`, `StoragePath`, `Url`, `OwnerId`, `Tags`, `Version`, `type`, `DepartmentId`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `IsDeleted`, `DeletedAt`, `DeletedBy`)
VALUES (500000000100001, 'logo128.png', 'image/png', 14345, '7EF8FF1116D444EC7D1B795093E598DD48FA20CCC5E16E55B3DA963E7F3E195B', 'Local', 'logo/logo128.png', '/uploads/logo/logo128.png', NULL, 'logo,system', 1, 'logo', NULL, '2025-08-10 08:45:05.000000', NULL, NULL, NULL, 0, NULL, NULL);


-- ============================================================================
-- Footer: 恢复外键检查
-- ============================================================================

SET FOREIGN_KEY_CHECKS = 1;

-- ============================================================================
-- GinkgoAdmin 安装初始化 SQL 执行完毕
-- ============================================================================

