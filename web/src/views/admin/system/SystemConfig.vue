<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div :class="['system-config-page', { dark: auth.theme === 'dark' }]">
    <div class="page-header">
      <h2 class="page-title">系统配置</h2>
      <div class="page-actions">
        <el-button @click="handleRefresh" :loading="loading">
          <el-icon><Refresh /></el-icon>
          刷新
        </el-button>
        <el-button v-permission="'/system/config:save'" type="primary" @click="handleSave" :loading="saving">
          <el-icon><Check /></el-icon>
          保存
        </el-button>
      </div>
    </div>

    <el-tabs v-model="activeTab" class="config-tabs">
      <!-- 站点配置 -->
      <el-tab-pane label="站点" name="site">
        <el-card class="config-card">
          <el-form :model="formData" label-width="160px" label-position="left">
            <el-form-item label="站点名称">
              <div class="config-value-row"><div class="config-value-control"><el-input v-model="formData.siteName" placeholder="请输入站点名称" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Site.Name')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Site.Name')">C#</el-button></div></div>
            </el-form-item>
            <el-form-item label="基础地址">
              <div class="config-value-row"><div class="config-value-control"><el-input v-model="formData.baseUrl" placeholder="https://example.com" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Site.BaseUrl')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Site.BaseUrl')">C#</el-button></div></div>
            </el-form-item>
            <el-form-item label="站点 LOGO">
              <div class="config-value-row"><div class="config-value-control">
                <ResourcePicker v-model="formData.logoUrl" accept="image/*" placeholder="LOGO URL" />
              </div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Site.Logo')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Site.Logo')">C#</el-button></div></div>
            </el-form-item>
            <el-form-item label="网站图标 (Favicon)">
              <div class="config-value-row"><div class="config-value-control">
                <ResourcePicker v-model="formData.favicon" accept="image/*" placeholder="Favicon URL" />
              </div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Site.Branding.Favicon')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Site.Branding.Favicon')">C#</el-button></div></div>
            </el-form-item>
            <el-form-item label="主题色">
              <div class="config-value-row"><div class="config-value-control"><ColorPicker v-model="formData.primaryColor" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Site.Theme.PrimaryColor')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Site.Theme.PrimaryColor')">C#</el-button></div></div>
            </el-form-item>
            <el-form-item label="辅助色">
              <div class="config-value-row"><div class="config-value-control"><ColorPicker v-model="formData.secondaryColor" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Site.Theme.SecondaryColor')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Site.Theme.SecondaryColor')">C#</el-button></div></div>
            </el-form-item>
            <el-form-item label="维护模式">
              <div class="config-value-row"><div class="config-value-control"><el-switch v-model="formData.maintenanceMode" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Site.Maintenance.Enabled')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Site.Maintenance.Enabled')">C#</el-button></div></div>
              <span class="form-tip">启用后，普通用户将无法访问系统</span>
            </el-form-item>
            <el-form-item label="时区">
              <div class="config-value-row"><div class="config-value-control"><el-input v-model="formData.timeZone" placeholder="Asia/Shanghai" readonly /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Site.TimeZone')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Site.TimeZone')">C#</el-button></div></div>
            </el-form-item>
            <el-form-item label="页脚文本">
              <div class="config-value-row"><div class="config-value-control"><el-input v-model="formData.footerText" type="textarea" :rows="3" placeholder="© 2025 GinkgoAdmin. All rights reserved." /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Site.Footer.Text')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Site.Footer.Text')">C#</el-button></div></div>
            </el-form-item>
            <el-form-item label="ICP 备案号">
              <div class="config-value-row"><div class="config-value-control"><el-input v-model="formData.icpNo" placeholder="京ICP备XXXXXXXX号" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Site.ICP')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Site.ICP')">C#</el-button></div></div>
              <span class="form-tip">网站底部显示的 ICP 备案号，链接到 beian.miit.gov.cn</span>
            </el-form-item>
            <el-form-item label="公安备案号">
              <div class="config-value-row"><div class="config-value-control"><el-input v-model="formData.policeIcpNo" placeholder="京公网安备XXXXXXXXXXX号" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Site.PoliceICP')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Site.PoliceICP')">C#</el-button></div></div>
              <span class="form-tip">网站底部显示的公安机关备案号，链接到 beian.gov.cn</span>
            </el-form-item>
          </el-form>
        </el-card>
      </el-tab-pane>

      <!-- 登录页面配置（仅作用于后台管理登录页 /admin/login） -->
      <el-tab-pane label="登录页面（后台）" name="login">
        <el-card class="config-card">
          <el-alert
            type="info"
            :closable="false"
            show-icon
            style="margin-bottom: 16px;"
            title="作用范围：后台管理登录页（/admin/login）"
            description="以下配置仅作用于后台管理员登录页面（左侧品牌/动画展示区）。前台 Web 站点登录页（/web/login）有独立的营销内容设计，不消费这些配置。"
          />
          <el-form :model="formData" label-width="160px" label-position="left">
            <el-form-item label="登录页副标题">
              <div class="config-value-row"><div class="config-value-control"><el-input v-model="formData.loginSubtitle" placeholder="欢迎使用 Ginkgo 后台管理系统" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Site.Subtitle')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Site.Subtitle')">C#</el-button></div></div>
              <span class="form-tip">显示在后台登录页站点名下方的副标题。</span>
            </el-form-item>
            <el-form-item label="欢迎文本">
              <div class="config-value-row"><div class="config-value-control"><el-input v-model="formData.welcomeText" type="textarea" :rows="3" placeholder="欢迎回来！" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Site.Login.WelcomeText')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Site.Login.WelcomeText')">C#</el-button></div></div>
              <span class="form-tip">未填写"登录页副标题"时作为兜底显示在副标题位置。</span>
            </el-form-item>
            <el-form-item label="登录背景图">
              <div class="config-value-row"><div class="config-value-control">
                <ResourcePicker v-model="formData.loginBackground" accept="image/*" placeholder="背景图 URL" />
              </div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Site.Login.LeftPanelBackground')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Site.Login.LeftPanelBackground')">C#</el-button></div></div>
              <span class="form-tip">替换后台登录页左侧面板背景；未配置时使用默认蓝色渐变。</span>
            </el-form-item>
            <el-form-item label="动画效果">
              <div class="config-value-row"><div class="config-value-control"><el-switch v-model="formData.animationEnabled" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Site.Animation.Enabled')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Site.Animation.Enabled')">C#</el-button></div></div>
              <span class="form-tip">关闭后，后台登录页将不显示浮动圆圈、几何装饰、Logo 旋转等动效。</span>
            </el-form-item>
            <el-form-item label="动画强度" v-if="formData.animationEnabled">
              <div class="config-value-row"><div class="config-value-control">
                <el-radio-group v-model="formData.animationIntensity">
                  <el-radio value="light">轻度</el-radio><el-radio value="medium">中度</el-radio><el-radio value="strong">强烈</el-radio>
                </el-radio-group>
              </div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Site.Animation.Intensity')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Site.Animation.Intensity')">C#</el-button></div></div>
              <span class="form-tip">轻度=仅核心动效；中度=默认；强烈=全部动效 + 加快速度。</span>
            </el-form-item>
          </el-form>
        </el-card>
      </el-tab-pane>

      <!-- 注册与安全 -->
      <el-tab-pane label="注册与安全" name="registration">
        <!-- 注册模式 -->
        <el-card class="config-card" style="margin-bottom: 16px">
          <template #header>
            <div style="display: flex; align-items: center; justify-content: space-between;">
              <span style="font-weight: 600;">注册模式</span>
              <div class="config-value-actions" style="margin: 0"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Registration.Mode')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Registration.Mode')">C#</el-button></div>
            </div>
          </template>
          <div class="reg-mode-grid">
            <div
              v-for="mode in regModeOptions"
              :key="mode.value"
              :class="['reg-mode-card', { active: formData.registrationMode === mode.value, disabled: mode.value === 'disabled' && formData.registrationMode === mode.value }]"
              @click="formData.registrationMode = mode.value as typeof formData.registrationMode"
            >
              <div class="reg-mode-icon">{{ mode.icon }}</div>
              <div class="reg-mode-label">{{ mode.label }}</div>
              <div class="reg-mode-desc">{{ mode.desc }}</div>
              <div v-if="formData.registrationMode === mode.value" class="reg-mode-check">✓</div>
            </div>
          </div>
          <el-alert v-if="formData.registrationMode === 'disabled'" type="warning" :closable="false" show-icon style="margin-top: 12px">
            <template #title>注册功能已关闭，用户无法自助注册帐号，只能由管理员手动创建。</template>
          </el-alert>
          <el-alert v-if="formData.registrationMode === 'free'" type="info" :closable="false" show-icon style="margin-top: 12px">
            <template #title>自由注册模式下，用户仅需填写用户名和密码即可完成注册，无需邮箱或手机验证。</template>
          </el-alert>
        </el-card>

        <!-- 登录规则 -->
        <el-card class="config-card" style="margin-bottom: 16px">
          <template #header><span style="font-weight: 600;">登录规则</span></template>
          <el-form :model="formData" label-width="200px" label-position="left">
            <el-form-item label="登录验证码">
              <div class="config-value-row"><div class="config-value-control"><el-switch v-model="formData.loginCaptchaEnabled" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Registration.LoginCaptcha')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Registration.LoginCaptcha')">C#</el-button></div></div>
              <span class="form-tip">开启后，用户登录时需要输入图形验证码</span>
            </el-form-item>
            <el-form-item label="允许的登录方式">
              <div class="config-value-row"><div class="config-value-control">
                <el-checkbox-group v-model="formData.loginMethods">
                  <el-checkbox value="password">密码登录</el-checkbox>
                  <el-checkbox value="email_code" :disabled="!['email_code', 'both_code'].includes(formData.registrationMode)">邮箱验证码登录</el-checkbox>
                  <el-checkbox value="sms_code" :disabled="!['phone_code', 'both_code'].includes(formData.registrationMode)">短信验证码登录</el-checkbox>
                </el-checkbox-group>
              </div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Registration.LoginMethods')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Registration.LoginMethods')">C#</el-button></div></div>
              <span class="form-tip">勾选用户可以使用的登录方式（邮箱/短信验证码登录需要对应注册模式支持）</span>
            </el-form-item>
          </el-form>
        </el-card>

        <!-- 注册默认配置 -->
        <el-card v-if="formData.registrationMode !== 'disabled'" class="config-card" style="margin-bottom: 16px">
          <template #header><span style="font-weight: 600;">注册默认配置</span></template>
          <el-form :model="formData" label-width="200px" label-position="left">
            <el-form-item label="默认注册部门">
              <div class="config-value-row"><div class="config-value-control">
                <el-select v-model="selectedDeptIds" multiple filterable placeholder="搜索并选择部门" style="width: 100%" clearable>
                  <el-option v-for="dept in deptOptions" :key="dept.id" :label="dept.name" :value="dept.id" />
                </el-select>
              </div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Registration.DefaultDepartmentId')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Registration.DefaultDepartmentId')">C#</el-button></div></div>
              <span class="form-tip">新注册用户默认归属的部门</span>
            </el-form-item>
            <el-form-item label="默认注册角色">
              <div class="config-value-row"><div class="config-value-control">
                <el-select v-model="selectedRoleIds" multiple filterable placeholder="搜索并选择角色" style="width: 100%" clearable>
                  <el-option v-for="role in roleOptions" :key="role.id" :label="role.name" :value="role.id" />
                </el-select>
              </div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Registration.DefaultRoleIds')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Registration.DefaultRoleIds')">C#</el-button></div></div>
              <span class="form-tip">新注册用户默认获得的角色权限</span>
            </el-form-item>
          </el-form>
        </el-card>

        <!-- 安全 -->
        <el-card class="config-card">
          <template #header><span style="font-weight: 600;">安全</span></template>
          <el-form label-width="200px" label-position="left">
            <el-form-item label="禁止访问 IP">
              <div class="config-value-row"><div class="config-value-control">
                <div class="tag-input-group">
                  <el-tag v-for="(ip, index) in blockedIPTags" :key="index" closable @close="removeBlockedIP(index)" style="margin: 2px 4px 2px 0">{{ ip }}</el-tag>
                  <el-input v-if="ipInputVisible" ref="ipInputRef" v-model="ipInputValue" size="small" style="width: 200px" placeholder="输入 IP 后回车" @keyup.enter="handleIpInputConfirm" @blur="handleIpInputConfirm" />
                  <el-button v-else size="small" @click="showIpInput"><el-icon><Plus /></el-icon> 添加 IP</el-button>
                </div>
              </div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Security.BlockedIPs')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Security.BlockedIPs')">C#</el-button></div></div>
              <span class="form-tip">添加的 IP 地址将被禁止访问系统</span>
            </el-form-item>
          </el-form>
        </el-card>
      </el-tab-pane>

      <!-- 数据权限 -->
      <el-tab-pane label="数据权限" name="dataPermission">
        <el-card class="config-card">
          <el-alert
            type="warning"
            :closable="false"
            show-icon
            style="margin-bottom: 16px;"
            title="数据权限过滤将作用于主框架所有继承 IRepository<T> 的查询，以及任何调用 IDataScopeResolver 的插件模块。"
          >
            <template #default>
              <div style="line-height: 1.7;">
                <div><b>启用前提：</b>下方"启用数据权限过滤"打开。关闭时所有用户查询不做范围过滤（仅靠业务代码自检）。</div>
                <div><b>策略生效顺序（从低到高）：</b>1. <code>appsettings.json</code> 的 <code>DataScope</code> 节 → 2. 此处"默认数据范围"（覆盖默认策略） → 3. <b>「角色管理」页中每个角色的"数据范围"</b>（按用户当前角色覆盖）。</div>
                <div><b>管理员豁免：</b>命中 <code>appsettings.json</code> <code>DataScope.AdminRoles</code> 的角色（默认 ADMIN/SUPERADMIN）将自动跳过过滤。</div>
                <div><b>"指定部门"在哪配？</b>属于角色级配置，不是全局默认。在 <b>「角色管理 → 编辑角色 → 数据范围」</b>里选 SpecifiedDepartments 后会出现部门多选树；本页只能选 All / OwnOnly / DepartmentOnly / DepartmentAndChildren 这 4 种通用策略。</div>
              </div>
            </template>
          </el-alert>
          <el-form :model="formData" label-width="160px" label-position="left">
            <el-form-item label="启用数据权限过滤">
              <div class="config-value-row"><div class="config-value-control"><el-switch v-model="formData.dataPermissionEnabled" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('DataPermission.Enabled')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('DataPermission.Enabled')">C#</el-button></div></div>
              <span class="form-tip">关闭=不限制；开启=按当前用户角色自动过滤所有 IRepository&lt;T&gt; 的查询。</span>
            </el-form-item>
            <el-form-item label="默认数据范围">
              <div class="config-value-row"><div class="config-value-control">
                <el-select v-model="formData.dataScope" placeholder="请选择" :disabled="!formData.dataPermissionEnabled">
                  <el-option label="全部数据 (All)" value="All" />
                  <el-option label="仅本人 (OwnOnly)" value="OwnOnly" />
                  <el-option label="本部门 (DepartmentOnly)" value="DepartmentOnly" />
                  <el-option label="本部门及子部门 (DepartmentAndChildren)" value="DepartmentAndChildren" />
                </el-select>
              </div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('DataPermission.DefaultScope')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('DataPermission.DefaultScope')">C#</el-button></div></div>
              <span class="form-tip">当用户的角色未在"角色管理"中显式指定数据范围时，使用此默认策略。</span>
            </el-form-item>
          </el-form>
        </el-card>
      </el-tab-pane>

      <!-- 邮件配置 -->
      <el-tab-pane label="邮件" name="mail">
        <el-card class="config-card">
          <el-form :model="formData" label-width="160px" label-position="left">
            <el-form-item label="SMTP 服务器">
              <div class="config-value-row"><div class="config-value-control"><el-input v-model="formData.smtpHost" placeholder="smtp.example.com" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Mail.Smtp.Host')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Mail.Smtp.Host')">C#</el-button></div></div>
            </el-form-item>
            <el-form-item label="SMTP 端口">
              <div class="config-value-row"><div class="config-value-control"><el-input v-model="formData.smtpPort" placeholder="587" type="number" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Mail.Smtp.Port')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Mail.Smtp.Port')">C#</el-button></div></div>
            </el-form-item>
            <el-form-item label="启用 SSL/TLS">
              <div class="config-value-row"><div class="config-value-control"><el-switch v-model="formData.smtpSsl" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Mail.Ssl.Enable')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Mail.Ssl.Enable')">C#</el-button></div></div>
            </el-form-item>
            <el-form-item label="用户名">
              <div class="config-value-row"><div class="config-value-control"><el-input v-model="formData.smtpUser" placeholder="user@example.com" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Mail.Smtp.UserName')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Mail.Smtp.UserName')">C#</el-button></div></div>
            </el-form-item>
            <el-form-item label="密码">
              <div class="config-value-row"><div class="config-value-control"><el-input v-model="formData.smtpPassword" type="password" placeholder="密码" show-password /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Mail.Smtp.Password')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Mail.Smtp.Password')">C#</el-button></div></div>
            </el-form-item>
            <el-form-item label="认证方式">
              <div class="config-value-row"><div class="config-value-control">
                <el-select v-model="formData.smtpAuthType" placeholder="请选择">
                  <el-option label="None" value="None" /><el-option label="Login" value="Login" /><el-option label="Plain" value="Plain" /><el-option label="CRAM-MD5" value="CramMd5" />
                </el-select>
              </div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Mail.Smtp.AuthType')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Mail.Smtp.AuthType')">C#</el-button></div></div>
            </el-form-item>
            <el-form-item label="发件人邮箱">
              <div class="config-value-row"><div class="config-value-control"><el-input v-model="formData.mailFrom" placeholder="noreply@example.com" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Mail.From.Address')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Mail.From.Address')">C#</el-button></div></div>
            </el-form-item>
            <el-form-item label="发件人名称">
              <div class="config-value-row"><div class="config-value-control"><el-input v-model="formData.mailFromName" placeholder="Ginkgo System" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Mail.From.DisplayName')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Mail.From.DisplayName')">C#</el-button></div></div>
            </el-form-item>
          </el-form>
        </el-card>

        <!-- 测试邮件 -->
        <el-card class="config-card" style="margin-top: 16px;">
          <template #header>
            <span style="font-weight: 600;">发送测试邮件</span>
          </template>
          <el-form label-width="160px" label-position="left">
            <el-form-item label="收件人邮箱">
              <div style="display: flex; gap: 12px; width: 100%;">
                <el-input
                  v-model="testEmailAddress"
                  placeholder="请输入收件人邮箱地址"
                  style="flex: 1;"
                  @keyup.enter="handleSendTestEmail"
                />
                <el-button
                  type="primary"
                  @click="handleSendTestEmail"
                  :loading="sendingTestEmail"
                  :disabled="!testEmailAddress"
                >
                  发送测试
                </el-button>
              </div>
              <span class="form-tip">请先保存邮件配置，再发送测试邮件验证配置是否正确</span>
            </el-form-item>
          </el-form>
        </el-card>
      </el-tab-pane>

      <!-- 网络与上传 -->
      <el-tab-pane label="网络与上传" name="network">
        <el-card class="config-card">
          <el-form :model="formData" label-width="220px" label-position="left">
            <el-form-item label="CORS 允许来源">
              <div class="config-value-row"><div class="config-value-control">
                <div class="tag-input-container">
                  <el-tag v-for="(tag, index) in corsOriginTags" :key="index" closable :disable-transitions="false" style="margin-right: 6px; margin-bottom: 6px" @close="corsOriginTags.splice(index, 1)">{{ tag }}</el-tag>
                  <el-input v-if="corsInputVisible" ref="corsInputRef" v-model="corsInputValue" size="small" style="width: 240px" placeholder="输入来源地址，按回车确认" @keyup.enter="handleCorsInputConfirm" @blur="handleCorsInputConfirm" />
                  <el-button v-else size="small" @click="showCorsInput">+ 添加来源</el-button>
                </div>
              </div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Site.Cors.AllowedOrigins')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Site.Cors.AllowedOrigins')">C#</el-button></div></div>
            </el-form-item>
            <el-form-item label="上传最大 MB">
              <div class="config-value-row"><div class="config-value-control"><el-input v-model="formData.uploadMaxMB" placeholder="20" type="number" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Upload.MaxSizeMB')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Upload.MaxSizeMB')">C#</el-button></div></div>
            </el-form-item>
            <el-form-item label="允许扩展名">
              <div class="config-value-row"><div class="config-value-control">
                <div class="tag-input-container">
                  <el-tag v-for="(tag, index) in uploadExtTags" :key="index" closable type="info" :disable-transitions="false" style="margin-right: 6px; margin-bottom: 6px" @close="uploadExtTags.splice(index, 1)">{{ tag }}</el-tag>
                  <el-input v-if="extInputVisible" ref="extInputRef" v-model="extInputValue" size="small" style="width: 160px" placeholder="如 .mp3，按回车确认" @keyup.enter="handleExtInputConfirm" @blur="handleExtInputConfirm" />
                  <el-button v-else size="small" @click="showExtInput">+ 添加扩展名</el-button>
                </div>
              </div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Upload.AllowedExtensions')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Upload.AllowedExtensions')">C#</el-button></div></div>
            </el-form-item>
            <el-form-item label="默认上传目录">
              <div class="config-value-row"><div class="config-value-control"><el-input v-model="formData.uploadBasePath" placeholder="/uploads" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Upload.BasePath')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Upload.BasePath')">C#</el-button></div></div>
              <span class="form-tip">域名后的相对目录，如 /uploads</span>
            </el-form-item>

            <el-divider content-position="left">图片压缩设置</el-divider>

            <el-form-item label="上传图片时后端压缩">
              <div class="config-value-row"><div class="config-value-control"><el-switch v-model="formData.imageCompressEnabled" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Upload.ImageCompress.Enabled')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Upload.ImageCompress.Enabled')">C#</el-button></div></div>
              <span class="form-tip">开启后，上传的图片将在后端自动压缩</span>
            </el-form-item>
            <el-form-item v-if="formData.imageCompressEnabled" label="压缩质量 (%)">
              <div class="config-value-row"><div class="config-value-control"><el-slider v-model="formData.imageCompressQuality" :min="10" :max="100" :step="5" show-input style="max-width: 400px" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Upload.ImageCompress.Quality')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Upload.ImageCompress.Quality')">C#</el-button></div></div>
              <span class="form-tip">值越小压缩率越高，建议 60-85</span>
            </el-form-item>
            <el-form-item v-if="formData.imageCompressEnabled" label="压缩后保留原图">
              <div class="config-value-row"><div class="config-value-control"><el-switch v-model="formData.imageCompressKeepOriginal" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Upload.ImageCompress.KeepOriginal')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Upload.ImageCompress.KeepOriginal')">C#</el-button></div></div>
              <span class="form-tip">开启后同时保存原图和压缩后的图片</span>
            </el-form-item>
          </el-form>
        </el-card>
      </el-tab-pane>

      <!-- 动态配置项 TAB（从字典 sysconfig 分类加载，位于多语言之前） -->
      <el-tab-pane
        v-for="group in dynamicGroups"
        :key="group.name"
        :label="group.name"
        :name="`dynamic-${group.name}`"
      >
        <el-card class="config-card">
          <el-form label-width="200px" label-position="left">
            <el-empty v-if="group.items.length === 0" description="该分组暂无配置项，请在「添加配置」中添加" :image-size="80" />
            <el-form-item
              v-for="item in group.items"
              :key="item.key"
              :label="item.label"
            >
              <div class="config-value-row">
                <div class="config-value-control">
                  <!-- String -->
                  <el-input v-if="item.type === 'String'" v-model="item.value" :placeholder="item.description || '请输入'" />
                  <!-- Text -->
                  <el-input v-else-if="item.type === 'Text'" v-model="item.value" type="textarea" :rows="4" :placeholder="item.description || '请输入多行文本'" />
                  <!-- Integer -->
                  <el-input-number v-else-if="item.type === 'Integer'" v-model="item.numberValue" :precision="0" :step="1" controls-position="right" style="width: 220px" @change="item.value = String(item.numberValue ?? 0)" />
                  <!-- Number / Decimal -->
                  <el-input-number v-else-if="item.type === 'Number' || item.type === 'Decimal'" v-model="item.numberValue" :precision="4" :step="0.1" controls-position="right" style="width: 220px" @change="item.value = String(item.numberValue ?? 0)" />
                  <!-- Bool -->
                  <el-switch v-else-if="item.type === 'Bool'" v-model="item.boolValue" @change="item.value = item.boolValue ? 'true' : 'false'" />
                  <!-- Json -->
                  <div v-else-if="item.type === 'Json'" style="width: 100%">
                    <el-input v-model="item.value" type="textarea" :rows="6" class="json-editor-textarea" :placeholder="item.description || '请输入 JSON'" />
                    <el-button size="small" style="margin-top: 4px" @click="formatJsonField(item)">格式化 JSON</el-button>
                  </div>
                  <!-- RichText -->
                  <DynamicEditor v-else-if="item.type === 'RichText'" v-model="item.value" editor-type="rich" :height="200" placeholder="请输入富文本内容" />
                  <!-- Password -->
                  <el-input v-else-if="item.type === 'Password'" v-model="item.value" type="password" show-password :placeholder="item.description || '请输入密码'" />
                  <!-- Color -->
                  <div v-else-if="item.type === 'Color'" class="color-inline">
                    <el-color-picker v-model="item.value" show-alpha />
                    <el-input v-model="item.value" style="width: 160px; margin-left: 8px" placeholder="#000000" />
                  </div>
                  <!-- Url -->
                  <el-input v-else-if="item.type === 'Url'" v-model="item.value" :placeholder="item.description || 'https://example.com'" clearable />
                  <!-- SingleImage -->
                  <div v-else-if="item.type === 'SingleImage'" class="file-picker-inline">
                    <el-image v-if="item.value" :src="resolveResourcePath(item.value)" fit="cover" class="file-picker-thumb" :preview-src-list="[resolveResourcePath(item.value)]" />
                    <div class="file-picker-btns">
                      <el-button size="small" @click="openConfigFileSelector({ multiple: false, accept: 'image/*', callback: (f) => { item.value = f[0]?.url || '' } })">选择图片</el-button>
                      <el-button v-if="item.value" size="small" text type="danger" @click="item.value = ''">清除</el-button>
                    </div>
                  </div>
                  <!-- MultiImage -->
                  <div v-else-if="item.type === 'MultiImage'" class="file-picker-inline">
                    <div class="file-picker-thumbs">
                      <el-image v-for="(url, idx) in parseFileUrls(item.value)" :key="idx" :src="resolveResourcePath(url)" fit="cover" class="file-picker-thumb" :preview-src-list="parseFileUrls(item.value).map(u => resolveResourcePath(u))" />
                    </div>
                    <div class="file-picker-btns">
                      <el-button size="small" @click="openConfigFileSelector({ multiple: true, accept: 'image/*', callback: (f) => { const urls = [...parseFileUrls(item.value), ...f.map(x => x.url || '')]; item.value = JSON.stringify(urls) } })">添加图片</el-button>
                      <el-button v-if="item.value" size="small" text type="danger" @click="item.value = ''">清空</el-button>
                    </div>
                  </div>
                  <!-- SingleFile -->
                  <div v-else-if="item.type === 'SingleFile'" class="file-picker-inline">
                    <el-input v-model="item.value" placeholder="文件路径" readonly style="flex:1" />
                    <el-button size="small" style="margin-left: 8px" @click="openConfigFileSelector({ multiple: false, accept: '*/*', callback: (f) => { item.value = f[0]?.url || '' } })">选择文件</el-button>
                    <el-button v-if="item.value" size="small" text type="danger" @click="item.value = ''">清除</el-button>
                  </div>
                  <!-- MultiFile -->
                  <div v-else-if="item.type === 'MultiFile'" class="file-picker-inline" style="flex-direction: column; align-items: flex-start">
                    <div class="file-picker-tags">
                      <el-tag v-for="(url, idx) in parseFileUrls(item.value)" :key="idx" closable size="small" style="margin: 2px 4px 2px 0" @close="() => { const arr = parseFileUrls(item.value); arr.splice(idx, 1); item.value = JSON.stringify(arr) }">{{ url.split('/').pop() }}</el-tag>
                    </div>
                    <div class="file-picker-btns" style="margin-top: 4px">
                      <el-button size="small" @click="openConfigFileSelector({ multiple: true, accept: '*/*', callback: (f) => { const urls = [...parseFileUrls(item.value), ...f.map(x => x.url || '')]; item.value = JSON.stringify(urls) } })">添加文件</el-button>
                      <el-button v-if="item.value" size="small" text type="danger" @click="item.value = ''">清空</el-button>
                    </div>
                  </div>
                  <!-- 默认 -->
                  <el-input v-else v-model="item.value" :placeholder="item.description || '请输入'" />
                </div>
                <div class="config-value-actions">
                  <el-tooltip content="复制 Vue 调用代码" placement="top">
                    <el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode(item.key)">Vue</el-button>
                  </el-tooltip>
                  <el-tooltip content="复制 C# 调用代码" placement="top">
                    <el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode(item.key)">C#</el-button>
                  </el-tooltip>
                </div>
              </div>
              <span v-if="item.description && !['Json','RichText'].includes(item.type)" class="form-tip">{{ item.description }}</span>
            </el-form-item>
          </el-form>
        </el-card>
      </el-tab-pane>

      <!-- 多语言配置 -->
      <el-tab-pane label="多语言" name="language">
        <el-card class="config-card">
          <el-form label-width="180px" label-position="left">
            <el-form-item label="启用多语言">
              <div class="config-value-row"><div class="config-value-control"><el-switch v-model="langConfig.enabled" /></div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Language.MultiLang.Enabled')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Language.MultiLang.Enabled')">C#</el-button></div></div>
              <span class="form-tip">启用后，前台 URL 将包含语言前缀（如 /zh/web）</span>
            </el-form-item>

            <el-form-item label="默认语言">
              <div class="config-value-row"><div class="config-value-control">
                <el-select v-model="langConfig.defaultLang" placeholder="请选择默认语言">
                  <el-option v-for="l in langConfig.langs" :key="l.code" :label="`${l.flag} ${l.label} (${l.code})`" :value="l.code" />
                </el-select>
              </div>
              <div class="config-value-actions"><el-button size="small" class="copy-btn copy-btn-vue" @click="copyVueCode('Language.MultiLang.Default')">Vue</el-button><el-button size="small" class="copy-btn copy-btn-csharp" @click="copyCSharpCode('Language.MultiLang.Default')">C#</el-button></div></div>
            </el-form-item>

            <el-form-item label="语言列表">
              <div class="lang-list">
                <div v-for="(l, idx) in langConfig.langs" :key="l.code" class="lang-row">
                  <span class="lang-flag">{{ l.flag }}</span>
                  <span class="lang-label">{{ l.label }}</span>
                  <span class="lang-code">({{ l.code }} → /{{ l.urlCode }}/web)</span>
                  <el-tag v-if="l.code === langConfig.defaultLang" size="small" type="success">默认</el-tag>
                  <el-tag v-if="l.required" size="small">必填</el-tag>
                  <div class="lang-actions">
                    <el-button size="small" text :disabled="idx === 0" @click="moveLang(idx, -1)">↑</el-button>
                    <el-button size="small" text :disabled="idx === langConfig.langs.length - 1" @click="moveLang(idx, 1)">↓</el-button>
                    <el-button size="small" text type="danger" :disabled="langConfig.langs.length <= 1" @click="removeLang(idx)">删除</el-button>
                  </div>
                </div>
                <div class="lang-add-row">
                  <el-select v-model="newLangCode" placeholder="选择要添加的语言" filterable style="width: 240px;">
                    <el-option
                      v-for="l in availableNewLangs"
                      :key="l.code"
                      :label="`${l.flag} ${l.label} (${l.code})`"
                      :value="l.code"
                    />
                  </el-select>
                  <el-button type="primary" size="small" @click="addLang" :disabled="!newLangCode">添加语言</el-button>
                </div>
              </div>
            </el-form-item>

            <el-divider />

            <el-form-item label="插件多语言覆盖">
              <span class="form-tip" style="margin-bottom: 8px; display: block;">
                默认所有插件跟随全局多语言设置，此处可单独关闭某个插件的多语言
              </span>
              <div class="plugin-override-list">
                <div v-for="p in pluginOverrideList" :key="p.id" class="plugin-override-row">
                  <span>{{ p.name }}</span>
                  <el-switch v-model="p.enabled" :active-text="'多语言'" :inactive-text="'单语言'" />
                </div>
                <div v-if="pluginOverrideList.length === 0" class="plugin-override-empty">
                  暂无已安装的插件
                </div>
              </div>
            </el-form-item>
          </el-form>
        </el-card>
      </el-tab-pane>

      <!-- 添加配置 TAB -->
      <el-tab-pane label="添加配置" name="add-config">
        <!-- 分组管理区 -->
        <el-card class="config-card" style="margin-bottom: 16px">
          <template #header><span style="font-weight: 600;">分组管理</span></template>
          <div v-loading="groupLoading">
            <div v-if="groupList.length === 0 && !groupLoading" style="color: var(--el-text-color-secondary); font-size: 13px; padding: 8px 0;">暂无分组，请在下方添加</div>
            <div v-for="g in groupList" :key="g.id" class="group-mgmt-row">
              <template v-if="g.editing">
                <el-input v-model="g.editName" size="small" style="flex: 1" @keyup.enter="handleSaveGroupEdit(g)" />
                <el-button size="small" type="primary" @click="handleSaveGroupEdit(g)">保存</el-button>
                <el-button size="small" @click="g.editing = false">取消</el-button>
              </template>
              <template v-else>
                <span class="group-mgmt-name">{{ g.name }}</span>
                <div class="group-mgmt-actions">
                  <el-button size="small" text type="primary" @click="g.editing = true; g.editName = g.name">编辑</el-button>
                  <el-button size="small" text type="danger" @click="handleDeleteGroup(g)">删除</el-button>
                </div>
              </template>
            </div>
            <div class="group-mgmt-add">
              <el-input v-model="newGroupName" size="small" placeholder="输入新分组名称" style="flex: 1" @keyup.enter="handleAddGroup" />
              <el-button size="small" type="primary" @click="handleAddGroup" :disabled="!newGroupName.trim()"><el-icon><Plus /></el-icon>添加分组</el-button>
            </div>
          </div>
        </el-card>

        <!-- 添加配置表单 -->
        <el-card class="config-card">
          <el-form :model="newConfigForm" label-width="160px" label-position="left">
            <el-form-item label="分组 (Class)">
              <el-select
                v-model="newConfigForm.class"
                placeholder="请选择分组"
                filterable
                allow-create
                style="width: 100%;"
              >
                <el-option
                  v-for="g in groupList"
                  :key="g.id"
                  :label="g.name"
                  :value="g.name"
                />
              </el-select>
              <span class="form-tip">选择现有分组或输入新分组名称（输入新名称会自动创建分组）</span>
            </el-form-item>

            <el-form-item label="配置键 (Key)">
              <el-input
                v-model="newConfigForm.key"
                placeholder="例如：GroupName.ConfigKey"
              />
              <span class="form-tip">建议格式：分组名.配置键</span>
            </el-form-item>

            <el-form-item label="类型 (Type)">
              <el-select v-model="newConfigForm.type" placeholder="请选择类型" style="width: 100%">
                <el-option label="String (单行文本)" value="String" />
                <el-option label="Text (多行文本)" value="Text" />
                <el-option label="Integer (整数)" value="Integer" />
                <el-option label="Number (数字)" value="Number" />
                <el-option label="Decimal (小数)" value="Decimal" />
                <el-option label="Bool (布尔开关)" value="Bool" />
                <el-option label="Json (JSON 数据)" value="Json" />
                <el-option label="RichText (富文本)" value="RichText" />
                <el-option label="Password (密码)" value="Password" />
                <el-option label="Color (颜色)" value="Color" />
                <el-option label="Url (链接)" value="Url" />
                <el-option label="SingleImage (单图)" value="SingleImage" />
                <el-option label="MultiImage (多图)" value="MultiImage" />
                <el-option label="SingleFile (单文件)" value="SingleFile" />
                <el-option label="MultiFile (多文件)" value="MultiFile" />
              </el-select>
            </el-form-item>

            <el-form-item label="配置值 (Value)">
              <!-- String: 单行文本 -->
              <el-input
                v-if="newConfigForm.type === 'String'"
                v-model="newConfigForm.value"
                placeholder="请输入字符串值"
              />

              <!-- Text: 多行文本 -->
              <el-input
                v-else-if="newConfigForm.type === 'Text'"
                v-model="newConfigForm.value"
                type="textarea"
                :rows="4"
                placeholder="请输入多行文本"
              />

              <!-- Integer: 整数 -->
              <el-input-number
                v-else-if="newConfigForm.type === 'Integer'"
                v-model="newConfigFormNumberValue"
                :precision="0"
                :step="1"
                controls-position="right"
                style="width: 220px"
                @change="newConfigForm.value = String(newConfigFormNumberValue ?? 0)"
              />

              <!-- Number / Decimal: 数字 -->
              <el-input-number
                v-else-if="newConfigForm.type === 'Number' || newConfigForm.type === 'Decimal'"
                v-model="newConfigFormNumberValue"
                :precision="4"
                :step="0.1"
                controls-position="right"
                style="width: 220px"
                @change="newConfigForm.value = String(newConfigFormNumberValue ?? 0)"
              />

              <!-- Bool: 开关 -->
              <el-switch
                v-else-if="newConfigForm.type === 'Bool'"
                v-model="newConfigFormBoolValue"
                @change="newConfigForm.value = newConfigFormBoolValue ? 'true' : 'false'"
              />

              <!-- Json: JSON 编辑器 -->
              <div v-else-if="newConfigForm.type === 'Json'" style="width: 100%">
                <el-input
                  v-model="newConfigForm.value"
                  type="textarea"
                  :rows="8"
                  class="json-editor-textarea"
                  placeholder='{"key": "value"}'
                />
                <el-button size="small" style="margin-top: 4px" @click="formatNewConfigJson">
                  格式化 JSON
                </el-button>
              </div>

              <!-- RichText: 富文本编辑器 -->
              <DynamicEditor
                v-else-if="newConfigForm.type === 'RichText'"
                v-model="newConfigForm.value"
                editor-type="rich"
                :height="200"
                placeholder="请输入富文本内容"
              />

              <!-- Password: 密码 -->
              <el-input
                v-else-if="newConfigForm.type === 'Password'"
                v-model="newConfigForm.value"
                type="password"
                show-password
                placeholder="请输入密码"
              />

              <!-- Color: 颜色选择 -->
              <div v-else-if="newConfigForm.type === 'Color'" class="color-inline">
                <el-color-picker v-model="newConfigForm.value" show-alpha />
                <el-input v-model="newConfigForm.value" style="width: 160px; margin-left: 8px" placeholder="#000000" />
              </div>

              <!-- Url: 链接 -->
              <el-input
                v-else-if="newConfigForm.type === 'Url'"
                v-model="newConfigForm.value"
                placeholder="https://example.com"
                clearable
              />

              <!-- SingleImage: 单图 -->
              <div v-else-if="newConfigForm.type === 'SingleImage'" class="file-picker-inline">
                <el-image v-if="newConfigForm.value" :src="resolveResourcePath(newConfigForm.value)" fit="cover" class="file-picker-thumb" />
                <div class="file-picker-btns">
                  <el-button size="small" @click="openConfigFileSelector({ multiple: false, accept: 'image/*', callback: (f) => { newConfigForm.value = f[0]?.url || '' } })">选择图片</el-button>
                  <el-button v-if="newConfigForm.value" size="small" text type="danger" @click="newConfigForm.value = ''">清除</el-button>
                </div>
              </div>

              <!-- MultiImage: 多图 -->
              <div v-else-if="newConfigForm.type === 'MultiImage'" class="file-picker-inline">
                <div class="file-picker-thumbs">
                  <el-image v-for="(url, idx) in parseFileUrls(newConfigForm.value)" :key="idx" :src="resolveResourcePath(url)" fit="cover" class="file-picker-thumb" />
                </div>
                <div class="file-picker-btns">
                  <el-button size="small" @click="openConfigFileSelector({ multiple: true, accept: 'image/*', callback: (f) => { const urls = [...parseFileUrls(newConfigForm.value), ...f.map(x => x.url || '')]; newConfigForm.value = JSON.stringify(urls) } })">添加图片</el-button>
                  <el-button v-if="newConfigForm.value" size="small" text type="danger" @click="newConfigForm.value = ''">清空</el-button>
                </div>
              </div>

              <!-- SingleFile: 单文件 -->
              <div v-else-if="newConfigForm.type === 'SingleFile'" class="file-picker-inline">
                <el-input v-model="newConfigForm.value" placeholder="文件路径" readonly style="flex:1" />
                <el-button size="small" style="margin-left: 8px" @click="openConfigFileSelector({ multiple: false, accept: '*/*', callback: (f) => { newConfigForm.value = f[0]?.url || '' } })">选择文件</el-button>
                <el-button v-if="newConfigForm.value" size="small" text type="danger" @click="newConfigForm.value = ''">清除</el-button>
              </div>

              <!-- MultiFile: 多文件 -->
              <div v-else-if="newConfigForm.type === 'MultiFile'" class="file-picker-inline" style="flex-direction: column; align-items: flex-start">
                <div class="file-picker-tags">
                  <el-tag v-for="(url, idx) in parseFileUrls(newConfigForm.value)" :key="idx" closable size="small" style="margin: 2px 4px 2px 0" @close="() => { const arr = parseFileUrls(newConfigForm.value); arr.splice(idx, 1); newConfigForm.value = JSON.stringify(arr) }">{{ url.split('/').pop() }}</el-tag>
                </div>
                <div class="file-picker-btns" style="margin-top: 4px">
                  <el-button size="small" @click="openConfigFileSelector({ multiple: true, accept: '*/*', callback: (f) => { const urls = [...parseFileUrls(newConfigForm.value), ...f.map(x => x.url || '')]; newConfigForm.value = JSON.stringify(urls) } })">添加文件</el-button>
                  <el-button v-if="newConfigForm.value" size="small" text type="danger" @click="newConfigForm.value = ''">清空</el-button>
                </div>
              </div>

              <!-- 默认 -->
              <el-input
                v-else
                v-model="newConfigForm.value"
                placeholder="请输入配置值"
              />
            </el-form-item>

            <el-form-item label="说明 (Description)">
              <el-input
                v-model="newConfigForm.description"
                placeholder="配置项说明"
              />
            </el-form-item>

            <el-form-item>
              <el-button v-permission="'/system/config:save'" type="primary" @click="handleAddConfig" :loading="adding">
                <el-icon><Plus /></el-icon>
                添加配置
              </el-button>
              <el-button @click="resetNewConfigForm">
                <el-icon><Refresh /></el-icon>
                重置
              </el-button>
            </el-form-item>
          </el-form>
        </el-card>
      </el-tab-pane>

      <!-- 验证码模板管理 -->
      <el-tab-pane label="验证码模板" name="verification-templates">
        <el-card class="config-card">
          <VerificationTemplateManager />
        </el-card>
      </el-tab-pane>
    </el-tabs>

    <!-- 通用配置文件选择器 -->
    <FileSelector
      v-model="configFileSelectorVisible"
      title="选择文件"
      :multiple="configFileSelectorMultiple"
      :accept="configFileSelectorAccept"
      @confirm="onConfigFileSelected"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, nextTick, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Refresh, Check, Plus } from '@element-plus/icons-vue'
import {
  getAllSettings,
  saveBatchSettings,
  upsertSetting,
  sendTestEmail,
  getDictionaryCategories,
  getDictionaryItems,
  type SettingDto,
  type DictionaryCategoryDto,
  type DictionaryItemDto
} from '../../../api/system'
import ColorPicker from '../../../components/ColorPicker.vue'
import FileSelector from '../../../components/FileSelector.vue'
import ResourcePicker from '../../../components/ResourcePicker.vue'
import DynamicEditor from '../../../components/DynamicEditor.vue'
import {
  getDictionaryCategories as getDictCategoriesApi,
  getDictionaryItems as getDictItemsApi,
  createDictionaryCategory as createDictCategoryApi,
  createDictionaryItem as createDictItemApi,
  updateDictionaryItem as updateDictItemApi,
  deleteDictionaryItem as deleteDictItemApi,
  type DictItemListItem
} from '../../../api/dictionary'
import VerificationTemplateManager from '../../../components/VerificationTemplateManager.vue'
import { type FileListItemDto } from '../../../api/files'
import { resolveResourcePath, fetchResourceConfig } from '../../../utils/resourceUrl'
import { useAuthStore } from '../../../stores/auth'
import { getDepartmentsTree, type DepartmentTreeNode } from '../../../api/department'
import { getRoleTree, type RoleTreeNode } from '../../../api/role'

// Stores
const auth = useAuthStore()

// 当前激活的标签页
const activeTab = ref('site')

// 加载状态
const loading = ref(false)
const saving = ref(false)
const adding = ref(false)

// 测试邮件
const testEmailAddress = ref('')
const sendingTestEmail = ref(false)

// 动态配置项数据结构
interface DynamicConfigItem {
  key: string
  value: string
  boolValue?: boolean
  numberValue?: number
  type: string
  label: string
  description?: string
  class?: string
}

interface DynamicConfigGroup {
  name: string
  items: DynamicConfigItem[]
}

// 动态配置分组
const dynamicGroups = ref<DynamicConfigGroup[]>([])

// 新增配置表单
const newConfigForm = reactive({
  class: '',
  key: '',
  value: '',
  type: 'String',
  description: ''
})

// 新增配置表单的辅助字段（数字/布尔类型使用）
const newConfigFormNumberValue = ref<number>(0)
const newConfigFormBoolValue = ref<boolean>(false)

/** 格式化新增配置表单中的 JSON 值 */
function formatNewConfigJson() {
  try {
    const parsed = JSON.parse(newConfigForm.value)
    newConfigForm.value = JSON.stringify(parsed, null, 2)
  } catch {
    ElMessage.warning('JSON 格式不正确，无法格式化')
  }
}

/** 格式化动态配置项的 JSON 值 */
function formatJsonField(item: DynamicConfigItem) {
  try {
    const parsed = JSON.parse(item.value)
    item.value = JSON.stringify(parsed, null, 2)
  } catch {
    ElMessage.warning('JSON 格式不正确，无法格式化')
  }
}

// ---------- 分组管理 ----------
interface GroupInfo {
  id: string
  name: string
  editing: boolean
  editName: string
}
const groupList = ref<GroupInfo[]>([])
const newGroupName = ref('')
const groupLoading = ref(false)
let _sysconfigCategoryId = ''

async function loadGroupList() {
  groupLoading.value = true
  try {
    const cats = await getDictCategoriesApi('sysconfig')
    const cat = cats.find(c => c.code.toLowerCase() === 'sysconfig')
    if (!cat) {
      // 自动创建 sysconfig 分类
      _sysconfigCategoryId = await createDictCategoryApi({ code: 'sysconfig', name: '系统自定义配置', category: 'system', enabled: true })
    } else {
      _sysconfigCategoryId = cat.id
    }
    const items = await getDictItemsApi(_sysconfigCategoryId)
    groupList.value = items.map(i => ({ id: i.id, name: i.itemValue || i.itemKey, editing: false, editName: '' }))
  } catch (e: any) {
    ElMessage.error('加载分组列表失败: ' + (e?.message || ''))
  } finally {
    groupLoading.value = false
  }
}

async function handleAddGroup() {
  const name = newGroupName.value.trim()
  if (!name) return ElMessage.warning('请输入分组名称')
  if (groupList.value.some(g => g.name === name)) return ElMessage.warning('分组已存在')
  try {
    const id = await createDictItemApi({ categoryId: _sysconfigCategoryId, itemKey: name, itemValue: name, enabled: true })
    groupList.value.push({ id: String(id), name, editing: false, editName: '' })
    newGroupName.value = ''
    ElMessage.success('分组已添加')
    loadSettings()
  } catch (e: any) {
    ElMessage.error('添加分组失败: ' + (e?.message || ''))
  }
}

async function handleSaveGroupEdit(g: GroupInfo) {
  const name = g.editName.trim()
  if (!name) return ElMessage.warning('分组名称不能为空')
  try {
    await updateDictItemApi(g.id, { itemKey: name, itemValue: name })
    g.name = name
    g.editing = false
    ElMessage.success('分组已更新')
    loadSettings()
  } catch (e: any) {
    ElMessage.error('更新分组失败: ' + (e?.message || ''))
  }
}

async function handleDeleteGroup(g: GroupInfo) {
  try {
    await ElMessageBox.confirm(`确定删除分组「${g.name}」？删除后该分组下的配置项不会被删除，但不再归类显示。`, '确认删除', { type: 'warning' })
    await deleteDictItemApi(g.id)
    groupList.value = groupList.value.filter(x => x.id !== g.id)
    ElMessage.success('分组已删除')
    loadSettings()
  } catch {}
}

// 监听新增配置类型切换，自动清空残留值
watch(() => newConfigForm.type, () => {
  newConfigForm.value = ''
  newConfigFormNumberValue.value = 0
  newConfigFormBoolValue.value = false
})

// ---------- 通用文件选择器（配置用） ----------
const configFileSelectorVisible = ref(false)
const configFileSelectorMultiple = ref(false)
const configFileSelectorAccept = ref('')
let _configFileCallback: ((files: FileListItemDto[]) => void) | null = null

function openConfigFileSelector(opts: { multiple: boolean; accept: string; callback: (files: FileListItemDto[]) => void }) {
  configFileSelectorMultiple.value = opts.multiple
  configFileSelectorAccept.value = opts.accept
  _configFileCallback = opts.callback
  configFileSelectorVisible.value = true
}

function onConfigFileSelected(files: FileListItemDto[]) {
  if (_configFileCallback && files.length > 0) {
    _configFileCallback(files)
    _configFileCallback = null
  }
}

/** 解析多文件值（逗号或 JSON 数组） */
function parseFileUrls(value: string): string[] {
  if (!value || !value.trim()) return []
  const trimmed = value.trim()
  if (trimmed.startsWith('[')) {
    try { const arr = JSON.parse(trimmed); if (Array.isArray(arr)) return arr.filter(Boolean) } catch {}
  }
  return trimmed.split(',').map(s => s.trim()).filter(Boolean)
}

// ---------- 注册模式选项 ----------
const regModeOptions = [
  { value: 'disabled', icon: '🚫', label: '关闭注册', desc: '不允许自助注册，仅管理员创建帐号' },
  { value: 'free', icon: '🔓', label: '自由注册', desc: '用户名 + 密码，无需验证' },
  { value: 'email_code', icon: '📧', label: '邮箱注册', desc: '邮箱作为帐号，需邮箱验证码' },
  { value: 'phone_code', icon: '📱', label: '手机注册', desc: '手机号作为帐号，需短信验证码' },
  { value: 'both_code' as const, icon: '🔐', label: '邮箱 + 手机注册', desc: '双重验证，邮箱和手机均需验证码' },
] as const

// ---------- 复制调用代码 ----------
async function copyVueCode(key: string) {
  const code = `// 读取配置项: ${key}
import { getAllSettings } from '@/api/system'
const settings = await getAllSettings()
const ${toCamelCase(key)} = settings.find(s => s.key === '${key}')?.value ?? ''`
  await navigator.clipboard.writeText(code)
  ElMessage.success('Vue 调用代码已复制')
}

async function copyCSharpCode(key: string) {
  const code = `// 读取配置项: ${key}
// 注入 ISettingsRepository _settingsRepo;
var setting = await _settingsRepo.GetAsync("${key}", null);
var ${toCamelCase(key)} = setting?.Value ?? "";`
  await navigator.clipboard.writeText(code)
  ElMessage.success('C# 调用代码已复制')
}

function toCamelCase(key: string): string {
  const parts = key.replace(/[^a-zA-Z0-9]/g, '.').split('.').filter(Boolean)
  return parts.map((p, i) => i === 0 ? p.charAt(0).toLowerCase() + p.slice(1) : p.charAt(0).toUpperCase() + p.slice(1)).join('')
}

// 表单数据
const formData = reactive({
  // 站点配置
  siteName: '',
  baseUrl: '',
  logoUrl: '',
  favicon: '',
  primaryColor: '#3b82f6',
  secondaryColor: '#2563eb',
  maintenanceMode: false,

  timeZone: 'Asia/Shanghai',
  footerText: '',
  icpNo: '',
  policeIcpNo: '',

  // 登录页面配置
  loginSubtitle: '',
  welcomeText: '',
  loginBackground: '',
  animationEnabled: true,
  animationIntensity: 'medium' as 'light' | 'medium' | 'strong',

  // 注册与安全
  registrationEnabled: true,
  requireCaptcha: true,
  registrationMode: 'free' as 'disabled' | 'free' | 'email' | 'email_code' | 'phone' | 'phone_code' | 'both_code',
  loginCaptchaEnabled: true,
  loginMethods: ['password'] as string[],
  defaultDepartmentId: '',
  defaultRoleIds: '[]',
  blockedIPs: '[]',

  // 数据权限（默认值与后端统一：DepartmentAndChildren；启用开关默认关闭以保持兼容）
  dataScope: 'DepartmentAndChildren',
  dataPermissionEnabled: false,
  // 兼容旧字段（不再展示，仅在加载历史数据时使用）
  allowCrossLevel: true,

  // 邮件配置
  smtpHost: '',
  smtpPort: '587',
  smtpSsl: true,
  smtpUser: '',
  smtpPassword: '',
  smtpAuthType: 'Login',
  mailFrom: '',
  mailFromName: '',

  // 网络与上传
  uploadMaxMB: '20',
  uploadBasePath: '/uploads',
  imageCompressEnabled: false,
  imageCompressQuality: 75,
  imageCompressKeepOriginal: false,
})

// ---------- CORS 来源标签 ----------
const corsOriginTags = ref<string[]>([])
const corsInputVisible = ref(false)
const corsInputValue = ref('')
const corsInputRef = ref<InstanceType<typeof import('element-plus')['ElInput']>>()

function showCorsInput() {
  corsInputVisible.value = true
  nextTick(() => corsInputRef.value?.input?.focus())
}

function handleCorsInputConfirm() {
  const val = corsInputValue.value.trim()
  if (val && !corsOriginTags.value.includes(val)) {
    corsOriginTags.value.push(val)
  }
  corsInputVisible.value = false
  corsInputValue.value = ''
}

// ---------- 扩展名标签 ----------
const uploadExtTags = ref<string[]>([])
const extInputVisible = ref(false)
const extInputValue = ref('')
const extInputRef = ref<InstanceType<typeof import('element-plus')['ElInput']>>()

function showExtInput() {
  extInputVisible.value = true
  nextTick(() => extInputRef.value?.input?.focus())
}

function handleExtInputConfirm() {
  let val = extInputValue.value.trim()
  if (val && !val.startsWith('.')) val = '.' + val
  if (val && !uploadExtTags.value.includes(val)) {
    uploadExtTags.value.push(val)
  }
  extInputVisible.value = false
  extInputValue.value = ''
}

// ---------- 部门/角色选择器 ----------
const deptOptions = ref<{id: string, name: string}[]>([])
const roleOptions = ref<{id: string, name: string}[]>([])
const selectedDeptIds = ref<string[]>([])
const selectedRoleIds = ref<string[]>([])

/** 扁平化部门树 */
function flattenDeptTree(nodes: DepartmentTreeNode[], prefix = ''): {id: string, name: string}[] {
  const result: {id: string, name: string}[] = []
  for (const node of nodes) {
    const label = prefix ? `${prefix} / ${node.name}` : node.name
    result.push({ id: node.id, name: label })
    if (node.children?.length) result.push(...flattenDeptTree(node.children, label))
  }
  return result
}

/** 扁平化角色树 */
function flattenRoleTree(nodes: RoleTreeNode[], prefix = ''): {id: string, name: string}[] {
  const result: {id: string, name: string}[] = []
  for (const node of nodes) {
    const label = prefix ? `${prefix} / ${node.name}` : node.name
    result.push({ id: node.id, name: label })
    if (node.children?.length) result.push(...flattenRoleTree(node.children, label))
  }
  return result
}

/** 加载部门与角色选项 */
async function loadDeptAndRoleOptions() {
  try {
    const [deptTree, roleTree] = await Promise.all([getDepartmentsTree(), getRoleTree()])
    deptOptions.value = flattenDeptTree(deptTree)
    roleOptions.value = flattenRoleTree(roleTree)
  } catch (e) {
    console.warn('加载部门/角色选项失败', e)
  }
}

// ---------- 禁止IP标签 ----------
const blockedIPTags = ref<string[]>([])
const ipInputVisible = ref(false)
const ipInputValue = ref('')
const ipInputRef = ref<InstanceType<typeof import('element-plus')['ElInput']>>()

function showIpInput() {
  ipInputVisible.value = true
  nextTick(() => ipInputRef.value?.input?.focus())
}

function handleIpInputConfirm() {
  const val = ipInputValue.value.trim()
  if (val && !blockedIPTags.value.includes(val)) {
    blockedIPTags.value.push(val)
  }
  ipInputVisible.value = false
  ipInputValue.value = ''
}

function removeBlockedIP(index: number) {
  blockedIPTags.value.splice(index, 1)
}

/** 将逗号/JSON数组格式的字符串解析为标签数组 */
function parseTagString(raw: string): string[] {
  if (!raw || !raw.trim()) return []
  const trimmed = raw.trim()
  // 尝试 JSON 数组解析
  if (trimmed.startsWith('[')) {
    try {
      const arr = JSON.parse(trimmed)
      if (Array.isArray(arr)) return arr.map((s: any) => String(s).trim()).filter(Boolean)
    } catch {}
  }
  // 逗号分隔
  return trimmed.split(/[,;，；\s]+/).map(s => s.trim()).filter(Boolean)
}

// 加载配置数据
async function loadSettings() {
  loading.value = true
  try {
    // 预加载 OSS 资源配置，确保 resolveResourcePath 能正确拼接预览 URL
    await fetchResourceConfig().catch(() => {})
    const settings = await getAllSettings()
    const map = new Map<string, string>()
    settings.forEach(s => {
      if (s.key && s.value !== undefined) {
        map.set(s.key, s.value)
      }
    })

    // 站点配置
    formData.siteName = map.get('Site.Name') || ''
    formData.baseUrl = map.get('Site.BaseUrl') || ''
    formData.logoUrl = map.get('Site.Logo') || ''
    formData.favicon = map.get('Site.Branding.Favicon') || ''
    formData.primaryColor = map.get('Site.Theme.PrimaryColor') || '#3b82f6'
    formData.secondaryColor = map.get('Site.Theme.SecondaryColor') || '#2563eb'
    formData.maintenanceMode = (map.get('Site.Maintenance.Enabled') || 'false').toLowerCase() === 'true'

    formData.timeZone = map.get('Site.TimeZone') || 'Asia/Shanghai'
    formData.footerText = map.get('Site.Footer.Text') || ''
    formData.icpNo = map.get('Site.ICP') || ''
    formData.policeIcpNo = map.get('Site.PoliceICP') || ''

    // 登录页面配置
    formData.loginSubtitle = map.get('Site.Subtitle') || ''
    formData.welcomeText = map.get('Site.Login.WelcomeText') || ''
    formData.loginBackground = map.get('Site.Login.LeftPanelBackground') || ''
    formData.animationEnabled = (map.get('Site.Animation.Enabled') || 'true').toLowerCase() === 'true'
    formData.animationIntensity = (map.get('Site.Animation.Intensity') || 'medium') as 'light' | 'medium' | 'strong'

    // 注册与安全
    formData.registrationEnabled = (map.get('Registration.Enabled') || 'true').toLowerCase() === 'true'
    formData.requireCaptcha = (map.get('Registration.RequireCaptcha') || 'true').toLowerCase() === 'true'
    formData.registrationMode = (map.get('Registration.Mode') || '') as any
    // 向后兼容：如果没有 Mode 设置，从旧字段推导
    if (!formData.registrationMode) {
      if (!formData.registrationEnabled) {
        formData.registrationMode = 'disabled'
      } else if (formData.requireCaptcha) {
        formData.registrationMode = 'email_code'
      } else {
        formData.registrationMode = 'free'
      }
    }
    formData.loginCaptchaEnabled = (map.get('Registration.LoginCaptcha') || 'true').toLowerCase() === 'true'
    try {
      formData.loginMethods = JSON.parse(map.get('Registration.LoginMethods') || '["password"]')
    } catch { formData.loginMethods = ['password'] }
    formData.defaultDepartmentId = map.get('Registration.DefaultDepartmentId') || ''
    formData.defaultRoleIds = map.get('Registration.DefaultRoleIds') || '[]'
    formData.blockedIPs = map.get('Security.BlockedIPs') || '[]'

    // 解析部门/角色/IP到选择器数组
    selectedDeptIds.value = parseTagString(formData.defaultDepartmentId)
    selectedRoleIds.value = parseTagString(formData.defaultRoleIds)
    blockedIPTags.value = parseTagString(formData.blockedIPs)

    // 加载部门和角色选项（用于名称回显）
    await loadDeptAndRoleOptions()

    // 数据权限（兼容历史 enum 值：Self/Dept/DeptAndChildren → OwnOnly/DepartmentOnly/DepartmentAndChildren）
    const rawScope = map.get('DataPermission.DefaultScope') || 'DepartmentAndChildren'
    const scopeAlias: Record<string, string> = {
      Self: 'OwnOnly',
      Dept: 'DepartmentOnly',
      DeptAndChildren: 'DepartmentAndChildren',
    }
    const mappedScope = scopeAlias[rawScope] || rawScope
    // 系统配置「默认数据范围」只支持 All / OwnOnly / DepartmentOnly / DepartmentAndChildren
    // 历史值若是 SpecifiedDepartments / Custom 等不可用作全局默认，统一回退到 DepartmentAndChildren
    const validScopes = new Set(['All', 'OwnOnly', 'DepartmentOnly', 'DepartmentAndChildren'])
    formData.dataScope = validScopes.has(mappedScope) ? mappedScope : 'DepartmentAndChildren'
    // 启用开关：优先读新 Key DataPermission.Enabled；未配置时默认 false（保险起见，避免误启用）
    formData.dataPermissionEnabled = (map.get('DataPermission.Enabled') || 'false').toLowerCase() === 'true'
    // 旧 Key AllowCrossLevel 仅用于回显（已废弃，新版不再展示）
    formData.allowCrossLevel = (map.get('DataPermission.AllowCrossLevel') || 'true').toLowerCase() === 'true'

    // 邮件配置
    formData.smtpHost = map.get('Mail.Smtp.Host') || ''
    formData.smtpPort = map.get('Mail.Smtp.Port') || '587'
    formData.smtpSsl = (map.get('Mail.Ssl.Enable') || 'true').toLowerCase() === 'true'
    formData.smtpUser = map.get('Mail.Smtp.UserName') || ''
    formData.smtpPassword = map.get('Mail.Smtp.Password') || ''
    formData.smtpAuthType = map.get('Mail.Smtp.AuthType') || 'Login'
    formData.mailFrom = map.get('Mail.From.Address') || ''
    formData.mailFromName = map.get('Mail.From.DisplayName') || ''

    // 网络与上传
    corsOriginTags.value = parseTagString(map.get('Site.Cors.AllowedOrigins') || '[]')
    formData.uploadMaxMB = map.get('Upload.MaxSizeMB') || '20'
    uploadExtTags.value = parseTagString(map.get('Upload.AllowedExtensions') || '.jpg,.png,.pdf,.xlsx,.mp3,.mp4,.zip,.rar,.doc,.docx')
    formData.uploadBasePath = map.get('Upload.BasePath') || '/uploads'
    formData.imageCompressEnabled = (map.get('Upload.ImageCompress.Enabled') || 'false').toLowerCase() === 'true'
    formData.imageCompressQuality = parseInt(map.get('Upload.ImageCompress.Quality') || '75', 10)
    formData.imageCompressKeepOriginal = (map.get('Upload.ImageCompress.KeepOriginal') || 'false').toLowerCase() === 'true'

    // 加载动态配置项
    await loadDynamicConfigs(settings)

    // 加载多语言配置
    loadLangConfig(map)

  } catch (error: any) {
    ElMessage.error(error?.message || '加载配置失败')
  } finally {
    loading.value = false
  }
}

// 加载动态配置项（从字典 sysconfig 分类 + 数据库 settings）
async function loadDynamicConfigs(allSettings: SettingDto[]) {
  try {
    // 已有专用标签页的内置分组，不在动态列表中重复显示
    const excludeGroups = new Set(['Language', 'Site', 'Registration', 'Security', 'DataPermission', 'Mail', 'Upload'])

    // 1. 从字典 sysconfig 分类获取分组名称
    const dictGroupNames = new Set<string>()
    try {
      const categoriesResp = await getDictionaryCategories(1, 200, 'sysconfig')
      const sysconfigCategory = categoriesResp.items.find(
        cat => cat.code.toLowerCase() === 'sysconfig'
      )
      if (sysconfigCategory) {
        const itemsResp = await getDictionaryItems(sysconfigCategory.id, 1, 2000)
        itemsResp.items.forEach(item => {
          // 字典条目本身就代表分组名（itemValue 或 itemKey）
          const name = (item.itemValue || item.itemKey || '').trim()
          if (name && !excludeGroups.has(name)) {
            dictGroupNames.add(name)
          }
        })
      }
    } catch {
      // 字典查询失败时静默降级
    }

    // 2. 从设置的 class 字段补充分组
    allSettings.forEach(s => {
      const cls = s.class?.trim()
      if (cls && !excludeGroups.has(cls)) {
        dictGroupNames.add(cls)
      }
    })

    // 3. 同时把 groupList（分组管理区已加载的）也合并进来
    groupList.value.forEach(g => {
      if (g.name && !excludeGroups.has(g.name)) {
        dictGroupNames.add(g.name)
      }
    })

    // 排序
    const allGroupsSorted = Array.from(dictGroupNames).sort()

    // 4. 为每个分组收集配置项（来自 allSettings 中 class 匹配的记录）
    const groups: DynamicConfigGroup[] = []
    for (const groupName of allGroupsSorted) {
      const groupItems: DynamicConfigItem[] = []

      allSettings.forEach(setting => {
        if (setting.class?.trim() !== groupName) return

        const key = setting.key
        const dotIndex = key.indexOf('.')
        const keyOnly = dotIndex > 0 ? key.substring(dotIndex + 1) : key
        const type = setting.type || 'String'
        const value = setting.value || ''

        groupItems.push({
          key,
          value,
          boolValue: type === 'Bool' ? value.toLowerCase() === 'true' : undefined,
          numberValue: ['Integer', 'Number', 'Decimal'].includes(type) ? parseFloat(value) || 0 : undefined,
          type,
          label: setting.description || keyOnly,
          description: setting.description,
          class: groupName
        })
      })

      // 即使没有配置项也创建分组（空分组显示提示）
      groups.push({
        name: groupName,
        items: groupItems
      })
    }

    dynamicGroups.value = groups

  } catch (error: any) {
    // 降级：只从设置中加载
    loadDynamicConfigsFromSettings(allSettings)
  }
}

// 从设置中加载动态配置（降级方案）
function loadDynamicConfigsFromSettings(allSettings: SettingDto[]) {
  const excludeGroups = new Set(['Language', 'Site', 'Registration', 'Security', 'DataPermission', 'Mail', 'Upload'])
  const groupMap = new Map<string, DynamicConfigItem[]>()

  // 先把 groupList 中的分组名加入（保证空分组也显示）
  groupList.value.forEach(g => {
    if (g.name && !excludeGroups.has(g.name) && !groupMap.has(g.name)) {
      groupMap.set(g.name, [])
    }
  })

  allSettings.forEach(setting => {
    if (!setting.class || !setting.class.trim()) return

    const className = setting.class.trim()
    if (excludeGroups.has(className)) return
    if (!groupMap.has(className)) {
      groupMap.set(className, [])
    }

    const key = setting.key
    const dotIndex = key.indexOf('.')
    const keyOnly = dotIndex > 0 ? key.substring(dotIndex + 1) : key
    const type = setting.type || 'String'
    const value = setting.value || ''

    groupMap.get(className)!.push({
      key,
      value,
      boolValue: type === 'Bool' ? value.toLowerCase() === 'true' : undefined,
      numberValue: ['Integer', 'Number', 'Decimal'].includes(type) ? parseFloat(value) || 0 : undefined,
      type,
      label: setting.description || keyOnly,
      description: setting.description,
      class: className
    })
  })

  const groups: DynamicConfigGroup[] = []
  Array.from(groupMap.keys()).sort().forEach(groupName => {
    groups.push({
      name: groupName,
      items: groupMap.get(groupName)!
    })
  })

  dynamicGroups.value = groups
}

// 保存配置
async function handleSave() {
  saving.value = true
  try {
    // 静态配置项的 Key 集合，用于去重
    const staticKeys = new Set([
      'Site.Name', 'Site.BaseUrl', 'Site.Logo', 'Site.Branding.Favicon',
      'Site.Theme.PrimaryColor', 'Site.Theme.SecondaryColor', 'Site.Maintenance.Enabled',
      'Site.DefaultLanguage', 'Site.TimeZone', 'Site.Footer.Text',
      'Site.Subtitle', 'Site.Login.WelcomeText', 'Site.Login.LeftPanelBackground',
      'Site.Animation.Enabled', 'Site.Animation.Intensity',
      'Registration.Mode', 'Registration.Enabled', 'Registration.RequireCaptcha', 'Registration.LoginCaptcha', 'Registration.LoginMethods',
      'Registration.DefaultDepartmentId', 'Registration.DefaultRoleIds',
      'Security.BlockedIPs',
      'DataPermission.DefaultScope', 'DataPermission.Enabled', 'DataPermission.AllowCrossLevel',
      'Mail.Smtp.Host', 'Mail.Smtp.Port', 'Mail.Ssl.Enable', 'Mail.Smtp.UserName', 'Mail.Smtp.Password', 'Mail.Smtp.AuthType',
      'Mail.From.Address', 'Mail.From.DisplayName',
      'Site.Cors.AllowedOrigins', 'Upload.MaxSizeMB', 'Upload.AllowedExtensions', 'Upload.BasePath'
    ])

    // 同步选择器数据到 formData
    formData.defaultDepartmentId = selectedDeptIds.value.join(',')
    formData.defaultRoleIds = JSON.stringify(selectedRoleIds.value)
    formData.blockedIPs = JSON.stringify(blockedIPTags.value)

    const settings: SettingDto[] = [
      // 站点配置
      { key: 'Site.Name', value: formData.siteName, type: 'String', description: '站点名称', class: 'Site' },
      { key: 'Site.BaseUrl', value: formData.baseUrl, type: 'String', description: '站点基础URL', class: 'Site' },
      { key: 'Site.Logo', value: formData.logoUrl, type: 'String', description: '站点LOGO URL', class: 'Site' },
      { key: 'Site.Branding.Favicon', value: formData.favicon, type: 'String' },
      { key: 'Site.Theme.PrimaryColor', value: formData.primaryColor, type: 'String' },
      { key: 'Site.Theme.SecondaryColor', value: formData.secondaryColor, type: 'String' },
      { key: 'Site.Maintenance.Enabled', value: formData.maintenanceMode ? 'true' : 'false', type: 'Bool' },
      { key: 'Site.TimeZone', value: formData.timeZone, type: 'String', description: '时区', class: 'Site' },
      { key: 'Site.Footer.Text', value: formData.footerText, type: 'String' },
      { key: 'Site.ICP', value: formData.icpNo, type: 'String', description: 'ICP备案号', class: 'Site' },
      { key: 'Site.PoliceICP', value: formData.policeIcpNo, type: 'String', description: '公安备案号', class: 'Site' },

      // 登录页面配置
      { key: 'Site.Subtitle', value: formData.loginSubtitle, type: 'String' },
      { key: 'Site.Login.WelcomeText', value: formData.welcomeText, type: 'String' },
      { key: 'Site.Login.LeftPanelBackground', value: formData.loginBackground, type: 'String' },
      { key: 'Site.Animation.Enabled', value: formData.animationEnabled ? 'true' : 'false', type: 'Bool' },
      { key: 'Site.Animation.Intensity', value: formData.animationIntensity, type: 'String' },

      // 注册与安全
      { key: 'Registration.Mode', value: formData.registrationMode, type: 'String', description: '注册模式' },
      { key: 'Registration.Enabled', value: formData.registrationMode !== 'disabled' ? 'true' : 'false', type: 'Bool' },
      { key: 'Registration.RequireCaptcha', value: ['email_code', 'phone_code', 'both_code'].includes(formData.registrationMode) ? 'true' : 'false', type: 'Bool' },
      { key: 'Registration.LoginCaptcha', value: formData.loginCaptchaEnabled ? 'true' : 'false', type: 'Bool', description: '登录验证码' },
      { key: 'Registration.LoginMethods', value: JSON.stringify(formData.loginMethods), type: 'Json', description: '允许的登录方式' },
      { key: 'Registration.DefaultDepartmentId', value: formData.defaultDepartmentId, type: 'String' },
      { key: 'Registration.DefaultRoleIds', value: formData.defaultRoleIds, type: 'Json' },
      { key: 'Security.BlockedIPs', value: formData.blockedIPs, type: 'Json' },

      // 数据权限：写入新 Key（DataPermission.Enabled / DataPermission.DefaultScope）
      // 旧 Key DataPermission.AllowCrossLevel 不再写入，原 DB 中的旧值留着不影响新逻辑
      { key: 'DataPermission.Enabled', value: formData.dataPermissionEnabled ? 'true' : 'false', type: 'Bool', description: '数据权限过滤总开关' },
      { key: 'DataPermission.DefaultScope', value: formData.dataScope, type: 'String', description: '默认数据范围（角色未指定时使用）' },

      // 邮件配置
      { key: 'Mail.Smtp.Host', value: formData.smtpHost, type: 'String' },
      { key: 'Mail.Smtp.Port', value: formData.smtpPort, type: 'Number' },
      { key: 'Mail.Ssl.Enable', value: formData.smtpSsl ? 'true' : 'false', type: 'Bool' },
      { key: 'Mail.Smtp.UserName', value: formData.smtpUser, type: 'String' },
      { key: 'Mail.Smtp.Password', value: formData.smtpPassword, type: 'String' },
      { key: 'Mail.Smtp.AuthType', value: formData.smtpAuthType, type: 'String' },
      { key: 'Mail.From.Address', value: formData.mailFrom, type: 'String' },
      { key: 'Mail.From.DisplayName', value: formData.mailFromName, type: 'String' },

      // 网络与上传
      { key: 'Site.Cors.AllowedOrigins', value: corsOriginTags.value.join(','), type: 'String' },
      { key: 'Upload.MaxSizeMB', value: formData.uploadMaxMB, type: 'Number' },
      { key: 'Upload.AllowedExtensions', value: uploadExtTags.value.join(','), type: 'String', description: '允许上传的文件扩展名' },
      { key: 'Upload.BasePath', value: formData.uploadBasePath, type: 'String', description: '默认上传目录' },
      { key: 'Upload.ImageCompress.Enabled', value: formData.imageCompressEnabled ? 'true' : 'false', type: 'Bool', description: '上传图片时是否启用后端压缩' },
      { key: 'Upload.ImageCompress.Quality', value: String(formData.imageCompressQuality), type: 'Number', description: '图片压缩质量（10-100）' },
      { key: 'Upload.ImageCompress.KeepOriginal', value: formData.imageCompressKeepOriginal ? 'true' : 'false', type: 'Bool', description: '压缩后是否保留原图' },
    ]

    // 添加动态配置项（排除已在静态列表中的 Key，避免重复覆盖）
    dynamicGroups.value.forEach(group => {
      group.items.forEach(item => {
        // 跳过已在静态列表中定义的 Key
        if (staticKeys.has(item.key)) return
        
        settings.push({
          key: item.key,
          value: item.value,
          type: item.type,
          description: item.description,
          class: item.class
        })
      })
    })

    // 添加多语言配置项
    settings.push(...getLangSettingsForSave())

    await saveBatchSettings(settings)
    ElMessage.success('保存成功')

    // 如果修改了站点名称或 LOGO，可以触发刷新
    // 这里可以添加事件总线或者 store 更新逻辑

  } catch (error: any) {
    ElMessage.error(error?.message || '保存失败')
  } finally {
    saving.value = false
  }
}

// 添加新配置
async function handleAddConfig() {
  // 验证表单
  if (!newConfigForm.class || !newConfigForm.class.trim()) {
    ElMessage.warning('请选择或输入分组名称')
    return
  }
  if (!newConfigForm.key || !newConfigForm.key.trim()) {
    ElMessage.warning('请输入配置键')
    return
  }

  adding.value = true
  try {
    const className = newConfigForm.class.trim()
    const key = newConfigForm.key.trim()

    // 如果分组不存在，自动创建字典条目
    if (_sysconfigCategoryId && !groupList.value.some(g => g.name === className)) {
      const id = await createDictItemApi({ categoryId: _sysconfigCategoryId, itemKey: className, itemValue: className, enabled: true })
      groupList.value.push({ id: String(id), name: className, editing: false, editName: '' })
    }

    // 如果 key 不包含点，自动添加分组前缀
    const fullKey = key.includes('.') ? key : `${className}.${key}`

    const setting: SettingDto = {
      key: fullKey,
      value: newConfigForm.value,
      type: newConfigForm.type,
      description: newConfigForm.description,
      class: className
    }

    await upsertSetting(setting)
    ElMessage.success('添加成功')

    // 重置表单
    resetNewConfigForm()

    // 重新加载配置
    await loadSettings()

    // 切换到对应的动态配置 TAB
    activeTab.value = `dynamic-${className}`

  } catch (error: any) {
    ElMessage.error(error?.message || '添加失败')
  } finally {
    adding.value = false
  }
}

// 重置新配置表单
function resetNewConfigForm() {
  newConfigForm.class = ''
  newConfigForm.key = ''
  newConfigForm.value = ''
  newConfigForm.type = 'String'
  newConfigForm.description = ''
  newConfigFormNumberValue.value = 0
  newConfigFormBoolValue.value = false
}

// 刷新配置
function handleRefresh() {
  loadSettings()
}

// 发送测试邮件
async function handleSendTestEmail() {
  if (!testEmailAddress.value) {
    ElMessage.warning('请输入收件人邮箱地址')
    return
  }
  sendingTestEmail.value = true
  try {
    await sendTestEmail(testEmailAddress.value)
    ElMessage.success('测试邮件发送成功，请检查收件箱')
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || error?.message || '测试邮件发送失败')
  } finally {
    sendingTestEmail.value = false
  }
}

// ====================================================================
// 多语言配置管理
// ====================================================================
import { toUrlCode, type LangItem, setLangConfig } from '../../../utils/lang'

// 所有可选的语言候选列表
const allLangOptions: LangItem[] = [
  { code: 'zh-CN', urlCode: 'zh', label: '中文', flag: '🇨🇳', required: true },
  { code: 'en', urlCode: 'en', label: 'English', flag: '🇺🇸', required: false },
  { code: 'ja', urlCode: 'ja', label: '日本語', flag: '🇯🇵', required: false },
  { code: 'ko', urlCode: 'ko', label: '한국어', flag: '🇰🇷', required: false },
  { code: 'fr', urlCode: 'fr', label: 'Français', flag: '🇫🇷', required: false },
  { code: 'de', urlCode: 'de', label: 'Deutsch', flag: '🇩🇪', required: false },
  { code: 'es', urlCode: 'es', label: 'Español', flag: '🇪🇸', required: false },
  { code: 'pt', urlCode: 'pt', label: 'Português', flag: '🇧🇷', required: false },
  { code: 'ru', urlCode: 'ru', label: 'Русский', flag: '🇷🇺', required: false },
  { code: 'ar', urlCode: 'ar', label: 'العربية', flag: '🇸🇦', required: false },
]

// 多语言配置响应式对象
const langConfig = reactive({
  enabled: true,
  defaultLang: 'zh-CN',
  langs: [...allLangOptions.slice(0, 2)] as LangItem[], // 默认中英文
})

// 新增语言选择
const newLangCode = ref('')

// 插件覆盖列表
const pluginOverrideList = ref<{ id: string; name: string; enabled: boolean }[]>([])

// 可添加的语言（排除已添加的）
const availableNewLangs = computed(() =>
  allLangOptions.filter(l => !langConfig.langs.some(el => el.code === l.code))
)

// 添加语言
function addLang() {
  const found = allLangOptions.find(l => l.code === newLangCode.value)
  if (found && !langConfig.langs.some(l => l.code === found.code)) {
    langConfig.langs.push({ ...found })
    newLangCode.value = ''
    ElMessage.success(`已添加 ${found.label}`)
  }
}

// 删除语言
function removeLang(idx: number) {
  const lang = langConfig.langs[idx]
  if (langConfig.langs.length <= 1) {
    ElMessage.warning('至少需要保留一种语言')
    return
  }
  // 如果删除的是默认语言，自动切到第一个
  if (lang.code === langConfig.defaultLang) {
    const remaining = langConfig.langs.filter((_, i) => i !== idx)
    langConfig.defaultLang = remaining[0]?.code || 'zh-CN'
  }
  langConfig.langs.splice(idx, 1)
}

// 移动语言顺序
function moveLang(idx: number, direction: number) {
  const newIdx = idx + direction
  if (newIdx < 0 || newIdx >= langConfig.langs.length) return
  const temp = langConfig.langs[idx]
  langConfig.langs[idx] = langConfig.langs[newIdx]
  langConfig.langs[newIdx] = temp
}

// 加载多语言配置（从系统设置读取）
function loadLangConfig(map: Map<string, string>) {
  const enabledStr = map.get('Language.MultiLang.Enabled')
  if (enabledStr !== undefined) {
    langConfig.enabled = enabledStr.toLowerCase() === 'true'
  }

  const defaultLangStr = map.get('Language.MultiLang.Default')
  if (defaultLangStr) {
    langConfig.defaultLang = defaultLangStr
  }

  const langsJson = map.get('Language.MultiLang.Languages')
  if (langsJson) {
    try {
      const parsed = JSON.parse(langsJson) as LangItem[]
      if (Array.isArray(parsed) && parsed.length > 0) {
        langConfig.langs = parsed.map(l => ({
          ...l,
          urlCode: l.urlCode || toUrlCode(l.code),
        }))
      }
    } catch {}
  }

  const overridesJson = map.get('Language.MultiLang.PluginOverrides')
  if (overridesJson) {
    try {
      const parsed = JSON.parse(overridesJson) as Record<string, boolean>
      pluginOverrideList.value = pluginOverrideList.value.map(p => ({
        ...p,
        enabled: parsed[p.id] !== false, // 默认启用
      }))
    } catch {}
  }

  // 同步到全局 lang.ts
  setLangConfig({
    enabled: langConfig.enabled,
    langs: langConfig.langs,
    defaultLang: langConfig.defaultLang,
  })
}

// 获取多语言保存的配置项
function getLangSettingsForSave(): SettingDto[] {
  return [
    {
      key: 'Language.MultiLang.Enabled',
      value: langConfig.enabled ? 'true' : 'false',
      type: 'Bool',
      description: '是否启用多语言',
      class: 'Language'
    },
    {
      key: 'Language.MultiLang.Default',
      value: langConfig.defaultLang,
      type: 'String',
      description: '默认语言代码',
      class: 'Language'
    },
    {
      key: 'Language.MultiLang.Languages',
      value: JSON.stringify(langConfig.langs),
      type: 'Json',
      description: '可用语言列表',
      class: 'Language'
    },
    {
      key: 'Language.MultiLang.PluginOverrides',
      value: JSON.stringify(
        Object.fromEntries(pluginOverrideList.value.map(p => [p.id, p.enabled]))
      ),
      type: 'Json',
      description: '插件多语言覆盖配置',
      class: 'Language'
    },
  ]
}

// 组件挂载时加载配置
onMounted(() => {
  loadSettings()
  loadGroupList()
})
</script>

<style scoped>
.system-config-page {
  padding: 24px;
  max-width: 1400px;
  margin: 0 auto;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.page-title {
  font-size: 24px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0;
}

.page-actions {
  display: flex;
  gap: 12px;
}

.config-tabs {
  background: transparent;
}

.config-card {
  margin-bottom: 24px;
}

.config-card :deep(.el-card__body) {
  padding: 24px;
}

.form-tip {
  margin-left: 12px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.tag-input-group {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 4px;
  min-height: 32px;
  width: 100%;
}

.tag-input-container {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0;
  min-height: 32px;
}

.logo-upload,
.favicon-upload,
.background-upload {
  display: flex;
  align-items: center;
  width: 100%;
}

.logo-upload .el-input,
.favicon-upload .el-input,
.background-upload .el-input {
  flex: 1;
}

.logo-preview,
.background-preview {
  margin-top: 12px;
  padding: 12px;
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  background: var(--el-fill-color-lighter);
}

.logo-preview img {
  max-width: 200px;
  max-height: 100px;
  object-fit: contain;
}

.background-preview img {
  max-width: 400px;
  max-height: 200px;
  object-fit: cover;
  border-radius: 4px;
}

/* 响应式设计 */
@media (max-width: 768px) {
  .system-config-page {
    padding: 16px;
  }

  .page-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 16px;
  }

  .page-actions {
    width: 100%;
  }

  .page-actions .el-button {
    flex: 1;
  }

  .config-card :deep(.el-form) {
    --el-form-label-width: 140px;
  }

  .logo-upload,
  .favicon-upload,
  .background-upload {
    flex-direction: column;
    align-items: stretch;
  }

  .logo-upload .el-button,
  .favicon-upload .el-button,
  .background-upload .el-button {
    margin-left: 0 !important;
    margin-top: 8px;
    width: 100%;
  }

  .logo-preview img,
  .background-preview img {
    max-width: 100%;
  }
}

/* 多语言配置管理样式 */
.lang-list {
  width: 100%;
}
.lang-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  margin-bottom: 6px;
  background: var(--el-fill-color-blank);
}
.lang-flag { font-size: 18px; }
.lang-label { font-weight: 500; min-width: 80px; }
.lang-code { color: var(--el-text-color-secondary); font-size: 12px; flex: 1; }
.lang-actions { display: flex; gap: 2px; margin-left: auto; }
.lang-add-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 8px;
}
.plugin-override-list { width: 100%; }
.plugin-override-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  margin-bottom: 6px;
}
.plugin-override-empty {
  color: var(--el-text-color-secondary);
  font-size: 13px;
  padding: 12px;
  text-align: center;
}

.json-editor-textarea :deep(.el-textarea__inner) {
  font-family: 'Monaco', 'Menlo', 'Ubuntu Mono', 'Consolas', monospace;
  font-size: 13px;
  line-height: 1.5;
  tab-size: 2;
}

/* ---------- 80/20 布局：值区域 + 复制按钮 ---------- */
.config-value-row {
  display: flex;
  align-items: flex-start;
  width: 100%;
  gap: 8px;
}
.config-value-control {
  flex: 0 0 80%;
  max-width: 80%;
  min-width: 0;
}
.config-value-actions {
  flex: 0 0 auto;
  display: flex;
  gap: 4px;
  padding-top: 4px;
  flex-shrink: 0;
}

/* 复制按钮样式 */
.copy-btn {
  font-size: 11px !important;
  padding: 4px 8px !important;
  min-height: 24px !important;
  border-radius: 4px !important;
  font-weight: 600;
  letter-spacing: 0.5px;
}
.copy-btn-vue {
  color: #42b883 !important;
  border-color: #42b883 !important;
  background: rgba(66,184,131,0.08) !important;
}
.copy-btn-vue:hover {
  background: rgba(66,184,131,0.18) !important;
}
.copy-btn-csharp {
  color: #68217a !important;
  border-color: #68217a !important;
  background: rgba(104,33,122,0.08) !important;
}
.copy-btn-csharp:hover {
  background: rgba(104,33,122,0.18) !important;
}

/* ---------- 颜色选择内联 ---------- */
.color-inline {
  display: flex;
  align-items: center;
}

/* ---------- 文件选择器内联 ---------- */
.file-picker-inline {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  width: 100%;
}
.file-picker-thumb {
  width: 60px;
  height: 60px;
  border-radius: 4px;
  border: 1px solid var(--el-border-color-lighter);
  flex-shrink: 0;
}
.file-picker-thumbs {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}
.file-picker-btns {
  display: flex;
  gap: 4px;
  align-items: center;
}
.file-picker-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 2px;
}

/* ---------- 注册模式卡片 ---------- */
.reg-mode-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 12px;
}
.reg-mode-card {
  position: relative;
  border: 2px solid var(--el-border-color-lighter);
  border-radius: 10px;
  padding: 16px 14px 14px;
  cursor: pointer;
  transition: all 0.2s;
  text-align: center;
  background: var(--el-bg-color);
}
.reg-mode-card:hover {
  border-color: var(--el-color-primary-light-3);
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.06);
}
.reg-mode-card.active {
  border-color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}
.reg-mode-card.active.disabled {
  border-color: var(--el-color-warning);
  background: var(--el-color-warning-light-9);
}
.reg-mode-icon {
  font-size: 28px;
  line-height: 1;
  margin-bottom: 8px;
}
.reg-mode-label {
  font-size: 14px;
  font-weight: 600;
  margin-bottom: 4px;
  color: var(--el-text-color-primary);
}
.reg-mode-desc {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  line-height: 1.4;
}
.reg-mode-check {
  position: absolute;
  top: 6px;
  right: 8px;
  width: 20px;
  height: 20px;
  border-radius: 50%;
  background: var(--el-color-primary);
  color: #fff;
  font-size: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
}
.reg-mode-card.active.disabled .reg-mode-check {
  background: var(--el-color-warning);
}

/* ---------- 分组管理 ---------- */
.group-mgmt-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 12px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  margin-bottom: 6px;
}
.group-mgmt-name {
  flex: 1;
  font-size: 14px;
  font-weight: 500;
}
.group-mgmt-actions {
  display: flex;
  gap: 2px;
  flex-shrink: 0;
}
.group-mgmt-add {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 10px;
}

/* 暗黑主题适配 - 已移至 web/src/styles/admin/themes/dark/pages.css */
/* 使用通用的暗黑主题样式类，无需在此重复定义 */
</style>

