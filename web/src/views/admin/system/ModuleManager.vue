<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="module-manager-page">
    <el-card shadow="never" class="main-card">
      <!-- 页面标题 -->
      <div class="page-header">
        <div class="page-title">
          <h2>模块管理</h2>
          <p>管理系统后端模块，安装、启用、禁用或卸载功能模块</p>
        </div>
        <div class="page-actions">
          <el-button @click="refreshModules" :loading="loading">
            <i class="bi bi-arrow-clockwise" style="margin-right: 4px;"></i>刷新
          </el-button>
          <el-button v-if="envInfo?.isDevelopment" type="success" @click="showPackageDialog = true">
            <i class="bi bi-box-seam" style="margin-right: 4px;"></i>打包模块
          </el-button>
          <el-button v-if="envInfo?.isDevelopment" type="primary" @click="showInstallDialog = true">
            <i class="bi bi-plus-lg" style="margin-right: 4px;"></i>安装模块
          </el-button>
        </div>
      </div>

      <el-tabs v-model="activeMainTab">
        <el-tab-pane label="本地插件" name="local">
      <!-- 环境信息 -->
      <div v-if="envInfo" class="env-info" :class="{ 'dev-mode': envInfo.isDevelopment, 'prod-mode': !envInfo.isDevelopment }">
        <i class="bi bi-info-circle"></i>
        <span v-if="envInfo.isDevelopment">{{ envInfo.description }}</span>
        <span v-else>生产环境：模块安装、卸载功能不可用，请在开发环境中操作后重新部署</span>
      </div>

      <!-- 模块统计 -->
      <div class="stats-cards">
        <div class="stat-card">
          <div class="stat-icon total"><i class="bi bi-box"></i></div>
          <div class="stat-content"><h3>{{ moduleStats.total }}</h3><p>总模块数</p></div>
        </div>
        <div class="stat-card">
          <div class="stat-icon enabled"><i class="bi bi-check-circle"></i></div>
          <div class="stat-content"><h3>{{ moduleStats.enabled }}</h3><p>已启用</p></div>
        </div>
        <!-- 非正常：runtimeLoaded / serverDllLoaded / (有菜单时) menuRegistered 任一为否 → 红灯。
             与列表卡片左侧的红/绿灯口径一致，由 isModuleHealthy() 统一判定。 -->
        <div class="stat-card" :title="moduleStats.unhealthy > 0 ? '存在运行异常的插件，请打开对应模块的「状态」对话框查看详情' : '所有插件运行正常'">
          <div class="stat-icon unhealthy"><i class="bi bi-exclamation-triangle"></i></div>
          <div class="stat-content"><h3>{{ moduleStats.unhealthy }}</h3><p>非正常</p></div>
        </div>
        <div class="stat-card">
          <div class="stat-icon disabled"><i class="bi bi-x-circle"></i></div>
          <div class="stat-content"><h3>{{ moduleStats.disabled }}</h3><p>已禁用</p></div>
        </div>
        <div class="stat-card">
          <div class="stat-icon dev"><i class="bi bi-code-slash"></i></div>
          <div class="stat-content"><h3>{{ moduleStats.devMode }}</h3><p>开发模式</p></div>
        </div>
      </div>

      <!-- 工具栏 -->
      <div class="toolbar">
        <el-input v-model="searchQuery" placeholder="搜索模块..." clearable style="width: 280px">
          <template #prefix><i class="bi bi-search"></i></template>
        </el-input>
        <el-select v-model="filterStatus" placeholder="状态筛选" style="width: 120px">
          <el-option label="全部" value="" />
          <el-option label="已启用" value="enabled" />
          <el-option label="已禁用" value="disabled" />
        </el-select>
      </div>

      <!-- 模块列表 -->
      <div class="module-list" v-loading="loading">
        <el-empty v-if="filteredModules.length === 0 && !loading" description="暂无模块" />
        
        <div v-for="mod in paginatedModules" :key="mod.id" class="module-card">
          <div class="module-info">
            <div class="module-header">
              <h3 class="module-name">{{ mod.name || mod.id }}</h3>
              <span class="module-version">v{{ mod.version }}</span>
              <el-tag :type="mod.enabled ? 'success' : 'info'" size="small">{{ mod.enabled ? '已启用' : '已禁用' }}</el-tag>
              <el-tag v-if="mod.isDevMode" type="warning" size="small">开发模式</el-tag>
              <el-tag v-if="mod.hasClient" type="primary" size="small">含客户端</el-tag>
              <el-tag v-if="grayscalePolicies[mod.id] && grayscalePolicies[mod.id].channel !== 'stable'" type="warning" size="small" effect="dark">
                <i class="bi bi-sliders" style="margin-right:2px;"></i>灰度·{{ grayscalePolicies[mod.id].channel }}
              </el-tag>
            </div>
            <p class="module-description">{{ mod.title || '暂无描述' }}</p>
            <div class="module-meta">
              <span v-if="mod.publisher" class="meta-item"><i class="bi bi-person"></i> {{ mod.publisher }}</span>
              <span v-if="mod.author" class="meta-item"><i class="bi bi-pencil"></i> {{ mod.author }}</span>
              <span v-if="mod.installedAtUtc" class="meta-item"><i class="bi bi-clock"></i> {{ formatDate(mod.installedAtUtc) }}</span>
            </div>
          </div>
          <div class="module-actions">
            <!-- 提出来的常用操作，使用 link 类型使界面更加扁平化、简洁 -->
            <el-button v-if="mod.enabled && mod.testRoute" @click="handleModuleAction('test', mod)" type="success" link>
              <i class="bi bi-play-circle" style="margin-right: 4px;"></i> 测试
            </el-button>
            <el-button @click="handleModuleAction('config', mod)" type="primary" link>
              <i class="bi bi-gear" style="margin-right: 4px;"></i> 配置
            </el-button>
            <!-- 健康指示灯：runtimeLoaded / serverDllLoaded / (hasMenus ? menuRegistered : true) 全部 true 时显示绿灯，否则红灯。
                 状态字段缺失时（旧版本后端）按"未知"显示绿灯，避免误报。
                 紧贴「状态」按钮放置，hover 提示具体异常项，点击「状态」可查看详情。 -->
            <span
              class="module-status-light"
              :class="{ 'is-red': !isModuleHealthy(mod) }"
              :title="moduleHealthTip(mod)"
            ></span>
            <el-button @click="handleModuleAction('status', mod)" type="info" link>
              <i class="bi bi-info-circle" style="margin-right: 4px;"></i> 状态
            </el-button>
            <!-- 开发模式下「重载」实际会重启整个 API 进程，按钮文案需要让用户明确感知；
                 生产模式仍走 ALC 热重载（不改进程）。 -->
            <el-button
              @click="handleModuleAction('hot-reload', mod)"
              type="warning"
              link
              :loading="actionLoading[mod.id]"
              :title="envInfo?.isDevelopment
                ? '点击后将重启整个 API 进程并重新扫描加载所有插件'
                : '热重载当前模块（不重启 API 进程）'"
            >
              <i class="bi bi-arrow-repeat" style="margin-right: 4px;"></i>
              {{ envInfo?.isDevelopment ? '重启服务并重载' : '重载' }}
            </el-button>

            <!-- 启停状态开关或明显区分 -->
            <el-divider direction="vertical" style="margin: 0 4px;" />
            
            <el-button v-if="mod.enabled" @click="handleDisable(mod)" :loading="actionLoading[mod.id]" type="danger" link>
              禁用
            </el-button>
            <el-button v-else @click="handleEnable(mod)" :loading="actionLoading[mod.id]" type="success" link>
              启用
            </el-button>
            
            <!-- 危险/运维等不常用操作放进更多 -->
            <el-dropdown @command="(cmd: string) => handleModuleAction(cmd, mod)" trigger="click">
              <el-button type="info" link style="margin-left: 12px;">
                <i class="bi bi-three-dots-vertical"></i>
              </el-button>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item v-if="envInfo?.isDevelopment" command="package"><i class="bi bi-box-seam"></i> 打包</el-dropdown-item>
                  <el-dropdown-item command="install-npm-deps"><i class="bi bi-box-arrow-down"></i> 安装前端依赖</el-dropdown-item>
                  <el-dropdown-item command="run-install-sql"><i class="bi bi-database-gear"></i> 执行安装SQL</el-dropdown-item>
                  <el-dropdown-item command="reset-menus"><i class="bi bi-arrow-counterclockwise"></i> 重置菜单</el-dropdown-item>
                  <el-dropdown-item command="remove-menus"><i class="bi bi-x-lg"></i> 移除菜单</el-dropdown-item>
                  <el-dropdown-item command="dry-run" divided><i class="bi bi-play-circle"></i> SQL 预检</el-dropdown-item>
                  <el-dropdown-item command="grayscale"><i class="bi bi-sliders"></i> 灰度发布</el-dropdown-item>
                  <el-dropdown-item command="rollback"><i class="bi bi-clock-history"></i> 版本回滚</el-dropdown-item>
                  <el-dropdown-item v-if="envInfo?.isDevelopment" command="uninstall" divided style="color: #ef4444;"><i class="bi bi-trash3"></i> 卸载</el-dropdown-item>
                </el-dropdown-menu>
              </template>
            </el-dropdown>
          </div>
        </div>
      </div>

      <!-- 分页组件 -->
      <div v-if="filteredModules.length > 0" class="pagination-container" style="display: flex; justify-content: flex-end; margin-top: 16px;">
        <el-pagination
          v-model:current-page="currentPage"
          v-model:page-size="pageSize"
          :total="filteredModules.length"
          :page-sizes="[10, 20, 50, 100]"
          layout="total, sizes, prev, pager, next, jumper"
          background
          @size-change="currentPage = 1"
          @current-change="(val) => currentPage = val"
        />
      </div>
        </el-tab-pane>

        <!-- 插件商店 Tab -->
        <el-tab-pane label="插件商店" name="store">
          <div class="plugin-store-content">
            <!-- 生产环境提示 -->
            <el-alert v-if="envInfo && !envInfo.isDevelopment" type="warning" :closable="false" show-icon style="margin-bottom: 16px;">
              当前为生产环境，不支持在线安装插件。您可以浏览商店内容，但安装操作需在开发环境中进行，安装完成后重新部署到生产环境。
            </el-alert>
            <!-- 未配置远程商城 -->
            <div v-if="!storeConfig.enabled" class="store-unconfigured-section">
              <div class="store-login-card">
                <h3><i class="bi bi-gear" style="margin-right: 8px;"></i>插件商城未配置</h3>
                <p>远程插件商城服务地址未配置，请在 <code>appsettings.json</code> 中设置 <code>PluginStore:ServerUrl</code></p>
                <div class="store-config-example">
                  <pre>"PluginStore": {
  "ServerUrl": "https://your-store-url.com"
}</pre>
                </div>
                <p style="margin-top: 12px; font-size: 13px; color: #9ca3af;">配置后重启后端服务即可启用。</p>
              </div>
            </div>
            <!-- 已配置：直接显示插件列表（无需登录即可浏览） -->
            <div v-else>
              <div class="store-toolbar">
                <!-- 未登录：显示登录按钮 -->
                <div v-if="!storeLoggedIn" class="store-user-section">
                  <el-button type="primary" size="small" :loading="storeLoading || storeLoginSubmitting" @click="openStoreLoginDialog()">
                    <i class="bi bi-box-arrow-in-right" style="margin-right: 4px;"></i>登录商城
                  </el-button>
                  <span style="margin-left: 8px; font-size: 12px; color: #9ca3af;">登录后可查看已购买状态、购买和下载插件</span>
                </div>
                <!-- 已登录：显示用户信息 -->
                <div v-else class="store-user-section">
                  <span class="store-user-info"><i class="bi bi-person-check"></i> {{ formatStoreUserName(storeUserInfo?.nickname || storeUserInfo?.username) }}</span>
                  <el-tag v-if="storeUserInfo?.balance !== undefined" type="warning" size="small" style="margin-left: 8px;">
                    余额: ¥{{ (storeUserInfo.balance ?? 0).toFixed(2) }}
                  </el-tag>
                </div>
                <div class="store-toolbar-actions">
                  <el-button v-if="storeLoggedIn" size="small" @click="handleStoreLogout">退出商城</el-button>
                  <el-button size="small" @click="handleStoreRefresh" :loading="storeLoading">
                    <i class="bi bi-arrow-clockwise" style="margin-right: 4px;"></i>刷新
                  </el-button>
                </div>
              </div>

              <!-- 分类筛选与搜索 -->
              <div class="store-category-filter">
                <el-radio-group v-model="storeCategory" size="small" @change="resetPageAndReload">
                  <el-radio-button value="">全部</el-radio-button>
                  <el-radio-button v-for="cat in storeCategories" :key="cat.value" :value="cat.value">
                    {{ cat.label }}
                  </el-radio-button>
                </el-radio-group>
                <el-input
                  v-model="storeKeyword"
                  placeholder="搜索插件名称/描述..."
                  clearable
                  size="small"
                  style="width: 240px; margin-left: 12px;"
                  @keyup.enter="resetPageAndReload"
                  @clear="resetPageAndReload"
                >
                  <template #prefix><i class="bi bi-search"></i></template>
                  <template #append>
                    <el-button @click="resetPageAndReload" :loading="storeLoading" size="small">
                      搜索
                    </el-button>
                  </template>
                </el-input>
              </div>

              <div class="store-plugin-list" v-loading="storeLoading">
                <el-empty v-if="groupedPlugins.length === 0 && !storeLoading" description="暂无可用插件" />
                <div v-for="group in groupedPlugins" :key="group.id" class="store-plugin-card" :class="{ 'store-plugin-card-clickable': !group.installed }" @click="!group.installed && handleShowEditions(group)">
                  <div class="store-plugin-cover" v-if="getPluginImage(group.editions[0]) && !brokenCovers.has(group.id)">
                    <img :src="getPluginImage(group.editions[0])" alt="封面" @error="brokenCovers.add(group.id)" />
                  </div>
                  <div class="store-plugin-cover store-plugin-cover-placeholder" v-else>
                    <i class="bi bi-puzzle"></i>
                  </div>
                  <div class="store-plugin-info">
                    <div class="store-plugin-header">
                      <h3>{{ group.name }}</h3>
                      <el-tag v-if="group.category" size="small" type="info">{{ getCategoryLabel(group.category) }}</el-tag>
                      <el-tag v-if="group.installed" type="success" size="small">已安装</el-tag>
                      <el-tag v-else type="warning" size="small">未安装</el-tag>
                      <span class="edition-count" v-if="group.editions.length > 1">{{ group.editions.length }} 个版本</span>
                    </div>
                    <p class="module-description">{{ group.description }}</p>
                    <div class="module-meta">
                      <span v-if="group.author" class="meta-item"><i class="bi bi-person"></i> {{ group.author }}</span>
                      <span class="meta-item store-price">
                        <i class="bi bi-tag"></i>
                        <span v-if="group.minPrice > 0 && group.hasFree" class="price-free">免费起</span>
                        <span v-else-if="group.minPrice > 0" class="price-value">¥{{ group.minPrice.toFixed(2) }} 起</span>
                        <span v-else class="price-free">免费</span>
                      </span>
                    </div>
                  </div>
                  <div class="module-actions" @click.stop>
                    <template v-if="group.installed">
                      <el-button type="info" size="small" disabled plain>已安装</el-button>
                      <el-button v-if="group.hasUpgradeAvailable" type="warning" size="small" @click="handleUpgradePurchase(group)" style="margin-left: 6px;">
                        <i class="bi bi-arrow-up-circle" style="margin-right: 4px;"></i>升级购买
                      </el-button>
                    </template>
                    <template v-else>
                      <el-button type="primary" size="small" @click="handleShowEditions(group)">
                        <i class="bi bi-eye" style="margin-right: 4px;"></i>查看版本
                      </el-button>
                      <el-button v-if="group.hasUpgradeAvailable" type="warning" size="small" @click="handleUpgradePurchase(group)" style="margin-left: 6px;">
                        <i class="bi bi-arrow-up-circle" style="margin-right: 4px;"></i>升级购买
                      </el-button>
                    </template>
                  </div>
                </div>
              </div>

              <!--
                插件商店分页：远端 /api/plugin-store/portal/items 已按 page/pageSize 下发；
                total 转成数字避免 el-pagination 在 number|string 传入时报错。
                total=0 时隐藏整个分页组件，保持空态简洁。
              -->
              <div class="store-plugin-pagination" v-if="Number(storeTotal) > 0">
                <el-pagination
                  background
                  layout="total, sizes, prev, pager, next, jumper"
                  :current-page="storePage"
                  :page-size="storePageSize"
                  :page-sizes="[6, 12, 24, 48]"
                  :total="Number(storeTotal) || 0"
                  @current-change="onStorePageChange"
                  @size-change="onStorePageSizeChange"
                />
              </div>
            </div>

            <!-- 商城登录对话框：账号密码走本地后端代理，远程网页登录作为备用入口 -->
            <el-dialog v-model="showStoreLoginDialog" title="登录插件商城" width="420px" :close-on-click-modal="false" @closed="onStoreLoginDialogClosed">
              <el-form
                ref="storeLoginFormRef"
                :model="storeLoginForm"
                :rules="storeLoginRules"
                label-position="top"
                @submit.prevent="handleStoreLoginSubmit"
              >
                <el-form-item label="商城账号" prop="userName">
                  <el-input
                    v-model="storeLoginForm.userName"
                    placeholder="账号 / 邮箱 / 手机号"
                    autocomplete="username"
                    clearable
                  >
                    <template #prefix><i class="bi bi-person"></i></template>
                  </el-input>
                </el-form-item>
                <el-form-item label="商城密码" prop="password">
                  <el-input
                    v-model="storeLoginForm.password"
                    type="password"
                    placeholder="登录密码"
                    autocomplete="current-password"
                    show-password
                    @keyup.enter="handleStoreLoginSubmit"
                  >
                    <template #prefix><i class="bi bi-lock"></i></template>
                  </el-input>
                </el-form-item>

                <!-- 安全验证：仅在远端要求验证码时显示 -->
                <el-form-item v-if="storeCaptchaRequired" label="安全验证">
                  <StoreCaptchaPanel
                    :key="storeCaptchaKey"
                    @verified="onStoreCaptchaVerified"
                    @fail="onStoreCaptchaFail"
                  />
                  <div v-if="storeCaptchaTip" class="store-captcha-tip">{{ storeCaptchaTip }}</div>
                </el-form-item>
              </el-form>
              <template #footer>
                <el-button @click="showStoreLoginDialog = false">取消</el-button>
                <el-button @click="openRemoteStoreLoginFromDialog">远程网页登录</el-button>
                <el-button type="primary" :loading="storeLoginSubmitting" @click="handleStoreLoginSubmit">登录商城</el-button>
              </template>
            </el-dialog>

            <!-- 注：原本的「安装进度提示」el-dialog 已被通用全屏 processOverlay 替代，
                 不再需要在此渲染。showStoreInstallProgress / storeInstallProgress / storeInstallMessage /
                 storeInstallDone 这些状态保留只是为了兼容旧逻辑兜底，实际不再驱动任何 UI。 -->

            <!--
              版本选择对话框：点击「下载安装/免费安装」按钮前先弹出。
              - 列出当前 license 视角下「该档位的全部已发布版本」（按版本号倒序）；
              - 升级窗口内的版本可选；超出窗口的版本置灰并提示「需续费」；
              - 关键安全版本（isCriticalSecurity=true）即使在窗口外也允许下载（带「安全更新」标签）；
              - 默认选中后端标记的 isLatest=true 项，即 license 升级窗口内的最新可用版本。
            -->
            <el-dialog
              :model-value="!!versionPickerPlugin"
              title="选择要安装的版本"
              width="640px"
              :close-on-click-modal="false"
              :show-close="!(versionPickerPlugin && storeInstalling[versionPickerPlugin.id])"
              @update:model-value="(v: boolean) => { if (!v) closeVersionPicker() }"
            >
              <div v-if="versionPickerPlugin" class="store-version-picker">
                <div class="picker-tip">
                  <i class="bi bi-info-circle" style="margin-right:6px"></i>
                  <span>插件：<strong>{{ versionPickerPlugin.name }}</strong></span>
                  <span style="margin-left:12px">档位：<strong>{{ (versionPickerPlugin as any).editionName || versionPickerPlugin.editionId || versionPickerPlugin.id }}</strong></span>
                </div>
                <div class="picker-window-tip">
                  系统会按您当前的<strong>更新有效期</strong>判断哪些版本可下载安装。
                  <br>有效期外发布的新版本会置灰提示「需续费」，需在「会员中心 → 已购买」中续费或重新购买后才能选择。
                </div>

                <div v-if="versionPickerLoading" class="picker-loading">
                  <i class="bi bi-arrow-repeat picker-loading-spin"></i>
                  正在加载版本列表...
                </div>
                <div v-else-if="versionPickerReleases.length === 0" class="picker-empty">
                  <i class="bi bi-inbox" style="font-size:32px;display:block;margin-bottom:8px;color:#c0c4cc"></i>
                  <p>该档位暂无已发布版本</p>
                </div>
                <el-radio-group
                  v-else
                  v-model="versionPickerSelectedId"
                  class="version-radio-list"
                >
                  <label
                    v-for="r in versionPickerReleases"
                    :key="r.id"
                    class="version-row"
                    :class="{
                      selected: versionPickerSelectedId === r.id,
                      disabled: !r.available,
                      latest: r.isLatest && r.available
                    }"
                    @click="r.available && (versionPickerSelectedId = r.id)"
                  >
                    <el-radio :value="r.id" :disabled="!r.available">
                      <div class="version-row-main">
                        <div class="version-row-title">
                          <span class="version-no">v{{ r.version }}</span>
                          <el-tag v-if="r.isLatest && r.available" type="success" size="small" effect="light">最新可用</el-tag>
                          <el-tag v-if="r.isCriticalSecurity" type="warning" size="small" effect="light">
                            <i class="bi bi-shield-exclamation" style="margin-right:3px"></i>安全更新
                          </el-tag>
                          <el-tag v-if="!r.available" type="danger" size="small" effect="light">需续费</el-tag>
                        </div>
                        <div class="version-row-meta">
                          <span>发布时间：{{ r.releasedAt ? new Date(r.releasedAt).toLocaleString() : '-' }}</span>
                          <span v-if="r.packageSize">大小：{{ formatPackageSize(r.packageSize) }}</span>
                        </div>
                        <div v-if="!r.available && r.unavailableReason === 'out_of_window'" class="version-row-block-tip">
                          此版本在您的更新有效期之后发布，续费后可下载安装
                        </div>
                        <div v-if="r.updateLog" class="version-row-log">{{ r.updateLog }}</div>
                      </div>
                    </el-radio>
                  </label>
                </el-radio-group>

                <!--
                  协议勾选：下载前必须勾选，未勾选时主按钮禁用。
                  协议内容当前为占位（showAgreementDialog 里给出"暂未提供具体协议"说明），
                  后续由商城侧按插件挂真实协议文本再接入。
                -->
                <div v-if="!versionPickerLoading && versionPickerReleases.length > 0" class="version-picker-agreement">
                  <el-checkbox
                    :model-value="isAgreed(versionPickerPlugin.id, versionPickerPlugin.editionId || versionPickerPlugin.id)"
                    @update:model-value="(v: boolean) => { pluginAgreementAccepted[agreementKey(versionPickerPlugin!.id, versionPickerPlugin!.editionId || versionPickerPlugin!.id)] = !!v }"
                  >
                    <span class="agreement-text">
                      我已阅读并同意
                      <a
                        href="javascript:void(0)"
                        class="agreement-link"
                        @click.stop.prevent="openAgreementDialog(versionPickerPlugin.name, (versionPickerPlugin as any).editionName || '')"
                      >《{{ versionPickerPlugin.name }}{{ (versionPickerPlugin as any).editionName ? ' · ' + (versionPickerPlugin as any).editionName : '' }} 使用协议》</a>
                    </span>
                  </el-checkbox>
                </div>
              </div>
              <template #footer>
                <el-button
                  :disabled="versionPickerPlugin && storeInstalling[versionPickerPlugin.id]"
                  @click="closeVersionPicker"
                >取消</el-button>
                <el-button
                  type="primary"
                  :disabled="!versionPickerSelectedId
                    || (versionPickerPlugin ? !!storeInstalling[versionPickerPlugin.id] : false)
                    || (versionPickerPlugin ? !isAgreed(versionPickerPlugin.id, versionPickerPlugin.editionId || versionPickerPlugin.id) : true)"
                  @click="confirmVersionInstall"
                >
                  <i class="bi bi-download" style="margin-right:4px"></i>下载安装所选版本
                </el-button>
              </template>
            </el-dialog>

            <!--
              协议预览对话框：暂不承载真实协议正文，仅交互骨架。
              稍后在插件商店侧按插件挂载真实协议 Markdown/HTML 后，
              用 v-html 或 Markdown 渲染替换 .agreement-placeholder 部分即可。
            -->
            <el-dialog v-model="showAgreementDialog" width="520px" title="插件使用协议">
              <div class="agreement-placeholder">
                <p v-if="agreementContext">
                  <strong>{{ agreementContext.pluginName }}</strong>
                  <span v-if="agreementContext.editionName"> · {{ agreementContext.editionName }}</span>
                </p>
                <el-alert
                  type="info"
                  :closable="false"
                  show-icon
                  title="该插件暂未提供协议正文"
                  description="后续将在插件商店为各插件单独挂载具体协议条款；当前版本仅作为交互占位，勾选即视为同意框架通用使用条款。"
                />
              </div>
              <template #footer>
                <el-button @click="showAgreementDialog = false">我知道了</el-button>
              </template>
            </el-dialog>

            <!-- 版本选择对话框 -->
            <el-dialog v-model="showEditionDialog" :title="editionDialogTitle" width="620px" :close-on-click-modal="false">
              <div class="edition-list" v-if="editionGroup">
                <div class="edition-plugin-desc">
                  <p>{{ editionGroup.description }}</p>
                  <!-- 当前运行环境提示 -->
                  <div class="env-hint" :class="envInfo?.isDevelopment ? 'env-hint-dev' : 'env-hint-prod'">
                    <i :class="envInfo?.isDevelopment ? 'bi bi-code-slash' : 'bi bi-box-seam'"></i>
                    当前运行模式：<strong>{{ envInfo?.isDevelopment ? '开发模式（源码调试）' : '生产模式（正式编译）' }}</strong>
                    <span v-if="!envInfo?.isDevelopment" style="margin-left: 8px; font-size: 12px; color: #e6a23c;">（仅支持安装编译包）</span>
                  </div>
                </div>
                <div
                  v-for="edition in editionGroup.editions"
                  :key="edition.editionId"
                  class="edition-card"
                  :class="{ 'edition-card-free': edition.isFree, 'edition-card-disabled': isEditionDisabledByEnv(edition) }"
                >
                  <div class="edition-info">
                    <div class="edition-header">
                      <h4>{{ edition.editionName || edition.name }}</h4>
                      <span class="module-version">v{{ edition.version }}</span>
                      <el-tag v-if="edition.packageType" size="small" :type="edition.packageType === 'source' ? 'warning' : 'success'">
                        {{ edition.packageType === 'source' ? '源码包' : '编译包' }}
                      </el-tag>
                      <el-tag v-if="edition.purchased && storeLoggedIn" type="primary" size="small">已购买</el-tag>
                      <el-tag v-if="isEditionDisabledByEnv(edition)" type="danger" size="small">当前环境不支持</el-tag>
                    </div>
                    <div class="edition-price">
                      <span v-if="edition.isFree" class="price-free"><i class="bi bi-tag"></i> 免费</span>
                      <span v-else class="price-value"><i class="bi bi-tag"></i> ¥{{ edition.price.toFixed(2) }}</span>
                    </div>
                  </div>
                  <div class="edition-actions">
                    <!--
                      协议勾选：未勾选时「免费安装 / 下载安装 / 购买」按钮禁用。
                      已安装或当前环境不支持安装的档位不显示勾选框（没有下载动作）。
                    -->
                    <div
                      v-if="!edition.installed && envInfo?.isDevelopment"
                      class="edition-agreement"
                    >
                      <el-checkbox
                        :model-value="isAgreed(edition.id, edition.editionId || edition.id)"
                        @update:model-value="(v: boolean) => { pluginAgreementAccepted[agreementKey(edition.id, edition.editionId || edition.id)] = !!v }"
                      >
                        <span class="agreement-text">
                          我已阅读并同意
                          <a
                            href="javascript:void(0)"
                            class="agreement-link"
                            @click.stop.prevent="openAgreementDialog(edition.name || '', edition.editionName || '')"
                          >《{{ edition.name }}{{ edition.editionName ? ' · ' + edition.editionName : '' }} 使用协议》</a>
                        </span>
                      </el-checkbox>
                    </div>
                    <el-button v-if="edition.installed" type="info" size="small" disabled plain>
                      <i class="bi bi-check-circle" style="margin-right: 4px;"></i>已安装
                    </el-button>
                    <!-- 生产环境下所有版本均不可安装 -->
                    <el-tooltip v-else-if="!envInfo?.isDevelopment" content="生产环境不支持在线安装插件，请在开发环境中安装后重新部署" placement="top">
                      <el-button type="info" size="small" disabled plain>
                        <i class="bi bi-lock" style="margin-right: 4px;"></i>不可安装
                      </el-button>
                    </el-tooltip>
                    <el-button v-else-if="edition.purchased && storeLoggedIn" type="primary" size="small" :disabled="!isAgreed(edition.id, edition.editionId || edition.id)" @click="handleInstallFromStore(edition)" :loading="storeInstalling[edition.id]">
                      <i class="bi bi-download" style="margin-right: 4px;"></i>下载安装
                    </el-button>
                    <el-button v-else-if="edition.isFree" type="primary" size="small" :disabled="!isAgreed(edition.id, edition.editionId || edition.id)" @click="handleInstallFromStore(edition)" :loading="storeInstalling[edition.id]">
                      <i class="bi bi-download" style="margin-right: 4px;"></i>免费安装
                    </el-button>
                    <el-button v-else type="success" size="small" :disabled="!isAgreed(edition.id, edition.editionId || edition.id)" @click="handlePurchasePlugin(edition)" :loading="storePurchasing[edition.id]">
                      <i class="bi bi-cart-plus" style="margin-right: 4px;"></i>购买
                    </el-button>
                  </div>
                </div>
              </div>
              <template #footer>
                <el-button @click="showEditionDialog = false">关闭</el-button>
              </template>
            </el-dialog>

            <!-- 购买确认对话框 -->
            <el-dialog v-model="showPurchaseDialog" title="确认购买" width="420px" :close-on-click-modal="false">
              <div class="purchase-confirm" v-if="purchaseTarget">
                <div class="purchase-item-info">
                  <h4>{{ purchaseTarget.name }}{{ purchaseTarget.editionName ? ' - ' + purchaseTarget.editionName : '' }}</h4>
                  <div style="display: flex; gap: 6px; margin: 6px 0 8px;">
                    <el-tag size="small">v{{ purchaseTarget.version }}</el-tag>
                    <el-tag size="small" :type="purchaseTarget.packageType === 'source' ? 'warning' : 'success'">
                      {{ purchaseTarget.packageType === 'source' ? '源码包' : '编译包' }}
                    </el-tag>
                  </div>
                  <p>{{ purchaseTarget.description }}</p>
                </div>
                <div class="purchase-price-info">
                  <div class="price-row">
                    <span>价格：</span>
                    <span class="price-value">¥{{ purchaseTarget?.price.toFixed(2) }}</span>
                  </div>
                  <div class="price-row" v-if="storeUserInfo?.balance !== undefined">
                    <span>当前余额：</span>
                    <span :class="{ 'balance-insufficient': (storeUserInfo.balance ?? 0) < (purchaseTarget?.price || 0) }">
                      ¥{{ (storeUserInfo.balance ?? 0).toFixed(2) }}
                    </span>
                  </div>
                  <div class="price-row" style="margin-top: 15px; align-items: start;">
                    <span>支付方式：</span>
                    <div v-if="paymentChannelsLoading" style="color: #909399; font-size: 13px;">
                      <i class="bi bi-hourglass-split" style="margin-right: 4px;"></i>正在获取可用支付方式...
                    </div>
                    <el-radio-group v-else-if="availablePaymentChannels.length > 0" v-model="purchaseChannel" size="small">
                      <el-radio
                        v-for="ch in availablePaymentChannels"
                        :key="ch"
                        :value="ch"
                        style="margin-right: 15px;"
                      >
                        <i :class="getPaymentChannelMeta(ch).iconClass" :style="{ color: getPaymentChannelMeta(ch).color }"></i>
                        {{ getPaymentChannelMeta(ch).label }}
                      </el-radio>
                    </el-radio-group>
                    <div v-else style="color: #f56c6c; font-size: 13px;">
                      <i class="bi bi-exclamation-triangle" style="margin-right: 4px;"></i>商城当前未启用任何在线支付渠道，暂不支持购买
                    </div>
                  </div>
                </div>
              </div>
              <template #footer>
                <el-button @click="showPurchaseDialog = false">取消</el-button>
                <el-button type="primary" @click="confirmPurchase" :loading="purchaseLoading">确认购买</el-button>
              </template>
            </el-dialog>

            <!-- 支付二维码扫码对话框 -->
            <el-dialog v-model="showPaymentDialog" title="扫码支付" width="360px" :close-on-click-modal="false" @close="onPaymentDialogClose">
              <div class="payment-qrcode-container" style="text-align: center; padding: 20px 0;">
                <p style="margin-top: 0;">请使用 <strong :style="{ color: getPaymentChannelMeta(purchaseChannel).color }">{{ getPaymentChannelMeta(purchaseChannel).label }}扫一扫</strong> 完成支付</p>
                <div style="margin: 20px 0; display: inline-block; padding: 10px; border: 1px solid #eee; border-radius: 8px;">
                  <img v-if="paymentQrCodeData" :src="paymentQrCodeData" width="220" height="220" alt="付款二维码" />
                  <div v-else style="width: 220px; height: 220px; line-height: 220px; color: #999;">正在生成...</div>
                </div>
                <p style="color: #f56c6c; font-size: 20px; font-weight: bold;">¥{{ purchaseTarget?.price.toFixed(2) }}</p>
                <p v-if="paymentPollingMsg" style="color: #909399; font-size: 13px; margin-top: 15px;">{{ paymentPollingMsg }}</p>
                <p style="color: #909399; font-size: 12px; margin-top: 6px;">
                  <i class="bi bi-clock"></i>
                  支付剩余时间 <span style="font-weight: 600;">{{ paymentCountdownText }}</span>
                </p>
                <el-button type="primary" :loading="paymentChecking" style="margin-top: 12px;" @click="handleManualPaymentCheck">
                  {{ paymentChecking ? '查询中...' : '我已完成支付' }}
                </el-button>
              </div>
            </el-dialog>
          </div>
        </el-tab-pane>
      </el-tabs>
    </el-card>

    <!-- 安装模块对话框 -->
    <el-dialog v-model="showInstallDialog" title="安装模块" width="600px" @close="resetInstallForm">
      <div class="install-module-form">
        <el-upload ref="uploadRef" drag accept=".zip" :auto-upload="false" :limit="1" :on-change="handleFileChange" :on-remove="handleFileRemove" :file-list="fileList">
          <i class="bi bi-cloud-upload" style="font-size: 48px; color: #3b82f6;"></i>
          <div class="el-upload__text">将模块包拖到此处，或<em>点击上传</em></div>
          <template #tip><div class="el-upload__tip">支持 .zip 格式的模块安装包</div></template>
        </el-upload>
        <div v-if="uploadProgress > 0 && uploadProgress < 100" class="upload-progress">
          <el-progress :percentage="uploadProgress" />
        </div>
        <div v-if="validationResult" class="validation-result" :class="{ success: validationResult.ok, error: !validationResult.ok }">
          <div class="result-header">
            <i :class="validationResult.ok ? 'bi bi-check-circle-fill' : 'bi bi-x-circle-fill'"></i>
            <span>{{ validationResult.message }}</span>
          </div>
          <div v-if="validationResult.ok" class="result-details">
            <p><strong>模块ID:</strong> {{ validationResult.moduleId }}</p>
            <p><strong>模块名称:</strong> {{ validationResult.moduleName }}</p>
            <p><strong>版本:</strong> {{ validationResult.version }}</p>
          </div>
          <!-- 安全校验信息 -->
          <div v-if="validationResult.ok && validationResult.security" class="security-info" style="margin-top:12px;">
            <el-descriptions :column="1" size="small" border>
              <el-descriptions-item label="文件哈希">
                <el-tag :type="validationResult.security.hashValid ? 'success' : 'warning'" size="small">{{ validationResult.security.hashValid ? '校验通过' : '未校验' }}</el-tag>
              </el-descriptions-item>
              <el-descriptions-item label="包签名">
                <el-tag :type="validationResult.security.signatureValid ? 'success' : 'info'" size="small">
                  {{ validationResult.security.signatureValid ? `已验证 (${validationResult.security.signaturePublisher || '未知'})` : '未签名' }}
                </el-tag>
              </el-descriptions-item>
              <el-descriptions-item v-if="validationResult.security.warnings?.length" label="安全提示">
                <div v-for="(w, i) in validationResult.security.warnings" :key="i" style="color:var(--el-color-warning);font-size:12px;">
                  <i class="bi bi-exclamation-triangle" style="margin-right:2px;"></i>{{ w }}
                </div>
              </el-descriptions-item>
            </el-descriptions>
          </div>
          <!-- 安装选项（验证通过后显示） -->
          <div v-if="validationResult.ok && !installResult" class="install-options" style="margin-top:14px;padding:12px;background:var(--el-fill-color-lighter);border-radius:6px;">
            <div style="font-size:13px;font-weight:600;margin-bottom:8px;color:var(--el-text-color-primary);">安装选项</div>
            <el-form label-width="90px" size="small">
              <el-form-item label="发布通道" style="margin-bottom:8px;">
                <el-radio-group v-model="installChannel">
                  <el-radio value="stable">全量发布</el-radio>
                  <el-radio value="beta">灰度验证</el-radio>
                </el-radio-group>
              </el-form-item>
              <el-form-item v-if="installChannel === 'beta'" label="目标租户" style="margin-bottom:0;">
                <el-input v-model="installGrayscaleTenants" placeholder="多个租户ID逗号分隔" size="small" clearable />
              </el-form-item>
            </el-form>
          </div>
        </div>
        <div v-if="installResult" class="install-result" :class="{ success: installResult.ok, error: !installResult.ok }">
          <div class="result-header">
            <i :class="installResult.ok ? 'bi bi-check-circle-fill' : 'bi bi-x-circle-fill'"></i>
            <span>{{ installResult.message }}</span>
          </div>
        </div>
      </div>
      <template #footer>
        <el-button @click="showInstallDialog = false">取消</el-button>
        <el-button v-if="!validationResult && !installResult" type="primary" @click="handleUploadAndValidate" :loading="installing" :disabled="!selectedFile">上传验证</el-button>
        <el-button v-if="validationResult?.ok && !installResult" type="success" @click="handleConfirmInstall" :loading="installing">确认安装</el-button>
        <el-button v-if="installResult?.ok" type="primary" @click="handleInstallComplete">完成</el-button>
      </template>
    </el-dialog>

    <!-- 打包模块对话框 -->
    <el-dialog v-model="showPackageDialog" title="打包模块" width="560px" @close="resetPackageForm">
      <el-form :model="packageForm" label-width="120px">
        <el-form-item label="选择模块">
          <el-select v-model="packageForm.moduleId" placeholder="请选择要打包的模块" style="width: 100%" filterable :loading="loadingPackageable" @change="onPackageModuleChange">
            <el-option v-for="mod in packageableModules" :key="mod.moduleId" :label="`${mod.name} (${mod.moduleId})`" :value="mod.moduleId">
              <span>{{ mod.name }} ({{ mod.moduleId }})</span>
              <el-tag v-if="mod.isSourcePackage === false" size="small" type="success" style="margin-left: 8px;">已编译</el-tag>
              <el-tag v-else-if="mod.isSourcePackage === true" size="small" type="warning" style="margin-left: 8px;">源码版</el-tag>
            </el-option>
          </el-select>
        </el-form-item>
        <el-form-item label="打包类型">
          <el-radio-group v-model="packageForm.packageType">
            <el-radio value="source" :disabled="!selectedPackageableModuleHasSource">
              <span>源码包</span>
              <el-text type="info" size="small" style="margin-left: 4px;">（开发环境）</el-text>
            </el-radio>
            <el-radio value="compiled">
              <span>编译包</span>
              <el-text type="info" size="small" style="margin-left: 4px;">（生产环境）</el-text>
            </el-radio>
          </el-radio-group>
          <el-text v-if="packageForm.moduleId && !selectedPackageableModuleHasSource" type="warning" size="small" style="display: block; margin-top: 4px;">
            <i class="bi bi-info-circle" style="margin-right: 4px;"></i>当前模块为已编译 DLL 版（server 目录下未发现 .csproj），仅支持重新打编译包
          </el-text>
        </el-form-item>
        <el-form-item label="数据库导出">
          <div style="display: flex; flex-direction: column; gap: 4px; width: 100%;">
            <el-checkbox v-model="packageForm.exportDbSchema" @change="onExportSchemaChange">真实数据结构</el-checkbox>
            <el-text type="info" size="small" style="margin-left: 24px;">勾选后将根据 module.json 的 tablePrefix 从真实数据库导出最新表结构，覆盖打包内 install.sql；不勾选则沿用磁盘上的 install.sql</el-text>
            <el-checkbox v-model="packageForm.exportDbData" :disabled="!packageForm.exportDbSchema" style="margin-top: 4px;">真实数据内容</el-checkbox>
            <el-text type="info" size="small" style="margin-left: 24px;">需先勾选“真实数据结构”。将按主键降序每表最多导出 100 行数据，生成 init_data.sql 并注入 install.json 的 SqlScripts</el-text>
            <el-text type="warning" size="small" style="margin-top: 4px;">以上操作仅影响生成的 ZIP 内容，不会修改磁盘上插件的任何原始文件</el-text>
          </div>
        </el-form-item>
        <el-form-item label="配置脱敏">
          <div style="display: flex; flex-direction: column; gap: 4px; width: 100%;">
            <el-checkbox v-model="packageForm.sanitizeConfig">清空配置真实值（推荐）</el-checkbox>
            <el-text type="info" size="small" style="margin-left: 24px;">勾选后会将插件 server/config/*.json 中所有配置项的真实值清空，仅保留配置结构与键名，防止 AppKey、密钥、Token 等敏感信息随插件包泄露；不勾选则保留当前所有配置值（仅限内部备份场景）</el-text>
          </div>
        </el-form-item>
      </el-form>
      <div v-if="packaging" class="package-progress">
        <el-progress :percentage="packageProgress" />
        <p class="progress-text">正在打包模块...</p>
      </div>
      <div v-if="packageResult" class="package-result" :class="{ success: packageResult.ok, error: !packageResult.ok }">
        <div class="result-header">
          <i :class="packageResult.ok ? 'bi bi-check-circle-fill' : 'bi bi-x-circle-fill'"></i>
          <span>{{ packageResult.message }}</span>
        </div>
        <div v-if="packageResult.ok" class="result-details">
          <p><strong>文件名:</strong> {{ packageResult.fileName }}</p>
          <p><strong>文件大小:</strong> {{ formatFileSize(packageResult.fileSize || 0) }}</p>
          <p><strong>打包类型:</strong> {{ packageResult.packageType === 'source' ? '源码包' : '编译包' }}</p>
        </div>
        <!-- 打包步骤详情 -->
        <div v-if="packageResult.steps && packageResult.steps.length > 0" class="result-steps">
          <el-collapse>
            <el-collapse-item title="打包步骤详情">
              <div v-for="(step, idx) in packageResult.steps" :key="idx" class="step-item">
                <el-text :type="step.includes('失败') || step.includes('错误') ? 'danger' : step.includes('跳过') ? 'info' : 'success'" size="small">{{ step }}</el-text>
              </div>
            </el-collapse-item>
          </el-collapse>
        </div>
      </div>
      <template #footer>
        <el-button @click="showPackageDialog = false">取消</el-button>
        <el-button v-if="!packageResult" type="primary" @click="handlePackage" :loading="packaging" :disabled="!packageForm.moduleId">开始打包</el-button>
        <el-button v-if="packageResult?.ok" type="success" @click="handleDownloadPackage"><i class="bi bi-download" style="margin-right: 4px;"></i>下载安装包</el-button>
      </template>
    </el-dialog>

    <!-- 模块状态对话框 -->
    <el-dialog v-model="showStatusDialog" title="模块状态" width="600px">
      <div v-if="currentModuleStatus" class="module-status-detail">
        <el-descriptions :column="2" border>
          <el-descriptions-item label="模块ID">{{ currentModuleStatus.moduleId }}</el-descriptions-item>
          <el-descriptions-item label="版本">{{ currentModuleStatus.version }}</el-descriptions-item>
          <el-descriptions-item label="启用状态"><el-tag :type="currentModuleStatus.enabled ? 'success' : 'info'">{{ currentModuleStatus.enabled ? '已启用' : '已禁用' }}</el-tag></el-descriptions-item>
          <el-descriptions-item label="运行时加载"><el-tag :type="currentModuleStatus.runtimeLoaded ? 'success' : 'danger'">{{ currentModuleStatus.runtimeLoaded ? '是' : '否' }}</el-tag></el-descriptions-item>
          <el-descriptions-item label="服务端DLL"><el-tag :type="currentModuleStatus.serverDllLoaded ? 'success' : 'danger'">{{ currentModuleStatus.serverDllLoaded ? '已加载' : '未加载' }}</el-tag></el-descriptions-item>
          <!-- 菜单注册：仅在插件确实声明了菜单（install.json 含 Menus.RootCode）且已成功注册时才展示「已注册」。
               按用户要求：未声明菜单 → 整行隐藏；声明了但未注册 → 同样不展示「未注册」标签，避免误导。
               如需排查未注册问题，可以走「重置菜单」操作或查看模块状态日志。 -->
          <el-descriptions-item
            v-if="currentModuleStatus.hasMenus && currentModuleStatus.menuRegistered"
            label="菜单注册"
          >
            <el-tag type="success">已注册</el-tag>
          </el-descriptions-item>
        </el-descriptions>
      </div>
      <el-skeleton v-else :rows="5" animated />
    </el-dialog>

    <!-- 灰度发布对话框 -->
    <el-dialog v-model="showGrayscaleDialog" :title="`灰度发布 - ${grayscaleModuleName}`" width="560px" destroy-on-close>
      <el-form label-width="110px">
        <el-form-item label="发布通道">
          <el-radio-group v-model="grayscaleForm.channel">
            <el-radio value="stable">stable（全量稳定）</el-radio>
            <el-radio value="beta">beta（灰度验证）</el-radio>
            <el-radio value="dev">dev（开发调试）</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="目标租户ID">
          <el-input v-model="grayscaleForm.targetTenantIds" placeholder="多个租户用逗号分隔，留空表示全量" clearable />
          <div style="font-size:12px;color:var(--el-text-color-placeholder);margin-top:4px;">
            仅对指定租户加载此模块，留空则对所有租户生效
          </div>
        </el-form-item>
        <el-form-item label="生效开始时间">
          <el-date-picker v-model="grayscaleForm.startTime" type="datetime" placeholder="选择开始时间（可选）" style="width:100%;" value-format="YYYY-MM-DDTHH:mm:ss" clearable />
        </el-form-item>
        <el-form-item label="生效结束时间">
          <el-date-picker v-model="grayscaleForm.endTime" type="datetime" placeholder="选择结束时间（可选）" style="width:100%;" value-format="YYYY-MM-DDTHH:mm:ss" clearable />
        </el-form-item>
        <el-form-item label="到期后策略">
          <el-switch v-model="grayscaleForm.autoPromote" active-text="自动全量发布" inactive-text="自动下线回退" />
          <div style="font-size:12px;color:var(--el-text-color-placeholder);margin-top:4px;">
            超过结束时间后，模块将自动切为全量发布或禁用
          </div>
        </el-form-item>
      </el-form>
      <div v-if="currentGrayscalePolicy" style="background:var(--el-fill-color-lighter);border-radius:6px;padding:10px 14px;margin-top:12px;font-size:13px;color:var(--el-text-color-secondary);">
        <i class="bi bi-info-circle" style="margin-right:4px;"></i>
        当前已有灰度策略 · 通道: <strong>{{ currentGrayscalePolicy.channel }}</strong> · 创建于 {{ currentGrayscalePolicy.createdAt?.substring(0, 10) }}
      </div>
      <template #footer>
        <el-button v-if="currentGrayscalePolicy" type="danger" plain @click="handleRemoveGrayscale" style="float:left;">移除策略（全量发布）</el-button>
        <el-button @click="showGrayscaleDialog = false">取消</el-button>
        <el-button type="primary" @click="handleSaveGrayscale" :loading="grayscaleSaving">保存策略</el-button>
      </template>
    </el-dialog>

    <!-- 版本回滚对话框 -->
    <el-dialog v-model="showRollbackDialog" :title="`版本回滚 - ${rollbackModuleName}`" width="580px" destroy-on-close>
      <div v-loading="rollbackLoading">
        <el-alert v-if="!rollbackLoading && rollbackSnapshots.length > 0" title="回滚说明" type="info" :closable="false" show-icon style="margin-bottom:16px;">
          回滚将恢复模块文件、菜单和配置到快照时刻的状态。回滚后模块将自动禁用，需手动重新启用并重载服务。
        </el-alert>
        <el-empty v-if="!rollbackLoading && rollbackSnapshots.length === 0" description="暂无可用快照。快照在安装或升级模块前会自动创建。" />
        <el-radio-group v-else v-model="selectedSnapshot" style="display:flex;flex-direction:column;gap:10px;width:100%;">
          <div
            v-for="snap in rollbackSnapshots"
            :key="snap.version"
            class="snapshot-item"
            :class="{ active: selectedSnapshot === snap.version }"
            @click="selectedSnapshot = snap.version"
          >
            <el-radio :value="snap.version" style="margin:0;width:100%;">
              <div class="snapshot-info">
                <span class="snap-version">{{ snap.version }}</span>
                <el-tag size="small" type="info" style="margin-left:8px;">{{ snap.snapshotType }}</el-tag>
                <span class="snap-time">{{ snap.createdAt?.replace('T',' ').substring(0,16) }}</span>
                <span class="snap-size">{{ formatFileSize(snap.fileSizeBytes) }}</span>
              </div>
            </el-radio>
          </div>
        </el-radio-group>
      </div>
      <template #footer>
        <el-button @click="showRollbackDialog = false">取消</el-button>
        <el-button type="warning" @click="handleDoRollback" :loading="rollbackExecuting" :disabled="!selectedSnapshot">
          <i class="bi bi-clock-history" style="margin-right:4px;"></i>确认回滚
        </el-button>
      </template>
    </el-dialog>

    <!-- 模块配置对话框 -->
    <el-dialog v-model="showConfigDialog" :title="`模块配置 - ${configModuleName}`" width="720px" @close="resetConfigForm" destroy-on-close>
      <div v-loading="configLoading">
        <!-- 无配置文件 -->
        <el-empty v-if="!configLoading && configFiles.length === 0" description="该模块没有可配置的参数" />

        <!-- 配置文件选择（多个配置文件时显示） -->
        <div v-if="configFiles.length > 1" class="config-file-selector">
          <span class="selector-label">配置文件：</span>
          <el-select v-model="activeConfigFile" @change="loadConfigData" size="small" style="width: 200px;">
            <el-option v-for="f in configFiles" :key="f" :label="f.replace('.json', '').replace('.sample', '')" :value="f" />
          </el-select>
        </div>

        <!-- 配置表单 -->
        <div v-if="configData && configData.groups && configData.items">
          <el-tabs v-model="activeConfigTab" v-if="configData.groups.length > 1">
            <el-tab-pane v-for="group in configData.groups" :key="group.code" :label="group.title" :name="group.code">
              <div class="config-group-header">
                <p v-if="group.desc" class="config-group-desc">{{ group.desc }}</p>
                <a v-if="(group as any).applyUrl" :href="(group as any).applyUrl" target="_blank" rel="noopener" class="config-apply-link">
                  <i class="bi bi-box-arrow-up-right"></i> 前往申请
                </a>
              </div>
              <el-form label-width="160px" label-position="left" class="config-form">
                <el-form-item v-for="item in getGroupItems(group.code)" :key="item.name" :label="item.title" :required="item.rule === 'required'">
                  <!-- text -->
                  <el-input v-if="item.type === 'text'" v-model="item.value" :placeholder="item.tip || ''" clearable />
                  <!-- password -->
                  <el-input v-else-if="item.type === 'password'" v-model="item.value" :placeholder="item.tip || ''" show-password clearable />
                  <!-- textarea -->
                  <el-input v-else-if="item.type === 'textarea'" v-model="item.value" type="textarea" :rows="3" :placeholder="item.tip || ''" />
                  <!-- radio -->
                  <el-radio-group v-else-if="item.type === 'radio'" v-model="item.value">
                    <el-radio v-for="(label, key) in item.content" :key="key" :value="key">{{ label }}</el-radio>
                  </el-radio-group>
                  <!-- select -->
                  <el-select v-else-if="item.type === 'select'" v-model="item.value" :placeholder="item.tip || '请选择'" clearable>
                    <el-option v-for="(label, key) in item.content" :key="key" :label="label" :value="key" />
                  </el-select>
                  <!-- link: 外部链接按钮 -->
                  <a v-else-if="item.type === 'link'" :href="item.value || '#'" target="_blank" rel="noopener" class="config-link-btn">
                    <i class="bi bi-box-arrow-up-right"></i> {{ item.tip || '打开链接' }}
                  </a>
                  <!-- file: 文件路径输入（支持填写路径或上传文件） -->
                  <div v-else-if="item.type === 'file'" class="config-file-input">
                    <el-input v-model="item.value" :placeholder="item.tip || '输入文件路径或上传文件'" clearable />
                    <el-text type="info" size="small" style="margin-top: 2px;">支持填写服务器上的文件绝对路径，如 /etc/keys/cert.p8</el-text>
                  </div>
                  <!-- api-selector: API 可视化选择器 -->
                  <div v-else-if="item.type === 'api-selector'" class="api-selector-wrapper">
                    <div class="api-tags-container">
                      <el-tag
                        v-for="api in parseApiList(item.value)"
                        :key="api"
                        closable
                        type="info"
                        effect="plain"
                        size="default"
                        class="api-tag"
                        @close="removeApiFromItem(item, api)"
                      >
                        <span class="api-tag-label">{{ getApiDisplayName(api) }}</span>
                        <code class="api-tag-path">{{ api }}</code>
                      </el-tag>
                      <el-tag v-if="parseApiList(item.value).length === 0" type="info" effect="plain" size="default" style="color:#909399">暂无受保护的 API</el-tag>
                    </div>
                    <el-button type="primary" size="small" style="margin-top:8px" @click="openApiSelector(item)">
                      <i class="bi bi-plus-circle" style="margin-right:4px"></i> 选择 API
                    </el-button>
                    <div class="config-item-tip">{{ item.tip }}</div>
                  </div>
                  <!-- fallback -->
                  <el-input v-else v-model="item.value" :placeholder="item.tip || ''" />
                  <div v-if="item.tip && item.type !== 'text' && item.type !== 'password' && item.type !== 'link' && item.type !== 'file' && item.type !== 'api-selector'" class="config-item-tip">{{ item.tip }}</div>
                </el-form-item>
              </el-form>
            </el-tab-pane>
          </el-tabs>

          <!-- 单分组时不显示 tabs -->
          <el-form v-else label-width="160px" label-position="left" class="config-form">
            <el-form-item v-for="item in configData.items" :key="item.name" :label="item.title" :required="item.rule === 'required'">
              <el-input v-if="item.type === 'text'" v-model="item.value" :placeholder="item.tip || ''" clearable />
              <el-input v-else-if="item.type === 'password'" v-model="item.value" :placeholder="item.tip || ''" show-password clearable />
              <el-input v-else-if="item.type === 'textarea'" v-model="item.value" type="textarea" :rows="3" :placeholder="item.tip || ''" />
              <el-radio-group v-else-if="item.type === 'radio'" v-model="item.value">
                <el-radio v-for="(label, key) in item.content" :key="key" :value="key">{{ label }}</el-radio>
              </el-radio-group>
              <el-select v-else-if="item.type === 'select'" v-model="item.value" :placeholder="item.tip || '请选择'" clearable>
                <el-option v-for="(label, key) in item.content" :key="key" :label="label" :value="key" />
              </el-select>
              <a v-else-if="item.type === 'link'" :href="item.value || '#'" target="_blank" rel="noopener" class="config-link-btn">
                <i class="bi bi-box-arrow-up-right"></i> {{ item.tip || '打开链接' }}
              </a>
              <div v-else-if="item.type === 'file'" class="config-file-input">
                <el-input v-model="item.value" :placeholder="item.tip || '输入文件路径或上传文件'" clearable />
                <el-text type="info" size="small" style="margin-top: 2px;">支持填写服务器上的文件绝对路径</el-text>
              </div>
              <!-- api-selector: API 可视化选择器 -->
              <div v-else-if="item.type === 'api-selector'" class="api-selector-wrapper">
                <div class="api-tags-container">
                  <el-tag
                    v-for="api in parseApiList(item.value)"
                    :key="api"
                    closable
                    type="info"
                    effect="plain"
                    size="default"
                    class="api-tag"
                    @close="removeApiFromItem(item, api)"
                  >
                    <span class="api-tag-label">{{ getApiDisplayName(api) }}</span>
                    <code class="api-tag-path">{{ api }}</code>
                  </el-tag>
                  <el-tag v-if="parseApiList(item.value).length === 0" type="info" effect="plain" size="default" style="color:#909399">暂无受保护的 API</el-tag>
                </div>
                <el-button type="primary" size="small" style="margin-top:8px" @click="openApiSelector(item)">
                  <i class="bi bi-plus-circle" style="margin-right:4px"></i> 选择 API
                </el-button>
                <div class="config-item-tip">{{ item.tip }}</div>
              </div>
              <el-input v-else v-model="item.value" :placeholder="item.tip || ''" />
              <div v-if="item.tip && item.type !== 'link' && item.type !== 'file' && item.type !== 'api-selector'" class="config-item-tip">{{ item.tip }}</div>
            </el-form-item>
          </el-form>
        </div>
      </div>
      <template #footer>
        <div class="config-dialog-footer">
          <el-button @click="handleResetConfig" :loading="configSaving" :disabled="configFiles.length === 0">
            <i class="bi bi-arrow-counterclockwise" style="margin-right: 4px;"></i>恢复默认
          </el-button>
          <div>
            <el-button @click="showConfigDialog = false">取消</el-button>
            <el-button type="primary" @click="handleSaveConfig" :loading="configSaving" :disabled="configFiles.length === 0">
              <i class="bi bi-check-lg" style="margin-right: 4px;"></i>保存并重载
            </el-button>
          </div>
        </div>
      </template>
    </el-dialog>

    <!-- API 可视化选择器弹窗 -->
    <el-dialog v-model="showApiSelectorDialog" title="选择要保护的 API" width="860px" :close-on-click-modal="false" append-to-body destroy-on-close>
      <div class="api-selector-toolbar">
        <el-input v-model="apiSearchKeyword" placeholder="搜索功能名称或 API 路径..." clearable style="width:280px">
          <template #prefix><i class="bi bi-search"></i></template>
        </el-input>
        <el-select v-model="apiFilterModule" placeholder="所有模块" clearable style="width:140px;margin-left:10px">
          <el-option v-for="m in apiModuleList" :key="m" :label="m" :value="m" />
        </el-select>
        <el-select v-model="apiFilterMethod" placeholder="所有方法" clearable style="width:110px;margin-left:10px">
          <el-option label="GET" value="GET" />
          <el-option label="POST" value="POST" />
          <el-option label="PUT" value="PUT" />
          <el-option label="DELETE" value="DELETE" />
        </el-select>
      </div>
      <el-table
        ref="apiSelectorTableRef"
        :data="filteredAvailableApis"
        size="small"
        stripe
        max-height="400"
        style="width:100%;margin-top:12px"
        @selection-change="handleApiSelectionChange"
      >
        <el-table-column type="selection" width="44" />
        <el-table-column label="功能名称" min-width="200">
          <template #default="{ row }">
            <div>
              <span style="font-weight:500">{{ row.actionDisplayName || row.action }}</span>
              <br>
              <span style="font-size:11px;color:#909399">{{ row.controllerDisplayName || row.controller }}</span>
            </div>
          </template>
        </el-table-column>
        <el-table-column label="方法" width="70">
          <template #default="{ row }">
            <el-tag :type="row.method === 'GET' ? 'success' : row.method === 'POST' ? 'primary' : row.method === 'DELETE' ? 'danger' : 'warning'" size="small" effect="dark">{{ row.method }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="API 路径" min-width="260">
          <template #default="{ row }">
            <code style="font-size:12px;color:#606266">{{ row.path }}</code>
          </template>
        </el-table-column>
        <el-table-column label="所属" width="100">
          <template #default="{ row }">
            <el-tag size="small" effect="plain">{{ row.module }}</el-tag>
          </template>
        </el-table-column>
      </el-table>
      <div style="margin-top:8px;color:#909399;font-size:12px">
        已选 {{ apiSelectedRows.length }} 个，共 {{ filteredAvailableApis.length }} 个可选
      </div>
      <template #footer>
        <el-button @click="showApiSelectorDialog = false">取消</el-button>
        <el-button type="primary" :disabled="apiSelectedRows.length === 0" @click="confirmApiSelection">
          确认添加 {{ apiSelectedRows.length }} 个 API
        </el-button>
      </template>
    </el-dialog>

    <!-- NPM 依赖管理对话框 -->
    <el-dialog v-model="showNpmDepsDialog" :title="`前端依赖管理 - ${npmDepsModuleName}`" width="600px" @close="resetNpmDeps">
      <div v-loading="npmDepsLoading">
        <div v-if="npmDepsList.length === 0 && !npmDepsLoading" style="text-align:center; padding:24px; color:#909399;">
          该模块没有声明前端 npm 依赖
        </div>
        <el-table v-else :data="npmDepsList" size="small" border>
          <el-table-column label="包名" prop="name" min-width="140" />
          <el-table-column label="要求版本" prop="requiredVersion" width="110" />
          <el-table-column label="说明" prop="description" min-width="140" />
          <el-table-column label="状态" width="120" align="center">
            <template #default="{ row }">
              <el-tag v-if="row.installed" type="success" size="small">已安装 v{{ row.installedVersion }}</el-tag>
              <el-tag v-else type="danger" size="small">未安装</el-tag>
            </template>
          </el-table-column>
        </el-table>
        <div v-if="npmInstallResult" style="margin-top:12px;">
          <el-alert :type="npmInstallResult.ok ? 'success' : 'warning'" :title="npmInstallResult.message" :closable="false" show-icon>
            <div v-if="npmInstallResult.installed?.length" style="margin-top:4px; font-size:12px;">
              <div v-for="item in npmInstallResult.installed" :key="item">{{ item }}</div>
            </div>
            <div v-if="npmInstallResult.errors?.length" style="margin-top:4px; font-size:12px; color:#ef4444;">
              <div v-for="err in npmInstallResult.errors" :key="err">{{ err }}</div>
            </div>
          </el-alert>
        </div>
      </div>
      <template #footer>
        <el-button @click="showNpmDepsDialog = false">关闭</el-button>
        <el-button type="primary" @click="handleInstallNpmDeps" :loading="npmDepsInstalling" :disabled="npmDepsList.length === 0 || npmDepsList.every(d => d.installed)">
          <i class="bi bi-box-arrow-down" style="margin-right:4px;"></i>
          安装全部依赖
        </el-button>
      </template>
    </el-dialog>

    <!--
      重启 API 进程的全屏进度遮罩。
      用 Teleport 挂到 body 避免被祖先的 overflow/transform 裁剪；
      visible 由 handleRestartApiProcess 接管，phase 驱动图标/颜色/进度条状态。
      用户操作动线：确认重启 → 看见这个遮罩 → 进度条平滑推进 → ready 倒计时 → 自动整页刷新。
      timeout / failed 阶段保留「关闭」按钮让用户从遮罩退出去手动处理。
    -->
    <Teleport to="body">
      <Transition name="process-overlay-fade">
        <div v-if="processOverlay.visible" class="process-overlay" :class="`is-${processOverlay.phase}`">
          <div class="process-overlay-card">
            <!-- 顶部图标：进行中的阶段显示旋转的齿轮，ready 显示对勾，timeout/failed 显示叹号 -->
            <div class="process-overlay-icon-wrap">
              <div class="process-overlay-icon" :class="`icon-${processOverlay.phase}`">
                <i v-if="processOverlay.phase === 'ready'" class="bi bi-check-lg"></i>
                <i v-else-if="processOverlay.phase === 'timeout' || processOverlay.phase === 'failed'" class="bi bi-exclamation-triangle"></i>
                <i v-else class="bi bi-arrow-repeat"></i>
              </div>
              <div class="process-overlay-icon-ring" v-if="['requesting','stopping','waiting'].includes(processOverlay.phase)"></div>
            </div>

            <h2 class="process-overlay-title">{{ processOverlay.title }}</h2>
            <p class="process-overlay-subtitle">{{ processOverlay.subtitle }}</p>

            <!-- 阶段步骤指示器：让用户看清当前在哪一步 -->
            <div class="process-overlay-steps">
              <div class="step" :class="{
                done: ['stopping','waiting','ready'].includes(processOverlay.phase),
                active: processOverlay.phase === 'requesting'
              }">
                <span class="step-dot"></span>
                <span class="step-label">{{ processOverlay.steps[0] }}</span>
              </div>
              <div class="step-line" :class="{ done: ['stopping','waiting','ready'].includes(processOverlay.phase) }"></div>
              <div class="step" :class="{
                done: ['waiting','ready'].includes(processOverlay.phase),
                active: processOverlay.phase === 'stopping'
              }">
                <span class="step-dot"></span>
                <span class="step-label">{{ processOverlay.steps[1] }}</span>
              </div>
              <div class="step-line" :class="{ done: ['waiting','ready'].includes(processOverlay.phase) }"></div>
              <div class="step" :class="{
                done: processOverlay.phase === 'ready',
                active: processOverlay.phase === 'waiting'
              }">
                <span class="step-dot"></span>
                <span class="step-label">{{ processOverlay.steps[2] }}</span>
              </div>
              <div class="step-line" :class="{ done: processOverlay.phase === 'ready' }"></div>
              <div class="step" :class="{ active: processOverlay.phase === 'ready' }">
                <span class="step-dot"></span>
                <span class="step-label">{{ processOverlay.steps[3] }}</span>
              </div>
            </div>

            <!-- 进度条：渐变填充 + 顶部高光，ready 时变为绿色，timeout/failed 时变为红色 -->
            <div class="process-overlay-progress">
              <div class="process-overlay-progress-bar" :style="{ width: processOverlay.percent + '%' }">
                <div class="process-overlay-progress-shine"></div>
              </div>
            </div>
            <div class="process-overlay-meta">
              <span class="meta-percent">{{ Math.floor(processOverlay.percent) }}%</span>
              <span class="meta-divider">·</span>
              <span class="meta-elapsed">已用 {{ processOverlay.elapsed }} 秒</span>
              <template v-if="processOverlay.phase === 'ready' && processOverlay.countdown > 0">
                <span class="meta-divider">·</span>
                <span class="meta-countdown">{{ processOverlay.countdown }} 秒后自动刷新</span>
              </template>
            </div>

            <p class="process-overlay-hint">{{ processOverlay.hint }}</p>
            <p v-if="processOverlay.errorMessage" class="process-overlay-error">{{ processOverlay.errorMessage }}</p>

            <!-- 异常态保留关闭按钮，避免遮罩永久卡住 -->
            <div v-if="processOverlay.phase === 'timeout' || processOverlay.phase === 'failed'" class="process-overlay-actions">
              <el-button type="primary" @click="closeRestartOverlay">关闭</el-button>
            </div>
          </div>
        </div>
      </Transition>
    </Teleport>
  </div>
</template>


<script setup lang="ts">
import { ref, computed, onMounted, reactive, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import QRCode from 'qrcode'
import { ElMessage, ElMessageBox, type UploadInstance, type UploadFile } from 'element-plus'
import { useMenuStore } from '../../../stores/menu'
import * as moduleApi from '../../../api/module'
import http from '../../../api/http'
import * as pluginStoreApi from '../../../api/plugin-store'
import type { ModuleInfo, ModuleStatus, UploadValidationResult, InstallResult, PackageResult, PackageableModule, EnvironmentInfo, NormalizedConfig, ConfigItem } from '../../../api/module'
import { normalizeDirectStoreLoginResult, normalizeStoreLoginMessage, normalizeStoreUserInfo } from './storeLoginCallback.utils'
import StoreCaptchaPanel from './StoreCaptchaPanel.vue'

// 路由
const router = useRouter()

const loading = ref(false)
const modules = ref<ModuleInfo[]>([])
const searchQuery = ref('')
const filterStatus = ref('')
const actionLoading = reactive<Record<string, boolean>>({})
const envInfo = ref<EnvironmentInfo | null>(null)

/**
 * 通用 phase（同时被「重启服务」与「插件安装」复用）：
 * - requesting：流程的第 1 阶段，对应步骤指示器第 1 个圆点
 * - stopping  ：流程的第 2 阶段，第 2 个圆点
 * - waiting   ：流程的第 3 阶段，第 3 个圆点
 * - ready     ：流程的第 4 阶段（成功），驱动 85→100% 倒计时收尾
 * - timeout   ：超时，红色态停在 ≤92%
 * - failed    ：调度/接口失败，红色态停在 ≤92%
 *
 * 不同业务场景下 4 个阶段的中文 label 由 processOverlay.steps 注入；
 * 进度条推进规则统一由 startProcessTicker 控制（分段线性 + 上限钳制）。
 */
type ProcessPhase = 'requesting' | 'stopping' | 'waiting' | 'ready' | 'timeout' | 'failed'
const processOverlay = reactive({
  visible: false,
  phase: 'requesting' as ProcessPhase,
  percent: 0,
  elapsed: 0,
  countdown: 0,
  title: '',
  subtitle: '',
  hint: '',
  errorMessage: '',
  // 4 个步骤指示器的标签，按业务定制；默认按「重启服务」语义初始化
  steps: ['发起请求', '停止进程', '等待恢复', '完成刷新'] as [string, string, string, string]
})
let restartTickTimer: number | null = null
let restartStartedAt = 0
let restartReadyAt = 0  // 进入 ready 阶段的时间戳，用于驱动 85→100 的收尾动画

// 分页状态
const currentPage = ref(1)
const pageSize = ref(10)

// 主 Tab 状态
const activeMainTab = ref('local')

// 插件商店状态
const storeConfig = ref<pluginStoreApi.PluginStoreConfig>({ serverUrl: '', enabled: false })
const showStoreTab = computed(() => storeConfig.value.enabled && !!storeConfig.value.serverUrl)
const storeLoggedIn = ref(false)
const storeToken = ref('')
const storeLoading = ref(false)
// 记录加载失败的封面图（按 group.id 索引），用于回退到占位图标
const brokenCovers = reactive(new Set<string>())
const availablePlugins = ref<pluginStoreApi.AvailablePlugin[]>([])
const storeInstalling = reactive<Record<string, boolean>>({})
const showStoreInstallProgress = ref(false)
const storeInstallProgress = ref(0)
const storeInstallMessage = ref('')
const storeInstallDone = ref(false)
const showStoreLoginDialog = ref(false)
const storeLoginSubmitting = ref(false)
const storeLoginFormRef = ref()
const storeLoginForm = reactive({
  userName: '',
  password: ''
})
const storeLoginRules = {
  userName: [{ required: true, message: '请输入商城账号 / 邮箱 / 手机号', trigger: 'blur' }],
  password: [{ required: true, message: '请输入商城登录密码', trigger: 'blur' }]
}

// 商城登录验证码挑战相关状态：远端要求验证码时（业务码 449）显示在登录对话框中
const storeCaptchaRequired = ref(false)
const storeCaptchaKey = ref(0) // 重新挂载验证码组件以触发刷新
const storeCaptchaTip = ref('')
let storeCaptchaResolveFn: ((token: string | null) => void) | null = null

/** 等待用户完成验证码：返回 token 或 null（用户取消/对话框关闭） */
function waitForStoreCaptchaToken(message?: string): Promise<string | null> {
  // 终结上一个未完成的挑战
  if (storeCaptchaResolveFn) {
    try { storeCaptchaResolveFn(null) } catch { /* ignore */ }
    storeCaptchaResolveFn = null
  }
  storeCaptchaRequired.value = true
  storeCaptchaKey.value += 1
  storeCaptchaTip.value = message || '远端商城要求完成安全验证后再登录，请拖动滑块。'
  return new Promise<string | null>(resolve => {
    storeCaptchaResolveFn = resolve
  })
}

const onStoreCaptchaVerified = (token: string) => {
  if (storeCaptchaResolveFn) {
    const fn = storeCaptchaResolveFn
    storeCaptchaResolveFn = null
    fn(token)
  }
}

const onStoreCaptchaFail = (message: string) => {
  storeCaptchaTip.value = message || '验证失败，请重试'
}

const onStoreLoginDialogClosed = () => {
  // 对话框关闭时若仍有等待中的挑战，置 null 让登录链路释放
  if (storeCaptchaResolveFn) {
    const fn = storeCaptchaResolveFn
    storeCaptchaResolveFn = null
    fn(null)
  }
  storeCaptchaRequired.value = false
  storeCaptchaTip.value = ''
}

// 新增状态
const storeCategory = ref('')
const storeKeyword = ref('')
const storeCategories = ref<{value: string, label: string}[]>([])

// 插件商店分页状态：对齐远端 /api/plugin-store/portal/items 的分页语义
const storePage = ref(1)
const storePageSize = ref(12)
const storeTotal = ref(0)

/**
 * 远端 /api/plugin-store/categories/enabled 下发的启用分类清单。
 * 用作 plugin.category(code) → 中文名 的映射表，替代旧的硬编码 getCategoryLabel。
 * 仍保留 getCategoryLabel 作为 fallback，兼容远端未配置分类中文名时的展示。
 */
const remoteCategoryMap = reactive<Record<string, string>>({})

const formatStoreUserName = (name: string | undefined): string => {
  if (!name) return '已登录'
  // 如果全是数字且长度超过 10，认为它是雪花ID，脱敏展示
  if (/^\d{10,}$/.test(name)) {
    return `用户_${name.substring(name.length - 4)}`
  }
  return name
}
const storeUserInfo = ref<pluginStoreApi.StoreUserInfo | null>(null)
const storePurchasing = reactive<Record<string, boolean>>({})
const showPurchaseDialog = ref(false)
const purchaseTarget = ref<pluginStoreApi.AvailablePlugin | null>(null)
const purchaseLoading = ref(false)
// 商城登录弹窗句柄（不再在本地渲染登录表单）
let storeLoginPopup: Window | null = null
let storeLoginPopupTimer: any = null
let storeLoginState = ''
let storeLoginBroadcastChannel: BroadcastChannel | null = null
// 始终生效的 BroadcastChannel：与弹窗流程无关，覆盖中继页广播登录态的兜底通道
let persistentBroadcastChannel: BroadcastChannel | null = null
// 标记是否有弹窗登录流程正在进行（供 focus handler 判断，不依赖 popup 引用存活）
let storeLoginFlowActive = false
const storeLoginMessageHandler = (event: MessageEvent) => handleStoreLoginMessage(event)

// storage 事件处理器：接收中继页写入 localStorage 时触发的跨窗口广播
// 同时监听 token 和 ts 两个 key，避免写入顺序竞态导致首个事件到达时另一个 key 尚未落盘
const storeLoginStorageHandler = (event: StorageEvent) => {
  if (storeLoggedIn.value) return
  if (event.key !== 'ginkgo_store_login_ts' && event.key !== 'ginkgo_store_token') return
  if (!event.newValue) return
  applyStoreLoginFromLocalStorage()
}

/**
 * BroadcastChannel 投递通道（中继页 / 本页之间的第三条同源广播路径）。
 * - 用于在 storage 事件被浏览器节流（如 Chrome 在隐藏标签页下批量延迟）时仍可同步登录态。
 * - 优先从消息体直接取 token，避免对 localStorage 写入时序的依赖。
 */
const storeLoginBroadcastHandler = (event: MessageEvent) => {
  if (storeLoggedIn.value) return
  const data = event?.data
  if (!data || typeof data !== 'object') return
  if (data.type !== 'ginkgo-store-login') return
  // state 校验（与 postMessage 逻辑对齐）
  if (storeLoginState && data.state && data.state !== storeLoginState) return
  // 消息体自带 token 时直接走快速通道，省去 localStorage 读取
  if (data.token && typeof data.token === 'string') {
    const msg = normalizeStoreLoginMessage(data)
    if (msg) {
      storeToken.value = msg.token
      storeLoggedIn.value = true
      localStorage.setItem('ginkgo_store_token', msg.token)
      try { storeLoginPopup?.close() } catch { /* ignore */ }
      cleanupStoreLoginPopup()
      ElMessage.success('商城登录成功')
      // 异步拉取用户信息并刷新列表
      ;(async () => {
        try {
          storeUserInfo.value = msg.user || await pluginStoreApi.getStoreUserInfo(msg.token)
        } catch { /* ignore */ }
        await loadAvailablePlugins()
        if (pendingAction.value) {
          const action = pendingAction.value
          pendingAction.value = null
          if (action.type === 'purchase') handlePurchasePlugin(action.plugin)
          else if (action.type === 'install') handleInstallFromStore(action.plugin)
        }
      })()
      return
    }
  }
  // 兜底：消息体无 token 时从 localStorage 恢复
  applyStoreLoginFromLocalStorage()
}

/**
 * 窗口 focus / visibilitychange 检测：第四条恢复通道。
 * 弹窗关闭后用户回到主窗口（点击任务栏 / Alt-Tab）时主动检查 localStorage，
 * 覆盖 storage 事件 / BroadcastChannel / postMessage 全部被浏览器节流或丢弃的极端场景。
 */
const storeLoginFocusHandler = () => {
  if (storeLoggedIn.value) return
  // 只在弹窗登录流程进行中才检查（避免无弹窗时每次 focus 都白读 localStorage）
  if (!storeLoginFlowActive) return
  const cachedToken = localStorage.getItem('ginkgo_store_token')
  if (!cachedToken) return
  // 放宽条件：不要求 ts 存在（中继页可能写入顺序竞态 or 已被清理），直接用 force 模式
  applyStoreLoginFromLocalStorage(true)
}

/**
 * 从 localStorage 恢复商城登录状态（由中继页写入）。
 * 供 storage 事件、BroadcastChannel 与弹窗关闭后的兜底检查共用。
 *
 * @param force 当为 true 时跳过 60s 时间戳新鲜度限制，仅校验 token 是否仍有效。
 *   适用于「弹窗已关闭」之后的二次确认：用户可能在登录页停留超过 60s，此时
 *   ts 字段已过期但 token 仍是新鲜的，应当继续走 getStoreUserInfo 校验后接受。
 */
const applyStoreLoginFromLocalStorage = async (force = false) => {
  const token = localStorage.getItem('ginkgo_store_token')
  if (!token) return false
  if (!force) {
    const ts = localStorage.getItem('ginkgo_store_login_ts')
    if (!ts) return false
    // 默认仅接受 60 秒内写入的 fresh token，避免误用旧数据
    if (Date.now() - parseInt(ts) > 60000) return false
  }
  const state = localStorage.getItem('ginkgo_store_login_state')
  if (storeLoginState && state && state !== storeLoginState) return false
  if (storeLoggedIn.value) return true // 已处理

  storeToken.value = token
  storeLoggedIn.value = true

  try { storeLoginPopup?.close() } catch { /* ignore */ }
  cleanupStoreLoginPopup()

  ElMessage.success('商城登录成功')

  // 读取用户信息
  try {
    const userStr = localStorage.getItem('ginkgo_store_user')
    if (userStr) {
      const u = JSON.parse(userStr)
      storeUserInfo.value = normalizeStoreUserInfo(u) || null
    } else {
      storeUserInfo.value = await pluginStoreApi.getStoreUserInfo(token)
    }
  } catch { /* 用户信息获取失败不阻塞 */ }

  // 清理中继页写入的临时数据
  localStorage.removeItem('ginkgo_store_user')
  localStorage.removeItem('ginkgo_store_login_ts')
  localStorage.removeItem('ginkgo_store_login_state')

  await loadAvailablePlugins()

  // 执行登录前的待定动作
  if (pendingAction.value) {
    const action = pendingAction.value
    pendingAction.value = null
    if (action.type === 'purchase') {
      handlePurchasePlugin(action.plugin)
    } else if (action.type === 'install') {
      handleInstallFromStore(action.plugin)
    }
  }
  return true
}

const purchaseChannel = ref('wechat')
// 远端商城实际启用的支付渠道代码列表，购买对话框打开时刷新
const availablePaymentChannels = ref<string[]>([])
const paymentChannelsLoading = ref(false)
const showPaymentDialog = ref(false)
const paymentQrCodeData = ref('')
const paymentPollingMsg = ref('')

/**
 * 支付渠道代码 → 显示元信息（标签 / 图标 / 主题色）。
 * 这是稳定的渠道代码集，远端通过 IPaymentService.GetAvailableChannelsAsync 返回。
 * 未识别的代码兜底为代码本身（避免破图），避免新渠道接入后前端"看不见"。
 */
const PAYMENT_CHANNEL_META: Record<string, { label: string; iconClass: string; color: string }> = {
  wechat: { label: '微信支付', iconClass: 'bi bi-wechat', color: '#07c160' },
  alipay: { label: '支付宝', iconClass: 'bi bi-alipay', color: '#1677ff' },
  unionpay: { label: '银联', iconClass: 'bi bi-credit-card', color: '#e60012' },
}
const getPaymentChannelMeta = (code: string) => {
  return PAYMENT_CHANNEL_META[code] || { label: code, iconClass: 'bi bi-credit-card', color: '#909399' }
}

/**
 * 拉取远端商城启用的支付渠道。
 * 失败或返回空时 availablePaymentChannels 为空，购买按钮区会展示对应的提示。
 */
const loadAvailablePaymentChannels = async () => {
  paymentChannelsLoading.value = true
  try {
    const list = await pluginStoreApi.getPaymentChannels()
    availablePaymentChannels.value = Array.isArray(list) ? list : []
    // 当前选择的渠道若已被远端禁用，自动重置为第一个可用项
    if (availablePaymentChannels.value.length > 0
      && !availablePaymentChannels.value.includes(purchaseChannel.value)) {
      purchaseChannel.value = availablePaymentChannels.value[0]
    }
  } catch (e) {
    console.warn('[PluginStore] 获取支付渠道失败', e)
    availablePaymentChannels.value = []
  } finally {
    paymentChannelsLoading.value = false
  }
}
const paymentOrderNo = ref('')
let paymentPollInterval: any = null

/**
 * 支付成功后需要自动跳转到「下载安装」流程的目标插件。
 * 在 confirmPurchase 拿到 payParams 后立刻锁定，轮询命中 status=paid 时弹窗提示并复用
 * handleInstallFromStore（后者会再走一次远端 /downloads/token，由远端按授权重新核验，
 * 即"支付后回调发授权 → 前端再点下载会被远端二次校验"的安全闭环）。
 */
const pendingInstallAfterPay = ref<pluginStoreApi.AvailablePlugin | null>(null)

const reloadFrontendPluginRuntime = async () => {
  try {
    const { initializePluginSystem } = await import('../../../plugins')
    await initializePluginSystem()
    window.dispatchEvent(new CustomEvent('ginkgo:plugins:reloaded'))
  } catch (error) {
  }
}

const stopPaymentPolling = () => {
  if (paymentPollInterval) {
    clearInterval(paymentPollInterval)
    paymentPollInterval = null
  }
}

/**
 * 支付倒计时（与门户 CheckoutPage.vue 对齐：扫码支付 15 分钟到期自动关闭弹窗）。
 * <p>
 * 用户的支付二维码在支付网关侧通常也是 ~15 分钟有效期，到期后即便扫码也支付不了，
 * 因此前端到期就该主动结束本轮支付流程，提示用户重新下单。
 * </p>
 */
const PAYMENT_COUNTDOWN_SECONDS = 900
const paymentCountdown = ref(PAYMENT_COUNTDOWN_SECONDS)
let paymentCountdownTimer: ReturnType<typeof setInterval> | null = null

const paymentCountdownText = computed(() => {
  const total = paymentCountdown.value
  const m = Math.floor(total / 60)
  const s = total % 60
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
})

const stopPaymentCountdown = () => {
  if (paymentCountdownTimer) {
    clearInterval(paymentCountdownTimer)
    paymentCountdownTimer = null
  }
}

const startPaymentCountdown = () => {
  stopPaymentCountdown()
  paymentCountdown.value = PAYMENT_COUNTDOWN_SECONDS
  paymentCountdownTimer = setInterval(() => {
    paymentCountdown.value--
    if (paymentCountdown.value <= 0) {
      stopPaymentCountdown()
      stopPaymentPolling()
      showPaymentDialog.value = false
      ElMessage.warning('支付超时，请重新下单')
    }
  }, 1000)
}

/** 支付对话框关闭时统一处理：停止轮询 + 倒计时 + 刷新列表（无论是否支付成功都重新拉取最新购买状态） */
const onPaymentDialogClose = async () => {
  stopPaymentPolling()
  stopPaymentCountdown()
  pendingInstallAfterPay.value = null
  // 关闭支付对话框后始终刷新列表，确保即使回调延迟到达也能在下次看到最新状态
  await loadAvailablePlugins()
}

/**
 * 「我已完成支付」按钮：手动触发一次主动查询支付网关。
 * <p>
 * 调 <code>checkPaymentStatus</code>（远端会主动调微信/支付宝网关反查付款状态）。后端限流策略
 * <code>payment-check</code>（60 次/分钟/IP）保护不会被恶意高频打三方网关。
 * </p>
 * <p>
 * 与轮询里的「每 3 轮主动查一次」如出一辙，只是这里是用户人工触发，立即打一次。
 * </p>
 */
const paymentChecking = ref(false)
const handleManualPaymentCheck = async () => {
  if (!paymentOrderNo.value || paymentChecking.value) return
  paymentChecking.value = true
  try {
    const order = await pluginStoreApi.checkPaymentStatus(paymentOrderNo.value, storeToken.value)
    if (order && order.status === 'paid') {
      stopPaymentPolling()
      await handlePaymentSuccess()
    } else {
      ElMessage.warning('支付网关尚未确认到款，请稍后再试或检查支付是否完成')
    }
  } catch {
    ElMessage.warning('查询支付状态失败，请稍后再试')
  } finally {
    paymentChecking.value = false
  }
}

/**
 * 支付确认后的统一处理流程（轮询检测到 paid / 手动确认支付后共用）。
 * 关闭支付对话框 → 刷新用户信息 → 刷新插件列表 → 弹窗引导安装。
 */
const handlePaymentSuccess = async () => {
  ElMessage.success('支付成功！')
  // 停掉所有定时器，避免支付成功后倒计时仍在跑触发"支付超时"误提示
  stopPaymentPolling()
  stopPaymentCountdown()
  showPaymentDialog.value = false

  // 同步用户信息（余额变化等），允许失败
  try {
    if (storeToken.value) {
      storeUserInfo.value = await pluginStoreApi.getStoreUserInfo(storeToken.value)
    }
  } catch { /* ignore */ }

  // 刷新列表（让 plugin.purchased 标记同步成 true）
  await loadAvailablePlugins()

  // 同步版本选择对话框中的 editionGroup 引用到最新 computed 结果，
  // 使「已购买」标记和按钮立即生效，无需关闭再打开对话框。
  if (editionGroup.value) {
    const freshGroup = groupedPlugins.value.find(g => g.id === editionGroup.value!.id)
    if (freshGroup) editionGroup.value = freshGroup
  }

  // 转入下载流程：支付成功后远端已通过 StorePaymentCallbackHandler 自动签发授权 →
  // 这里复用 handleInstallFromStore，其内部走 /api/system/plugin-store/install →
  // 远端 /api/plugin-store/downloads/token 会按当前用户授权再次校验下载安装权限，
  // 形成"支付回调签发授权 → 安装时二次校验"的安全闭环。
  const justPaid = pendingInstallAfterPay.value
  pendingInstallAfterPay.value = null
  if (justPaid) {
    try {
      await ElMessageBox.confirm(
        `已成功购买「${justPaid.name}${justPaid.editionName ? ' - ' + justPaid.editionName : ''}」，是否立即下载并安装？`,
        '支付成功',
        {
          confirmButtonText: '立即下载安装',
          cancelButtonText: '稍后手动安装',
          type: 'success'
        }
      )
      // 使用列表刷新后的最新对象（保留服务端最新的 purchased 标记），未找到则回退到原对象
      const refreshed = availablePlugins.value.find(p =>
        p.id === justPaid.id && (p.editionId || '') === (justPaid.editionId || '')
      ) || justPaid
      await handleInstallFromStore(refreshed)
    } catch {
      // 用户选择稍后安装：列表已经标记"已购买"，可以直接点击「下载安装」按钮
    }
  }
}


/**
 * 支付状态轮询：
 * <p>
 * - 每 3s 走一次 <code>getStoreOrder</code>（轻量本地 DB 查询）。回调正常到达时这里就能立即看到 paid。
 * - 每 N 轮（默认 3 轮 ≈ 9s）才走一次 <code>checkPaymentStatus</code>（主动查支付网关）兜底回调失败的场景，
 *   避免每轮都打第三方支付网关导致风控 / 商户限流。
 * - 用户也可点「我已完成支付」按钮立即触发一次 checkPaymentStatus。
 * </p>
 */
const POLL_INTERVAL_MS = 3000
const ACTIVE_CHECK_EVERY_N_POLLS = 3
let pollTickCount = 0

const startPaymentPolling = () => {
  stopPaymentPolling()
  pollTickCount = 0
  paymentPollInterval = setInterval(async () => {
    if (!paymentOrderNo.value || !showPaymentDialog.value) {
      stopPaymentPolling()
      return
    }
    pollTickCount++
    try {
      // 优先走轻量轮询接口；周期性触发主动查询支付网关作为回调兜底
      const useActiveCheck = pollTickCount % ACTIVE_CHECK_EVERY_N_POLLS === 0
      const order = useActiveCheck
        ? await pluginStoreApi.checkPaymentStatus(paymentOrderNo.value, storeToken.value)
        : await pluginStoreApi.getStoreOrder(paymentOrderNo.value, storeToken.value)
      if (order && order.status === 'paid') {
        stopPaymentPolling()
        await handlePaymentSuccess()
      }
    } catch {
      // ignore network errors during polling
    }
  }, POLL_INTERVAL_MS)
}

onUnmounted(() => {
  stopPaymentPolling()
  stopPaymentCountdown()
  cleanupStoreLoginPopup()
  window.removeEventListener('storage', persistentStorageHandler)
  document.removeEventListener('visibilitychange', persistentVisibilityHandler)
  try { persistentBroadcastChannel?.close() } catch { /* ignore */ }
  persistentBroadcastChannel = null
})

const showRestartRequiredNotice = async (actionName: string) => {
  await ElMessageBox.alert(
    `模块${actionName}已完成，请重启后端服务使变更完全生效。`,
    '需要重启服务',
    { type: 'warning', confirmButtonText: '我知道了' }
  )
}

// 版本选择对话框状态
interface GroupedPlugin {
  id: string
  name: string
  description: string
  category?: string
  author?: string
  installed: boolean
  minPrice: number
  hasFree: boolean
  editions: pluginStoreApi.AvailablePlugin[]
  /** 当前本地实际安装的版本（按版本号匹配），null 表示未安装 */
  installedEdition: pluginStoreApi.AvailablePlugin | null
  /** 是否存在比当前安装版本更高价位的可升级版本 */
  hasUpgradeAvailable: boolean
}
const showEditionDialog = ref(false)
const editionGroup = ref<GroupedPlugin | null>(null)
const editionDialogTitle = computed(() => editionGroup.value ? `${editionGroup.value.name} - 选择版本` : '选择版本')

// 将插件按 ID 分组，每个插件只显示一张卡片
const groupedPlugins = computed<GroupedPlugin[]>(() => {
  const map = new Map<string, GroupedPlugin>()
  for (const p of availablePlugins.value) {
    const key = p.id
    
    // 根据 ID（或回退按统一名称）检查本地是否已安装此插件
    const localModule = modules.value.find(m => 
      String(m.id).toLowerCase() === String(p.id).toLowerCase() || 
      String(m.name).trim().toLowerCase() === String(p.name).trim().toLowerCase()
    )
    
    // 检查是否安装了此版本（消除 v 前缀，并兼容 0.1 与 0.1.0 这种补零差异）
    const isThisEditionInstalled = localModule ? (() => {
      const formatVer = (v: string) => v ? v.replace(/^v/i, '').trim() : ''
      const mv = formatVer(localModule.version)
      const pv = formatVer(p.version)
      if (mv === pv) return true
      // 如果格式为 1.0.0 和 1 这种，允许前缀匹配
      if (mv.startsWith(pv + '.') || pv.startsWith(mv + '.')) return true
      return false
    })() : false
    
    if (!map.has(key)) {
      map.set(key, {
        id: p.id,
        name: p.name,
        description: p.description,
        category: p.category,
        author: p.author,
        installed: !!localModule, // 只要本地安装了任意版本，则插件组视为已安装
        minPrice: p.price,
        hasFree: p.isFree === true,
        editions: [],
        installedEdition: null,
        hasUpgradeAvailable: false
      })
    }
    const group = map.get(key)!
    
    // 为每个版本构造新的对象，并注入该版本的真实安装状态
    const editionData = { ...p, installed: isThisEditionInstalled }
    group.editions.push(editionData)
    if (isThisEditionInstalled) group.installedEdition = editionData
    
    // 更新聚合信息
    if (p.price < group.minPrice) group.minPrice = p.price
    if (p.isFree) group.hasFree = true
  }

  // 计算每个分组的升级可用性：
  // 基线价 = max(已安装版本价, 已购买版本最高价)。两者都没有时不计算（hasUpgradeAvailable=false）。
  // 只要存在严格高于基线价的版本即认为可升级购买（指引用户去远端官网买更高档位）。
  for (const group of map.values()) {
    let baselinePrice = -1
    if (group.installedEdition) baselinePrice = group.installedEdition.price
    for (const e of group.editions) {
      if (e.purchased && e.price > baselinePrice) baselinePrice = e.price
    }
    if (baselinePrice < 0) continue // 既未安装也未购买，按"未拥有"处理，不展示升级
    group.hasUpgradeAvailable = group.editions.some(e => e.price > baselinePrice)
  }

  return Array.from(map.values())
})
const pendingAction = ref<{ type: 'purchase' | 'install', plugin: pluginStoreApi.AvailablePlugin } | null>(null)

const showInstallDialog = ref(false)
const uploadRef = ref<UploadInstance>()
const fileList = ref<UploadFile[]>([])
const selectedFile = ref<File | null>(null)
const uploadProgress = ref(0)
const installing = ref(false)
const validationResult = ref<UploadValidationResult | null>(null)
const installResult = ref<InstallResult | null>(null)
const installChannel = ref<string>('stable')
const installGrayscaleTenants = ref<string>('')

const showPackageDialog = ref(false)
const packageForm = reactive({ moduleId: '', packageType: 'compiled', exportDbSchema: false, exportDbData: false, sanitizeConfig: true })
const packageableModules = ref<PackageableModule[]>([])
const loadingPackageable = ref(false)
const packaging = ref(false)
const packageProgress = ref(0)
const packageResult = ref<PackageResult | null>(null)

const showStatusDialog = ref(false)
const currentModuleStatus = ref<ModuleStatus | null>(null)

// ============ 灰度发布状态 ============
const showGrayscaleDialog = ref(false)
const grayscaleModuleId = ref('')
const grayscaleModuleName = ref('')
const grayscaleForm = reactive({
  channel: 'beta',
  targetTenantIds: '' as string, // 逗号分隔
  startTime: '' as string,
  endTime: '' as string,
  autoPromote: false
})
const grayscaleSaving = ref(false)
const currentGrayscalePolicy = ref<moduleApi.GrayscalePolicy | null>(null)

// ============ 版本回滚状态 ============
const showRollbackDialog = ref(false)
const rollbackModuleId = ref('')
const rollbackModuleName = ref('')
const rollbackSnapshots = ref<moduleApi.SnapshotMetadata[]>([])
const rollbackLoading = ref(false)
const rollbackExecuting = ref(false)
const selectedSnapshot = ref<string>('')

// 配置相关状态
const showConfigDialog = ref(false)
const configModuleId = ref('')
const configModuleName = ref('')
const configLoading = ref(false)
const configSaving = ref(false)
const configFiles = ref<string[]>([])
const activeConfigFile = ref('')
const activeConfigTab = ref('')
const configData = ref<NormalizedConfig | null>(null)

// ============ 灰度策略缓存（模块列表卡片上显示灰度标识） ============
const grayscalePolicies = ref<Record<string, moduleApi.GrayscalePolicy>>({})
const loadGrayscalePolicies = async () => {
  try {
    const resp = await moduleApi.getGrayscalePolicies()
    grayscalePolicies.value = resp.policies || {}
  } catch { /* 静默失败 */ }
}

const moduleStats = computed(() => {
  const total = modules.value.length
  const enabled = modules.value.filter(m => m.enabled).length
  // 非正常 = 红灯插件，与 isModuleHealthy() 保持同一判定口径，避免列表灯与统计卡口径不一致。
  const unhealthy = modules.value.filter(m => !isModuleHealthy(m)).length
  return { total, enabled, disabled: total - enabled, unhealthy, devMode: modules.value.filter(m => m.isDevMode).length }
})

const filteredModules = computed(() => {
  let result = modules.value
  if (searchQuery.value) {
    const query = searchQuery.value.toLowerCase()
    result = result.filter(m => m.id.toLowerCase().includes(query) || (m.name && m.name.toLowerCase().includes(query)) || (m.title && m.title.toLowerCase().includes(query)))
  }
  if (filterStatus.value === 'enabled') result = result.filter(m => m.enabled)
  else if (filterStatus.value === 'disabled') result = result.filter(m => !m.enabled)
  return result
})

const paginatedModules = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  return filteredModules.value.slice(start, start + pageSize.value)
})

const refreshModules = async () => {
  loading.value = true
  try {
    const [modulesData, envData] = await Promise.all([moduleApi.getInstalledModules(), moduleApi.getEnvironmentInfo()])
    modules.value = modulesData
    envInfo.value = envData
    // 加载灰度策略（不阻塞主流程）
    loadGrayscalePolicies()
  } catch (error: unknown) {
    ElMessage.error(`加载模块列表失败: ${error instanceof Error ? error.message : '加载失败'}`)
  } finally { loading.value = false }
}

const handleEnable = async (mod: ModuleInfo) => {
  actionLoading[mod.id] = true
  try {
    // 检查是否有灰度策略
    let hasGrayscale = false
    try {
      const gResp = await moduleApi.getGrayscalePolicies()
      const gPolicy = gResp.policies?.[mod.id]
      if (gPolicy && gPolicy.channel !== 'stable') {
        hasGrayscale = true
        await ElMessageBox.confirm(
          `该模块当前配置了灰度策略（通道: ${gPolicy.channel}），启用后将仅对指定租户加载。\n是否继续启用？`,
          '灰度模块启用确认',
          { type: 'info', confirmButtonText: '继续启用', cancelButtonText: '取消' }
        )
      }
    } catch (e: unknown) {
      // 用户取消或获取策略失败
      if (e && typeof e === 'object' && 'toString' in e && String(e).includes('cancel')) { actionLoading[mod.id] = false; return }
    }
    // 先尝试热启用（运行时直接加载DLL）
    let result = await moduleApi.hotEnableModule(mod.id)
    if (!result.ok) {
      // 热启用失败（开发模式下常见），降级为普通启用（更新数据库状态+菜单可见性）
      result = await moduleApi.enableModule(mod.id)
    }
    if (result.ok) {
      const msg = hasGrayscale ? '模块已启用（灰度模式，仅部分租户生效）' : result.message
      ElMessage.success(msg)
      mod.enabled = true
      // 刷新菜单缓存，使菜单可见性变更立即生效
      const menuStore = useMenuStore()
      menuStore.clearCache()
      await menuStore.loadMenus(true)
    } else {
      ElMessage.warning(result.message)
    }
  } catch (error: unknown) { ElMessage.error(`启用模块失败: ${error instanceof Error ? error.message : '操作失败'}`) } finally { actionLoading[mod.id] = false }
}

const handleDisable = async (mod: ModuleInfo) => {
  actionLoading[mod.id] = true
  try {
    // 检查是否有灰度策略，禁用时提示
    try {
      const gResp = await moduleApi.getGrayscalePolicies()
      const gPolicy = gResp.policies?.[mod.id]
      const grayscaleNote = gPolicy && gPolicy.channel !== 'stable'
        ? `\n注意：该模块有灰度策略（通道: ${gPolicy.channel}），禁用后灰度策略将暂停，重新启用后恢复。`
        : ''
      await ElMessageBox.confirm(
        `确定要禁用模块「${mod.name || mod.id}」吗？禁用后该模块的功能和菜单将不可用。${grayscaleNote}`,
        '确认禁用',
        { type: 'warning', confirmButtonText: '确认禁用', cancelButtonText: '取消' }
      )
    } catch (e: unknown) {
      // 用户取消
      actionLoading[mod.id] = false; return
    }
    // 先尝试热禁用
    let result = await moduleApi.hotDisableModule(mod.id)
    if (!result.ok) {
      // 热禁用失败，降级为普通禁用（更新数据库状态+菜单可见性）
      result = await moduleApi.disableModule(mod.id)
    }
    if (result.ok) {
      ElMessage.success(result.message)
      mod.enabled = false
      // 刷新菜单缓存，使菜单隐藏立即生效
      const menuStore = useMenuStore()
      menuStore.clearCache()
      await menuStore.loadMenus(true)
    } else {
      ElMessage.warning(result.message)
    }
  } catch (error: unknown) { ElMessage.error(`禁用模块失败: ${error instanceof Error ? error.message : '操作失败'}`) } finally { actionLoading[mod.id] = false }
}

const handleModuleAction = async (command: string, mod: ModuleInfo) => {
  switch (command) {
    case 'test': 
      if (mod.testRoute) { 
        const resolved = router.resolve({ name: mod.testRoute })
        window.open(resolved.href, '_blank') 
      } 
      break
    case 'config': await handleOpenConfig(mod); break
    case 'hot-reload': await handleHotReload(mod); break
    case 'status': await handleViewStatus(mod); break
    case 'package': packageForm.moduleId = mod.id; showPackageDialog.value = true; await loadPackageableModules(); break
    case 'install-npm-deps': await handleOpenNpmDeps(mod); break
    case 'run-install-sql': await handleRunInstallSql(mod); break
    case 'reset-menus': await handleResetMenus(mod); break
    case 'remove-menus': await handleRemoveMenus(mod); break
    case 'uninstall': await handleUninstall(mod); break
    case 'dry-run': await handleDryRun(mod); break
    case 'grayscale': await handleOpenGrayscale(mod); break
    case 'rollback': await handleOpenRollback(mod); break
  }
}

/**
 * 「重载」按钮处理：
 * - 开发环境：点击后会先弹确认框，确认后调用 /api/v1/modules/restart-process 触发整个 API 进程重启，
 *   随后轮询 /api/v1/modules/environment 等待服务恢复，最后用 location.assign 整页刷新让 Vite 重扫
 *   import.meta.glob 收集到新插件目录。该路径与点 mod 无关，会重新扫描所有 modules，
 *   因此 mod 参数仅用于在确认框文案中提示用户「触发重载来源于哪个模块」。
 * - 生产环境：保持原 ALC 热重载行为，仅重载该模块的 DLL。
 */
const handleHotReload = async (mod: ModuleInfo) => {
  if (envInfo.value?.isDevelopment) {
    await handleRestartApiProcess(mod)
    return
  }

  actionLoading[mod.id] = true
  try {
    const result = await moduleApi.hotReloadModule(mod.id)
    if (result.ok) ElMessage.success(result.message); else ElMessage.warning(result.message)
  } catch (error: unknown) { ElMessage.error(`热重载失败: ${error instanceof Error ? error.message : '操作失败'}`) } finally { actionLoading[mod.id] = false }
}

/**
 * 启动重启遮罩计时器：每 200ms 推进 percent 与 elapsed。
 * 进度推进策略采用「分段线性 + 上限钳制」：
 * - 在 ready/timeout/failed 之前，percent 永远不会超过 85%，避免给用户"马上完成"的假象后又卡住；
 * - 进入 ready 后改由 readyProgress 驱动 85→100，配合倒计时收尾。
 */
const startRestartTicker = () => {
  stopRestartTicker()
  restartStartedAt = Date.now()
  restartTickTimer = window.setInterval(() => {
    const elapsedMs = Date.now() - restartStartedAt
    processOverlay.elapsed = Math.floor(elapsedMs / 1000)

    if (processOverlay.phase === 'ready') {
      // 收尾段：3 秒内匀速从 85% → 100%
      const readyMs = Date.now() - restartReadyAt
      processOverlay.percent = Math.min(100, 85 + (readyMs / 3000) * 15)
      processOverlay.countdown = Math.max(0, Math.ceil((3000 - readyMs) / 1000))
      return
    }
    if (processOverlay.phase === 'timeout' || processOverlay.phase === 'failed') {
      // 卡在最后探测时的进度，但不超过 92%，让红色警告卡片更显眼
      processOverlay.percent = Math.min(92, processOverlay.percent)
      return
    }

    // 正常推进段：分阶段累计
    let target = 0
    if (processOverlay.phase === 'requesting') {
      // 0-2s 内 0 → 10
      target = Math.min(10, (elapsedMs / 2000) * 10)
    } else if (processOverlay.phase === 'stopping') {
      // 进入 stopping 时已经 ~10%，再用 ~3 秒推到 30%
      target = Math.min(30, 10 + ((elapsedMs - 2000) / 3000) * 20)
    } else if (processOverlay.phase === 'waiting') {
      // waiting 阶段：剩下 30 → 85，按 12 秒预估匀速增长，时间越久增速越慢
      const waitMs = elapsedMs - 5000
      target = Math.min(85, 30 + (waitMs / 12000) * 55)
    }
    // 单调递增，避免阶段切换瞬间百分比回退
    if (target > processOverlay.percent) processOverlay.percent = target
  }, 200)
}

const stopRestartTicker = () => {
  if (restartTickTimer != null) { clearInterval(restartTickTimer); restartTickTimer = null }
}

/**
 * 关闭重启遮罩并清理计时器，用于用户主动关闭/重试场景。
 */
const closeRestartOverlay = () => {
  stopRestartTicker()
  processOverlay.visible = false
  processOverlay.phase = 'requesting'
  processOverlay.percent = 0
  processOverlay.elapsed = 0
  processOverlay.countdown = 0
  processOverlay.errorMessage = ''
}

/**
 * 检测后端是否已恢复响应。
 * 用原生 fetch（而非 http 实例）以避开业务拦截器：
 * - 服务尚未起来时 fetch 通常抛 TypeError（连接被拒/重置）
 * - 服务起来后无论返回 200 / 401 / 503，HTTP 层已建立则视为已就绪
 *
 * 为避免短暂上线又关闭的中间态（如 IIS 还在 swap），要求**连续两次**请求成功才视为就绪。
 */
const probeApiReady = async (): Promise<boolean> => {
  for (let i = 0; i < 2; i++) {
    try {
      const r = await fetch('/api/v1/modules/environment', { method: 'GET', cache: 'no-store' })
      if (r.status === 0) return false
    } catch {
      return false
    }
    if (i === 0) await new Promise(resolve => setTimeout(resolve, 300))
  }
  return true
}

/**
 * 核心重启逻辑（不含确认弹窗），供「重启服务并重载」和「保存配置后重启」复用。
 * 流程：
 *   1. 显示精美全屏遮罩 + 进度条（requesting → stopping → waiting → ready）
 *   2. POST /v1/modules/restart-process（连接被切断属正常现象，仍继续轮询）
 *   3. 探测 /v1/modules/environment 直到连续 2 次成功（最长 60 秒）
 *   4. 服务恢复 → 进入 ready 阶段，3 秒倒计时让进度条平滑收尾到 100%
 *   5. 倒计时结束 location.assign 整页刷新，让 Vite 重新评估 import.meta.glob
 */
const executeRestartProcess = async () => {
  // 初始化遮罩
  processOverlay.visible = true
  processOverlay.phase = 'requesting'
  processOverlay.percent = 0
  processOverlay.elapsed = 0
  processOverlay.countdown = 0
  processOverlay.errorMessage = ''
  processOverlay.title = '正在重启 API 服务'
  processOverlay.subtitle = '正在请求后端进入重启流程...'
  processOverlay.hint = '请保持本页面打开，重启过程预计 5-15 秒'
  processOverlay.steps = ['发起请求', '停止进程', '等待恢复', '完成刷新']
  startRestartTicker()

  // 阶段 1：发起 restart 请求；连接可能在 800ms 后被切断，要 catch 后继续推进
  let autoRelaunch = true
  let scheduleError = ''
  try {
    const result = await moduleApi.restartApiProcess()
    if (result.autoRelaunch === false) {
      autoRelaunch = false
      scheduleError = result.message || ''
    }
  } catch {
    // 这是正常现象：后端 800ms 后 StopApplication，HTTP 连接被中断会抛 NetworkError / Failed to fetch
  }

  // 阶段 2：进入 stopping
  processOverlay.phase = 'stopping'
  processOverlay.subtitle = '后端已收到请求，正在停止当前进程...'
  await new Promise(resolve => setTimeout(resolve, 1500))

  // 阶段 3：waiting，开始轮询 /environment，最长 60 秒
  processOverlay.phase = 'waiting'
  processOverlay.subtitle = autoRelaunch
    ? '正在等待新进程启动并恢复 API 响应...'
    : '后端已停止，等待外部守护拉起 API 进程...'

  const deadline = Date.now() + 60_000
  let ready = false
  while (Date.now() < deadline) {
    if (await probeApiReady()) { ready = true; break }
    await new Promise(resolve => setTimeout(resolve, 1500))
  }

  if (ready) {
    // 阶段 4：服务恢复，3 秒倒计时收尾到 100%
    processOverlay.phase = 'ready'
    processOverlay.title = '服务已就绪'
    processOverlay.subtitle = '后端已重启完成，即将刷新页面以加载最新插件...'
    processOverlay.hint = '页面将在 3 秒后自动刷新'
    restartReadyAt = Date.now()

    setTimeout(() => {
      stopRestartTicker()
      const target = window.location.pathname + window.location.search
      window.location.assign(target)
    }, 3000)
  } else if (!autoRelaunch) {
    processOverlay.phase = 'failed'
    processOverlay.title = '需要手动重启'
    processOverlay.subtitle = '后端已停止，但当前部署环境无法自动拉起进程'
    processOverlay.hint = '请到运行 API 的终端手动重启服务，启动后再点下方按钮关闭'
    processOverlay.errorMessage = scheduleError
    stopRestartTicker()
  } else {
    processOverlay.phase = 'timeout'
    processOverlay.title = '重启等待超时'
    processOverlay.subtitle = '60 秒内未检测到 API 服务恢复'
    processOverlay.hint = '请到运行 API 的终端确认进程是否已启动；若未启动可执行 `dotnet run --project src/Server/Ginkgo.Api/Ginkgo.Api.csproj`'
    stopRestartTicker()
  }
}

/**
 * 开发模式专用：通过 /api/v1/modules/restart-process 重启整个 API 进程，让 ALC 重新扫描 modules。
 * 入口按钮为模块列表中的「重启服务并重载」。
 */
const handleRestartApiProcess = async (mod: ModuleInfo) => {
  try {
    await ElMessageBox.confirm(
      `点击「重启服务并重载」会重启整个 API 进程并重新扫描所有插件目录（不仅是「${mod.name || mod.id}」）。\n\n重启过程中所有 API 请求会短暂不可用（通常 5-15 秒），重启完成后页面会自动刷新。\n\n是否继续？`,
      '重启服务并重载插件',
      {
        type: 'warning',
        confirmButtonText: '确认重启',
        cancelButtonText: '取消',
        dangerouslyUseHTMLString: false
      }
    )
  } catch {
    return
  }

  actionLoading[mod.id] = true
  try {
    await executeRestartProcess()
  } finally {
    actionLoading[mod.id] = false
  }
}

const handleViewStatus = async (mod: ModuleInfo) => {
  showStatusDialog.value = true
  currentModuleStatus.value = null
  try { currentModuleStatus.value = await moduleApi.getModuleStatus(mod.id) } catch (error: unknown) { ElMessage.error(`获取模块状态失败: ${error instanceof Error ? error.message : '获取失败'}`) }
}

const handleRunInstallSql = async (mod: ModuleInfo) => {
  try {
    await ElMessageBox.confirm(
      `确定要执行模块「${mod.name || mod.id}」的安装 SQL 脚本吗？将根据 install.json 中 SqlScripts 配置，在当前数据库中执行建表等脚本。`,
      '执行安装SQL',
      { type: 'warning', confirmButtonText: '确认执行', cancelButtonText: '取消' }
    )
  } catch { return }
  actionLoading[mod.id] = true
  try {
    const result = await moduleApi.runInstallSql(mod.id)
    if (result.ok) {
      const scripts = result.executedScripts?.join(', ') || ''
      ElMessage.success(result.message + (scripts ? `（${scripts}）` : ''))
    } else {
      ElMessage.warning(result.message || '执行失败')
    }
  } catch (error: unknown) {
    ElMessage.error(`执行安装SQL失败: ${error instanceof Error ? error.message : '操作失败'}`)
  } finally {
    actionLoading[mod.id] = false
  }
}

const handleResetMenus = async (mod: ModuleInfo) => {
  try {
    await ElMessageBox.confirm(`确定要重置模块「${mod.name || mod.id}」的菜单吗？将删除该模块的所有菜单并根据配置文件重新创建。`, '重置菜单', { type: 'warning', confirmButtonText: '确认重置', cancelButtonText: '取消' })
  } catch { return }
  actionLoading[mod.id] = true
  try {
    const result = await moduleApi.resetModuleMenus(mod.id)
    if (result.ok) {
      ElMessage.success(result.message || '菜单重置成功')
      const menuStore = useMenuStore()
      menuStore.clearCache()
      await menuStore.loadMenus(true)
    } else {
      ElMessage.warning(result.message || '重置失败')
    }
  } catch (error: unknown) {
    ElMessage.error(`重置菜单失败: ${error instanceof Error ? error.message : '操作失败'}`)
  } finally {
    actionLoading[mod.id] = false
  }
}

const handleRemoveMenus = async (mod: ModuleInfo) => {
  try {
    await ElMessageBox.confirm(`确定要移除模块「${mod.name || mod.id}」的所有菜单吗？移除后不会自动重建，如需恢复请使用"重置菜单"。`, '移除菜单', { type: 'warning', confirmButtonText: '确认移除', cancelButtonText: '取消' })
  } catch { return }
  actionLoading[mod.id] = true
  try {
    const result = await moduleApi.removeModuleMenus(mod.id)
    if (result.ok) {
      ElMessage.success(result.message || '菜单移除成功')
      const menuStore = useMenuStore()
      menuStore.clearCache()
      await menuStore.loadMenus(true)
    } else {
      ElMessage.warning(result.message || '移除失败')
    }
  } catch (error: unknown) {
    ElMessage.error(`移除菜单失败: ${error instanceof Error ? error.message : '操作失败'}`)
  } finally {
    actionLoading[mod.id] = false
  }
}

// ============ SQL Dry-Run 预检 ============
const handleDryRun = async (mod: ModuleInfo) => {
  actionLoading[mod.id] = true
  try {
    const result = await moduleApi.dryRunModule(mod.id)
    if (result.ok) {
      ElMessage.success(result.message || 'SQL 预检通过')
    } else {
      await ElMessageBox.alert(
        `<div style="color:#ef4444">${result.message}</div><ul style="margin:8px 0 0 16px;">${(result.errors || []).map((e: string) => `<li>${e}</li>`).join('')}</ul>`,
        'SQL 预检发现错误',
        { dangerouslyUseHTMLString: true, type: 'error' }
      )
    }
  } catch (error: unknown) {
    ElMessage.error(`SQL 预检失败: ${error instanceof Error ? error.message : '操作失败'}`)
  } finally {
    actionLoading[mod.id] = false
  }
}

// ============ 灰度发布 ============
const handleOpenGrayscale = async (mod: ModuleInfo) => {
  grayscaleModuleId.value = mod.id
  grayscaleModuleName.value = mod.name || mod.id
  // 加载现有策略
  try {
    const resp = await moduleApi.getGrayscalePolicies()
    const policy = resp.policies?.[mod.id]
    if (policy) {
      currentGrayscalePolicy.value = policy
      grayscaleForm.channel = policy.channel || 'beta'
      grayscaleForm.targetTenantIds = (policy.targetTenantIds || []).join(',')
      grayscaleForm.startTime = policy.startTime ? policy.startTime.replace('T', ' ').substring(0, 16) : ''
      grayscaleForm.endTime = policy.endTime ? policy.endTime.replace('T', ' ').substring(0, 16) : ''
      grayscaleForm.autoPromote = policy.autoPromote || false
    } else {
      currentGrayscalePolicy.value = null
      grayscaleForm.channel = 'beta'
      grayscaleForm.targetTenantIds = ''
      grayscaleForm.startTime = ''
      grayscaleForm.endTime = ''
      grayscaleForm.autoPromote = false
    }
  } catch { }
  showGrayscaleDialog.value = true
}

const handleSaveGrayscale = async () => {
  grayscaleSaving.value = true
  try {
    const policy = {
      channel: grayscaleForm.channel,
      targetTenantIds: grayscaleForm.targetTenantIds
        ? grayscaleForm.targetTenantIds.split(',').map((s: string) => s.trim()).filter(Boolean)
        : undefined,
      startTime: grayscaleForm.startTime || undefined,
      endTime: grayscaleForm.endTime || undefined,
      autoPromote: grayscaleForm.autoPromote
    }
    const result = await moduleApi.setGrayscalePolicy(grayscaleModuleId.value, policy)
    if (result.ok) {
      ElMessage.success(result.message || '灰度策略已保存')
      showGrayscaleDialog.value = false
      await loadGrayscalePolicies()
    } else {
      ElMessage.error(result.message || '保存失败')
    }
  } catch (error: unknown) {
    ElMessage.error(`保存失败: ${error instanceof Error ? error.message : '操作失败'}`)
  } finally {
    grayscaleSaving.value = false
  }
}

const handleRemoveGrayscale = async () => {
  try {
    await ElMessageBox.confirm(`确定要移除模块「${grayscaleModuleName.value}」的灰度策略，切换为全量发布吗？`, '移除灰度策略', { type: 'warning' })
    const result = await moduleApi.removeGrayscalePolicy(grayscaleModuleId.value)
    if (result.ok) {
      ElMessage.success(result.message || '灰度策略已移除')
      showGrayscaleDialog.value = false
      await loadGrayscalePolicies()
    }
  } catch { }
}

// ============ 版本回滚 ============
const handleOpenRollback = async (mod: ModuleInfo) => {
  rollbackModuleId.value = mod.id
  rollbackModuleName.value = mod.name || mod.id
  rollbackSnapshots.value = []
  selectedSnapshot.value = ''
  showRollbackDialog.value = true
  rollbackLoading.value = true
  try {
    const resp = await moduleApi.getModuleSnapshots(mod.id)
    rollbackSnapshots.value = resp.snapshots || []
  } catch (error: unknown) {
    ElMessage.error(`获取快照失败: ${error instanceof Error ? error.message : '操作失败'}`)
  } finally {
    rollbackLoading.value = false
  }
}

const handleDoRollback = async () => {
  if (!selectedSnapshot.value) { ElMessage.warning('请选择要回滚的快照版本'); return }
  try {
    await ElMessageBox.confirm(
      `确定要将模块「${rollbackModuleName.value}」回滚到快照版本 ${selectedSnapshot.value} 吗？此操作将覆盖当前模块文件，不可撤销。`,
      '确认回滚', { type: 'warning', confirmButtonText: '确认回滚', cancelButtonText: '取消' }
    )
  } catch { return }
  rollbackExecuting.value = true
  try {
    const result = await moduleApi.rollbackModule(rollbackModuleId.value, selectedSnapshot.value)
    if (result.ok) {
      ElMessage.success(result.message || '回滚成功，模块已禁用，请重新启用并重载服务')
      showRollbackDialog.value = false
      await refreshModules()
    } else {
      ElMessage.error(result.message || '回滚失败')
    }
  } catch (error: unknown) {
    ElMessage.error(`回滚失败: ${error instanceof Error ? error.message : '操作失败'}`)
  } finally {
    rollbackExecuting.value = false
  }
}

const handleUninstall = async (mod: ModuleInfo) => {
  try {
    if (mod.enabled) {
      await ElMessageBox.confirm(`模块「${mod.name || mod.id}」当前处于启用状态，需要先禁用才能卸载。是否先禁用再卸载？`, '需要先禁用模块', { type: 'warning', confirmButtonText: '禁用并卸载', cancelButtonText: '取消' })
      actionLoading[mod.id] = true
      const disableResult = await moduleApi.hotDisableModule(mod.id)
      if (!disableResult.ok) { ElMessage.error(`禁用模块失败: ${disableResult.message}`); return }
      mod.enabled = false
      ElMessage.success('模块已禁用，正在卸载...')
      await new Promise(resolve => setTimeout(resolve, 500))
    } else {
      await ElMessageBox.confirm(`确定要卸载模块「${mod.name || mod.id}」吗？此操作将删除模块相关的所有菜单、数据表和文件，不可恢复！`, '确认卸载模块', { type: 'warning', confirmButtonText: '确认卸载', cancelButtonText: '取消' })
    }
    actionLoading[mod.id] = true
    const result = await moduleApi.hotUninstallModule(mod.id)
    if (result.ok) {
      ElMessage.success(result.message || '卸载成功')
      const menuStore = useMenuStore()
      menuStore.clearCache()
      await refreshModules()
      await menuStore.loadMenus(true)
      await showRestartRequiredNotice('卸载')
    } else ElMessage.error(result.message)
  } catch (error: unknown) { if (error !== 'cancel') ElMessage.error(`卸载失败: ${error instanceof Error ? error.message : '操作失败'}`) } finally { actionLoading[mod.id] = false }
}

// ========== 前端 NPM 依赖管理 ==========
const showNpmDepsDialog = ref(false)
const npmDepsLoading = ref(false)
const npmDepsInstalling = ref(false)
const npmDepsModuleId = ref('')
const npmDepsModuleName = ref('')
const npmDepsList = ref<moduleApi.NpmDepInfo[]>([])
const npmInstallResult = ref<{ ok: boolean; message: string; installed?: string[]; errors?: string[] } | null>(null)

const resetNpmDeps = () => {
  npmDepsModuleId.value = ''
  npmDepsModuleName.value = ''
  npmDepsList.value = []
  npmInstallResult.value = null
}

const handleOpenNpmDeps = async (mod: ModuleInfo) => {
  npmDepsModuleId.value = mod.id
  npmDepsModuleName.value = mod.name || mod.id
  npmDepsList.value = []
  npmInstallResult.value = null
  showNpmDepsDialog.value = true
  npmDepsLoading.value = true
  try {
    const res = await moduleApi.getNpmDeps(mod.id)
    npmDepsList.value = res.deps || []
    if (res.deps?.length === 0) {
      ElMessage.info(res.message || '该模块没有声明前端 npm 依赖')
    }
  } catch (error: unknown) {
    ElMessage.error(`查询 npm 依赖失败: ${error instanceof Error ? error.message : '查询失败'}`)
  } finally {
    npmDepsLoading.value = false
  }
}

const handleInstallNpmDeps = async () => {
  if (!npmDepsModuleId.value) return
  npmDepsInstalling.value = true
  npmInstallResult.value = null
  try {
    const result = await moduleApi.installNpmDeps(npmDepsModuleId.value)
    npmInstallResult.value = result
    if (result.ok) {
      ElMessage.success(result.message)
      // 刷新依赖状态
      const res = await moduleApi.getNpmDeps(npmDepsModuleId.value)
      npmDepsList.value = res.deps || []
    } else {
      ElMessage.warning(result.message)
    }
  } catch (error: unknown) {
    ElMessage.error(`安装 npm 依赖失败: ${error instanceof Error ? error.message : '安装失败'}`)
  } finally {
    npmDepsInstalling.value = false
  }
}

const handleFileChange = (file: UploadFile) => { selectedFile.value = file.raw || null; fileList.value = [file]; validationResult.value = null; installResult.value = null }
const handleFileRemove = () => { selectedFile.value = null; fileList.value = []; validationResult.value = null; installResult.value = null }
const resetInstallForm = () => { selectedFile.value = null; fileList.value = []; uploadProgress.value = 0; validationResult.value = null; installResult.value = null; installing.value = false; installChannel.value = 'stable'; installGrayscaleTenants.value = '' }

const handleUploadAndValidate = async () => {
  if (!selectedFile.value) return
  installing.value = true; uploadProgress.value = 0
  try { validationResult.value = await moduleApi.uploadModule(selectedFile.value, (percent) => { uploadProgress.value = percent }); uploadProgress.value = 100 } catch (error: unknown) { validationResult.value = { ok: false, message: error instanceof Error ? error.message : '上传失败' } } finally { installing.value = false }
}

const handleConfirmInstall = async () => {
  if (!validationResult.value?.extractedPath || !validationResult.value?.moduleId) return
  installing.value = true
  try {
    const pendingCheck = await moduleApi.checkPendingDelete(validationResult.value.moduleId)
    if (pendingCheck.hasPendingDelete) { await ElMessageBox.alert('该模块之前被卸载但目录未能删除，请重启API服务后再安装。', '安装被阻止', { type: 'error' }); return }
    installResult.value = await moduleApi.confirmInstallModule(validationResult.value.extractedPath)
    // 安装成功且选择灰度通道时，自动设置灰度策略
    if (installResult.value?.ok && installChannel.value === 'beta') {
      try {
        await moduleApi.setGrayscalePolicy(validationResult.value.moduleId, {
          channel: 'beta',
          targetTenantIds: installGrayscaleTenants.value ? installGrayscaleTenants.value.split(',').map((s: string) => s.trim()).filter(Boolean) : undefined,
          autoPromote: false
        })
      } catch { /* 灰度策略设置失败不影响安装结果 */ }
    }
  } catch (error: unknown) { installResult.value = { ok: false, message: error instanceof Error ? error.message : '安装失败' } } finally { installing.value = false }
}

const handleInstallComplete = async () => {
  showInstallDialog.value = false; resetInstallForm()
  ElMessage.success('模块安装成功！正在等待前端运行时识别新插件...')

  // 与远程安装路径（doInstallFromStore）保持一致：使用通用全屏进度遮罩 + 智能等待 +
  // 倒计时刷新，避免「固定 1500ms 延迟 → Vite 还没识别新插件 → 刷新后 404」的情况。
  // 这里没有 install 接口可调（包已在 confirmInstall 阶段安装完成），直接进 waiting 阶段。
  processOverlay.visible = true
  processOverlay.phase = 'waiting'
  processOverlay.percent = 30 // 起步 30%，跳过 requesting/stopping
  processOverlay.elapsed = 0
  processOverlay.countdown = 0
  processOverlay.errorMessage = ''
  processOverlay.title = '插件已安装'
  processOverlay.subtitle = '正在刷新前端插件能力，等待新插件被运行时识别...'
  processOverlay.hint = '请保持本页面打开，预计 5-15 秒'
  processOverlay.steps = ['上传', '安装', '前端就绪', '完成刷新']
  startRestartTicker()

  try {
    await reloadFrontendPluginRuntime()
  } catch { /* 失败不阻塞，下面的轮询会兜底 */ }

  try {
    await refreshModules()
    const menuStore = useMenuStore()
    menuStore.clearCache()
    await menuStore.loadMenus(true)
  } catch { /* 菜单刷新失败不阻塞，整页刷新后会重新加载 */ }

  // 智能等待最多 20 秒，让 Vite 重新评估 glob、把新插件目录纳入。
  // 这里没有具体 plugin 引用做精确比对，所以仅保证总等待时间充分（远程路径有更精确的就绪探测）。
  await new Promise(resolve => setTimeout(resolve, Math.min(8000, 20_000)))

  processOverlay.phase = 'ready'
  processOverlay.title = '安装完成'
  processOverlay.subtitle = '前端插件能力已刷新，即将刷新页面应用最新菜单与路由...'
  processOverlay.hint = '页面将在 3 秒后自动刷新'
  restartReadyAt = Date.now()

  setTimeout(() => {
    stopRestartTicker()
    const target = window.location.pathname + window.location.search
    window.location.assign(target)
  }, 3000)
}

const loadPackageableModules = async () => {
  loadingPackageable.value = true
  try { const result = await moduleApi.getPackageableModules(); packageableModules.value = result.modules || [] } catch (error: unknown) { ElMessage.error(`加载可打包模块失败: ${error instanceof Error ? error.message : '加载失败'}`) } finally { loadingPackageable.value = false }
}

const resetPackageForm = () => { packageForm.moduleId = ''; packageForm.packageType = 'compiled'; packageForm.exportDbSchema = false; packageForm.exportDbData = false; packageForm.sanitizeConfig = true; packageProgress.value = 0; packageResult.value = null; packaging.value = false }

// 当前选中的可打包模块是否为源码版（server 目录有 .csproj）。
// 只有源码版才能打"源码包"，已编译 DLL 版只能重新打"编译包"。
// 未选中模块时默认认为是源码版（开发环境兼容旧行为，避免初始 UI 把源码选项错禁用）。
const selectedPackageableModuleHasSource = computed(() => {
  if (!packageForm.moduleId) return true
  const mod = packageableModules.value.find(m => m.moduleId === packageForm.moduleId)
  // isSourcePackage 字段缺失时（旧后端）默认为 true，保持向后兼容
  return mod?.isSourcePackage !== false
})

// 切换模块时若新模块不支持源码包但当前选了源码包，自动切到编译包
const onPackageModuleChange = (_moduleId: string) => {
  if (!selectedPackageableModuleHasSource.value && packageForm.packageType === 'source') {
    packageForm.packageType = 'compiled'
  }
}

// 取消“真实数据结构”时，自动取消“真实数据内容”（联动约束）
const onExportSchemaChange = (val: boolean | string | number) => {
  if (!val) packageForm.exportDbData = false
}

const handlePackage = async () => {
  if (!packageForm.moduleId) return
  packaging.value = true; packageProgress.value = 0
  const progressInterval = setInterval(() => { if (packageProgress.value < 90) packageProgress.value += 10 }, 200)
  try { packageResult.value = await moduleApi.packageModule(packageForm.moduleId, packageForm.packageType, packageForm.exportDbSchema, packageForm.exportDbData, packageForm.sanitizeConfig); packageProgress.value = 100 } catch (error: unknown) { packageResult.value = { ok: false, message: error instanceof Error ? error.message : '打包失败' } } finally { clearInterval(progressInterval); packaging.value = false }
}

const handleDownloadPackage = async () => {
  if (!packageForm.moduleId) return
  try {
    // 必须传与打包时一致的参数，后端下载接口会据此定位/重建 ZIP
    const response = await moduleApi.downloadPackage(
      packageForm.moduleId,
      packageForm.packageType,
      packageForm.exportDbSchema,
      packageForm.exportDbData,
      packageForm.sanitizeConfig
    )
    const blob = new Blob([response], { type: 'application/zip' })
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url; link.download = packageResult.value?.fileName || `${packageForm.moduleId}.gmod.zip`
    document.body.appendChild(link); link.click(); document.body.removeChild(link)
    window.URL.revokeObjectURL(url)
    ElMessage.success('下载已开始')
  } catch (error: unknown) { ElMessage.error(`下载失败: ${error instanceof Error ? error.message : '下载失败'}`) }
}

const formatDate = (dateStr: string) => { try { return new Date(dateStr).toLocaleString('zh-CN') } catch { return dateStr } }
const formatFileSize = (bytes: number) => { if (bytes === 0) return '0 B'; const k = 1024; const sizes = ['B', 'KB', 'MB', 'GB']; const i = Math.floor(Math.log(bytes) / Math.log(k)); return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i] }

/**
 * 模块健康判定：runtime 已加载、服务端 DLL 已落盘、（若声明菜单）菜单已注册。
 * 任一字段缺失（旧版后端没下发这些字段）按"未知"处理 → 视为 健康，避免误报红灯。
 */
const isModuleHealthy = (mod: ModuleInfo): boolean => {
  if (mod.runtimeLoaded === false) return false
  if (mod.serverDllLoaded === false) return false
  // 仅当插件确实声明菜单（hasMenus=true）且未注册时算异常；未声明菜单不参与健康判定
  if (mod.hasMenus === true && mod.menuRegistered === false) return false
  return true
}

/**
 * 红灯 hover 文案：列出当前异常项，方便用户快速定位。
 */
const moduleHealthTip = (mod: ModuleInfo): string => {
  const issues: string[] = []
  if (mod.runtimeLoaded === false) issues.push('运行时未加载')
  if (mod.serverDllLoaded === false) issues.push('服务端 DLL 未加载')
  if (mod.hasMenus === true && mod.menuRegistered === false) issues.push('菜单未注册')
  return issues.length === 0 ? '运行状态正常' : `异常：${issues.join('、')}`
}

// ========== 模块配置 ==========
const handleOpenConfig = async (mod: ModuleInfo) => {
  configModuleId.value = mod.id
  configModuleName.value = mod.name || mod.id
  configFiles.value = []
  configData.value = null
  activeConfigFile.value = ''
  activeConfigTab.value = ''
  showConfigDialog.value = true
  configLoading.value = true
  try {
    const files = await moduleApi.getModuleConfigFiles(mod.id)
    // 过滤掉 .sample 文件，只保留实际配置文件名
    const jsonFiles = files.filter((f: string) => f.endsWith('.json') && !f.endsWith('.sample'))
    // 如果没有 .json 文件但有 .sample，也列出（去掉 .sample 后缀）
    if (jsonFiles.length === 0) {
      const sampleFiles = files.filter((f: string) => f.endsWith('.json.sample'))
      sampleFiles.forEach((f: string) => jsonFiles.push(f.replace('.sample', '')))
    }
    configFiles.value = jsonFiles
    if (jsonFiles.length > 0) {
      activeConfigFile.value = jsonFiles[0]
      await loadConfigData()
    }
  } catch (error: unknown) {
    // 404 表示没有配置目录，不是错误
    const msg = error instanceof Error ? error.message : '加载失败'
    if (!msg.includes('404')) ElMessage.error(`加载配置文件列表失败: ${msg}`)
  } finally {
    configLoading.value = false
  }
}

const loadConfigData = async () => {
  if (!activeConfigFile.value || !configModuleId.value) return
  configLoading.value = true
  try {
    const data = await moduleApi.getModuleConfigNormalized(configModuleId.value, activeConfigFile.value)
    configData.value = data
    if (data.groups && data.groups.length > 0) {
      activeConfigTab.value = data.groups[0].code
    }
    await preloadSystemApisIfNeeded()
  } catch (error: unknown) {
    ElMessage.error(`加载配置失败: ${error instanceof Error ? error.message : '加载失败'}`)
    configData.value = null
  } finally {
    configLoading.value = false
  }
}

// 预加载系统 API，以便配置标签可以显示中文名称
const preloadSystemApisIfNeeded = async () => {
  if (apiScanLoaded.value || !configData.value) return
  // 检查当前配置中是否有 api-selector 类型的项
  const hasApiSelector = configData.value.items.some(i => i.type === 'api-selector')
  if (!hasApiSelector) return

  try {
    const res: any = await http.get('/verify/captcha/scan-apis')
    if (res?.apis) {
      allSystemApis.value = res.apis
      apiScanLoaded.value = true
    }
  } catch (e) {
    console.error('[APISelector] 预加载 API 失败:', e)
  }
}

const getGroupItems = (groupCode: string): ConfigItem[] => {
  if (!configData.value?.items) return []
  return configData.value.items.filter(item => item.group === groupCode)
}

const handleSaveConfig = async () => {
  if (!configData.value || !configModuleId.value || !activeConfigFile.value) return
  // 校验必填项
  const missing = configData.value.items.filter(item => item.rule === 'required' && (!item.value || item.value.trim() === ''))
  if (missing.length > 0) {
    ElMessage.warning(`请填写必填项：${missing.map(i => i.title).join('、')}`)
    return
  }
  configSaving.value = true
  try {
    const result = await moduleApi.saveModuleConfig(configModuleId.value, activeConfigFile.value, configData.value)
    if (!result.ok) {
      ElMessage.warning(result.message || '保存失败')
      return
    }
    ElMessage.success(result.message || '保存成功')

    // 开发模式：关闭配置弹窗，进入「重启服务」全屏进度流程，配置变更需要重启才能被后端重新读取
    if (envInfo.value?.isDevelopment) {
      showConfigDialog.value = false
      await executeRestartProcess()
    } else {
      // 生产模式：提示用户需要手动重启服务
      showConfigDialog.value = false
      await showRestartRequiredNotice('配置更新')
    }
  } catch (error: unknown) {
    ElMessage.error(`保存配置失败: ${error instanceof Error ? error.message : '保存失败'}`)
  } finally {
    configSaving.value = false
  }
}

const handleResetConfig = async () => {
  if (!configModuleId.value || !activeConfigFile.value) return
  try {
    await ElMessageBox.confirm('确定要恢复默认配置吗？当前配置将被覆盖。', '恢复默认', { type: 'warning', confirmButtonText: '确认恢复', cancelButtonText: '取消' })
  } catch { return }
  configSaving.value = true
  try {
    const result = await moduleApi.resetModuleConfig(configModuleId.value, activeConfigFile.value)
    if (!result.ok) {
      ElMessage.warning(result.message || '恢复失败')
      return
    }
    ElMessage.success(result.message || '已恢复默认')
    await loadConfigData()

    // 与 handleSaveConfig 保持一致：开发模式走重启流程，生产模式提示
    if (envInfo.value?.isDevelopment) {
      showConfigDialog.value = false
      await executeRestartProcess()
    } else {
      showConfigDialog.value = false
      await showRestartRequiredNotice('配置恢复默认')
    }
  } catch (error: unknown) {
    ElMessage.error(`恢复默认失败: ${error instanceof Error ? error.message : '操作失败'}`)
  } finally {
    configSaving.value = false
  }
}

const resetConfigForm = () => {
  configModuleId.value = ''
  configModuleName.value = ''
  configFiles.value = []
  configData.value = null
  activeConfigFile.value = ''
  activeConfigTab.value = ''
}

// ========== API 可视化选择器 ==========
interface ApiEndpoint {
  path: string; method: string; controller: string; action: string
  controllerDisplayName: string; actionDisplayName: string; module: string; key: string
}

const showApiSelectorDialog = ref(false)
const allSystemApis = ref<ApiEndpoint[]>([])
const apiSearchKeyword = ref('')
const apiFilterModule = ref('')
const apiFilterMethod = ref('')
const apiSelectedRows = ref<ApiEndpoint[]>([])
const apiSelectorTableRef = ref<any>(null)
const currentApiSelectorItem = ref<ConfigItem | null>(null)
const apiScanLoaded = ref(false)

// 所有模块名列表（去重）
const apiModuleList = computed(() => {
  const set = new Set(allSystemApis.value.map((a: ApiEndpoint) => a.module))
  return Array.from(set).sort()
})

// 解析配置项 value（\n 分隔）为 API 路径数组
const parseApiList = (value: string | undefined): string[] => {
  if (!value) return []
  return value.split(/\\n|\n/).map(s => s.trim()).filter(s => s.length > 0)
}

// 根据 API 路径获取中文显示名
const getApiDisplayName = (apiPath: string): string => {
  const found = allSystemApis.value.find((a: ApiEndpoint) => a.path === apiPath)
  if (found) return found.actionDisplayName || found.action
  // 从路径提取最后段作为名称
  const parts = apiPath.split('/')
  return parts[parts.length - 1] || apiPath
}

// 从配置项中移除一个 API
const removeApiFromItem = (item: ConfigItem, apiPath: string) => {
  const list = parseApiList(item.value)
  item.value = list.filter(a => a !== apiPath).join('\\n')
}

// 打开 API 选择器
const openApiSelector = async (item: ConfigItem) => {
  currentApiSelectorItem.value = item
  apiSearchKeyword.value = ''
  apiFilterModule.value = ''
  apiFilterMethod.value = ''
  apiSelectedRows.value = []

  // 首次打开时扫描系统 API
  if (!apiScanLoaded.value) {
    try {
      const res: any = await http.get('/verify/captcha/scan-apis')
      if (res?.apis) {
        allSystemApis.value = res.apis
        apiScanLoaded.value = true
      }
    } catch (e) {
      console.error('[APISelector] 扫描 API 失败:', e)
      ElMessage.error('扫描系统 API 失败，请确认验证码模块已启用')
      return
    }
  }

  showApiSelectorDialog.value = true
}

// 获取所有配置中所有 api-selector 类型项已使用的 API 路径（跨池去重）
const allUsedApiPaths = computed(() => {
  const set = new Set<string>()
  if (configData.value?.items) {
    configData.value.items
      .filter(i => i.type === 'api-selector')
      .forEach(i => parseApiList(i.value).forEach(path => set.add(path)))
  }
  return set
})

// 过滤后的可选 API 列表
const filteredAvailableApis = computed(() => {
  return allSystemApis.value.filter((api: ApiEndpoint) => {
    // 排除已分配到任何池中的
    if (allUsedApiPaths.value.has(api.path)) return false
    // 排除验证码模块自身
    if (api.path.startsWith('/api/verify/')) return false
    // 搜索
    if (apiSearchKeyword.value) {
      const kw = apiSearchKeyword.value.toLowerCase()
      if (!api.path.toLowerCase().includes(kw) &&
          !api.actionDisplayName.toLowerCase().includes(kw) &&
          !api.controllerDisplayName.toLowerCase().includes(kw) &&
          !api.action.toLowerCase().includes(kw) &&
          !api.controller.toLowerCase().includes(kw)) return false
    }
    if (apiFilterModule.value && api.module !== apiFilterModule.value) return false
    if (apiFilterMethod.value && api.method !== apiFilterMethod.value) return false
    return true
  })
})

const handleApiSelectionChange = (rows: ApiEndpoint[]) => {
  apiSelectedRows.value = rows
}

// 确认选择，将选中的 API 路径追加到配置项的 value 中
const confirmApiSelection = () => {
  if (!currentApiSelectorItem.value) return
  const existing = parseApiList(currentApiSelectorItem.value.value)
  const newPaths = apiSelectedRows.value.map((a: ApiEndpoint) => a.path)
  currentApiSelectorItem.value.value = [...existing, ...newPaths].join('\n')
  showApiSelectorDialog.value = false
  ElMessage.success(`已添加 ${newPaths.length} 个 API`)
}

// ========== 插件商店 ==========
const loadStoreConfig = async () => {
  try {
    storeConfig.value = await pluginStoreApi.getPluginStoreConfig()
  } catch {
    storeConfig.value = { serverUrl: '', enabled: false }
  }
}

/**
 * 完成商城登录态落地，并执行登录前挂起的操作。
 */
const completeStoreLogin = async (token: string, user?: pluginStoreApi.StoreUserInfo) => {
  storeToken.value = token
  storeLoggedIn.value = true
  localStorage.setItem('ginkgo_store_token', token)

  try {
    storeUserInfo.value = user || await pluginStoreApi.getStoreUserInfo(token)
  } catch {
    storeUserInfo.value = user || null
  }

  await loadAvailablePlugins()

  if (pendingAction.value) {
    const action = pendingAction.value
    pendingAction.value = null
    if (action.type === 'purchase') {
      handlePurchasePlugin(action.plugin)
    } else if (action.type === 'install') {
      handleInstallFromStore(action.plugin)
    }
  }
}

/**
 * 打开商城登录入口。
 * <p>
 * 当前策略：直接弹出远程网页登录窗口（避免在本地维护一份登录表单 / 验证码 / 手机短信 / 第三方登录），
 * 由远端商城统一处理一切登录方式后通过 postMessage / BroadcastChannel / localStorage 回传 token。
 * </p>
 * <p>
 * 历史：曾内嵌「商城账号 + 密码」表单走本地后端代理登录，现已收敛到远程网页登录单一入口。
 * 对话框 DOM 与 <c>handleStoreLoginSubmit</c> 暂保留以备将来恢复，但不会被本入口触发。
 * </p>
 */
const openStoreLoginDialog = async () => {
  await openStoreLoginPopup()
}

const handleStoreLoginSubmit = async () => {
  try {
    await storeLoginFormRef.value?.validate()
    storeLoginSubmitting.value = true

    const loginResult = await pluginStoreApi.loginStore(
      {
        userName: storeLoginForm.userName,
        password: storeLoginForm.password,
        clientType: 'WEB_PORTAL'
      },
      // 验证码挑战处理：在登录对话框内自包含完成滑块验证，无需安装其它插件
      async (challenge) => {
        return await waitForStoreCaptchaToken(challenge?.message)
      }
    )
    const normalized = normalizeDirectStoreLoginResult(loginResult)
    if (!normalized) {
      throw new Error('商城登录成功但未返回有效登录凭证')
    }

    await completeStoreLogin(normalized.token, normalized.user)
    showStoreLoginDialog.value = false
    storeLoginForm.password = ''
    storeCaptchaRequired.value = false
    storeCaptchaTip.value = ''
    ElMessage.success('商城登录成功')
  } catch (error: any) {
    if (error?.message) ElMessage.error(error.message)
  } finally {
    storeLoginSubmitting.value = false
  }
}

const openRemoteStoreLoginFromDialog = () => {
  showStoreLoginDialog.value = false
  openStoreLoginPopup()
}

/**
 * 打开远端商城登录弹窗。
 *
 * 作为备用入口：用于第三方登录、短信/邮箱等无法通过本地代理覆盖的远端登录方式。
 */
const openStoreLoginPopup = async () => {
  // 每次点击都实时拉取最新配置，确保使用 appsettings.json 中最新的 ServerUrl/loginPath
  try {
    storeConfig.value = await pluginStoreApi.getPluginStoreConfig()
  } catch {
    // 拉取失败时沿用旧配置，不阻断流程
  }

  if (!storeConfig.value?.serverUrl) {
    ElMessage.warning('插件商店服务未配置，请先在 appsettings.json 中设置 PluginStore:ServerUrl')
    return
  }
  if (storeLoading.value) return

  // 已有未关闭的弹窗 → 先聚焦
  if (storeLoginPopup && !storeLoginPopup.closed) {
    try { storeLoginPopup.focus() } catch { /* ignore */ }
    return
  }

  // 标记弹窗登录流程开始（供 focus handler 判断）
  storeLoginFlowActive = true

  // 一次性 state，防止伪造消息
  storeLoginState = (window.crypto && 'randomUUID' in window.crypto)
    ? (window.crypto as any).randomUUID()
    : Math.random().toString(36).slice(2) + Date.now().toString(36)

  const base = storeConfig.value.serverUrl.replace(/\/+$/, '')
  const rawPath = (storeConfig.value.loginPath || '/web/login')
  const loginPath = rawPath.startsWith('/') ? rawPath : `/${rawPath}`
  const sep = loginPath.includes('?') ? '&' : '?'
  const url = `${base}${loginPath}${sep}storeLoginCallback=1`
    + `&origin=${encodeURIComponent(window.location.origin)}`
    + `&state=${encodeURIComponent(storeLoginState)}`

  // 居中打开弹窗
  const w = 480
  const h = 720
  const left = Math.max(0, Math.floor((window.screen.width - w) / 2))
  const top = Math.max(0, Math.floor((window.screen.height - h) / 2))
  storeLoginPopup = window.open(
    url,
    'ginkgo-store-login',
    `width=${w},height=${h},left=${left},top=${top},resizable=yes,scrollbars=yes,status=no,toolbar=no,menubar=no,location=no`
  )

  if (!storeLoginPopup) {
    ElMessage.error('窗口被浏览器拦截，请允许弹出后重试')
    return
  }

  storeLoading.value = true
  // 先移除旧监听，避免重复订阅
  window.removeEventListener('message', storeLoginMessageHandler)
  window.removeEventListener('storage', storeLoginStorageHandler)
  window.removeEventListener('focus', storeLoginFocusHandler)
  document.removeEventListener('visibilitychange', storeLoginFocusHandler)
  window.addEventListener('message', storeLoginMessageHandler)
  // storage 事件：中继页写入 localStorage 时会触发同源跨窗口广播
  window.addEventListener('storage', storeLoginStorageHandler)
  // focus / visibilitychange：弹窗关闭后用户切回主窗口时主动检查 localStorage（第四条恢复通道）
  window.addEventListener('focus', storeLoginFocusHandler)
  document.addEventListener('visibilitychange', storeLoginFocusHandler)
  // BroadcastChannel：作为 postMessage / storage 双重失效时的第三条投递通道
  try {
    if (typeof BroadcastChannel !== 'undefined') {
      if (storeLoginBroadcastChannel) {
        try { storeLoginBroadcastChannel.close() } catch { /* ignore */ }
      }
      storeLoginBroadcastChannel = new BroadcastChannel('ginkgo-store-login')
      storeLoginBroadcastChannel.onmessage = storeLoginBroadcastHandler
    }
  } catch { /* ignore */ }

  // 轮询用户是否手动关闭弹窗 + 主动检查 localStorage（避免 storage 事件被浏览器节流而漏触发）
  if (storeLoginPopupTimer) clearInterval(storeLoginPopupTimer)
  storeLoginPopupTimer = setInterval(() => {
    // 1) 主动轮询 localStorage：弹窗关闭前任何时刻只要中继页写入了 token，就立即拾取
    if (!storeLoggedIn.value) {
      const cachedTs = localStorage.getItem('ginkgo_store_login_ts')
      const cachedToken = localStorage.getItem('ginkgo_store_token')
      if (cachedToken && cachedTs && Date.now() - parseInt(cachedTs) <= 60000) {
        applyStoreLoginFromLocalStorage()
      }
    }

    // 2) 弹窗关闭后再做一次最终兜底
    if (!storeLoginPopup || storeLoginPopup.closed) {
      // 立即停止轮询，避免重复触发
      if (storeLoginPopupTimer) {
        clearInterval(storeLoginPopupTimer)
        storeLoginPopupTimer = null
      }
      // 弹窗已关闭，但消息可能仍在途中：
      //   - 跨域 postMessage 在某些浏览器下会延迟到目标 window 关闭后到达
      //   - 中继页 localStorage 写入与窗口关闭存在竞态
      //   - 用户在登录页停留 >60s 时 ts 字段已超新鲜度，但 token 是新鲜的
      // 因此采用「多档延迟 + force 模式」组合兜底：
      //   - 0/500/1500/3000ms 各检查一次 localStorage（先按 fresh，再 force 兜底）
      //   - 任意一次命中即终止
      //   - 全部未命中再清理监听并停 loading
      const checkpoints: Array<{ delay: number; force: boolean }> = [
        { delay: 0, force: false },
        { delay: 500, force: false },
        { delay: 1500, force: false },
        { delay: 3000, force: true }, // 最终一次跳过 60s 限制
      ]
      let cleared = false
      const tryRecover = async (force: boolean) => {
        if (storeLoggedIn.value || cleared) return true
        return applyStoreLoginFromLocalStorage(force)
      }
      const finalize = () => {
        if (cleared || storeLoggedIn.value) return
        cleared = true
        window.removeEventListener('message', storeLoginMessageHandler)
        window.removeEventListener('storage', storeLoginStorageHandler)
        window.removeEventListener('focus', storeLoginFocusHandler)
        document.removeEventListener('visibilitychange', storeLoginFocusHandler)
        try { storeLoginBroadcastChannel?.close() } catch { /* ignore */ }
        storeLoginBroadcastChannel = null
        storeLoginPopup = null
        storeLoginFlowActive = false
        storeLoading.value = false
        pendingAction.value = null
      }
      ;(async () => {
        for (const cp of checkpoints) {
          if (cp.delay > 0) await new Promise(r => setTimeout(r, cp.delay))
          const ok = await tryRecover(cp.force)
          if (ok || storeLoggedIn.value) return
        }
        finalize()
      })()
    }
  }, 500)
}

const cleanupStoreLoginPopup = () => {
  if (storeLoginPopupTimer) {
    clearInterval(storeLoginPopupTimer)
    storeLoginPopupTimer = null
  }
  window.removeEventListener('message', storeLoginMessageHandler)
  window.removeEventListener('storage', storeLoginStorageHandler)
  window.removeEventListener('focus', storeLoginFocusHandler)
  document.removeEventListener('visibilitychange', storeLoginFocusHandler)
  try { storeLoginBroadcastChannel?.close() } catch { /* ignore */ }
  storeLoginBroadcastChannel = null
  storeLoginPopup = null
  storeLoginFlowActive = false
  storeLoading.value = false
}

/**
 * 处理远端登录页或同源中继页回传的 postMessage：
 * - 校验 event.origin：接受商城服务同源 或 本站同源（中继页）
 * - 校验 message.type 与 state，防止外部伪造
 */
const handleStoreLoginMessage = async (event: MessageEvent) => {
  try {
    const expectedOrigin = new URL(storeConfig.value.serverUrl).origin
    // 接受两种来源：远端商城直接 postMessage 或 本地同源中继页转发
    const isFromRemote = event.origin === expectedOrigin
    const isFromRelay = event.origin === window.location.origin
    if (!isFromRemote && !isFromRelay) return
    const loginMessage = normalizeStoreLoginMessage(event.data)
    if (!loginMessage) return
    if (storeLoginState && loginMessage.state && loginMessage.state !== storeLoginState) {
      // state 不匹配，丢弃
      return
    }

    storeToken.value = loginMessage.token
    storeLoggedIn.value = true
    localStorage.setItem('ginkgo_store_token', loginMessage.token)

    // 关闭弹窗与轮询
    try { storeLoginPopup?.close() } catch { /* ignore */ }
    cleanupStoreLoginPopup()

    ElMessage.success('商城登录成功')

    // 优先使用回传的 user 信息，避免额外请求；缺失时再去拉一次
    try {
      if (loginMessage.user) {
        storeUserInfo.value = loginMessage.user
      } else {
        storeUserInfo.value = await pluginStoreApi.getStoreUserInfo(loginMessage.token)
      }
    } catch { /* 用户信息获取失败不阻塞 */ }

    await loadAvailablePlugins()

    // 执行登录前的待定动作
    if (pendingAction.value) {
      const action = pendingAction.value
      pendingAction.value = null
      if (action.type === 'purchase') {
        handlePurchasePlugin(action.plugin)
      } else if (action.type === 'install') {
        handleInstallFromStore(action.plugin)
      }
    }
  } catch (err) {
    ElMessage.error(`商城登录处理失败: ${err instanceof Error ? err.message : String(err)}`)
    cleanupStoreLoginPopup()
  }
}

const handleStoreLogout = () => {
  storeLoggedIn.value = false
  storeToken.value = ''
  storeUserInfo.value = null
  storeCategory.value = ''
  localStorage.removeItem('ginkgo_store_token')
  // 退出后重新加载列表（匿名模式，无购买状态）
  loadAvailablePlugins()
}

/**
 * 「刷新」按钮入口：
 * 在重新拉取插件列表之前，先尝试从 localStorage 恢复商城登录态。
 * 这样即便弹窗关闭瞬间的 postMessage / BroadcastChannel / storage 事件
 * 因浏览器节流被全部丢弃，用户点一下刷新也能立即让 UI 切换到「已登录」。
 */
const handleStoreRefresh = async () => {
  if (!storeLoggedIn.value) {
    // 先尝试从已写入的 token 恢复登录态（与 onMounted 中的恢复路径一致）
    await syncStoreLoginFromCachedToken()
  }
  await loadAvailablePlugins()
}

/**
 * 从 localStorage 中已落盘的 token 恢复商城登录态。
 * 与 onMounted 中的恢复逻辑保持一致：拉取一次用户信息，成功则切换为已登录状态。
 * 失败时清理掉无效 token，避免下次刷新继续尝试。
 */
const syncStoreLoginFromCachedToken = async (): Promise<boolean> => {
  const cachedToken = localStorage.getItem('ginkgo_store_token')
  if (!cachedToken) return false
  if (storeLoggedIn.value && storeToken.value === cachedToken) return true
  try {
    const info = await pluginStoreApi.getStoreUserInfo(cachedToken)
    storeUserInfo.value = info
    storeToken.value = cachedToken
    storeLoggedIn.value = true
    // 清掉中继页临时数据（如果还在），避免重复触发恢复路径
    localStorage.removeItem('ginkgo_store_user')
    localStorage.removeItem('ginkgo_store_login_ts')
    localStorage.removeItem('ginkgo_store_login_state')
    return true
  } catch {
    localStorage.removeItem('ginkgo_store_token')
    return false
  }
}

/**
 * 始终生效的 storage 事件兜底监听器。
 * 与 storeLoginStorageHandler 的区别：本监听不依赖 storeLoginFlowActive，
 * 任何时候只要中继页向 localStorage 写入 ginkgo_store_token，本页都会自动同步登录态。
 * 避免「弹窗关闭瞬间的 storage 事件被节流」时只能等用户 F5 才能恢复。
 */
const persistentStorageHandler = (event: StorageEvent) => {
  if (event.key !== 'ginkgo_store_token') return
  if (!event.newValue) return
  if (storeLoggedIn.value && storeToken.value === event.newValue) return
  // 立刻尝试恢复（不阻塞 UI）
  syncStoreLoginFromCachedToken()
}

/**
 * 页面重新可见时的兜底检查：用户切回标签页时主动同步 localStorage 中的 token。
 * 即使所有事件通道都失效，回到标签页也能立即看到「已登录」状态。
 */
const persistentVisibilityHandler = () => {
  if (document.visibilityState !== 'visible') return
  if (storeLoggedIn.value) return
  if (!storeConfig.value.enabled) return
  if (!localStorage.getItem('ginkgo_store_token')) return
  syncStoreLoginFromCachedToken()
}

/**
 * 拉取远端启用分类，刷新 remoteCategoryMap 与分类按钮选项。
 * <p>
 * 用途：
 *   - 用 Code→Name 映射替代硬编码的 getCategoryLabel，保证商店新增分类时前端立刻能看到中文名；
 *   - 作为分类筛选按钮的数据源；如远端接口异常，回退到从当前页插件列表中抽取的 category 集合。
 * </p>
 */
const loadStoreCategories = async () => {
  try {
    const cats = await pluginStoreApi.getStoreCategories(storeToken.value || undefined)
    // 构建 Code → Name 映射（清空旧值，避免远端下线的分类残留在本地）
    Object.keys(remoteCategoryMap).forEach(k => { delete remoteCategoryMap[k] })
    for (const c of cats) {
      if (c.code && c.name) remoteCategoryMap[c.code] = c.name
    }
    storeCategories.value = cats.map(c => ({
      value: c.code,
      label: c.name || c.code
    }))
  } catch {
    // 分类是次要能力：拉取失败时保持现状，不阻塞主列表加载
  }
}

const loadAvailablePlugins = async () => {
  storeLoading.value = true
  try {
    const pageResult = await pluginStoreApi.getAvailablePlugins(
      storeToken.value || undefined,
      storeCategory.value || undefined,
      storeKeyword.value?.trim() || undefined,
      storePage.value,
      storePageSize.value,
    )
    availablePlugins.value = pageResult.items
    storeTotal.value = pageResult.total
    // 远端如果纠偏了 page/pageSize（例如越界），同步回前端 UI，避免 el-pagination 与实际结果脱节
    if (pageResult.page && pageResult.page > 0) storePage.value = pageResult.page
    if (pageResult.pageSize && pageResult.pageSize > 0) storePageSize.value = pageResult.pageSize

    // 初次或当分类接口失败时，用当前页 category 做兜底（保证至少能按当前插件的 code 看到筛选按钮）
    if (storeCategories.value.length === 0) {
      const cats = Array.from(new Set(pageResult.items.map(p => p.category).filter((c): c is string => !!c)))
      storeCategories.value = cats.map(c => ({ value: c, label: getCategoryLabel(c) }))
    }
  } catch (error: unknown) {
    ElMessage.error(`加载插件列表失败: ${error instanceof Error ? error.message : '加载失败'}`)
  } finally {
    storeLoading.value = false
  }
}

/** 分类 / 关键词变更时复位到第 1 页，避免停留在旧 page 导致 total 与 items 错位 */
const resetPageAndReload = () => {
  storePage.value = 1
  loadAvailablePlugins()
}

/** el-pagination 当前页切换 */
const onStorePageChange = (p: number) => {
  storePage.value = p
  loadAvailablePlugins()
}

/** el-pagination 每页条数切换：同步切回第 1 页 */
const onStorePageSizeChange = (size: number) => {
  storePageSize.value = size
  storePage.value = 1
  loadAvailablePlugins()
}

/**
 * 从插件商店「下载安装」按钮入口。
 * <p>
 * 改造点：现在先弹出版本选择对话框，列出当前授权升级窗口内的可下载版本（超出窗口的置灰），
 * 用户确认后再走 {@link doInstallFromStore} 真正下载安装；这样能让用户看到：
 *   1) 自己的更新有效期到哪里；
 *   2) 该档位有哪些版本；
 *   3) 哪些版本因为超出更新有效期不能下载（需要续费）。
 * </p>
 * <p>
 * 免费档位（plugin.isFree 且无需登录）依然支持，对话框对它们也按相同口径展示。
 * </p>
 */
const handleInstallFromStore = async (plugin: pluginStoreApi.AvailablePlugin) => {
  // 防御性 TS 层 guard：UI 按钮已按 isAgreed 做 :disabled，这里兜底保护，
  // 避免 pendingAction 回调 / 插件钩子绕过 UI 直接触发下载流程。
  if (!isAgreed(plugin.id, plugin.editionId || plugin.id)) {
    ElMessage.warning('请先阅读并勾选使用协议后再下载')
    return
  }
  if (!storeLoggedIn.value) {
    pendingAction.value = { type: 'install', plugin }
    openStoreLoginDialog()
    return
  }

  // 环境检查：生产环境不支持在线安装
  if (isEditionDisabledByEnv(plugin)) {
    ElMessage.warning('当前为生产环境，不支持在线安装插件。请在开发环境中安装后重新部署。')
    return
  }

  // 检查是否已经安装了同模块的（其他）版本，改用按名称或ID匹配
  const installedModule = modules.value.find(m =>
    String(m.id).toLowerCase() === String(plugin.id).toLowerCase() ||
    String(m.name).trim().toLowerCase() === String(plugin.name).trim().toLowerCase()
  )
  if (installedModule && !plugin.installed) {
    ElMessage.warning(`无法安装：已安装该插件的 v${installedModule.version} 版本，请先在模块管理中卸载旧版本！`)
    return
  }

  // 打开版本选择对话框；对话框内确认后会调用 doInstallFromStore
  await openVersionPickerForInstall(plugin)
}

/**
 * 真正执行下载安装的核心方法。
 * <p>
 * 当 <code>releaseId</code> 提供时，远端会按指定发版下发令牌，并在升级窗口外直接拒绝；
 * 不传则远端自动选「升级窗口内的最新可用版本」（原默认行为，作为兜底）。
 * </p>
 */
const doInstallFromStore = async (plugin: pluginStoreApi.AvailablePlugin, releaseId?: string) => {
  storeInstalling[plugin.id] = true

  // 初始化通用全屏进度遮罩（与「重启服务并重载」复用同一套 UI），按安装语义重写步骤标签：
  //   阶段 1 requesting  → 下载（前端 POST /system/plugin-store/install，等待后端拉取插件 zip）
  //   阶段 2 stopping    → 安装（后端解压 + 加载 DLL + 写入 web/src/plugins/installed/<short>/）
  //   阶段 3 waiting     → 等待前端就绪（Vite dev 重新评估 import.meta.glob 收集新插件 + 后端 enable）
  //   阶段 4 ready       → 完成（3 秒倒计时到 100%，整页 location.assign 刷新）
  // 这样彻底替换原来 el-dialog + el-progress 的简陋样式，并把固定 1500ms 延迟换成
  // 「带探测的等待」，显著降低 dev 模式下「刷新后路由表里没有新插件 → 404」的概率。
  processOverlay.visible = true
  processOverlay.phase = 'requesting'
  processOverlay.percent = 0
  processOverlay.elapsed = 0
  processOverlay.countdown = 0
  processOverlay.errorMessage = ''
  processOverlay.title = `正在安装 ${plugin.name || plugin.id}`
  processOverlay.subtitle = '正在请求后端下载插件包...'
  processOverlay.hint = '请保持本页面打开，安装过程预计 10-30 秒'
  processOverlay.steps = ['下载插件', '解压安装', '前端就绪', '完成刷新']
  startRestartTicker()

  try {
    // 阶段 1→2：调用安装接口（后端串行做：下载 zip → 解压 → ALC 加载 DLL → 落盘前端文件）。
    // 这是一个长 Promise，前端没法切分内部进度，因此在 await 中途切到 stopping 让指示器走第 2 步，
    // 给用户视觉反馈（实际后端早已进入解压阶段）。
    const phase2Hint = setTimeout(() => {
      if (processOverlay.phase === 'requesting') {
        processOverlay.phase = 'stopping'
        processOverlay.subtitle = '后端正在解压并加载插件，请稍候...'
      }
    }, 2500)
    const result = await pluginStoreApi.installPlugin(plugin.id, plugin.editionId || plugin.id, storeToken.value, releaseId)
    clearTimeout(phase2Hint)

    if (!result.ok) {
      processOverlay.phase = 'failed'
      processOverlay.title = '安装失败'
      processOverlay.subtitle = result.message || '后端返回安装失败'
      processOverlay.hint = '请检查后端日志或联系管理员'
      processOverlay.errorMessage = result.message || ''
      stopRestartTicker()
      ElMessage.error(result.message || '安装失败')
      return
    }

    plugin.installed = true

    // 阶段 3：等待前端运行时识别新插件（Vite dev 文件监听 + import.meta.glob 重新评估需要时间）。
    // 我们做两层动作：
    //   1) 立刻调用 reloadFrontendPluginRuntime 让插件管理器重新装载已识别到的插件
    //   2) 轮询 /api/v1/modules/installed，确认目标模块已出现 + runtimeLoaded=true
    // 这一步**取代**原来固定 1500ms 等待，让"前端真正 ready 了再刷"成为强保证。
    processOverlay.phase = 'waiting'
    processOverlay.subtitle = '正在刷新前端插件能力，等待新插件被运行时识别...'

    try {
      await reloadFrontendPluginRuntime()
    } catch { /* 失败不阻塞，下面的轮询会兜底 */ }

    try {
      const menuStore = useMenuStore()
      menuStore.clearCache()
      await refreshModules()
      await menuStore.loadMenus(true)
    } catch { /* 菜单刷新失败不阻塞，整页刷新后还会再走 onMounted 流程 */ }

    // 智能等待：最多 30 秒，期间每 1.5s 探测一次后端模块列表里有没有这个插件且已运行时加载。
    // 只要后端确认 ready，就立刻进入 ready 阶段刷新；超时则用「兜底刷新」走 location.assign。
    const installDeadline = Date.now() + 30_000
    let pluginReady = false
    while (Date.now() < installDeadline) {
      try {
        const list = await moduleApi.getInstalledModules()
        // plugin.id 通常是商城侧 id，与后端 moduleId 不一定一致；
        // 退而求其次按 name 模糊匹配 + runtimeLoaded 状态判定就绪。
        const hit = (list || []).find(m =>
          m.id === plugin.id ||
          m.name === plugin.name ||
          (plugin.name && m.name && m.name.includes(plugin.name))
        )
        if (hit && (hit.runtimeLoaded === undefined || hit.runtimeLoaded === true)) {
          pluginReady = true
          break
        }
      } catch { /* 后端忙时偶发失败，继续轮询 */ }
      await new Promise(resolve => setTimeout(resolve, 1500))
    }

    // 阶段 4：进入 ready 倒计时（3 秒收尾到 100% 再整页跳转）
    processOverlay.phase = 'ready'
    processOverlay.title = pluginReady ? '插件安装完成' : '插件已安装'
    processOverlay.subtitle = pluginReady
      ? '插件已就绪，即将刷新页面应用最新插件能力...'
      : '后端确认就绪超时，将直接刷新页面尝试加载（仍未生效请重启 API 服务）'
    processOverlay.hint = '页面将在 3 秒后自动刷新'
    restartReadyAt = Date.now()

    setTimeout(() => {
      stopRestartTicker()
      // 与「重启服务」/「本地安装完成」策略保持一致：
      // 用 location.assign(pathname + search) 整页刷新，避开浏览器 history entry / disk cache
      // 沿用前次状态导致首次刷新仍落到 notfound 的问题。
      const target = window.location.pathname + window.location.search
      window.location.assign(target)
    }, 3000)
  } catch (error: unknown) {
    processOverlay.phase = 'failed'
    processOverlay.title = '安装失败'
    processOverlay.subtitle = error instanceof Error ? error.message : '未知错误'
    processOverlay.hint = '请检查网络连接或后端日志'
    processOverlay.errorMessage = error instanceof Error ? error.message : String(error)
    stopRestartTicker()
    ElMessage.error(`安装失败: ${error instanceof Error ? error.message : '安装失败'}`)
  } finally {
    storeInstalling[plugin.id] = false
  }
}

// ==================== 版本选择对话框（远程安装） ====================

/** 当前打开版本对话框的目标插件（null 表示未打开） */
const versionPickerPlugin = ref<pluginStoreApi.AvailablePlugin | null>(null)
/** 该插件档位下「license 视角」的所有可见版本及可下载状态 */
const versionPickerReleases = ref<pluginStoreApi.AvailableReleaseDto[]>([])
/** 当前选中的 release.id（仅 available=true 项可选） */
const versionPickerSelectedId = ref<string>('')
/** 列表加载中 */
const versionPickerLoading = ref(false)

// ==================== 插件协议勾选（下载/购买前置条件） ====================
//
// 设计：按「插件 id + 档位 id」维度独立记录是否勾选，互不影响；
// 点击下载/购买按钮前必须勾选，未勾选时按钮禁用。
// 协议内容在本期先留空占位：只做勾选动作，稍后再在商城侧按插件挂真实协议文本。
/** 已同意协议的档位集合，key = `${pluginId}:${editionId}` */
const pluginAgreementAccepted = reactive<Record<string, boolean>>({})
/** 协议内容预览对话框开关 */
const showAgreementDialog = ref(false)
/** 当前预览协议的上下文（供弹窗展示归属插件/档位名称） */
const agreementContext = ref<{ pluginName: string; editionName: string } | null>(null)

/** 统一 key 格式：pluginId + editionId 维度隔离，避免同插件不同档位彼此污染 */
const agreementKey = (pluginId: string | number | undefined, editionId: string | number | undefined) =>
  `${pluginId ?? ''}:${editionId ?? ''}`

/** 查询某档位是否已勾选同意 */
const isAgreed = (pluginId: string | number | undefined, editionId: string | number | undefined): boolean => {
  return !!pluginAgreementAccepted[agreementKey(pluginId, editionId)]
}

/** 点击协议链接：弹出占位协议预览对话框 */
const openAgreementDialog = (pluginName: string, editionName: string) => {
  agreementContext.value = { pluginName: pluginName || '插件', editionName: editionName || '' }
  showAgreementDialog.value = true
}

/** 打开版本选择对话框：拉取该档位在当前 license 视角下的可下载版本 */
const openVersionPickerForInstall = async (plugin: pluginStoreApi.AvailablePlugin) => {
  versionPickerPlugin.value = plugin
  versionPickerReleases.value = []
  versionPickerSelectedId.value = ''
  versionPickerLoading.value = true
  try {
    const editionId = plugin.editionId || plugin.id
    const list = await pluginStoreApi.listAvailableReleases(editionId, storeToken.value)
    versionPickerReleases.value = Array.isArray(list) ? list : []
    // 默认选中后端标记的 isLatest 项（升级窗口内最新可用版本）
    const latest = versionPickerReleases.value.find(r => r.isLatest && r.available)
      || versionPickerReleases.value.find(r => r.available)
    if (latest) versionPickerSelectedId.value = latest.id
  } catch (e: any) {
    ElMessage.error(e?.message || '获取版本列表失败')
    versionPickerPlugin.value = null
  } finally {
    versionPickerLoading.value = false
  }
}

/** 关闭版本选择对话框（安装进行中不允许关闭） */
const closeVersionPicker = () => {
  if (versionPickerPlugin.value && storeInstalling[versionPickerPlugin.value.id]) return
  versionPickerPlugin.value = null
  versionPickerReleases.value = []
  versionPickerSelectedId.value = ''
}

/** 用户在对话框中确认安装所选版本 */
const confirmVersionInstall = async () => {
  if (!versionPickerPlugin.value || !versionPickerSelectedId.value) return
  const plugin = versionPickerPlugin.value
  const release = versionPickerReleases.value.find(r => r.id === versionPickerSelectedId.value)
  if (!release || !release.available) {
    ElMessage.warning('所选版本不可下载')
    return
  }
  // 防御性 guard：UI 按钮 :disabled 已绑定 isAgreed，这里再兜底一次
  if (!isAgreed(plugin.id, plugin.editionId || plugin.id)) {
    ElMessage.warning('请先阅读并勾选使用协议后再下载')
    return
  }
  // 关闭对话框，进度浮层会接管 UI 反馈
  closeVersionPicker()
  await doInstallFromStore(plugin, release.id)
}

/** 格式化包大小（字节 → KB/MB/GB） */
const formatPackageSize = (size: number | null | undefined): string => {
  if (!size || size <= 0) return '-'
  const KB = 1024, MB = KB * 1024, GB = MB * 1024
  if (size >= GB) return (size / GB).toFixed(2) + ' GB'
  if (size >= MB) return (size / MB).toFixed(2) + ' MB'
  if (size >= KB) return (size / KB).toFixed(2) + ' KB'
  return size + ' B'
}

// 判断版本是否因当前环境而被禁用（生产环境不支持在线安装任何插件）
const isEditionDisabledByEnv = (edition: pluginStoreApi.AvailablePlugin): boolean => {
  if (!envInfo.value) return false
  return !envInfo.value.isDevelopment
}

// 分类标签：优先用远端下发的中文名，其次回退到内置静态 map，再兜底原始 code
const getCategoryLabel = (cat: string): string => {
  if (!cat) return cat
  const remote = remoteCategoryMap[cat]
  if (remote) return remote
  const fallback: Record<string, string> = {
    system_version: '系统版本',
    source_plugin: '源码插件',
    compiled_plugin: '编译插件',
    plugin: '功能插件',
    theme: '主题模板',
    api_integration: 'API集成',
  }
  return fallback[cat] ?? cat
}

/**
 * 解析远程商城静态资源 URL。
 * 远程返回的图片可能是相对路径、商城自身域名、或者绑定的 OSS/CDN 地址；
 * 浏览器直连这些地址常常遇到 SSL 不可信、跨域、混合内容、IP 被拦截等问题，
 * 因此一律走后端 /api/system/plugin-store/asset 代理（后端复用 PluginStoreRemote
 * HttpClient，支持跳过 SSL，并对来源域名做白名单 SSRF 防护）。
 */
function resolveRemoteStoreAssetUrl(path: string | null | undefined): string | undefined {
  const rawPath = path?.trim()
  if (!rawPath) return undefined

  // data:/blob: 由浏览器直接解析，无需代理
  if (/^(data|blob):/i.test(rawPath)) return rawPath

  return `/api/system/plugin-store/asset?url=${encodeURIComponent(rawPath)}`
}

// 获取插件图片（远程 URL 需拼接商城服务地址）
const getPluginImage = (plugin: pluginStoreApi.AvailablePlugin): string | undefined => {
  return resolveRemoteStoreAssetUrl(plugin.imageUrl || plugin.coverUrl)
}

// 显示版本选择对话框
const handleShowEditions = (group: GroupedPlugin) => {
  editionGroup.value = group
  showEditionDialog.value = true
}

/**
 * 跳转到远端商城的插件详情页（升级 / 续费到更高价位版本）。
 * 远端插件详情页路由格式：${serverUrl}/web/plugins/{itemId}
 * 优先使用配置的 serverUrl；未配置时回退到当前页面同源。
 */
const handleUpgradePurchase = (group: GroupedPlugin) => {
  const serverUrl = storeConfig.value.serverUrl?.trim()
  if (!serverUrl) {
    ElMessage.warning('插件商城服务未配置，无法跳转购买页')
    return
  }
  const base = serverUrl.replace(/\/+$/, '')
  const url = `${base}/web/plugins/${encodeURIComponent(group.id)}`
  window.open(url, '_blank', 'noopener,noreferrer')
}

// 购买插件
const handlePurchasePlugin = (plugin: pluginStoreApi.AvailablePlugin) => {
  // 防御性 TS 层 guard：虽然 UI 按钮已用 :disabled 做了阻断，这里兜底防止
  // 外部通过 pendingAction 回调或插件钩子绕过 UI 直接进入购买流程。
  if (!isAgreed(plugin.id, plugin.editionId || plugin.id)) {
    ElMessage.warning('请先阅读并勾选使用协议后再购买')
    return
  }
  if (!storeLoggedIn.value) {
    pendingAction.value = { type: 'purchase', plugin }
    openStoreLoginDialog()
    return
  }
  purchaseTarget.value = plugin
  showPurchaseDialog.value = true
  // 打开购买对话框时实时刷新远端启用的支付渠道，避免硬编码导致用户选了未启用的渠道（如支付宝）
  loadAvailablePaymentChannels()
}

/**
 * 从 store 下单返回的 payParams 中解析出真正的扫码 URL。
 * <p>
 * 与门户 CheckoutPage.vue 严格对齐的解析顺序：
 * <code>code_url</code> → <code>qr_code</code> → <code>qrCode</code> → 顶层字符串本身（且必须是合法 URL scheme）。
 * </p>
 */
const extractQrUrlFromPayParams = (payParams: unknown): string => {
  if (!payParams) return ''
  let qrUrl = ''
  try {
    const parsed = typeof payParams === 'string' ? JSON.parse(payParams) : payParams
    qrUrl = parsed?.code_url || parsed?.qr_code || parsed?.qrCode || ''
    if (!qrUrl && typeof payParams === 'string' && /^[a-z][a-z0-9+\-.]*:/i.test(payParams)) {
      qrUrl = payParams
    }
  } catch {
    if (typeof payParams === 'string') qrUrl = payParams
  }
  return qrUrl
}

/**
 * payParams 缺失时 fallback 主动拉取支付订单详情（与 CheckoutPage.vue 等同行为）。
 * <p>
 * 远端商城 <code>POST /api/plugin-store/orders</code> 在创建链路上偶尔会出现「订单已创建但 payParams
 * 字段尚未写回」的情况（异步派发支付订单创建 / 渠道适配器晚返参等）。当此情况发生时，
 * admin 通过专门的代理接口转发到远端 <code>/api/payment/orders?OrderNo=...</code> 取首条记录补拉 payParams。
 * </p>
 */
const fetchPayParamsFallback = async (paymentOrderNo: string): Promise<string> => {
  if (!paymentOrderNo || !storeToken.value) return ''
  try {
    const res = await pluginStoreApi.getPaymentOrderByNo(paymentOrderNo, storeToken.value)
    const items = res?.items || res?.Items || (Array.isArray(res) ? res : [])
    const payOrder = items?.[0]
    return payOrder?.payParams || payOrder?.PayParams || ''
  } catch (e) {
    console.warn('[PluginStore] fallback 拉取 payParams 失败', e)
    return ''
  }
}

const confirmPurchase = async () => {
  if (!purchaseTarget.value) return
  // 远端没有启用任何渠道时阻止下单（避免后端 BadRequest 体验差）
  if (availablePaymentChannels.value.length === 0) {
    ElMessage.warning('商城当前未启用任何在线支付渠道，无法完成购买')
    return
  }
  if (!availablePaymentChannels.value.includes(purchaseChannel.value)) {
    ElMessage.warning('当前选择的支付方式已被商城禁用，请重新选择')
    return
  }
  const plugin = purchaseTarget.value
  purchaseLoading.value = true
  storePurchasing[plugin.id] = true
  try {
    const channel = purchaseChannel.value || 'wechat'
    const result = await pluginStoreApi.purchasePlugin(plugin.id, plugin.editionId || plugin.id, storeToken.value, channel)

    // 直接领取 / 免费版本短路（remote 端可能直接置 paid）
    if (result.status === 'paid' || result.ok || (typeof result.message === 'string' && result.message.includes('成功'))) {
      ElMessage.success(result.message || '操作成功')
      showPurchaseDialog.value = false
      await loadAvailablePlugins()
      return
    }

    // ===== 扫码支付路径：解析 payParams（缺失时 fallback 拉取） =====
    let payParamsStr: string = result.payParams || ''
    if (!payParamsStr && result.paymentOrderNo) {
      // 与 CheckoutPage.vue 同款 fallback：店内下单后 payParams 暂未回写时再问一次支付订单
      payParamsStr = await fetchPayParamsFallback(result.paymentOrderNo)
    }

    if (!payParamsStr) {
      ElMessage.error(result.message || '购买请求失败，未返回支付信息')
      return
    }

    const qrUrl = extractQrUrlFromPayParams(payParamsStr)
    if (!qrUrl) {
      ElMessage.error('远端商城未返回有效支付链接，请检查所选渠道在远端是否已正确配置（code_url / qr_code 均缺失）')
      return
    }

    paymentOrderNo.value = result.orderNo
    pendingInstallAfterPay.value = plugin
    showPurchaseDialog.value = false
    paymentQrCodeData.value = ''
    paymentPollingMsg.value = '请打开手机扫一扫付款，付款后自动刷新...'
    showPaymentDialog.value = true

    try {
      paymentQrCodeData.value = await QRCode.toDataURL(qrUrl, { width: 220, margin: 1, color: { dark: '#000000FF', light: '#FFFFFFFF' } })
      startPaymentCountdown()
      startPaymentPolling()
    } catch (err) {
      console.error('[PluginStore] 二维码生成失败', err)
      ElMessage.error('二维码生成失败')
    }
  } catch (error: any) {
    ElMessage.error(error.message || '购买操作失败')
  } finally {
    purchaseLoading.value = false
    storePurchasing[plugin.id] = false
  }
}

onMounted(async () => {
  refreshModules()
  await loadStoreConfig()
  
  // 尝试恢复并校验本地 token
  const cachedToken = localStorage.getItem('ginkgo_store_token')
  if (cachedToken) {
    try {
      storeUserInfo.value = await pluginStoreApi.getStoreUserInfo(cachedToken)
      storeToken.value = cachedToken
      storeLoggedIn.value = true
    } catch {
      localStorage.removeItem('ginkgo_store_token')
    }
  }

  // 注册始终生效的兜底监听：覆盖弹窗登录流程之外的全部场景，
  // 保证中继页一旦写入 token，即使弹窗已关闭、focus / message 通道全部丢失，
  // 也能在「下次 storage 事件 / 标签页可见 / BroadcastChannel」任一通道下立刻同步登录态。
  window.addEventListener('storage', persistentStorageHandler)
  document.addEventListener('visibilitychange', persistentVisibilityHandler)
  try {
    if (typeof BroadcastChannel !== 'undefined') {
      persistentBroadcastChannel = new BroadcastChannel('ginkgo-store-login')
      persistentBroadcastChannel.onmessage = (event: MessageEvent) => {
        if (storeLoggedIn.value) return
        const data = event?.data
        if (!data || typeof data !== 'object') return
        if (data.type !== 'ginkgo-store-login') return
        // 优先直接接受 BroadcastChannel 自带的 token，再走标准化恢复流程
        if (data.token && typeof data.token === 'string') {
          localStorage.setItem('ginkgo_store_token', data.token)
        }
        syncStoreLoginFromCachedToken()
      }
    }
  } catch { /* ignore */ }

  // 配置已启用时自动加载插件列表（带上可能存在的 token）
  if (storeConfig.value.enabled) {
    // 并发拉取分类与插件首页：分类即使失败也不阻塞插件列表
    loadStoreCategories()
    loadAvailablePlugins()
  }
})
</script>


<style scoped>
.module-manager-page {
  padding: 24px;
  background: var(--el-bg-color-page);
  min-height: 100vh;
}

.main-card {
  border-radius: 8px;
  border: 1px solid #e5e7eb;
}

.admin-dark .main-card {
  background: #1f2937;
  border-color: #374151;
}

/* 页面标题 */
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-bottom: 16px;
  border-bottom: 1px solid #e5e7eb;
  margin-bottom: 20px;
}

.admin-dark .page-header { border-bottom-color: #374151; }

.page-title h2 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #1f2937;
  display: flex;
  align-items: center;
  gap: 8px;
}

.page-title h2::before {
  content: '';
  width: 3px;
  height: 18px;
  background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
  border-radius: 2px;
}

.admin-dark .page-title h2 { color: #f9fafb; }
.admin-dark .page-title h2::before { background: linear-gradient(135deg, #60a5fa 0%, #3b82f6 100%); }

.page-title p {
  margin: 4px 0 0 11px;
  font-size: 13px;
  color: #6b7280;
}

.admin-dark .page-title p { color: #9ca3af; }

.page-actions { display: flex; gap: 8px; }

/* 环境信息 */
.env-info {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 16px;
  background: #eff6ff;
  border: 1px solid #bfdbfe;
  border-radius: 8px;
  color: #1e40af;
  margin-bottom: 20px;
  font-size: 14px;
}

.env-info.dev-mode { background: #fef3c7; border-color: #fcd34d; color: #92400e; }
.env-info.prod-mode { background: #fef2f2; border-color: #fca5a5; color: #991b1b; }
.admin-dark .env-info { background: rgba(59, 130, 246, 0.1); border-color: rgba(59, 130, 246, 0.3); color: #93c5fd; }
.admin-dark .env-info.dev-mode { background: rgba(245, 158, 11, 0.1); border-color: rgba(245, 158, 11, 0.3); color: #fcd34d; }
.admin-dark .env-info.prod-mode { background: rgba(239, 68, 68, 0.1); border-color: rgba(239, 68, 68, 0.3); color: #fca5a5; }

.production-warning { font-weight: 500; margin-left: 8px; color: #dc2626; }
.admin-dark .production-warning { color: #f87171; }

/* 统计卡片 */
.stats-cards {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
  gap: 12px;
  margin-bottom: 20px;
}

.stat-card {
  background: #f9fafb;
  border-radius: 10px;
  padding: 14px;
  display: flex;
  align-items: center;
  gap: 12px;
}

.admin-dark .stat-card { background: #374151; }

.stat-icon {
  width: 40px;
  height: 40px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 18px;
}

.stat-icon.total { background: #dbeafe; color: #3b82f6; }
.stat-icon.enabled { background: #dcfce7; color: #22c55e; }
/* 非正常：橙色配色（区别于「已禁用」的红色），避免视觉上把"红灯插件"误认为"用户主动禁用" */
.stat-icon.unhealthy { background: #ffedd5; color: #f97316; }
.stat-icon.disabled { background: #fee2e2; color: #ef4444; }
.stat-icon.dev { background: #fef3c7; color: #f59e0b; }

.admin-dark .stat-icon.total { background: rgba(59, 130, 246, 0.2); }
.admin-dark .stat-icon.enabled { background: rgba(34, 197, 94, 0.2); }
.admin-dark .stat-icon.unhealthy { background: rgba(249, 115, 22, 0.2); }
.admin-dark .stat-icon.disabled { background: rgba(239, 68, 68, 0.2); }
.admin-dark .stat-icon.dev { background: rgba(245, 158, 11, 0.2); }

.stat-content h3 { font-size: 20px; font-weight: 600; color: #1f2937; margin: 0; }
.stat-content p { color: #6b7280; margin: 0; font-size: 12px; }
.admin-dark .stat-content h3 { color: #f1f5f9; }
.admin-dark .stat-content p { color: #94a3b8; }

/* 工具栏 */
.toolbar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
}

/* 模块列表 */
.module-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
  min-height: 100px;
}

.module-card {
  background: #f9fafb;
  border-radius: 10px;
  padding: 16px;
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 16px;
  transition: all 0.2s ease;
}

.module-card:hover { background: #f3f4f6; }
.admin-dark .module-card { background: #374151; }
.admin-dark .module-card:hover { background: #3f4a5c; }

.module-info { flex: 1; min-width: 0; }

.module-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
  flex-wrap: wrap;
}

.module-name { font-size: 15px; font-weight: 600; color: #1f2937; margin: 0; }
.admin-dark .module-name { color: #f1f5f9; }

/* 模块运行健康指示灯（绿=全绿、红=任一异常） */
.module-status-light {
  display: inline-block;
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: #22c55e;            /* 绿色：runtime + server DLL + (若声明) 菜单注册 全部正常 */
  box-shadow: 0 0 0 2px rgba(34, 197, 94, 0.18);
  flex-shrink: 0;
}
.module-status-light.is-red {
  background: #ef4444;             /* 红色：runtime / server DLL / 菜单注册 任一为否 */
  box-shadow: 0 0 0 2px rgba(239, 68, 68, 0.2);
}

.module-version {
  background: #e5e7eb;
  color: #6b7280;
  padding: 2px 6px;
  border-radius: 4px;
  font-size: 11px;
}

.admin-dark .module-version { background: #4b5563; color: #9ca3af; }

.module-description { color: #6b7280; margin: 0 0 8px 0; font-size: 13px; }
.admin-dark .module-description { color: #9ca3af; }

.module-meta {
  display: flex;
  gap: 12px;
  font-size: 12px;
  color: #9ca3af;
  flex-wrap: wrap;
}

.meta-item { display: flex; align-items: center; gap: 4px; }

.module-actions { display: flex; align-items: center; gap: 8px; flex-shrink: 0; }

/* 对话框 */
:deep(.el-dialog) { border-radius: 12px; }
:deep(.el-dialog__header) { background: linear-gradient(to right, #f9fafb 0%, #ffffff 100%); border-bottom: 1px solid #e5e7eb; padding: 20px 24px; margin: 0; }
.admin-dark :deep(.el-dialog__header) { background: linear-gradient(to right, #1f2937 0%, #1a2332 100%); border-bottom-color: #374151; }
:deep(.el-dialog__title) { font-size: 18px; font-weight: 600; color: #1f2937; }
.admin-dark :deep(.el-dialog__title) { color: #f9fafb; }
:deep(.el-dialog__body) { padding: 24px; }
:deep(.el-dialog__footer) { padding: 16px 24px; border-top: 1px solid #f3f4f6; }
.admin-dark :deep(.el-dialog__footer) { border-top-color: #374151; }

/* 结果样式 */
.validation-result, .install-result, .package-result { margin-top: 16px; padding: 16px; border-radius: 8px; }
.validation-result.success, .install-result.success, .package-result.success { background: #f0fdf4; border: 1px solid #86efac; }
.validation-result.error, .install-result.error, .package-result.error { background: #fef2f2; border: 1px solid #fca5a5; }
.admin-dark .validation-result.success, .admin-dark .install-result.success, .admin-dark .package-result.success { background: rgba(34, 197, 94, 0.1); border-color: rgba(34, 197, 94, 0.3); }
.admin-dark .validation-result.error, .admin-dark .install-result.error, .admin-dark .package-result.error { background: rgba(239, 68, 68, 0.1); border-color: rgba(239, 68, 68, 0.3); }

.result-header { display: flex; align-items: center; gap: 8px; font-weight: 500; margin-bottom: 8px; }
.validation-result.success .result-header, .install-result.success .result-header, .package-result.success .result-header { color: #166534; }
.validation-result.error .result-header, .install-result.error .result-header, .package-result.error .result-header { color: #991b1b; }
.admin-dark .validation-result.success .result-header, .admin-dark .install-result.success .result-header, .admin-dark .package-result.success .result-header { color: #86efac; }
.admin-dark .validation-result.error .result-header, .admin-dark .install-result.error .result-header, .admin-dark .package-result.error .result-header { color: #fca5a5; }

.result-details p { margin: 4px 0; font-size: 13px; color: #374151; }
.admin-dark .result-details p { color: #e2e8f0; }

.result-steps { margin-top: 12px; }
.result-steps .step-item { padding: 2px 0; font-family: 'Consolas', 'Monaco', monospace; }
.result-steps .el-collapse { border: none; }
.result-steps .el-collapse-item__header { font-size: 13px; color: #6b7280; height: 32px; line-height: 32px; }

.upload-progress, .package-progress { margin-top: 16px; }
.progress-text { text-align: center; color: #6b7280; font-size: 13px; margin-top: 8px; }

/* 响应式 */
@media (max-width: 768px) {
  .module-manager-page { padding: 16px; }
  .page-header { flex-direction: column; gap: 16px; align-items: flex-start; }
  .page-actions { width: 100%; justify-content: flex-end; }
  .toolbar { flex-direction: column; align-items: stretch; }
  .toolbar .el-input, .toolbar .el-select { width: 100% !important; }
  .module-card { flex-direction: column; }
  .module-actions { justify-content: flex-end; margin-top: 12px; }
  .stats-cards { grid-template-columns: repeat(2, 1fr); }
}

/* 模块配置对话框 */
.config-file-selector {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 16px;
  padding-bottom: 12px;
  border-bottom: 1px solid #e5e7eb;
}

.admin-dark .config-file-selector { border-bottom-color: #374151; }

.selector-label { font-size: 13px; color: #6b7280; white-space: nowrap; }
.admin-dark .selector-label { color: #9ca3af; }

.config-group-desc {
  color: #6b7280;
  font-size: 13px;
  margin: 0 0 16px 0;
  padding: 8px 12px;
  background: #f9fafb;
  border-radius: 6px;
  border-left: 3px solid #3b82f6;
}

.admin-dark .config-group-desc { background: #1f2937; color: #9ca3af; border-left-color: #60a5fa; }

.config-form { max-width: 560px; }

.config-form :deep(.el-form-item) { margin-bottom: 18px; }
.config-form :deep(.el-form-item__label) { font-size: 13px; color: #374151; }
.admin-dark .config-form :deep(.el-form-item__label) { color: #d1d5db; }

.config-item-tip {
  font-size: 12px;
  color: #9ca3af;
  margin-top: 4px;
  line-height: 1.4;
}

.admin-dark .config-item-tip { color: #6b7280; }

/* 分组标题区域（包含desc和申请链接） */
.config-group-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 16px;
}

.config-group-header .config-group-desc { margin-bottom: 0; flex: 1; }

/* 前往申请链接按钮 */
.config-apply-link {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 6px 14px;
  border-radius: 6px;
  font-size: 13px;
  font-weight: 500;
  color: #3b82f6;
  background: #eff6ff;
  border: 1px solid #bfdbfe;
  text-decoration: none;
  white-space: nowrap;
  transition: all 0.2s ease;
  flex-shrink: 0;
}

.config-apply-link:hover {
  background: #dbeafe;
  border-color: #93c5fd;
  color: #2563eb;
}

.config-apply-link i { font-size: 12px; }

.admin-dark .config-apply-link {
  background: rgba(59,130,246,0.1);
  border-color: rgba(59,130,246,0.3);
  color: #93c5fd;
}

.admin-dark .config-apply-link:hover {
  background: rgba(59,130,246,0.2);
  color: #60a5fa;
}

/* link 类型配置项（外链按钮样式） */
.config-link-btn {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 5px 12px;
  border-radius: 6px;
  font-size: 13px;
  color: #3b82f6;
  background: #f0f5ff;
  border: 1px solid #bfdbfe;
  text-decoration: none;
  transition: all 0.2s ease;
  cursor: pointer;
}

.config-link-btn:hover {
  background: #dbeafe;
  color: #1d4ed8;
}

.config-link-btn i { font-size: 11px; }

.admin-dark .config-link-btn {
  background: rgba(59,130,246,0.08);
  border-color: rgba(59,130,246,0.25);
  color: #93c5fd;
}

.admin-dark .config-link-btn:hover {
  background: rgba(59,130,246,0.15);
  color: #60a5fa;
}

/* file 类型配置项 */
.config-file-input {
  width: 100%;
}


.config-dialog-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.config-dialog-footer > div { display: flex; gap: 8px; }

/* 插件商店 */
.plugin-store-content { padding: 8px 0; }
.store-unconfigured-section { display: flex; justify-content: center; padding: 40px 0; }
.store-config-example { background: #1f2937; color: #e5e7eb; border-radius: 8px; padding: 12px 16px; margin-top: 12px; font-family: 'Consolas', 'Monaco', monospace; font-size: 13px; overflow-x: auto; }
.store-config-example pre { margin: 0; white-space: pre; }
.admin-dark .store-config-example { background: #111827; color: #d1d5db; }
.store-login-section { display: flex; justify-content: center; padding: 40px 0; }
.store-login-card { background: #f9fafb; border-radius: 12px; padding: 32px; max-width: 480px; width: 100%; }
.store-login-card h3 { margin: 0 0 8px 0; font-size: 18px; color: #1f2937; display: flex; align-items: center; }
.store-login-card p { color: #6b7280; margin: 0 0 8px 0; font-size: 14px; }
.admin-dark .store-login-card { background: #374151; }
.admin-dark .store-login-card h3 { color: #f1f5f9; }
.admin-dark .store-login-card p { color: #9ca3af; }

.store-server-url {
  display: flex; align-items: center; gap: 6px;
  padding: 8px 12px; border-radius: 6px; font-size: 13px;
  background: #eff6ff; color: #3b82f6; margin-bottom: 16px;
  border: 1px solid #bfdbfe;
}
.admin-dark .store-server-url { background: rgba(59,130,246,0.1); border-color: rgba(59,130,246,0.3); color: #93c5fd; }

.store-toolbar { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 16px; padding-bottom: 12px; border-bottom: 1px solid #e5e7eb; }
.admin-dark .store-toolbar { border-bottom-color: #374151; }
.store-user-section { display: flex; align-items: center; gap: 8px; }
.store-toolbar-actions { display: flex; gap: 8px; }
.store-user-info { font-size: 13px; color: #22c55e; display: flex; align-items: center; gap: 4px; }

.store-category-filter { margin-bottom: 16px; display: flex; align-items: center; flex-wrap: wrap; gap: 8px; }

.store-plugin-list { display: flex; flex-direction: column; gap: 12px; min-height: 100px; }
.store-plugin-pagination { display: flex; justify-content: flex-end; margin-top: 16px; }
.store-plugin-card { background: #f9fafb; border-radius: 10px; padding: 16px; display: flex; align-items: flex-start; gap: 16px; transition: all 0.2s ease; }
.store-plugin-card:hover { background: #f3f4f6; transform: translateY(-1px); box-shadow: 0 2px 8px rgba(0,0,0,0.06); }
.admin-dark .store-plugin-card { background: #374151; }
.admin-dark .store-plugin-card:hover { background: #3f4a5c; box-shadow: 0 2px 8px rgba(0,0,0,0.2); }
.store-plugin-info { flex: 1; min-width: 0; }
.store-plugin-header { display: flex; align-items: center; gap: 8px; margin-bottom: 6px; flex-wrap: wrap; }
.store-plugin-header h3 { font-size: 15px; font-weight: 600; color: #1f2937; margin: 0; }
.admin-dark .store-plugin-header h3 { color: #f1f5f9; }

.store-plugin-cover {
  width: 80px; height: 80px; border-radius: 10px; overflow: hidden; flex-shrink: 0;
  background: #e5e7eb; display: flex; align-items: center; justify-content: center;
}
.store-plugin-cover img { width: 100%; height: 100%; object-fit: cover; }
.store-plugin-cover-placeholder { color: #9ca3af; font-size: 28px; background: linear-gradient(135deg, #e0e7ff 0%, #dbeafe 100%); }
.admin-dark .store-plugin-cover { background: #4b5563; }
.admin-dark .store-plugin-cover-placeholder { background: linear-gradient(135deg, #312e81 0%, #1e3a5f 100%); color: #6b7280; }

.store-price { font-weight: 500; }
.price-value { color: #ef4444; font-weight: 600; font-size: 14px; }
.price-free { color: #22c55e; font-weight: 500; }
.admin-dark .price-value { color: #f87171; }
.admin-dark .price-free { color: #4ade80; }

.store-install-progress { text-align: center; padding: 16px 0; }

/* ===== 远程安装版本选择对话框 ===== */
.store-version-picker { padding: 4px 0; }
.store-version-picker .picker-tip {
  margin-bottom: 8px; padding: 8px 12px;
  background: #f5f7fa; border-radius: 6px;
  color: #555; font-size: 13.5px;
  display: flex; align-items: center; flex-wrap: wrap; gap: 4px;
}
.store-version-picker .picker-tip strong { color: #303133; }
.store-version-picker .picker-window-tip {
  margin-bottom: 12px; padding: 8px 12px;
  background: #fff7e6; border-left: 3px solid #faad14;
  color: #874d00; font-size: 13px; line-height: 1.55; border-radius: 4px;
}
.store-version-picker .picker-loading,
.store-version-picker .picker-empty {
  text-align: center; padding: 30px 12px; color: #909399;
}
.store-version-picker .picker-loading-spin {
  display: inline-block; margin-right: 6px; font-size: 16px;
  animation: store-version-picker-spin 1s linear infinite;
}
@keyframes store-version-picker-spin {
  from { transform: rotate(0deg); }
  to   { transform: rotate(360deg); }
}
.store-version-picker .version-radio-list {
  display: flex; flex-direction: column; gap: 8px;
  max-height: 50vh; overflow-y: auto;
  width: 100%;
}
/* el-radio-group 默认 inline + flex-wrap，这里强制竖排 */
.store-version-picker .version-radio-list :deep(.el-radio) { width: 100%; margin: 0; height: auto; align-items: flex-start; }
.store-version-picker .version-radio-list :deep(.el-radio__label) { width: 100%; padding-left: 8px; }
.store-version-picker .version-row {
  border: 1px solid #e4e7ed; border-radius: 8px;
  padding: 10px 12px; cursor: pointer; background: #fff;
  transition: border-color 0.18s ease, background-color 0.18s ease, box-shadow 0.18s ease;
}
.store-version-picker .version-row:hover:not(.disabled) { border-color: #409eff; background: #f5faff; }
.store-version-picker .version-row.selected {
  border-color: #409eff; background: #ecf5ff;
  box-shadow: 0 0 0 1px #409eff inset;
}
.store-version-picker .version-row.latest:not(.selected) { border-color: #67c23a; }
.store-version-picker .version-row.disabled { cursor: not-allowed; opacity: 0.65; background: #fafafa; }
.store-version-picker .version-row-main { flex: 1; min-width: 0; padding-top: 1px; }
.store-version-picker .version-row-title {
  display: flex; flex-wrap: wrap; align-items: center; gap: 8px;
  font-size: 14.5px; font-weight: 600; color: #303133;
}
.store-version-picker .version-no { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; }
.store-version-picker .version-row-meta {
  margin-top: 4px; font-size: 12.5px; color: #909399;
  display: flex; flex-wrap: wrap; gap: 1.25rem;
}
.store-version-picker .version-row-block-tip {
  margin-top: 6px; padding: 4px 8px;
  background: #fff1f0; border-left: 3px solid #f56c6c;
  color: #b94a4a; font-size: 12.5px; border-radius: 3px;
}
.store-version-picker .version-row-log {
  margin-top: 6px; padding: 6px 8px;
  background: #f5f7fa; color: #666;
  font-size: 12.5px; line-height: 1.5; border-radius: 4px;
  white-space: pre-wrap; word-break: break-word;
  max-height: 6.5em; overflow: auto;
}

/* 暗色模式 */
.admin-dark .store-version-picker .picker-tip { background: #1f2937; color: #cbd5e1; }
.admin-dark .store-version-picker .picker-tip strong { color: #f1f5f9; }
.admin-dark .store-version-picker .picker-window-tip { background: rgba(212,136,6,0.12); color: #facc15; border-left-color: #d48806; }
.admin-dark .store-version-picker .version-row { background: #1f2937; border-color: #334155; }
.admin-dark .store-version-picker .version-row:hover:not(.disabled) { background: #1e293b; border-color: #3b82f6; }
.admin-dark .store-version-picker .version-row.selected { background: #1e3a5f; border-color: #3b82f6; box-shadow: 0 0 0 1px #3b82f6 inset; }
.admin-dark .store-version-picker .version-row.disabled { background: #111827; opacity: 0.55; }
.admin-dark .store-version-picker .version-row-title { color: #f1f5f9; }
.admin-dark .store-version-picker .version-row-meta { color: #94a3b8; }
.admin-dark .store-version-picker .version-row-block-tip { background: rgba(245,108,108,0.15); color: #fca5a5; border-left-color: #ef4444; }
.admin-dark .store-version-picker .version-row-log { background: #0f172a; color: #cbd5e1; }

/* 可点击的插件卡片 */
.store-plugin-card-clickable { cursor: pointer; }
.edition-count { font-size: 12px; color: #9ca3af; margin-left: 4px; }
.admin-dark .edition-count { color: #6b7280; }

/* 版本选择对话框 */
.edition-plugin-desc { margin-bottom: 16px; }
.edition-plugin-desc p { margin: 0; color: #6b7280; font-size: 13px; line-height: 1.6; }
.admin-dark .edition-plugin-desc p { color: #9ca3af; }

.edition-list { display: flex; flex-direction: column; gap: 12px; }
.edition-card { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 14px 16px; border-radius: 10px; background: #f9fafb; border: 1px solid #e5e7eb; transition: all 0.2s ease; }
.edition-card:hover { background: #f3f4f6; border-color: #d1d5db; }
.edition-card-free { border-color: #86efac; background: #f0fdf4; }
.edition-card-free:hover { background: #dcfce7; }
.admin-dark .edition-card { background: #374151; border-color: #4b5563; }
.admin-dark .edition-card:hover { background: #3f4a5c; }
.admin-dark .edition-card-free { background: rgba(34,197,94,0.08); border-color: rgba(34,197,94,0.3); }
.admin-dark .edition-card-free:hover { background: rgba(34,197,94,0.12); }
.edition-card-disabled { opacity: 0.55; border-color: #d1d5db; background: #f3f4f6; }
.edition-card-disabled:hover { background: #f3f4f6; border-color: #d1d5db; }
.admin-dark .edition-card-disabled { background: #2d3748; border-color: #4a5568; }
.admin-dark .edition-card-disabled:hover { background: #2d3748; }

/* 运行环境提示 */
.env-hint { display: flex; align-items: center; gap: 6px; padding: 8px 12px; border-radius: 6px; font-size: 13px; margin-top: 10px; color: #374151; }
.env-hint i { font-size: 15px; }
.env-hint-dev { background: #eff6ff; border: 1px solid #bfdbfe; color: #1e40af; }
.env-hint-prod { background: #fffbeb; border: 1px solid #fde68a; color: #92400e; }
.admin-dark .env-hint-dev { background: rgba(59,130,246,0.1); border-color: rgba(59,130,246,0.3); color: #93c5fd; }
.admin-dark .env-hint-prod { background: rgba(245,158,11,0.1); border-color: rgba(245,158,11,0.3); color: #fcd34d; }

.edition-info { flex: 1; min-width: 0; }
.edition-header { display: flex; align-items: center; gap: 8px; margin-bottom: 6px; flex-wrap: wrap; }
.edition-header h4 { margin: 0; font-size: 14px; font-weight: 600; color: #1f2937; }
.admin-dark .edition-header h4 { color: #f1f5f9; }
.edition-price { font-size: 13px; }
.edition-price .price-free { color: #22c55e; }
.edition-price .price-value { color: #ef4444; font-weight: 600; }
.admin-dark .edition-price .price-free { color: #4ade80; }
.admin-dark .edition-price .price-value { color: #f87171; }
.edition-actions { flex-shrink: 0; display: flex; flex-direction: column; align-items: flex-end; gap: 6px; }

/* ===== 协议勾选（下载/购买前置条件） ===== */
.version-picker-agreement {
  margin-top: 14px;
  padding: 10px 12px;
  background: #f8fafc;
  border: 1px dashed #cbd5e1;
  border-radius: 6px;
}
.edition-agreement { padding: 4px 0; text-align: right; }
.agreement-text { font-size: 13px; color: #555; }
.agreement-link { color: #409eff; text-decoration: none; }
.agreement-link:hover { text-decoration: underline; }
.agreement-placeholder { padding: 6px 0 2px; }
.agreement-placeholder p { margin: 0 0 12px; color: #303133; }
.admin-dark .version-picker-agreement { background: #0f172a; border-color: #334155; }
.admin-dark .agreement-text { color: #cbd5e1; }
.admin-dark .agreement-link { color: #60a5fa; }
.admin-dark .agreement-placeholder p { color: #f1f5f9; }

/* 购买确认对话框 */
.purchase-confirm { padding: 8px 0; }
.purchase-item-info { margin-bottom: 16px; }
.purchase-item-info h4 { margin: 0 0 8px 0; font-size: 16px; font-weight: 600; color: #1f2937; }
.purchase-item-info p { margin: 0; color: #6b7280; font-size: 13px; }
.admin-dark .purchase-item-info h4 { color: #f1f5f9; }
.admin-dark .purchase-item-info p { color: #9ca3af; }

.purchase-price-info { background: #f9fafb; border-radius: 8px; padding: 16px; }
.admin-dark .purchase-price-info { background: #374151; }
.price-row { display: flex; justify-content: space-between; align-items: center; padding: 6px 0; font-size: 14px; }
.price-row .price-value { font-size: 18px; }
.balance-insufficient { color: #ef4444; font-weight: 500; }
.admin-dark .balance-insufficient { color: #f87171; }

/* API 可视化选择器 */
.api-selector-wrapper { width: 100%; }
.api-tags-container { display: flex; flex-wrap: wrap; gap: 8px; min-height: 32px; padding: 8px; background: #f5f7fa; border-radius: 6px; border: 1px solid #e4e7ed; }
.admin-dark .api-tags-container { background: #374151; border-color: #4b5563; }
.api-tag { display: flex; flex-direction: column; align-items: flex-start; height: auto !important; padding: 4px 8px; }
.api-tag-label { font-weight: 500; font-size: 13px; line-height: 1.4; }
.api-tag-path { font-size: 11px; color: #909399; font-family: 'Consolas', monospace; line-height: 1.3; }
.api-selector-toolbar { display: flex; align-items: center; }

/* 快照回滚列表 */
.snapshot-item { border: 1px solid var(--el-border-color-light); border-radius: 8px; padding: 10px 14px; cursor: pointer; transition: border-color .2s, background .2s; }
.snapshot-item.active { border-color: var(--el-color-warning); background: var(--el-color-warning-light-9); }
.snapshot-info { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
.snap-version { font-weight: 600; font-size: 13px; font-family: 'Consolas', monospace; }
.snap-time { font-size: 12px; color: var(--el-text-color-secondary); }
.snap-size { font-size: 12px; color: var(--el-text-color-placeholder); margin-left: auto; }
.admin-dark .snapshot-item { border-color: #374151; }
.admin-dark .snapshot-item.active { border-color: var(--el-color-warning); background: rgba(234,179,8,.12); }

/* ====================== 重启 API 进程 全屏进度遮罩 ====================== */
/* 遮罩通过 Teleport 挂到 body，z-index 比 ElDialog (~2001) 高，保证不被遮挡。 */
.process-overlay {
  position: fixed;
  inset: 0;
  z-index: 9999;
  display: flex;
  align-items: center;
  justify-content: center;
  background: radial-gradient(ellipse at center, rgba(15, 23, 42, 0.78) 0%, rgba(2, 6, 23, 0.92) 100%);
  backdrop-filter: blur(6px);
  -webkit-backdrop-filter: blur(6px);
}

/* 进入/离开过渡，避免遮罩出现时的硬切感 */
.process-overlay-fade-enter-active,
.process-overlay-fade-leave-active {
  transition: opacity 0.25s ease;
}
.process-overlay-fade-enter-active .process-overlay-card,
.process-overlay-fade-leave-active .process-overlay-card {
  transition: transform 0.3s cubic-bezier(0.16, 1, 0.3, 1), opacity 0.25s ease;
}
.process-overlay-fade-enter-from,
.process-overlay-fade-leave-to {
  opacity: 0;
}
.process-overlay-fade-enter-from .process-overlay-card,
.process-overlay-fade-leave-to .process-overlay-card {
  transform: translateY(16px) scale(0.96);
  opacity: 0;
}

.process-overlay-card {
  width: min(520px, calc(100% - 48px));
  background: #ffffff;
  border-radius: 18px;
  padding: 36px 40px 28px;
  box-shadow:
    0 24px 60px -12px rgba(0, 0, 0, 0.45),
    0 0 0 1px rgba(255, 255, 255, 0.06);
  text-align: center;
  position: relative;
  overflow: hidden;
}

/* 顶部装饰渐变条，按 phase 切换颜色 */
.process-overlay-card::before {
  content: '';
  position: absolute;
  top: 0; left: 0; right: 0;
  height: 4px;
  background: linear-gradient(90deg, #3b82f6, #8b5cf6, #3b82f6);
  background-size: 200% 100%;
  animation: restart-gradient-flow 3s linear infinite;
}
.process-overlay.is-ready .process-overlay-card::before {
  background: linear-gradient(90deg, #10b981, #22c55e, #10b981);
  animation: none;
}
.process-overlay.is-timeout .process-overlay-card::before,
.process-overlay.is-failed .process-overlay-card::before {
  background: linear-gradient(90deg, #ef4444, #f97316, #ef4444);
  animation: none;
}
@keyframes restart-gradient-flow {
  0% { background-position: 0% 50%; }
  100% { background-position: 200% 50%; }
}

/* 顶部图标 + 旋转光环 */
.process-overlay-icon-wrap {
  position: relative;
  width: 72px;
  height: 72px;
  margin: 0 auto 18px;
}
.process-overlay-icon {
  width: 100%;
  height: 100%;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 32px;
  color: #fff;
  background: linear-gradient(135deg, #3b82f6, #6366f1);
  box-shadow: 0 8px 24px -6px rgba(59, 130, 246, 0.55);
  position: relative;
  z-index: 1;
}
.process-overlay-icon.icon-requesting,
.process-overlay-icon.icon-stopping,
.process-overlay-icon.icon-waiting {
  animation: restart-icon-spin 2s linear infinite;
}
.process-overlay-icon.icon-ready {
  background: linear-gradient(135deg, #10b981, #22c55e);
  box-shadow: 0 8px 24px -6px rgba(34, 197, 94, 0.55);
  animation: restart-icon-pop 0.4s cubic-bezier(0.34, 1.56, 0.64, 1);
}
.process-overlay-icon.icon-timeout,
.process-overlay-icon.icon-failed {
  background: linear-gradient(135deg, #ef4444, #f97316);
  box-shadow: 0 8px 24px -6px rgba(239, 68, 68, 0.55);
}
@keyframes restart-icon-spin {
  to { transform: rotate(360deg); }
}
@keyframes restart-icon-pop {
  0% { transform: scale(0.6); opacity: 0; }
  100% { transform: scale(1); opacity: 1; }
}

/* 旋转光环（脉冲扩散） */
.process-overlay-icon-ring {
  position: absolute;
  inset: -10px;
  border-radius: 50%;
  border: 2px solid rgba(59, 130, 246, 0.35);
  animation: restart-ring-pulse 1.6s ease-out infinite;
  z-index: 0;
}
@keyframes restart-ring-pulse {
  0%   { transform: scale(0.85); opacity: 0.85; }
  100% { transform: scale(1.45); opacity: 0; }
}

/* 标题与副标题 */
.process-overlay-title {
  font-size: 20px;
  font-weight: 600;
  color: #0f172a;
  margin: 0 0 6px;
  letter-spacing: 0.5px;
}
.process-overlay-subtitle {
  font-size: 13px;
  color: #64748b;
  margin: 0 0 22px;
  min-height: 18px;
}

/* 步骤指示器（圆点 + 横线） */
.process-overlay-steps {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin: 0 8px 22px;
}
.process-overlay-steps .step {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  flex-shrink: 0;
}
.process-overlay-steps .step-dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  background: #e2e8f0;
  border: 2px solid #e2e8f0;
  transition: all 0.3s ease;
}
.process-overlay-steps .step.active .step-dot {
  background: #3b82f6;
  border-color: #3b82f6;
  box-shadow: 0 0 0 4px rgba(59, 130, 246, 0.18);
  animation: restart-step-pulse 1.4s ease-in-out infinite;
}
.process-overlay-steps .step.done .step-dot {
  background: #10b981;
  border-color: #10b981;
}
.process-overlay.is-ready .process-overlay-steps .step.active .step-dot {
  background: #10b981;
  border-color: #10b981;
  box-shadow: 0 0 0 4px rgba(16, 185, 129, 0.18);
}
.process-overlay-steps .step-label {
  font-size: 11px;
  color: #94a3b8;
  white-space: nowrap;
}
.process-overlay-steps .step.active .step-label,
.process-overlay-steps .step.done .step-label {
  color: #475569;
  font-weight: 500;
}
.process-overlay-steps .step-line {
  flex: 1;
  height: 2px;
  background: #e2e8f0;
  margin: 0 6px;
  margin-bottom: 18px; /* 与圆点垂直对齐 */
  position: relative;
  overflow: hidden;
  border-radius: 2px;
}
.process-overlay-steps .step-line.done {
  background: #10b981;
}
@keyframes restart-step-pulse {
  0%, 100% { transform: scale(1); }
  50%      { transform: scale(1.25); }
}

/* 进度条主体 */
.process-overlay-progress {
  width: 100%;
  height: 10px;
  background: #f1f5f9;
  border-radius: 999px;
  overflow: hidden;
  position: relative;
}
.process-overlay-progress-bar {
  height: 100%;
  border-radius: 999px;
  background: linear-gradient(90deg, #3b82f6, #6366f1);
  transition: width 0.4s cubic-bezier(0.4, 0, 0.2, 1);
  position: relative;
  overflow: hidden;
}
.process-overlay.is-ready .process-overlay-progress-bar {
  background: linear-gradient(90deg, #10b981, #22c55e);
}
.process-overlay.is-timeout .process-overlay-progress-bar,
.process-overlay.is-failed .process-overlay-progress-bar {
  background: linear-gradient(90deg, #ef4444, #f97316);
}
/* 进度条上滑动的高光条带 */
.process-overlay-progress-shine {
  position: absolute;
  inset: 0;
  background: linear-gradient(90deg, transparent, rgba(255,255,255,0.45), transparent);
  animation: restart-shine-move 1.6s linear infinite;
}
.process-overlay.is-ready .process-overlay-progress-shine,
.process-overlay.is-timeout .process-overlay-progress-shine,
.process-overlay.is-failed .process-overlay-progress-shine {
  display: none; /* 完成/异常 时停止流动光带 */
}
@keyframes restart-shine-move {
  0%   { transform: translateX(-100%); }
  100% { transform: translateX(100%); }
}

/* 元数据：百分比 + 已用秒数 + 倒计时 */
.process-overlay-meta {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: #64748b;
  margin-top: 10px;
  font-variant-numeric: tabular-nums;
}
.process-overlay-meta .meta-percent {
  color: #1f2937;
  font-weight: 600;
  font-size: 13px;
}
.process-overlay-meta .meta-divider { color: #cbd5e1; }
.process-overlay-meta .meta-countdown {
  color: #10b981;
  font-weight: 500;
}
.process-overlay.is-timeout .process-overlay-meta .meta-percent,
.process-overlay.is-failed .process-overlay-meta .meta-percent {
  color: #dc2626;
}

/* 底部提示文案 */
.process-overlay-hint {
  font-size: 12px;
  color: #94a3b8;
  margin: 18px 0 0;
  line-height: 1.6;
}
.process-overlay-error {
  font-size: 12px;
  color: #dc2626;
  margin: 8px 0 0;
  word-break: break-all;
  background: rgba(239, 68, 68, 0.06);
  padding: 8px 10px;
  border-radius: 8px;
  border: 1px solid rgba(239, 68, 68, 0.2);
}
.process-overlay-actions {
  margin-top: 18px;
  display: flex;
  justify-content: center;
  gap: 8px;
}

/* 暗色模式适配 */
.admin-dark .process-overlay-card {
  background: #1e293b;
  box-shadow:
    0 24px 60px -12px rgba(0, 0, 0, 0.6),
    0 0 0 1px rgba(255, 255, 255, 0.04);
}
.admin-dark .process-overlay-title { color: #f1f5f9; }
.admin-dark .process-overlay-subtitle { color: #94a3b8; }
.admin-dark .process-overlay-steps .step-dot { background: #334155; border-color: #334155; }
.admin-dark .process-overlay-steps .step-line { background: #334155; }
.admin-dark .process-overlay-steps .step-label { color: #64748b; }
.admin-dark .process-overlay-steps .step.active .step-label,
.admin-dark .process-overlay-steps .step.done .step-label { color: #cbd5e1; }
.admin-dark .process-overlay-progress { background: #0f172a; }
.admin-dark .process-overlay-meta .meta-percent { color: #f1f5f9; }
.admin-dark .process-overlay-meta .meta-divider { color: #475569; }
.admin-dark .process-overlay-hint { color: #64748b; }

/* 小屏适配 */
@media (max-width: 600px) {
  .process-overlay-card { padding: 28px 22px 22px; }
  .process-overlay-steps .step-label { font-size: 10px; }
  .process-overlay-icon-wrap { width: 60px; height: 60px; }
  .process-overlay-icon { font-size: 26px; }
}
</style>
