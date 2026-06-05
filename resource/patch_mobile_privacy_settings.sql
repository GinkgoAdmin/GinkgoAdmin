-- 移动端隐私合规配置迁移脚本（MySQL）
-- 用途：已有环境升级 — 将原「App」分组迁移为「移动端」，并补齐/更新隐私政策示例内容
-- 新安装请直接使用 mysql_install.sql / pg_install.sql / mssql_install_snowflake.sql

-- 1. 字典分组：App -> 移动端
UPDATE `ginkgo_Sys_DictionaryItem` di
INNER JOIN `ginkgo_Sys_Dictionary` d ON di.`DictId` = d.`Id`
SET di.`Code` = 'Mobile', di.`Value` = '移动端'
WHERE d.`Code` = 'sysconfig' AND (di.`Code` = 'App' OR di.`Value` = 'App');

INSERT INTO `ginkgo_Sys_DictionaryItem` (`Id`, `DictId`, `ParentId`, `Code`, `Value`, `SortOrder`, `IsActive`, `ExtraJson`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`, `UpdatedBy`)
SELECT 400000000000099, 300000000000002, NULL, 'Mobile', '移动端', 90, 1, NULL, NOW(), NULL, 0, NULL, NULL
FROM DUAL
WHERE NOT EXISTS (
  SELECT 1 FROM `ginkgo_Sys_DictionaryItem` di
  INNER JOIN `ginkgo_Sys_Dictionary` d ON di.`DictId` = d.`Id`
  WHERE d.`Code` = 'sysconfig' AND (di.`Code` = 'Mobile' OR di.`Value` = '移动端')
);

-- 2. 已有 App 分组配置迁移 class
UPDATE `ginkgo_Sys_Settings` SET `class` = 'Mobile' WHERE `class` = 'App';

-- 3. 移动端配置项（不存在则插入）
INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`)
SELECT 'App.HomePlugin', '', 'String', 'UNIAPP端首页替换插件ID', 1, NOW(), NULL, 'Mobile', 500000000000080
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM `ginkgo_Sys_Settings` WHERE `Key` = 'App.HomePlugin');

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`)
SELECT 'App.Privacy.ShowPopup', 'true', 'Bool', '首次启动弹出隐私政策', 1, NOW(), NULL, 'Mobile', 500000000000081
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM `ginkgo_Sys_Settings` WHERE `Key` = 'App.Privacy.ShowPopup');

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`)
SELECT 'App.Privacy.PolicyVersion', '1.0.0', 'String', '隐私政策版本号', 1, NOW(), NULL, 'Mobile', 500000000000082
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM `ginkgo_Sys_Settings` WHERE `Key` = 'App.Privacy.PolicyVersion');

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`)
SELECT 'App.Privacy.PolicyContent',
'<h2>隐私政策</h2><p><strong>更新日期：</strong>2026年1月1日&nbsp;|&nbsp;<strong>生效日期：</strong>2026年1月1日</p><p>感谢您使用本应用（以下简称「我们」）。我们深知个人信息对您的重要性，并会尽全力保护您的个人信息安全。请在注册、登录或使用本应用前，仔细阅读并充分理解本政策。</p><h3>一、我们如何收集和使用个人信息</h3><p>为向您提供账户注册、登录验证、消息通知、业务办理等基础功能，我们可能收集以下信息：</p><ul><li><strong>帐号信息：</strong>用户名、昵称、密码（加密存储）</li><li><strong>联系信息：</strong>手机号码、电子邮箱（由您自愿填写）</li><li><strong>头像与简介：</strong>用于个人资料展示（可选）</li><li><strong>设备信息：</strong>设备型号、操作系统版本、应用版本号</li><li><strong>日志信息：</strong>登录时间、操作记录（用于安全审计与故障排查）</li></ul><h3>二、我们如何使用 Cookie 和同类技术</h3><p>为保障登录状态与安全，我们可能在本地存储必要的令牌与偏好设置（如字体大小、隐私同意记录），不会用于与提供服务无关的目的。</p><h3>三、信息的存储与保护</h3><p>您的个人信息存储于中华人民共和国境内服务器。我们采取加密传输、权限隔离、访问日志审计等安全措施保护您的数据。</p><h3>四、您的权利</h3><p>您依法享有以下权利，可在应用<strong>「我的」→「隐私与合规」</strong>中操作：</p><ul><li>查阅、更正您的个人信息</li><li>删除非必要的个人信息（邮箱、手机、头像、个人简介等）</li><li>注销用户帐号</li><li>撤回对本隐私政策的同意</li></ul><h3>五、未成年人保护</h3><p>若您未满 18 周岁，请在监护人陪同下阅读本政策，并在取得监护人同意后再使用本应用。</p><h3>六、政策更新</h3><p>我们可能适时修订本政策。重大变更将以应用内弹窗或公告方式通知您；若您继续使用，即视为同意修订后的政策。</p><h3>七、联系我们</h3><p>如对本政策有任何疑问、意见或投诉，请通过应用内「帮助中心」或联系系统管理员，我们将在合理期限内回复。</p>',
'RichText', '隐私政策内容（示例，可在后台「系统配置-移动端」修改）', 1, NOW(), NULL, 'Mobile', 500000000000083
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM `ginkgo_Sys_Settings` WHERE `Key` = 'App.Privacy.PolicyContent');

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`)
SELECT 'App.Privacy.UserAgreementContent',
'<h2>用户服务协议</h2><p><strong>更新日期：</strong>2026年1月1日&nbsp;|&nbsp;<strong>生效日期：</strong>2026年1月1日</p><p>欢迎您使用本应用。本协议是您与运营方之间关于使用本应用服务所订立的协议，请您仔细阅读。</p><h3>一、服务说明</h3><p>本应用提供企业级移动办公、业务管理与消息通知等能力。您完成注册并登录，即表示您已阅读、理解并同意受本协议及《隐私政策》约束。</p><h3>二、帐号注册与安全</h3><p>您应提供真实、准确、完整的信息完成注册，并妥善保管帐号与密码。因帐号保管不善导致的损失，由您自行承担相应责任。</p><h3>三、用户行为规范</h3><p>您在使用本应用时，不得从事以下行为：</p><ul><li>违反法律法规、公序良俗或本协议约定</li><li>传播违法、虚假、侵权或骚扰性信息</li><li>以任何方式干扰、破坏系统或他人正常使用</li><li>未经授权访问、抓取或篡改系统数据</li></ul><h3>四、隐私保护</h3><p>我们重视您的隐私保护，个人信息处理规则详见《隐私政策》。使用本应用即表示您同时同意《隐私政策》。</p><h3>五、知识产权</h3><p>本应用的界面设计、程序代码、文档资料等知识产权归运营方或相关权利人所有。未经授权，不得复制、修改、传播或用于商业用途。</p><h3>六、免责声明</h3><p>因不可抗力、网络故障、第三方服务异常等非我们可控原因导致的服务中断，我们将在合理范围内协助恢复，但不承担由此产生的间接损失。</p><h3>七、协议变更与终止</h3><p>我们有权根据业务需要更新本协议，更新后将通过应用内公告或弹窗告知。您可随时通过「隐私与合规」申请注销帐号以终止服务。</p><h3>八、联系我们</h3><p>如对本协议有任何疑问，请通过应用内「帮助中心」联系我们。</p>',
'RichText', '用户协议内容（示例，可在后台「系统配置-移动端」修改）', 1, NOW(), NULL, 'Mobile', 500000000000084
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM `ginkgo_Sys_Settings` WHERE `Key` = 'App.Privacy.UserAgreementContent');

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`)
SELECT 'App.Privacy.EnableCorrectInfo', 'true', 'Bool', '开启更正/删除个人信息', 1, NOW(), NULL, 'Mobile', 500000000000085
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM `ginkgo_Sys_Settings` WHERE `Key` = 'App.Privacy.EnableCorrectInfo');

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`)
SELECT 'App.Privacy.EnableDeleteAccount', 'true', 'Bool', '开启注销用户帐号', 1, NOW(), NULL, 'Mobile', 500000000000086
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM `ginkgo_Sys_Settings` WHERE `Key` = 'App.Privacy.EnableDeleteAccount');

INSERT INTO `ginkgo_Sys_Settings` (`Key`, `Value`, `Type`, `Description`, `Version`, `UpdatedAt`, `UpdatedBy`, `class`, `Id`)
SELECT 'App.Privacy.EnableWithdrawConsent', 'true', 'Bool', '开启撤回同意隐私协议', 1, NOW(), NULL, 'Mobile', 500000000000087
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM `ginkgo_Sys_Settings` WHERE `Key` = 'App.Privacy.EnableWithdrawConsent');

-- 4. 已有环境：将示例内容同步为完整版（若仍为旧版短内容或未配置）
UPDATE `ginkgo_Sys_Settings` SET `Value` = '<h2>隐私政策</h2><p><strong>更新日期：</strong>2026年1月1日&nbsp;|&nbsp;<strong>生效日期：</strong>2026年1月1日</p><p>感谢您使用本应用（以下简称「我们」）。我们深知个人信息对您的重要性，并会尽全力保护您的个人信息安全。请在注册、登录或使用本应用前，仔细阅读并充分理解本政策。</p><h3>一、我们如何收集和使用个人信息</h3><p>为向您提供账户注册、登录验证、消息通知、业务办理等基础功能，我们可能收集以下信息：</p><ul><li><strong>帐号信息：</strong>用户名、昵称、密码（加密存储）</li><li><strong>联系信息：</strong>手机号码、电子邮箱（由您自愿填写）</li><li><strong>头像与简介：</strong>用于个人资料展示（可选）</li><li><strong>设备信息：</strong>设备型号、操作系统版本、应用版本号</li><li><strong>日志信息：</strong>登录时间、操作记录（用于安全审计与故障排查）</li></ul><h3>二、我们如何使用 Cookie 和同类技术</h3><p>为保障登录状态与安全，我们可能在本地存储必要的令牌与偏好设置（如字体大小、隐私同意记录），不会用于与提供服务无关的目的。</p><h3>三、信息的存储与保护</h3><p>您的个人信息存储于中华人民共和国境内服务器。我们采取加密传输、权限隔离、访问日志审计等安全措施保护您的数据。</p><h3>四、您的权利</h3><p>您依法享有以下权利，可在应用<strong>「我的」→「隐私与合规」</strong>中操作：</p><ul><li>查阅、更正您的个人信息</li><li>删除非必要的个人信息（邮箱、手机、头像、个人简介等）</li><li>注销用户帐号</li><li>撤回对本隐私政策的同意</li></ul><h3>五、未成年人保护</h3><p>若您未满 18 周岁，请在监护人陪同下阅读本政策，并在取得监护人同意后再使用本应用。</p><h3>六、政策更新</h3><p>我们可能适时修订本政策。重大变更将以应用内弹窗或公告方式通知您；若您继续使用，即视为同意修订后的政策。</p><h3>七、联系我们</h3><p>如对本政策有任何疑问、意见或投诉，请通过应用内「帮助中心」或联系系统管理员，我们将在合理期限内回复。</p>', `Description` = '隐私政策内容（示例，可在后台「系统配置-移动端」修改）', `Type` = 'RichText', `class` = 'Mobile', `UpdatedAt` = NOW()
WHERE `Key` = 'App.Privacy.PolicyContent' AND (`Value` IS NULL OR `Value` = '' OR CHAR_LENGTH(`Value`) < 500);

UPDATE `ginkgo_Sys_Settings` SET `Value` = '<h2>用户服务协议</h2><p><strong>更新日期：</strong>2026年1月1日&nbsp;|&nbsp;<strong>生效日期：</strong>2026年1月1日</p><p>欢迎您使用本应用。本协议是您与运营方之间关于使用本应用服务所订立的协议，请您仔细阅读。</p><h3>一、服务说明</h3><p>本应用提供企业级移动办公、业务管理与消息通知等能力。您完成注册并登录，即表示您已阅读、理解并同意受本协议及《隐私政策》约束。</p><h3>二、帐号注册与安全</h3><p>您应提供真实、准确、完整的信息完成注册，并妥善保管帐号与密码。因帐号保管不善导致的损失，由您自行承担相应责任。</p><h3>三、用户行为规范</h3><p>您在使用本应用时，不得从事以下行为：</p><ul><li>违反法律法规、公序良俗或本协议约定</li><li>传播违法、虚假、侵权或骚扰性信息</li><li>以任何方式干扰、破坏系统或他人正常使用</li><li>未经授权访问、抓取或篡改系统数据</li></ul><h3>四、隐私保护</h3><p>我们重视您的隐私保护，个人信息处理规则详见《隐私政策》。使用本应用即表示您同时同意《隐私政策》。</p><h3>五、知识产权</h3><p>本应用的界面设计、程序代码、文档资料等知识产权归运营方或相关权利人所有。未经授权，不得复制、修改、传播或用于商业用途。</p><h3>六、免责声明</h3><p>因不可抗力、网络故障、第三方服务异常等非我们可控原因导致的服务中断，我们将在合理范围内协助恢复，但不承担由此产生的间接损失。</p><h3>七、协议变更与终止</h3><p>我们有权根据业务需要更新本协议，更新后将通过应用内公告或弹窗告知。您可随时通过「隐私与合规」申请注销帐号以终止服务。</p><h3>八、联系我们</h3><p>如对本协议有任何疑问，请通过应用内「帮助中心」联系我们。</p>', `Description` = '用户协议内容（示例，可在后台「系统配置-移动端」修改）', `Type` = 'RichText', `class` = 'Mobile', `UpdatedAt` = NOW()
WHERE `Key` = 'App.Privacy.UserAgreementContent' AND (`Value` IS NULL OR `Value` = '' OR CHAR_LENGTH(`Value`) < 500);
