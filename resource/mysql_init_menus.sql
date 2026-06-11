/*
 Ginkgo 菜单初始化数据脚本（MySQL 版 - Snowflake ID）

 - 本脚本用于初始化 Ginkgo 框架的核心系统菜单
 - 需要先执行 mysql_install.sql 创建表结构和基础数据
 - 模块特定菜单由各模块的 install.sql 脚本负责

 ============================== 设计说明（2026-04-30 重构后）==============================
 - 菜单类型四级：
     Directory  目录（不可点击，仅承载子项）
     Item       页面入口（可点击，承载实际页面）
     Button     权限按钮（前端 v-permission 控制；其本身不带 Resource/Method）
     Api        后端接口（必带 Resource/Method；位于 Button 下作为子节点 或 直接挂在 Item 下作为页面级 API）

 - 命名规则：
     Item / Directory : Code = 'sys' / 'sys:<page>'                      （PermissionCode = NULL，已废弃）
     Button           : Route = '/system/<page>:<action>'   Code = 'sys:<page>:<action>'
     Api (按钮子)     : Route = '<button-route>:api[:<sub>]' Code = 'sys:<page>:<action>:api[:<sub>]'
     Api (页面级)     : Route = '<item-route>:api:<sub>'    Code = 'sys:<page>:api:<sub>'

 - ID 段分配：
     系统管理根目录:                 600000000000001
     系统管理子页 (Item/Directory):  600000000001001 - 600000000001012
     主框架 Button/Api 节点:         8010000000000010 - 8010000000000323（按页面分块，详见各小节）
     首页 (Item) 与其 Button/Api:    由 SeedDataInitializer 在程序首次启动时通过雪花 ID 创建，
                                       不在本脚本范围内（占用 9051xxx 段）

 - 各页面节点 ID 段：
     用户管理:   8010000000000010 - 8010000000000024
     角色权限:   8010000000000030 - 8010000000000046
     部门管理:   8010000000000060 - 8010000000000073
     菜单管理:   8010000000000090 - 8010000000000128
     数据字典:   8010000000000150 - 8010000000000166
     日志管理:   8010000000000180 - 8010000000000181
     文件管理:   8010000000000185 - 8010000000000197
     定时任务:   8010000000000210 - 8010000000000225
     模块管理:   8010000000000230 - 8010000000000265
     系统配置:   8010000000000280 - 8010000000000291
     通知管理:   8010000000000300 - 8010000000000323
*/

SET NAMES utf8mb4;

-- ============================================================================
-- 1. 系统管理根目录
-- ============================================================================
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `ItemMode`, `Code`, `SupportedClients`, `WpfRouteUrl`, `WebRouteUrl`, `WpfDisplayMode`, `WebDisplayMode`)
VALUES (600000000000001, 'sys', NULL, '系统管理', '/system', 'gear', 0, 1, NULL, '2025-08-10 00:00:00', 0, 'Directory', NULL, 'sys', 'WEB,WPF', '', '', 'Route', 'Route');


-- ============================================================================
-- 2. 系统管理子菜单 (ParentId: 600000000000001)
-- ============================================================================
-- 系统配置 (Directory, OrderNo: 0)
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `ItemMode`, `Code`, `SupportedClients`, `WpfRouteUrl`, `WebRouteUrl`, `WpfDisplayMode`, `WebDisplayMode`)
VALUES (600000000001008, 'sys', 600000000000001, '系统配置', '/system/config', 'gear', 0, 1, NULL, '2025-08-10 00:00:00', 0, 'Item', 'Tab', 'sys:config', 'WPF,WEB', 'SystemConfigPage', '../views/admin/system/SystemConfig.vue', 'Route', 'Route');

-- 通知管理 (Directory, OrderNo: 0)
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `ItemMode`, `Code`, `SupportedClients`, `WpfRouteUrl`, `WebRouteUrl`, `WpfDisplayMode`, `WebDisplayMode`)
VALUES (600000000001009, 'sys', 600000000000001, '通知管理', '/system/notify', 'bell', 0, 1, NULL, '2025-08-10 00:00:00', 0, 'Item', NULL, 'sys:notify', 'WPF,WEB', 'NotificationsPage', '../views/admin/system/Notifications.vue', 'Route', 'Route');

-- 用户管理 (Item, OrderNo: 110)
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `ItemMode`, `Code`, `SupportedClients`, `WpfRouteUrl`, `WebRouteUrl`)
VALUES (600000000001001, 'sys', 600000000000001, '用户管理', '/system/users', 'people', 110, 1, NULL, '2025-08-10 00:00:00', 0, 'Item', 'Tab', 'sys:users', 'WPF,WEB', 'UsersPage', '../views/admin/system/Users.vue');

-- 角色权限 (Item, OrderNo: 120)
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `ItemMode`, `Code`, `SupportedClients`, `WpfRouteUrl`, `WebRouteUrl`)
VALUES (600000000001002, 'sys', 600000000000001, '角色权限', '/system/roles', 'key', 120, 1, NULL, '2025-08-10 00:00:00', 0, 'Item', 'Tab', 'sys:roles', 'WPF,WEB', 'RolesPage', '../views/admin/system/Roles.vue');

-- 部门管理 (Item, OrderNo: 140)
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `ItemMode`, `Code`, `SupportedClients`, `WpfRouteUrl`, `WebRouteUrl`)
VALUES (600000000001003, 'sys', 600000000000001, '部门管理', '/system/departments', 'building', 140, 1, NULL, '2025-08-10 00:00:00', 0, 'Item', 'Tab', 'sys:depts', 'WPF,WEB', 'DepartmentsPage', '../views/admin/system/Departments.vue');

-- 菜单管理 (Item, OrderNo: 150)
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `ItemMode`, `Code`, `SupportedClients`, `WpfRouteUrl`, `WebRouteUrl`)
VALUES (600000000001004, 'sys', 600000000000001, '菜单管理', '/system/menus', 'list', 150, 1, NULL, '2025-08-10 00:00:00', 0, 'Item', 'Tab', 'sys:menus', 'WPF,WEB', 'MenusPage', '../views/admin/system/Menus.vue');

-- 数据字典 (Item, OrderNo: 160)
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `ItemMode`, `Code`, `SupportedClients`, `WpfRouteUrl`, `WebRouteUrl`)
VALUES (600000000001005, 'sys', 600000000000001, '数据字典', '/system/dictionaries', 'book', 160, 1, NULL, '2025-08-10 00:00:00', 0, 'Item', 'Tab', 'sys:dicts', 'WPF,WEB', 'DictionariesPage', '../views/admin/system/Dictionaries.vue');

-- 日志管理 (Item, OrderNo: 170)
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `ItemMode`, `Code`, `SupportedClients`, `WpfRouteUrl`, `WebRouteUrl`)
VALUES (600000000001006, 'sys', 600000000000001, '日志管理', '/system/logs', 'journal-text', 170, 1, NULL, '2025-08-10 00:00:00', 0, 'Item', 'Tab', 'sys:logs', 'WPF,WEB', 'LogsManagePage', '../views/admin/system/Logs.vue');

-- 文件管理 (Item, OrderNo: 180)
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `ItemMode`, `Code`, `SupportedClients`, `WpfRouteUrl`, `WebRouteUrl`)
VALUES (600000000001007, 'sys', 600000000000001, '文件管理', '/system/files', 'folder', 180, 1, NULL, '2025-08-10 00:00:00', 0, 'Item', 'Tab', 'sys:files', 'WPF,WEB', 'FilesPage', '../views/admin/system/Files.vue');

-- 定时任务 (Item, OrderNo: 190)
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `ItemMode`, `Code`, `SupportedClients`, `WpfRouteUrl`, `WebRouteUrl`)
VALUES (600000000001012, 'sys', 600000000000001, '定时任务', '/system/scheduled-tasks', 'clock', 190, 1, NULL, '2025-08-10 00:00:00', 0, 'Item', 'Tab', 'sys:tasks', 'WPF,WEB', 'ScheduledTasksPage', '../views/admin/system/ScheduledTasks.vue');

-- 模块管理 (Item, OrderNo: 200)
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `ItemMode`, `Code`, `SupportedClients`, `WpfRouteUrl`, `WebRouteUrl`, `WebDisplayMode`)
VALUES (600000000001011, 'sys', 600000000000001, '模块管理', '/system/modules', 'puzzle', 200, 1, NULL, '2025-08-10 00:00:00', 0, 'Item', 'Tab', 'sys:modules', 'WPF,WEB', 'ModulesPage', '../views/admin/system/ModuleManager.vue', 'Route');


-- ============================================================================
-- 3. 用户管理 Button / Api (ParentId: 600000000001001)
-- ============================================================================
-- Buttons
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`) VALUES
(8010000000000010, 600000000001001, '新增用户', '/system/users:add',            'person-plus', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:users:add',            'WPF,WEB'),
(8010000000000011, 600000000001001, '编辑用户', '/system/users:edit',           'pencil',      2, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:users:edit',           'WPF,WEB'),
(8010000000000012, 600000000001001, '删除用户', '/system/users:delete',         'person-dash', 3, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:users:delete',         'WPF,WEB'),
(8010000000000013, 600000000001001, '重置密码', '/system/users:reset-password', 'key',         4, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:users:reset-password', 'WPF,WEB');

-- Apis under buttons
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000014, 8010000000000010, '创建用户接口',   '/system/users:add:api',            'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:users:add:api',            'WPF,WEB', '/api/v1/users',                       'POST'),
(8010000000000015, 8010000000000011, '更新用户接口',   '/system/users:edit:api:update',    'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:users:edit:api:update',    'WPF,WEB', '/api/v1/users/{id}',                  'PUT'),
(8010000000000016, 8010000000000011, '保存用户角色',   '/system/users:edit:api:roles',     'gear', 2, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:users:edit:api:roles',     'WPF,WEB', '/api/v1/users/{id}/roles',            'POST'),
(8010000000000017, 8010000000000011, '保存用户部门',   '/system/users:edit:api:depts',     'gear', 3, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:users:edit:api:depts',     'WPF,WEB', '/api/v1/users/{id}/departments',      'POST'),
(8010000000000018, 8010000000000011, '管理员改密',     '/system/users:edit:api:password',  'gear', 4, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:users:edit:api:password',  'WPF,WEB', '/api/v1/users/{id}/password',         'POST'),
(8010000000000019, 8010000000000012, '删除用户接口',   '/system/users:delete:api',         'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:users:delete:api',         'WPF,WEB', '/api/v1/users/{id}',                  'DELETE'),
(8010000000000020, 8010000000000013, '重置密码接口',   '/system/users:reset-password:api', 'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:users:reset-password:api', 'WPF,WEB', '/api/v1/users/{id}/reset-password',   'POST');

-- Page-level Apis
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000021, 600000000001001, '用户列表',   '/system/users:api:list',      'list',        10, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:users:api:list',      'WPF,WEB', '/api/v1/users',                  'GET'),
(8010000000000022, 600000000001001, '用户详情',   '/system/users:api:detail',    'info-circle', 11, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:users:api:detail',    'WPF,WEB', '/api/v1/users/{id}',             'GET'),
(8010000000000023, 600000000001001, '取用户角色', '/system/users:api:roles-get', 'key',         12, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:users:api:roles-get', 'WPF,WEB', '/api/v1/users/{id}/roles',       'GET'),
(8010000000000024, 600000000001001, '取用户部门', '/system/users:api:depts-get', 'building',    13, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:users:api:depts-get', 'WPF,WEB', '/api/v1/users/{id}/departments', 'GET');


-- ============================================================================
-- 4. 角色权限 Button / Api (ParentId: 600000000001002)
-- ============================================================================
-- Buttons
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`) VALUES
(8010000000000030, 600000000001002, '新增角色',     '/system/roles:add',        'plus',   1, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:roles:add',        'WPF,WEB'),
(8010000000000031, 600000000001002, '编辑角色',     '/system/roles:edit',       'pencil', 2, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:roles:edit',       'WPF,WEB'),
(8010000000000032, 600000000001002, '删除角色',     '/system/roles:delete',     'trash',  3, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:roles:delete',     'WPF,WEB'),
(8010000000000033, 600000000001002, '保存权限',     '/system/roles:save',       'save',   4, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:roles:save',       'WPF,WEB'),
(8010000000000034, 600000000001002, '数据范围设置', '/system/roles:data-scope', 'shield', 5, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:roles:data-scope', 'WPF,WEB');

-- Apis under buttons
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000035, 8010000000000030, '创建角色接口', '/system/roles:add:api',        'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:roles:add:api',        'WPF,WEB', '/api/v1/roles',                  'POST'),
(8010000000000036, 8010000000000031, '更新角色接口', '/system/roles:edit:api',       'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:roles:edit:api',       'WPF,WEB', '/api/v1/roles/{id}',             'PUT'),
(8010000000000037, 8010000000000032, '删除角色接口', '/system/roles:delete:api',     'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:roles:delete:api',     'WPF,WEB', '/api/v1/roles/{id}',             'DELETE'),
(8010000000000038, 8010000000000033, '保存角色权限', '/system/roles:save:api',       'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:roles:save:api',       'WPF,WEB', '/api/v1/roles/{id}/permissions', 'POST'),
(8010000000000039, 8010000000000034, '设置数据范围', '/system/roles:data-scope:api', 'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:roles:data-scope:api', 'WPF,WEB', '/api/v1/roles/{id}/data-scope',  'POST');

-- Page-level Apis
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000040, 600000000001002, '角色列表',   '/system/roles:api:list',       'list',        10, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:roles:api:list',       'WPF,WEB', '/api/v1/roles',                  'GET'),
(8010000000000041, 600000000001002, '角色树',     '/system/roles:api:tree',       'diagram-3',   11, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:roles:api:tree',       'WPF,WEB', '/api/v1/roles/tree',             'GET'),
(8010000000000042, 600000000001002, '角色详情',   '/system/roles:api:detail',     'info-circle', 12, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:roles:api:detail',     'WPF,WEB', '/api/v1/roles/{id}',             'GET'),
(8010000000000043, 600000000001002, '全部权限',   '/system/roles:api:perms-all',  'key',         13, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:roles:api:perms-all',  'WPF,WEB', '/api/v1/roles/permissions/all',  'GET'),
(8010000000000044, 600000000001002, '权限树',     '/system/roles:api:perms-tree', 'diagram-3',   14, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:roles:api:perms-tree', 'WPF,WEB', '/api/v1/roles/permissions/tree', 'GET'),
(8010000000000045, 600000000001002, '取角色权限', '/system/roles:api:perms-get',  'key',         15, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:roles:api:perms-get',  'WPF,WEB', '/api/v1/roles/{id}/permissions', 'GET'),
(8010000000000046, 600000000001002, '取数据范围', '/system/roles:api:scope-get',  'shield',      16, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:roles:api:scope-get',  'WPF,WEB', '/api/v1/roles/{id}/data-scope',  'GET');


-- ============================================================================
-- 5. 部门管理 Button / Api (ParentId: 600000000001003)
-- ============================================================================
-- Buttons
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`) VALUES
(8010000000000060, 600000000001003, '新增部门',        '/system/departments:add',          'plus',         1, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:depts:add',         'WPF,WEB'),
(8010000000000061, 600000000001003, '编辑部门',        '/system/departments:edit',         'pencil',       2, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:depts:edit',        'WPF,WEB'),
(8010000000000062, 600000000001003, '删除部门',        '/system/departments:delete',       'trash',        3, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:depts:delete',      'WPF,WEB'),
(8010000000000063, 600000000001003, '设置/撤销负责人', '/system/departments:set-manager',  'person-check', 4, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:depts:set-manager', 'WPF,WEB'),
(8010000000000064, 600000000001003, '移除部门用户',    '/system/departments:remove-user',  'person-dash',  5, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:depts:remove-user', 'WPF,WEB');

-- Apis under buttons
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000065, 8010000000000060, '创建部门接口',     '/system/departments:add:api',         'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:depts:add:api',         'WPF,WEB', '/api/v1/departments',                             'POST'),
(8010000000000066, 8010000000000061, '更新部门接口',     '/system/departments:edit:api',        'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:depts:edit:api',        'WPF,WEB', '/api/v1/departments/{id}',                        'PUT'),
(8010000000000067, 8010000000000062, '删除部门接口',     '/system/departments:delete:api',      'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:depts:delete:api',      'WPF,WEB', '/api/v1/departments/{id}',                        'DELETE'),
(8010000000000068, 8010000000000063, '设置负责人接口',   '/system/departments:set-manager:api', 'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:depts:set-manager:api', 'WPF,WEB', '/api/v1/departments/{id}/users/{userId}/manager', 'POST'),
(8010000000000069, 8010000000000064, '移除部门用户接口', '/system/departments:remove-user:api', 'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:depts:remove-user:api', 'WPF,WEB', '/api/v1/departments/{id}/users/{userId}',         'DELETE');

-- Page-level Apis
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000070, 600000000001003, '部门列表', '/system/departments:api:list',   'list',        10, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:depts:api:list',   'WPF,WEB', '/api/v1/departments',            'GET'),
(8010000000000071, 600000000001003, '部门树',   '/system/departments:api:tree',   'diagram-3',   11, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:depts:api:tree',   'WPF,WEB', '/api/v1/departments/tree/all',   'GET'),
(8010000000000072, 600000000001003, '部门详情', '/system/departments:api:detail', 'info-circle', 12, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:depts:api:detail', 'WPF,WEB', '/api/v1/departments/{id}',       'GET'),
(8010000000000073, 600000000001003, '部门用户', '/system/departments:api:users',  'people',      13, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:depts:api:users',  'WPF,WEB', '/api/v1/departments/{id}/users', 'GET');


-- ============================================================================
-- 6. 菜单管理 Button / Api (ParentId: 600000000001004) - 含菜单组子按钮
-- ============================================================================
-- Buttons (菜单本身)
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`) VALUES
(8010000000000090, 600000000001004, '新增菜单', '/system/menus:add',         'plus',        1, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:menus:add',         'WPF,WEB'),
(8010000000000091, 600000000001004, '编辑菜单', '/system/menus:edit',        'pencil',      2, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:menus:edit',        'WPF,WEB'),
(8010000000000092, 600000000001004, '删除菜单', '/system/menus:delete',      'trash',       3, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:menus:delete',      'WPF,WEB'),
(8010000000000093, 600000000001004, '批量删除', '/system/menus:batchdelete', 'trash',       4, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:menus:batchdelete', 'WPF,WEB'),
(8010000000000094, 600000000001004, '查看菜单', '/system/menus:info',        'info-circle', 5, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:menus:info',        'WPF,WEB'),
(8010000000000095, 600000000001004, '复制菜单', '/system/menus:copy',        'clipboard',   6, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:menus:copy',        'WPF,WEB');

-- Buttons (菜单组子页)
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`) VALUES
(8010000000000096, 600000000001004, '菜单组：新建',           '/system/menus:groups:add',              'folder-plus',       20, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:menus:groups:add',              'WPF,WEB'),
(8010000000000097, 600000000001004, '菜单组：编辑',           '/system/menus:groups:edit',             'pencil',            21, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:menus:groups:edit',             'WPF,WEB'),
(8010000000000098, 600000000001004, '菜单组：删除',           '/system/menus:groups:delete',           'trash',             22, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:menus:groups:delete',           'WPF,WEB'),
(8010000000000099, 600000000001004, '菜单项：添加',           '/system/menus:groups:item:add',         'plus',              23, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:menus:groups:item:add',         'WPF,WEB'),
(8010000000000100, 600000000001004, '菜单项：编辑',           '/system/menus:groups:item:edit',        'pencil',            24, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:menus:groups:item:edit',        'WPF,WEB'),
(8010000000000101, 600000000001004, '菜单项：删除',           '/system/menus:groups:item:delete',      'trash',             25, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:menus:groups:item:delete',      'WPF,WEB'),
(8010000000000102, 600000000001004, '菜单项：批量删除',       '/system/menus:groups:item:batchdelete', 'trash',             26, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:menus:groups:item:batchdelete', 'WPF,WEB'),
(8010000000000103, 600000000001004, '菜单项：保存排序',       '/system/menus:groups:item:sort',        'sort-numeric-down', 27, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:menus:groups:item:sort',        'WPF,WEB'),
(8010000000000104, 600000000001004, '菜单项：从系统菜单导入', '/system/menus:groups:item:import',      'box-arrow-in-down', 28, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:menus:groups:item:import',      'WPF,WEB'),
(8010000000000105, 600000000001004, '菜单组角色授权：保存',   '/system/menus:groups:role-perm:set',    'shield-check',      29, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:menus:groups:role-perm:set',    'WPF,WEB');

-- Apis under menu buttons
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000106, 8010000000000090, '创建菜单接口', '/system/menus:add:api',         'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:add:api',         'WPF,WEB', '/api/v1/menus',              'POST'),
(8010000000000107, 8010000000000091, '更新菜单接口', '/system/menus:edit:api',        'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:edit:api',        'WPF,WEB', '/api/v1/menus/{id}',         'PUT'),
(8010000000000108, 8010000000000092, '删除菜单接口', '/system/menus:delete:api',      'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:delete:api',      'WPF,WEB', '/api/v1/menus/{id}',         'DELETE'),
(8010000000000109, 8010000000000093, '批量删除接口', '/system/menus:batchdelete:api', 'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:batchdelete:api', 'WPF,WEB', '/api/v1/menus/batch-delete', 'POST'),
(8010000000000110, 8010000000000095, '复制菜单接口', '/system/menus:copy:api',        'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:copy:api',        'WPF,WEB', '/api/v1/menus',              'POST');

-- Apis under menu-group buttons
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000111, 8010000000000096, '创建菜单组接口',     '/system/menus:groups:add:api',              'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:groups:add:api',              'WPF,WEB', '/api/v1/menu-groups',                                    'POST'),
(8010000000000112, 8010000000000097, '更新菜单组接口',     '/system/menus:groups:edit:api',             'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:groups:edit:api',             'WPF,WEB', '/api/v1/menu-groups/{id}',                               'PUT'),
(8010000000000113, 8010000000000098, '删除菜单组接口',     '/system/menus:groups:delete:api',           'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:groups:delete:api',           'WPF,WEB', '/api/v1/menu-groups/{id}',                               'DELETE'),
(8010000000000114, 8010000000000099, '创建菜单项接口',     '/system/menus:groups:item:add:api',         'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:groups:item:add:api',         'WPF,WEB', '/api/v1/menu-groups/{groupId}/items',                    'POST'),
(8010000000000115, 8010000000000100, '更新菜单项接口',     '/system/menus:groups:item:edit:api',        'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:groups:item:edit:api',        'WPF,WEB', '/api/v1/menu-groups/{groupId}/items/{id}',               'PUT'),
(8010000000000116, 8010000000000101, '删除菜单项接口',     '/system/menus:groups:item:delete:api',      'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:groups:item:delete:api',      'WPF,WEB', '/api/v1/menu-groups/{groupId}/items/{id}',               'DELETE'),
(8010000000000117, 8010000000000102, '批量删除菜单项接口', '/system/menus:groups:item:batchdelete:api', 'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:groups:item:batchdelete:api', 'WPF,WEB', '/api/v1/menu-groups/{groupId}/items/batch-delete',       'POST'),
(8010000000000118, 8010000000000103, '保存排序接口',       '/system/menus:groups:item:sort:api',        'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:groups:item:sort:api',        'WPF,WEB', '/api/v1/menu-groups/{groupId}/items/sort',               'PUT'),
(8010000000000119, 8010000000000104, '从系统菜单导入接口', '/system/menus:groups:item:import:api',      'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:groups:item:import:api',      'WPF,WEB', '/api/v1/menu-groups/{groupId}/items/import-from-system', 'POST'),
(8010000000000120, 8010000000000105, '设置菜单组角色权限', '/system/menus:groups:role-perm:set:api',    'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:groups:role-perm:set:api',    'WPF,WEB', '/api/v1/menu-groups/role-permissions',                   'PUT');

-- Page-level Apis
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000121, 600000000001004, '菜单列表',           '/system/menus:api:list',                'list',        40, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:api:list',                'WPF,WEB', '/api/v1/menus',                                'GET'),
(8010000000000122, 600000000001004, '菜单详情',           '/system/menus:api:detail',              'info-circle', 41, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:api:detail',              'WPF,WEB', '/api/v1/menus/{id}',                           'GET'),
(8010000000000123, 600000000001004, '管理端全树',         '/system/menus:api:tree-all',            'diagram-3',   42, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:api:tree-all',            'WPF,WEB', '/api/v1/menus/tree/all',                       'GET'),
(8010000000000124, 600000000001004, '菜单组列表',         '/system/menus:groups:api:list',         'list',        43, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:groups:api:list',         'WPF,WEB', '/api/v1/menu-groups',                          'GET'),
(8010000000000125, 600000000001004, '菜单组详情',         '/system/menus:groups:api:detail',       'info-circle', 44, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:groups:api:detail',       'WPF,WEB', '/api/v1/menu-groups/{id}',                     'GET'),
(8010000000000126, 600000000001004, '菜单组项树',         '/system/menus:groups:api:items',        'diagram-3',   45, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:groups:api:items',        'WPF,WEB', '/api/v1/menu-groups/{groupId}/items',          'GET'),
(8010000000000127, 600000000001004, '菜单项详情',         '/system/menus:groups:api:item-detail',  'info-circle', 46, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:groups:api:item-detail',  'WPF,WEB', '/api/v1/menu-groups/{groupId}/items/{id}',     'GET'),
(8010000000000128, 600000000001004, '菜单组角色权限：取', '/system/menus:groups:api:role-perm-get','shield',      47, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:menus:groups:api:role-perm-get','WPF,WEB', '/api/v1/menu-groups/role-permissions/{roleId}','GET');


-- ============================================================================
-- 7. 数据字典 Button / Api (ParentId: 600000000001005)
-- ============================================================================
-- Buttons
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`) VALUES
(8010000000000150, 600000000001005, '分类：新增', '/system/dictionaries:cat:add',     'plus',   1, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:dicts:cat:add',     'WPF,WEB'),
(8010000000000151, 600000000001005, '分类：编辑', '/system/dictionaries:cat:edit',    'pencil', 2, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:dicts:cat:edit',    'WPF,WEB'),
(8010000000000152, 600000000001005, '分类：删除', '/system/dictionaries:cat:delete',  'trash',  3, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:dicts:cat:delete',  'WPF,WEB'),
(8010000000000153, 600000000001005, '条目：新增', '/system/dictionaries:item:add',    'plus',   4, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:dicts:item:add',    'WPF,WEB'),
(8010000000000154, 600000000001005, '条目：编辑', '/system/dictionaries:item:edit',   'pencil', 5, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:dicts:item:edit',   'WPF,WEB'),
(8010000000000155, 600000000001005, '条目：删除', '/system/dictionaries:item:delete', 'trash',  6, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:dicts:item:delete', 'WPF,WEB');

-- Apis under buttons
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000156, 8010000000000150, '创建分类接口', '/system/dictionaries:cat:add:api',     'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:dicts:cat:add:api',     'WPF,WEB', '/api/v1/dictionaries/categories',      'POST'),
(8010000000000157, 8010000000000151, '更新分类接口', '/system/dictionaries:cat:edit:api',    'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:dicts:cat:edit:api',    'WPF,WEB', '/api/v1/dictionaries/categories/{id}', 'PUT'),
(8010000000000158, 8010000000000152, '删除分类接口', '/system/dictionaries:cat:delete:api',  'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:dicts:cat:delete:api',  'WPF,WEB', '/api/v1/dictionaries/categories/{id}', 'DELETE'),
(8010000000000159, 8010000000000153, '创建条目接口', '/system/dictionaries:item:add:api',    'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:dicts:item:add:api',    'WPF,WEB', '/api/v1/dictionaries/items',           'POST'),
(8010000000000160, 8010000000000154, '更新条目接口', '/system/dictionaries:item:edit:api',   'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:dicts:item:edit:api',   'WPF,WEB', '/api/v1/dictionaries/items/{id}',      'PUT'),
(8010000000000161, 8010000000000155, '删除条目接口', '/system/dictionaries:item:delete:api', 'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:dicts:item:delete:api', 'WPF,WEB', '/api/v1/dictionaries/items/{id}',      'DELETE');

-- Page-level Apis
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000162, 600000000001005, '分类列表',     '/system/dictionaries:api:cat-list',    'list',        10, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:dicts:api:cat-list',    'WPF,WEB', '/api/v1/dictionaries/categories',      'GET'),
(8010000000000163, 600000000001005, '分类详情',     '/system/dictionaries:api:cat-detail',  'info-circle', 11, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:dicts:api:cat-detail',  'WPF,WEB', '/api/v1/dictionaries/categories/{id}', 'GET'),
(8010000000000164, 600000000001005, '条目列表',     '/system/dictionaries:api:item-list',   'list',        12, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:dicts:api:item-list',   'WPF,WEB', '/api/v1/dictionaries/items',           'GET'),
(8010000000000165, 600000000001005, '条目详情',     '/system/dictionaries:api:item-detail', 'info-circle', 13, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:dicts:api:item-detail', 'WPF,WEB', '/api/v1/dictionaries/items/{id}',      'GET'),
(8010000000000166, 600000000001005, '按编码批量取', '/system/dictionaries:api:by-codes',    'collection',  14, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:dicts:api:by-codes',    'WPF,WEB', '/api/v1/dictionaries/by-codes',        'GET');


-- ============================================================================
-- 8. 日志管理 Button / Api (ParentId: 600000000001006)
-- ============================================================================
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`) VALUES
(8010000000000180, 600000000001006, '查看详情', '/system/logs:detail', 'info-circle', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:logs:detail', 'WPF,WEB');

INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000181, 600000000001006, '日志列表', '/system/logs:api:list', 'list', 10, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:logs:api:list', 'WPF,WEB', '/api/v1/logs', 'GET');


-- ============================================================================
-- 9. 文件管理 Button / Api (ParentId: 600000000001007)
-- ============================================================================
-- Buttons
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`) VALUES
(8010000000000185, 600000000001007, '上传文件', '/system/files:upload',      'cloud-upload',     1, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:files:upload',      'WPF,WEB'),
(8010000000000186, 600000000001007, '批量迁移', '/system/files:move',        'arrow-left-right', 2, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:files:move',        'WPF,WEB'),
(8010000000000187, 600000000001007, '批量删除', '/system/files:batchdelete', 'trash',            3, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:files:batchdelete', 'WPF,WEB'),
(8010000000000188, 600000000001007, '下载',     '/system/files:download',    'download',         4, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:files:download',    'WPF,WEB'),
(8010000000000189, 600000000001007, '删除',     '/system/files:delete',      'trash',            5, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:files:delete',      'WPF,WEB');

-- Apis under buttons
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000190, 8010000000000185, '上传文件接口', '/system/files:upload:api',      'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:files:upload:api',      'WPF,WEB', '/api/v1/files/upload',        'POST'),
(8010000000000191, 8010000000000186, '批量迁移接口', '/system/files:move:api',        'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:files:move:api',        'WPF,WEB', '/api/v1/files/batch-move',    'POST'),
(8010000000000192, 8010000000000187, '批量删除接口', '/system/files:batchdelete:api', 'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:files:batchdelete:api', 'WPF,WEB', '/api/v1/files/batch-delete',  'POST'),
(8010000000000193, 8010000000000188, '下载接口',     '/system/files:download:api',    'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:files:download:api',    'WPF,WEB', '/api/v1/files/{id}/download', 'GET'),
(8010000000000194, 8010000000000189, '删除接口',     '/system/files:delete:api',      'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:files:delete:api',      'WPF,WEB', '/api/v1/files/{id}',          'DELETE');

-- Page-level Apis
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000195, 600000000001007, '文件列表', '/system/files:api:list',    'list',        10, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:files:api:list',    'WPF,WEB', '/api/v1/files',              'GET'),
(8010000000000196, 600000000001007, '文件详情', '/system/files:api:detail',  'info-circle', 11, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:files:api:detail',  'WPF,WEB', '/api/v1/files/{id}',         'GET'),
(8010000000000197, 600000000001007, '内容读取', '/system/files:api:content', 'eye',         12, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:files:api:content', 'WPF,WEB', '/api/v1/files/{id}/content', 'GET');


-- ============================================================================
-- 10. 定时任务 Button / Api (ParentId: 600000000001012)
-- ============================================================================
-- Buttons
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`) VALUES
(8010000000000210, 600000000001012, '新增任务', '/system/scheduled-tasks:add',     'plus',         1, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:tasks:add',     'WPF,WEB'),
(8010000000000211, 600000000001012, '编辑任务', '/system/scheduled-tasks:edit',    'pencil',       2, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:tasks:edit',    'WPF,WEB'),
(8010000000000212, 600000000001012, '删除任务', '/system/scheduled-tasks:delete',  'trash',        3, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:tasks:delete',  'WPF,WEB'),
(8010000000000213, 600000000001012, '手动触发', '/system/scheduled-tasks:trigger', 'play-circle',  4, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:tasks:trigger', 'WPF,WEB'),
(8010000000000214, 600000000001012, '查看日志', '/system/scheduled-tasks:logs',    'journal-text', 5, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:tasks:logs',    'WPF,WEB'),
(8010000000000215, 600000000001012, '测试执行', '/system/scheduled-tasks:test',    'lightning',    6, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:tasks:test',    'WPF,WEB');

-- Apis under buttons
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000216, 8010000000000210, '新增任务接口', '/system/scheduled-tasks:add:api',     'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:tasks:add:api',     'WPF,WEB', '/api/v1/scheduled-tasks',                   'POST'),
(8010000000000217, 8010000000000211, '更新任务接口', '/system/scheduled-tasks:edit:api',    'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:tasks:edit:api',    'WPF,WEB', '/api/v1/scheduled-tasks/{taskKey}',         'PUT'),
(8010000000000218, 8010000000000212, '删除任务接口', '/system/scheduled-tasks:delete:api',  'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:tasks:delete:api',  'WPF,WEB', '/api/v1/scheduled-tasks/{taskKey}',         'DELETE'),
(8010000000000219, 8010000000000213, '触发执行接口', '/system/scheduled-tasks:trigger:api', 'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:tasks:trigger:api', 'WPF,WEB', '/api/v1/scheduled-tasks/{taskKey}/trigger', 'POST'),
(8010000000000220, 8010000000000214, '任务日志接口', '/system/scheduled-tasks:logs:api',    'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:tasks:logs:api',    'WPF,WEB', '/api/v1/scheduled-tasks/{taskKey}/logs',    'GET'),
(8010000000000221, 8010000000000215, '测试执行接口', '/system/scheduled-tasks:test:api',    'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:tasks:test:api',    'WPF,WEB', '/api/v1/scheduled-tasks/test-execute',      'POST');

-- Page-level Apis
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000222, 600000000001012, '任务列表',   '/system/scheduled-tasks:api:list',      'list',        10, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:tasks:api:list',      'WPF,WEB', '/api/v1/scheduled-tasks',                     'GET'),
(8010000000000223, 600000000001012, '任务详情',   '/system/scheduled-tasks:api:detail',    'info-circle', 11, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:tasks:api:detail',    'WPF,WEB', '/api/v1/scheduled-tasks/{taskKey}',           'GET'),
(8010000000000224, 600000000001012, '执行提供器', '/system/scheduled-tasks:api:providers', 'plug',        12, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:tasks:api:providers', 'WPF,WEB', '/api/v1/scheduled-tasks/execution-providers', 'GET'),
(8010000000000225, 600000000001012, '可调用动作', '/system/scheduled-tasks:api:actions',   'lightning',   13, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:tasks:api:actions',   'WPF,WEB', '/api/v1/scheduled-tasks/invocable-actions',   'GET');


-- ============================================================================
-- 11. 模块管理 Button / Api (ParentId: 600000000001011)
-- ============================================================================
-- Buttons
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`) VALUES
(8010000000000230, 600000000001011, '安装模块',         '/system/modules:install',         'download',                1, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:modules:install',         'WPF,WEB'),
(8010000000000231, 600000000001011, '卸载模块',         '/system/modules:uninstall',       'trash',                   2, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:modules:uninstall',       'WPF,WEB'),
(8010000000000232, 600000000001011, '升级模块',         '/system/modules:upgrade',         'upload',                  3, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:modules:upgrade',         'WPF,WEB'),
(8010000000000233, 600000000001011, '启用模块',         '/system/modules:enable',          'check-circle',            4, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:modules:enable',          'WPF,WEB'),
(8010000000000234, 600000000001011, '禁用模块',         '/system/modules:disable',         'pause-circle',            5, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:modules:disable',         'WPF,WEB'),
(8010000000000235, 600000000001011, '热启用',           '/system/modules:hot-enable',      'lightning-charge',        6, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:modules:hot-enable',      'WPF,WEB'),
(8010000000000236, 600000000001011, '热禁用',           '/system/modules:hot-disable',     'lightning',               7, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:modules:hot-disable',     'WPF,WEB'),
(8010000000000237, 600000000001011, '热重载',           '/system/modules:hot-reload',      'arrow-clockwise',         8, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:modules:hot-reload',      'WPF,WEB'),
(8010000000000238, 600000000001011, '重置菜单',         '/system/modules:reset-menus',     'arrow-counterclockwise',  9, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:modules:reset-menus',     'WPF,WEB'),
(8010000000000239, 600000000001011, '移除菜单',         '/system/modules:remove-menus',    'trash',                  10, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:modules:remove-menus',    'WPF,WEB'),
(8010000000000240, 600000000001011, '执行安装SQL',      '/system/modules:run-install-sql', 'database',               11, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:modules:run-install-sql', 'WPF,WEB'),
(8010000000000241, 600000000001011, '配置：保存并重载', '/system/modules:config:save',     'save',                   12, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:modules:config:save',     'WPF,WEB'),
(8010000000000242, 600000000001011, '配置：重置',       '/system/modules:config:reset',    'arrow-counterclockwise', 13, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:modules:config:reset',    'WPF,WEB'),
(8010000000000243, 600000000001011, '配置：删除',       '/system/modules:config:delete',   'trash',                  14, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:modules:config:delete',   'WPF,WEB'),
(8010000000000244, 600000000001011, '配置：编辑保存',   '/system/modules:config:apply',    'pencil-square',          15, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:modules:config:apply',    'WPF,WEB');

-- Apis under buttons
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000245, 8010000000000230, '安装接口',         '/system/modules:install:api',         'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:modules:install:api',         'WPF,WEB', '/api/v1/modules/install',                'POST'),
(8010000000000246, 8010000000000231, '卸载接口',         '/system/modules:uninstall:api',       'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:modules:uninstall:api',       'WPF,WEB', '/api/v1/modules/uninstall',              'POST'),
(8010000000000247, 8010000000000232, '升级接口',         '/system/modules:upgrade:api',         'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:modules:upgrade:api',         'WPF,WEB', '/api/v1/modules/upgrade',                'POST'),
(8010000000000248, 8010000000000233, '启用接口',         '/system/modules:enable:api',          'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:modules:enable:api',          'WPF,WEB', '/api/v1/modules/enable',                 'POST'),
(8010000000000249, 8010000000000234, '禁用接口',         '/system/modules:disable:api',         'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:modules:disable:api',         'WPF,WEB', '/api/v1/modules/disable',                'POST'),
(8010000000000250, 8010000000000235, '热启用接口',       '/system/modules:hot-enable:api',      'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:modules:hot-enable:api',      'WPF,WEB', '/api/v1/modules/hot/enable',             'POST'),
(8010000000000251, 8010000000000236, '热禁用接口',       '/system/modules:hot-disable:api',     'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:modules:hot-disable:api',     'WPF,WEB', '/api/v1/modules/hot/disable',            'POST'),
(8010000000000252, 8010000000000237, '热重载接口',       '/system/modules:hot-reload:api',      'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:modules:hot-reload:api',      'WPF,WEB', '/api/v1/modules/hot/reload',             'POST'),
(8010000000000253, 8010000000000238, '重置菜单接口',     '/system/modules:reset-menus:api',     'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:modules:reset-menus:api',     'WPF,WEB', '/api/v1/modules/reset-menus',            'POST'),
(8010000000000254, 8010000000000239, '移除菜单接口',     '/system/modules:remove-menus:api',    'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:modules:remove-menus:api',    'WPF,WEB', '/api/v1/modules/remove-menus',           'POST'),
(8010000000000255, 8010000000000240, '安装SQL接口',      '/system/modules:run-install-sql:api', 'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:modules:run-install-sql:api', 'WPF,WEB', '/api/v1/modules/run-install-sql',        'POST'),
(8010000000000256, 8010000000000241, '配置保存重载接口', '/system/modules:config:save:api',     'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:modules:config:save:api',     'WPF,WEB', '/api/v1/modules/config/save-and-reload', 'POST'),
(8010000000000257, 8010000000000242, '配置重置接口',     '/system/modules:config:reset:api',    'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:modules:config:reset:api',    'WPF,WEB', '/api/v1/modules/config/reset',           'POST'),
(8010000000000258, 8010000000000243, '配置删除接口',     '/system/modules:config:delete:api',   'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:modules:config:delete:api',   'WPF,WEB', '/api/v1/modules/config/delete',          'DELETE'),
(8010000000000259, 8010000000000244, '配置应用接口',     '/system/modules:config:apply:api',    'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:modules:config:apply:api',    'WPF,WEB', '/api/v1/modules/config/apply',           'POST');

-- Page-level Apis
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000260, 600000000001011, '仓库扫描',     '/system/modules:api:scan',              'search',          20, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:modules:api:scan',              'WPF,WEB', '/api/v1/modules/repo',                'GET'),
(8010000000000261, 600000000001011, '已安装列表',   '/system/modules:api:installed',         'list',            21, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:modules:api:installed',         'WPF,WEB', '/api/v1/modules/installed',           'GET'),
(8010000000000262, 600000000001011, 'DB刷新',       '/system/modules:api:installed-refresh', 'arrow-clockwise', 22, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:modules:api:installed-refresh', 'WPF,WEB', '/api/v1/modules/installed/refresh-db','GET'),
(8010000000000263, 600000000001011, '读取配置',     '/system/modules:api:config',            'gear',            23, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:modules:api:config',            'WPF,WEB', '/api/v1/modules/config',              'GET'),
(8010000000000264, 600000000001011, '配置文件列表', '/system/modules:api:config-files',      'files',           24, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:modules:api:config-files',      'WPF,WEB', '/api/v1/modules/config/files',        'GET'),
(8010000000000265, 600000000001011, '规范化读取',   '/system/modules:api:config-normalized', 'gear-fill',       25, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:modules:api:config-normalized', 'WPF,WEB', '/api/v1/modules/config/normalized',   'GET');


-- ============================================================================
-- 12. 系统配置 Button / Api (ParentId: 600000000001008)
-- ============================================================================
-- Buttons
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`) VALUES
(8010000000000280, 600000000001008, '保存配置',     '/system/config:save',        'save',              1, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:config:save',        'WPF,WEB'),
(8010000000000281, 600000000001008, '添加单项配置', '/system/config:save-one',    'plus',              2, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:config:save-one',    'WPF,WEB'),
(8010000000000282, 600000000001008, '测试邮件',     '/system/config:test-email',  'envelope',          3, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:config:test-email',  'WPF,WEB'),
(8010000000000283, 600000000001008, '管理多语言',   '/system/config:lang:manage', 'translate',         4, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:config:lang:manage', 'WPF,WEB'),
(8010000000000284, 600000000001008, '通用数据导入', '/system/config:import',      'box-arrow-in-down', 5, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:config:import',      'WPF,WEB');

-- Apis under buttons
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000285, 8010000000000280, '批量保存接口', '/system/config:save:api',       'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:config:save:api',       'WPF,WEB', '/api/v1/settings/batch',      'POST'),
(8010000000000286, 8010000000000281, '单项保存接口', '/system/config:save-one:api',   'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:config:save-one:api',   'WPF,WEB', '/api/v1/settings',            'POST'),
(8010000000000287, 8010000000000282, '测试邮件接口', '/system/config:test-email:api', 'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:config:test-email:api', 'WPF,WEB', '/api/v1/settings/test-email', 'POST'),
(8010000000000288, 8010000000000284, '通用导入接口', '/system/config:import:api',     'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:config:import:api',     'WPF,WEB', '/api/v1/import/{table}',      'POST');

-- Page-level Apis
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000289, 600000000001008, '公开配置', '/system/config:api:get',           'gear',        10, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:config:api:get',           'WPF,WEB', '/api/v1/settings',         'GET'),
(8010000000000290, 600000000001008, '全部配置', '/system/config:api:get-all',       'gear-wide',   11, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:config:api:get-all',       'WPF,WEB', '/api/v1/settings/all',     'GET'),
(8010000000000291, 600000000001008, '队列度量', '/system/config:api:queue-metrics', 'speedometer',12, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:config:api:queue-metrics', 'WPF,WEB', '/api/system/queue-metrics','GET');


-- ============================================================================
-- 13. 通知管理 Button / Api (ParentId: 600000000001009)
-- ============================================================================
-- Buttons
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`) VALUES
(8010000000000300, 600000000001009, '新建通知',           '/system/notify:add',             'plus',      1, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:notify:add',             'WPF,WEB'),
(8010000000000301, 600000000001009, '编辑通知',           '/system/notify:edit',            'pencil',    2, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:notify:edit',            'WPF,WEB'),
(8010000000000302, 600000000001009, '发布通知',           '/system/notify:publish',         'send',      3, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:notify:publish',         'WPF,WEB'),
(8010000000000303, 600000000001009, '删除通知',           '/system/notify:delete',          'trash',     4, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:notify:delete',          'WPF,WEB'),
(8010000000000304, 600000000001009, '下架通知',           '/system/notify:soft-delete',     'archive',   5, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:notify:soft-delete',     'WPF,WEB'),
(8010000000000305, 600000000001009, '站内消息：发送',     '/system/notify:msg:send',        'chat-dots', 6, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:notify:msg:send',        'WPF,WEB'),
(8010000000000306, 600000000001009, '站内消息：批量删除', '/system/notify:msg:batchdelete', 'trash',     7, 1, NULL, '2025-08-10 00:00:00', 0, 'Button', 'sys:notify:msg:batchdelete', 'WPF,WEB');

-- Apis under buttons
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000307, 8010000000000300, '新建通知接口',     '/system/notify:add:api',             'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:notify:add:api',             'WPF,WEB', '/api/v1/notifications',                'POST'),
(8010000000000308, 8010000000000301, '编辑通知接口',     '/system/notify:edit:api',            'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:notify:edit:api',            'WPF,WEB', '/api/v1/notifications/{id}',           'PUT'),
(8010000000000309, 8010000000000302, '发布通知接口',     '/system/notify:publish:api',         'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:notify:publish:api',         'WPF,WEB', '/api/v1/notifications/{id}/publish',   'POST'),
(8010000000000310, 8010000000000303, '删除通知接口',     '/system/notify:delete:api',          'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:notify:delete:api',          'WPF,WEB', '/api/v1/notifications/{id}',           'DELETE'),
(8010000000000311, 8010000000000304, '下架通知接口',     '/system/notify:soft-delete:api',     'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:notify:soft-delete:api',     'WPF,WEB', '/api/v1/notifications/{id}/soft',      'DELETE'),
(8010000000000312, 8010000000000305, '发送站内消息接口', '/system/notify:msg:send:api',        'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:notify:msg:send:api',        'WPF,WEB', '/api/message',                         'POST'),
(8010000000000313, 8010000000000306, '批量删除消息接口', '/system/notify:msg:batchdelete:api', 'gear', 1, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:notify:msg:batchdelete:api', 'WPF,WEB', '/api/message/admin/batch',             'DELETE');

-- Page-level Apis
INSERT INTO `ginkgo_Sys_Menu` (`Id`, `Module`, `ParentId`, `Name`, `Route`, `Icon`, `OrderNo`, `Visible`, `PermissionCode`, `CreatedAt`, `IsDeleted`, `Type`, `Code`, `SupportedClients`, `Resource`, `Method`) VALUES
(8010000000000314, 600000000001009, '通知列表',         '/system/notify:api:list',             'list',        10, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:notify:api:list',             'WPF,WEB', '/api/v1/notifications',                    'GET'),
(8010000000000315, 600000000001009, '通知列表（兼容）', '/system/notify:api:list-compat',      'list',        11, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:notify:api:list-compat',      'WPF,WEB', '/api/v1/notifications/list',               'GET'),
(8010000000000316, 600000000001009, '通知详情',         '/system/notify:api:detail',           'info-circle', 12, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:notify:api:detail',           'WPF,WEB', '/api/v1/notifications/{id}',               'GET'),
(8010000000000317, 600000000001009, '通知统计',         '/system/notify:api:stats',            'bar-chart',   13, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:notify:api:stats',            'WPF,WEB', '/api/v1/notifications/{id}/stats',         'GET'),
(8010000000000318, 600000000001009, '通知摘要',         '/system/notify:api:stats-summary',    'bar-chart',   14, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:notify:api:stats-summary',    'WPF,WEB', '/api/v1/notifications/{id}/stats/summary', 'GET'),
(8010000000000319, 600000000001009, '已发布通知详情',   '/system/notify:api:published-detail', 'info-circle', 15, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:notify:api:published-detail', 'WPF,WEB', '/api/v1/notifications/{id}/detail',        'GET'),
(8010000000000320, 600000000001009, '通知附件',         '/system/notify:api:attachments',      'paperclip',   16, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:notify:api:attachments',      'WPF,WEB', '/api/v1/notifications/{id}/attachments',   'GET'),
(8010000000000321, 600000000001009, '站内消息后台列表', '/system/notify:api:msg-list',         'chat-dots',   17, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:notify:api:msg-list',         'WPF,WEB', '/api/message/admin/list',                  'GET'),
(8010000000000322, 600000000001009, '站内消息后台统计', '/system/notify:api:msg-stats',        'bar-chart',   18, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:notify:api:msg-stats',        'WPF,WEB', '/api/message/admin/stats',                 'GET'),
(8010000000000323, 600000000001009, '站内消息后台详情', '/system/notify:api:msg-detail',       'info-circle', 19, 1, NULL, '2025-08-10 00:00:00', 0, 'Api', 'sys:notify:api:msg-detail',       'WPF,WEB', '/api/message/admin/detail',                'GET');


-- ============================================================================
-- End of Menu Data (主框架菜单初始化完成)
-- 节点统计：1 系统管理目录 + 11 子页 (9 Item + 2 Directory) + 77 Button + 128 Api = 217
-- 首页 (Item) 与其 6 个 Button/Api 由 SeedDataInitializer 在程序启动时通过雪花 ID 创建
-- ============================================================================
