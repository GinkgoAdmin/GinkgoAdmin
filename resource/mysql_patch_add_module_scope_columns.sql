/*
  补丁：为共享菜单/字典/配置及菜单组相关表补全结构（MySQL）
  适用：已安装但表结构缺少 Module / RequireGrant / IsDefault / RoleMenuGroupItem 的历史实例
  说明：
    - sys = 主框架系统级数据
    - 插件数据应在安装时写入对应 ModuleId
  执行前请确认已连接到正确的 MySQL 业务库
*/

SET NAMES utf8mb4;

-- ginkgo_Sys_Menu
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
   WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ginkgo_Sys_Menu' AND COLUMN_NAME = 'Module') = 0,
  'ALTER TABLE `ginkgo_Sys_Menu` ADD COLUMN `Module` VARCHAR(64) NOT NULL DEFAULT ''sys'' COMMENT ''所属模块标识（sys=系统级，其他为插件ModuleId）'' AFTER `Id`',
  'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
UPDATE `ginkgo_Sys_Menu` SET `Module` = 'sys' WHERE `Module` IS NULL OR `Module` = '';
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.STATISTICS
   WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ginkgo_Sys_Menu' AND INDEX_NAME = 'IX_Menu_Module') = 0,
  'CREATE INDEX `IX_Menu_Module` ON `ginkgo_Sys_Menu` (`Module`)',
  'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ginkgo_Sys_Dictionary
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
   WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ginkgo_Sys_Dictionary' AND COLUMN_NAME = 'Module') = 0,
  'ALTER TABLE `ginkgo_Sys_Dictionary` ADD COLUMN `Module` VARCHAR(64) NOT NULL DEFAULT ''sys'' COMMENT ''所属模块标识（sys=系统级，其他为插件ModuleId）'' AFTER `Id`',
  'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
UPDATE `ginkgo_Sys_Dictionary` SET `Module` = 'sys' WHERE `Module` IS NULL OR `Module` = '';
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.STATISTICS
   WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ginkgo_Sys_Dictionary' AND INDEX_NAME = 'IX_Dictionary_Module') = 0,
  'CREATE INDEX `IX_Dictionary_Module` ON `ginkgo_Sys_Dictionary` (`Module`)',
  'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ginkgo_Sys_DictionaryItem
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
   WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ginkgo_Sys_DictionaryItem' AND COLUMN_NAME = 'Module') = 0,
  'ALTER TABLE `ginkgo_Sys_DictionaryItem` ADD COLUMN `Module` VARCHAR(64) NOT NULL DEFAULT ''sys'' COMMENT ''所属模块标识（sys=系统级，其他为插件ModuleId）'' AFTER `Id`',
  'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
UPDATE `ginkgo_Sys_DictionaryItem` SET `Module` = 'sys' WHERE `Module` IS NULL OR `Module` = '';
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.STATISTICS
   WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ginkgo_Sys_DictionaryItem' AND INDEX_NAME = 'IX_DictionaryItem_Module') = 0,
  'CREATE INDEX `IX_DictionaryItem_Module` ON `ginkgo_Sys_DictionaryItem` (`Module`)',
  'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ginkgo_Sys_Settings
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
   WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ginkgo_Sys_Settings' AND COLUMN_NAME = 'Module') = 0,
  'ALTER TABLE `ginkgo_Sys_Settings` ADD COLUMN `Module` VARCHAR(64) NOT NULL DEFAULT ''sys'' COMMENT ''所属模块标识（sys=系统级，其他为插件ModuleId）'' AFTER `Key`',
  'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
UPDATE `ginkgo_Sys_Settings` SET `Module` = 'sys' WHERE `Module` IS NULL OR `Module` = '';
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.STATISTICS
   WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ginkgo_Sys_Settings' AND INDEX_NAME = 'IX_Settings_Module') = 0,
  'CREATE INDEX `IX_Settings_Module` ON `ginkgo_Sys_Settings` (`Module`)',
  'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ginkgo_Sys_MenuGroupItem.Module / RequireGrant
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
   WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ginkgo_Sys_MenuGroupItem' AND COLUMN_NAME = 'Module') = 0,
  'ALTER TABLE `ginkgo_Sys_MenuGroupItem` ADD COLUMN `Module` VARCHAR(128) NOT NULL DEFAULT ''sys'' COMMENT ''模块归属（sys 或插件ModuleId）'' AFTER `Enabled`',
  'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
   WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ginkgo_Sys_MenuGroupItem' AND COLUMN_NAME = 'Module'
     AND CHARACTER_MAXIMUM_LENGTH < 128) > 0,
  'ALTER TABLE `ginkgo_Sys_MenuGroupItem` MODIFY COLUMN `Module` VARCHAR(128) NOT NULL DEFAULT ''sys'' COMMENT ''模块归属（sys 或插件ModuleId）''',
  'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
UPDATE `ginkgo_Sys_MenuGroupItem` SET `Module` = 'sys' WHERE `Module` IS NULL OR `Module` = '';
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.STATISTICS
   WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ginkgo_Sys_MenuGroupItem' AND INDEX_NAME = 'IX_MenuGroupItem_Module') = 0,
  'CREATE INDEX `IX_MenuGroupItem_Module` ON `ginkgo_Sys_MenuGroupItem` (`Module`)',
  'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
   WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ginkgo_Sys_MenuGroupItem' AND COLUMN_NAME = 'RequireGrant') = 0,
  'ALTER TABLE `ginkgo_Sys_MenuGroupItem` ADD COLUMN `RequireGrant` TINYINT(1) NOT NULL DEFAULT 0 COMMENT ''是否需要授权（0=公共可见 1=需授权）'' AFTER `Module`',
  'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ginkgo_Sys_MenuGroup.IsDefault
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
   WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ginkgo_Sys_MenuGroup' AND COLUMN_NAME = 'IsDefault') = 0,
  'ALTER TABLE `ginkgo_Sys_MenuGroup` ADD COLUMN `IsDefault` TINYINT(1) NOT NULL DEFAULT 0 COMMENT ''是否为该终端类型的默认菜单组'' AFTER `IsSystem`',
  'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ginkgo_Sys_RoleMenuGroupItem
CREATE TABLE IF NOT EXISTS `ginkgo_Sys_RoleMenuGroupItem` (
  `Id` BIGINT NOT NULL COMMENT '主键（Snowflake ID）',
  `RoleId` BIGINT NOT NULL COMMENT '角色Id',
  `MenuGroupItemId` BIGINT NOT NULL COMMENT '菜单组项Id',
  `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间',
  `CreatedBy` BIGINT NULL COMMENT '创建人',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='角色菜单组项授权表';
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.STATISTICS
   WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ginkgo_Sys_RoleMenuGroupItem' AND INDEX_NAME = 'UK_RoleMenuGroupItem') = 0,
  'CREATE UNIQUE INDEX `UK_RoleMenuGroupItem` ON `ginkgo_Sys_RoleMenuGroupItem` (`RoleId`, `MenuGroupItemId`)',
  'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
