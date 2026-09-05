<template>
  <el-container class="admin-layout">
    <el-aside width="220px" class="admin-layout__aside">
      <div class="admin-layout__logo"><span>映智汇认证平台</span></div>
      <el-menu :default-active="activeMenu" router class="admin-layout__menu" background-color="#304156" text-color="#bfcbd9" active-text-color="#409eff">
        <el-menu-item index="/system/user">
          <span>系统管理</span>
        </el-menu-item>
        <el-sub-menu index="cert">
          <template #title>体系认证</template>
          <el-menu-item index="/cert/iso-standard">ISO 标准</el-menu-item>
          <el-menu-item index="/cert/iso-clause">ISO 条款</el-menu-item>
          <el-menu-item index="/cert/cert-stage">认证阶段</el-menu-item>
          <el-menu-item index="/cert/cert-body">认证机构</el-menu-item>
          <el-menu-item index="/cert/enterprise">企业管理</el-menu-item>
        </el-sub-menu>
        <el-sub-menu index="business">
          <template #title>业务管理</template>
          <el-menu-item index="/business/directory-manager">标准目录管理</el-menu-item>
          <el-menu-item index="/business/doc-extraction-rule">文档提取规则</el-menu-item>
          <el-menu-item index="/business/nc-config">NC 规则配置</el-menu-item>
          <el-menu-item index="/business/report-rule-config">报告章节定义</el-menu-item>
          <el-menu-item index="/business/prompt-template">Prompt 模板</el-menu-item>
          <el-menu-item index="/business/skill-manage">Skill 管理</el-menu-item>
          <el-menu-item index="/business/sys-config-manager">系统参数配置</el-menu-item>
          <el-menu-item index="/business/ai-usage">AI 费用监控</el-menu-item>
        </el-sub-menu>
      </el-menu>
    </el-aside>

    <el-container>
      <el-header class="admin-layout__header">
        <div class="admin-layout__header-left"><span class="admin-layout__page-title">{{ pageTitle }}</span></div>
        <div class="admin-layout__header-right">
          <el-dropdown>
            <span class="admin-layout__user">{{ authStore.userInfo?.userName || '管理员' }}<el-icon><arrow-down /></el-icon></span>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item @click="handleLogout">退出登录</el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </el-header>

      <el-main class="admin-layout__main">
        <router-view v-slot="{ Component }">
          <keep-alive><component :is="Component" /></keep-alive>
        </router-view>
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowDown } from '@element-plus/icons-vue'
import { useAuthStore } from '@/store/auth'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

const activeMenu = computed(() => route.path)
const pageTitle = computed(() => {
  const map: Record<string, string> = {
    '/system/user': '用户管理', '/system/role': '角色管理', '/system/dept': '部门管理',
    '/system/dict': '字典管理', '/system/menu': '菜单管理', '/system/log': '日志管理',
    '/cert/iso-standard': 'ISO 标准', '/cert/iso-clause': 'ISO 条款',
    '/cert/cert-stage': '认证阶段', '/cert/cert-body': '认证机构', '/cert/enterprise': '企业管理',
    '/business/directory-manager': '标准目录管理', '/business/doc-extraction-rule': '文档提取规则',
    '/business/nc-config': 'NC 规则配置', '/business/report-rule-config': '报告章节定义',
    '/business/prompt-template': 'Prompt 模板', '/business/skill-manage': 'Skill 管理',
    '/business/sys-config-manager': '系统参数配置', '/business/ai-usage': 'AI 费用监控'
  }
  return map[route.path] || '映智汇认证平台'
})

function handleLogout() { authStore.clearToken(); router.push('/login') }
</script>

<style scoped>
.admin-layout { height: 100vh; }
.admin-layout__aside { background: #304156; overflow: hidden; }
.admin-layout__logo { height: 60px; display: flex; align-items: center; justify-content: center; color: #fff; font-size: 16px; font-weight: bold; border-bottom: 1px solid #3a4a5b; }
.admin-layout__menu { border-right: none; }
.admin-layout__header { display: flex; align-items: center; justify-content: space-between; background: #fff; border-bottom: 1px solid #e4e7ed; padding: 0 20px; }
.admin-layout__page-title { font-size: 16px; font-weight: 600; color: #303133; }
.admin-layout__header-right { display: flex; align-items: center; }
.admin-layout__user { display: flex; align-items: center; gap: 4px; cursor: pointer; color: #606266; }
.admin-layout__main { background: #f5f7fa; padding: 20px; }
</style>
