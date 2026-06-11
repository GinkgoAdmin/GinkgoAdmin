-- 为 ginkgo_Sys_MenuGroupItem 增加 IsUniappHome 列（UNIAPP 框架启动首页标记）
-- 适用：已安装但表结构缺少 IsUniappHome 的历史实例

SET @db := DATABASE();

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
   WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'ginkgo_Sys_MenuGroupItem' AND COLUMN_NAME = 'IsUniappHome') = 0,
  'ALTER TABLE `ginkgo_Sys_MenuGroupItem` ADD COLUMN `IsUniappHome` TINYINT(1) NOT NULL DEFAULT 0 COMMENT ''是否设为UNIAPP框架启动首页'' AFTER `RequireGrant`',
  'SELECT ''IsUniappHome column already exists'' AS Info'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
