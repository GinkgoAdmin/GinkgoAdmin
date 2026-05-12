/*
 Ginkgo 增量更新 - 多语言字段扩展 & 角色字段补充
 版本：v2026.03.23
 说明：为菜单、字典分类、字典条目、通知消息、系统设置表添加多语言 JSON 字段
       用于存储各语言版本的显示文本，格式: {"zh-CN":"值","en":"Value","ja":"値"}
       原有字段保持不变，作为默认值/回退值
*/

-- ============================================================================
-- 1. 菜单表：添加菜单名称多语言字段
-- ============================================================================
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
  WHERE TABLE_NAME = 'ginkgo_Sys_Menu' AND COLUMN_NAME = 'NameI18n');
SET @sql = IF(@col_exists = 0, 
  'ALTER TABLE `ginkgo_Sys_Menu` ADD COLUMN `NameI18n` JSON NULL COMMENT ''菜单名称-多语言 {"zh-CN":"系统管理","en":"System"}'' AFTER `Name`',
  'SELECT ''Column NameI18n already exists in ginkgo_Sys_Menu''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ============================================================================
-- 2. 字典分类表：添加字典名称和描述多语言字段
-- ============================================================================
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
  WHERE TABLE_NAME = 'ginkgo_Sys_Dictionary' AND COLUMN_NAME = 'NameI18n');
SET @sql = IF(@col_exists = 0, 
  'ALTER TABLE `ginkgo_Sys_Dictionary` ADD COLUMN `NameI18n` JSON NULL COMMENT ''字典名称-多语言'' AFTER `Name`',
  'SELECT ''Column NameI18n already exists in ginkgo_Sys_Dictionary''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
  WHERE TABLE_NAME = 'ginkgo_Sys_Dictionary' AND COLUMN_NAME = 'DescriptionI18n');
SET @sql = IF(@col_exists = 0, 
  'ALTER TABLE `ginkgo_Sys_Dictionary` ADD COLUMN `DescriptionI18n` JSON NULL COMMENT ''字典描述-多语言'' AFTER `Description`',
  'SELECT ''Column DescriptionI18n already exists in ginkgo_Sys_Dictionary''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ============================================================================
-- 3. 字典条目表：添加条目值多语言字段
-- ============================================================================
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
  WHERE TABLE_NAME = 'ginkgo_Sys_DictionaryItem' AND COLUMN_NAME = 'ValueI18n');
SET @sql = IF(@col_exists = 0, 
  'ALTER TABLE `ginkgo_Sys_DictionaryItem` ADD COLUMN `ValueI18n` JSON NULL COMMENT ''条目值-多语言'' AFTER `Value`',
  'SELECT ''Column ValueI18n already exists in ginkgo_Sys_DictionaryItem''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ============================================================================
-- 4. 通知消息表（ginkgo_Sys_NotifyMessage）：添加标题多语言字段
-- ============================================================================
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
  WHERE TABLE_NAME = 'ginkgo_Sys_NotifyMessage' AND COLUMN_NAME = 'TitleI18n');
SET @sql = IF(@col_exists = 0, 
  'ALTER TABLE `ginkgo_Sys_NotifyMessage` ADD COLUMN `TitleI18n` JSON NULL COMMENT ''标题-多语言'' AFTER `Title`',
  'SELECT ''Column TitleI18n already exists in ginkgo_Sys_NotifyMessage''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ============================================================================
-- 5. 系统设置表：添加描述多语言字段
-- ============================================================================
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
  WHERE TABLE_NAME = 'ginkgo_Sys_Settings' AND COLUMN_NAME = 'DescriptionI18n');
SET @sql = IF(@col_exists = 0, 
  'ALTER TABLE `ginkgo_Sys_Settings` ADD COLUMN `DescriptionI18n` JSON NULL COMMENT ''描述-多语言'' AFTER `Description`',
  'SELECT ''Column DescriptionI18n already exists in ginkgo_Sys_Settings''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ============================================================================
-- 6. 角色表：添加超级管理员标记和允许客户端字段
-- ============================================================================
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ginkgo_Sys_Role' AND COLUMN_NAME = 'IsSuperAdmin');
SET @sql = IF(@col_exists = 0, 
  'ALTER TABLE `ginkgo_Sys_Role` ADD COLUMN `IsSuperAdmin` TINYINT(1) NOT NULL DEFAULT 0 COMMENT ''是否超级管理员 0-否 1-是''',
  'SELECT ''Column IsSuperAdmin already exists in ginkgo_Sys_Role''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ginkgo_Sys_Role' AND COLUMN_NAME = 'AllowedClients');
SET @sql = IF(@col_exists = 0, 
  'ALTER TABLE `ginkgo_Sys_Role` ADD COLUMN `AllowedClients` VARCHAR(200) NOT NULL DEFAULT ''WEB_PORTAL'' COMMENT ''允许的客户端类型，多个用逗号分隔''',
  'SELECT ''Column AllowedClients already exists in ginkgo_Sys_Role''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
