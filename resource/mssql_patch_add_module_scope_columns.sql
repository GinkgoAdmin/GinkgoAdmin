/*

  补丁：为共享菜单/字典/配置及菜单组相关表补全结构（SQL Server / Snowflake ID 版）

  适用：已安装但表结构缺少 Module / RequireGrant / IsDefault / RoleMenuGroupItem 的历史实例

*/



IF COL_LENGTH('ginkgo_Sys_Menu', 'Module') IS NULL

BEGIN

  ALTER TABLE [ginkgo_Sys_Menu] ADD [Module] NVARCHAR(64) NOT NULL

    CONSTRAINT [DF_ginkgo_Sys_Menu_Module] DEFAULT 'sys';

END;

GO

UPDATE [ginkgo_Sys_Menu] SET [Module] = 'sys' WHERE [Module] IS NULL OR [Module] = '';

GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Menu_Module' AND object_id = OBJECT_ID('ginkgo_Sys_Menu'))

  CREATE INDEX [IX_Menu_Module] ON [ginkgo_Sys_Menu] ([Module]);

GO



IF COL_LENGTH('ginkgo_Sys_Dictionary', 'Module') IS NULL

BEGIN

  ALTER TABLE [ginkgo_Sys_Dictionary] ADD [Module] NVARCHAR(64) NOT NULL

    CONSTRAINT [DF_ginkgo_Sys_Dictionary_Module] DEFAULT 'sys';

END;

GO

UPDATE [ginkgo_Sys_Dictionary] SET [Module] = 'sys' WHERE [Module] IS NULL OR [Module] = '';

GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dictionary_Module' AND object_id = OBJECT_ID('ginkgo_Sys_Dictionary'))

  CREATE INDEX [IX_Dictionary_Module] ON [ginkgo_Sys_Dictionary] ([Module]);

GO



IF COL_LENGTH('ginkgo_Sys_DictionaryItem', 'Module') IS NULL

BEGIN

  ALTER TABLE [ginkgo_Sys_DictionaryItem] ADD [Module] NVARCHAR(64) NOT NULL

    CONSTRAINT [DF_ginkgo_Sys_DictionaryItem_Module] DEFAULT 'sys';

END;

GO

UPDATE [ginkgo_Sys_DictionaryItem] SET [Module] = 'sys' WHERE [Module] IS NULL OR [Module] = '';

GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DictionaryItem_Module' AND object_id = OBJECT_ID('ginkgo_Sys_DictionaryItem'))

  CREATE INDEX [IX_DictionaryItem_Module] ON [ginkgo_Sys_DictionaryItem] ([Module]);

GO



IF COL_LENGTH('ginkgo_Sys_Settings', 'Module') IS NULL

BEGIN

  ALTER TABLE [ginkgo_Sys_Settings] ADD [Module] NVARCHAR(64) NOT NULL

    CONSTRAINT [DF_ginkgo_Sys_Settings_Module] DEFAULT 'sys';

END;

GO

UPDATE [ginkgo_Sys_Settings] SET [Module] = 'sys' WHERE [Module] IS NULL OR [Module] = '';

GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Settings_Module' AND object_id = OBJECT_ID('ginkgo_Sys_Settings'))

  CREATE INDEX [IX_Settings_Module] ON [ginkgo_Sys_Settings] ([Module]);

GO



IF COL_LENGTH('ginkgo_Sys_MenuGroup', 'IsDefault') IS NULL

BEGIN

  ALTER TABLE [ginkgo_Sys_MenuGroup] ADD [IsDefault] BIT NOT NULL

    CONSTRAINT [DF_ginkgo_Sys_MenuGroup_IsDefault] DEFAULT 0;

END;

GO



IF COL_LENGTH('ginkgo_Sys_MenuGroupItem', 'Module') IS NULL

BEGIN

  ALTER TABLE [ginkgo_Sys_MenuGroupItem] ADD [Module] NVARCHAR(128) NOT NULL

    CONSTRAINT [DF_ginkgo_Sys_MenuGroupItem_Module] DEFAULT 'sys';

END;

GO

IF COL_LENGTH('ginkgo_Sys_MenuGroupItem', 'Module') IS NOT NULL

BEGIN

  ALTER TABLE [ginkgo_Sys_MenuGroupItem] ALTER COLUMN [Module] NVARCHAR(128) NOT NULL;

END;

GO

UPDATE [ginkgo_Sys_MenuGroupItem] SET [Module] = 'sys' WHERE [Module] IS NULL OR [Module] = '';

GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MenuGroupItem_Module' AND object_id = OBJECT_ID('ginkgo_Sys_MenuGroupItem'))

  CREATE INDEX [IX_MenuGroupItem_Module] ON [ginkgo_Sys_MenuGroupItem] ([Module]);

GO



IF COL_LENGTH('ginkgo_Sys_MenuGroupItem', 'RequireGrant') IS NULL

BEGIN

  ALTER TABLE [ginkgo_Sys_MenuGroupItem] ADD [RequireGrant] BIT NOT NULL

    CONSTRAINT [DF_ginkgo_Sys_MenuGroupItem_RequireGrant] DEFAULT 0;

END;

GO



IF OBJECT_ID('ginkgo_Sys_RoleMenuGroupItem', 'U') IS NULL

BEGIN

  CREATE TABLE [ginkgo_Sys_RoleMenuGroupItem] (

    [Id] BIGINT NOT NULL,

    [RoleId] BIGINT NOT NULL,

    [MenuGroupItemId] BIGINT NOT NULL,

    [CreatedAt] DATETIME2(6) NOT NULL,

    [CreatedBy] BIGINT NULL,

    PRIMARY KEY ([Id])

  );

END;

GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_RoleMenuGroupItem' AND object_id = OBJECT_ID('ginkgo_Sys_RoleMenuGroupItem'))

  CREATE UNIQUE INDEX [UK_RoleMenuGroupItem] ON [ginkgo_Sys_RoleMenuGroupItem] ([RoleId], [MenuGroupItemId]);

GO

