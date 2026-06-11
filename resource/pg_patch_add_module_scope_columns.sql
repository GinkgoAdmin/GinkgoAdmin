/*

  补丁：为共享菜单/字典/配置及菜单组相关表补全结构（PostgreSQL）

  适用：已安装但表结构缺少 Module / RequireGrant / IsDefault / RoleMenuGroupItem 的历史实例

*/



ALTER TABLE "ginkgo_Sys_Menu"

  ADD COLUMN IF NOT EXISTS "Module" VARCHAR(64) NOT NULL DEFAULT 'sys';

UPDATE "ginkgo_Sys_Menu" SET "Module" = 'sys' WHERE "Module" IS NULL OR "Module" = '';

CREATE INDEX IF NOT EXISTS "IX_Menu_Module" ON "ginkgo_Sys_Menu" ("Module");



ALTER TABLE "ginkgo_Sys_Dictionary"

  ADD COLUMN IF NOT EXISTS "Module" VARCHAR(64) NOT NULL DEFAULT 'sys';

UPDATE "ginkgo_Sys_Dictionary" SET "Module" = 'sys' WHERE "Module" IS NULL OR "Module" = '';

CREATE INDEX IF NOT EXISTS "IX_Dictionary_Module" ON "ginkgo_Sys_Dictionary" ("Module");



ALTER TABLE "ginkgo_Sys_DictionaryItem"

  ADD COLUMN IF NOT EXISTS "Module" VARCHAR(64) NOT NULL DEFAULT 'sys';

UPDATE "ginkgo_Sys_DictionaryItem" SET "Module" = 'sys' WHERE "Module" IS NULL OR "Module" = '';

CREATE INDEX IF NOT EXISTS "IX_DictionaryItem_Module" ON "ginkgo_Sys_DictionaryItem" ("Module");



ALTER TABLE "ginkgo_Sys_Settings"

  ADD COLUMN IF NOT EXISTS "Module" VARCHAR(64) NOT NULL DEFAULT 'sys';

UPDATE "ginkgo_Sys_Settings" SET "Module" = 'sys' WHERE "Module" IS NULL OR "Module" = '';

CREATE INDEX IF NOT EXISTS "IX_Settings_Module" ON "ginkgo_Sys_Settings" ("Module");



ALTER TABLE "ginkgo_Sys_MenuGroup"

  ADD COLUMN IF NOT EXISTS "IsDefault" BOOLEAN NOT NULL DEFAULT FALSE;



ALTER TABLE "ginkgo_Sys_MenuGroupItem"

  ADD COLUMN IF NOT EXISTS "Module" VARCHAR(128) NOT NULL DEFAULT 'sys';

UPDATE "ginkgo_Sys_MenuGroupItem" SET "Module" = 'sys' WHERE "Module" IS NULL OR "Module" = '';

ALTER TABLE "ginkgo_Sys_MenuGroupItem"

  ALTER COLUMN "Module" TYPE VARCHAR(128);

ALTER TABLE "ginkgo_Sys_MenuGroupItem"

  ADD COLUMN IF NOT EXISTS "RequireGrant" BOOLEAN NOT NULL DEFAULT FALSE;

CREATE INDEX IF NOT EXISTS "IX_MenuGroupItem_Module" ON "ginkgo_Sys_MenuGroupItem" ("Module");



CREATE TABLE IF NOT EXISTS "ginkgo_Sys_RoleMenuGroupItem" (

  "Id" BIGINT NOT NULL,

  "RoleId" BIGINT NOT NULL,

  "MenuGroupItemId" BIGINT NOT NULL,

  "CreatedAt" TIMESTAMP(6) NOT NULL,

  "CreatedBy" BIGINT NULL,

  PRIMARY KEY ("Id")

);

CREATE UNIQUE INDEX IF NOT EXISTS "UK_RoleMenuGroupItem" ON "ginkgo_Sys_RoleMenuGroupItem" ("RoleId", "MenuGroupItemId");

