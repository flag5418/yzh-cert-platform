<template>
  <div class="admin-login">
    <!-- 左侧品牌区 -->
    <div class="admin-login__brand">
      <div class="brand-content">
        <div class="brand-logo">YZH</div>
        <h1 class="brand-title">映智汇认证管理平台</h1>
        <p class="brand-subtitle">CERTIFICATION MANAGEMENT SYSTEM</p>
        <div class="brand-features">
          <div class="feature-item">
            <el-icon><i class="bi bi-check-circle-fill"></i></el-icon>
            <span>严谨 · 规范 · 专业</span>
          </div>
          <div class="feature-item">
            <el-icon><i class="bi bi-shield-check"></i></el-icon>
            <span>ISO 体系认证全流程</span>
          </div>
          <div class="feature-item">
            <el-icon><i class="bi bi-cpu"></i></el-icon>
            <span>AI 智能审核引擎</span>
          </div>
        </div>
      </div>
      <div class="brand-footer">© 2026 映智汇 (YZH) 版权所有</div>
    </div>

    <!-- 右侧登录区 -->
    <div class="admin-login__form-wrap">
      <div class="login-form">
        <div class="form-header">
          <div class="form-title">账号登录</div>
          <div class="form-subtitle">请输入您的账号信息</div>
        </div>
        <el-form ref="formRef" :model="form" :rules="rules" class="login-form__body">
          <el-form-item prop="userName">
            <el-input v-model="form.userName" placeholder="请输入用户名" prefix-icon="User" size="large" />
          </el-form-item>
          <el-form-item prop="password">
            <el-input v-model="form.password" type="password" placeholder="请输入密码" prefix-icon="Lock" size="large" show-password @keyup.enter="handleLogin" />
          </el-form-item>
          <el-form-item>
            <el-button type="primary" size="large" :loading="loading" class="login-form__btn" @click="handleLogin">登 录</el-button>
          </el-form-item>
        </el-form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import type { FormInstance } from 'element-plus'
import { useAuthStore } from '@/store/auth'

const router = useRouter()
const authStore = useAuthStore()
const formRef = ref<FormInstance>()
const loading = ref(false)

const form = reactive({ userName: '', password: '' })
const rules = {
  userName: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }]
}

async function handleLogin() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  loading.value = true
  try {
    const mockToken = 'mock-admin-token-' + Date.now()
    authStore.setToken(mockToken)
    authStore.userInfo = { userName: form.userName, roles: ['admin'] }
    ElMessage.success('登录成功')
    router.push('/')
  } catch (e: any) {
    ElMessage.error(e?.message || '登录失败')
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.admin-login {
  display: flex;
  min-height: 100vh;
  background: var(--yzh-color-bg-page, #f5f7fa);
}

/* 左侧品牌区 */
.admin-login__brand {
  flex: 1;
  background: linear-gradient(135deg, #1e3a8a 0%, #1e40af 50%, #0f172a 100%);
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  padding: 60px 40px;
  position: relative;
  overflow: hidden;
}

.admin-login__brand::before {
  content: '';
  position: absolute;
  top: -20%;
  right: -10%;
  width: 600px;
  height: 600px;
  background: rgba(255, 255, 255, 0.03);
  border-radius: 50%;
}

.admin-login__brand::after {
  content: '';
  position: absolute;
  bottom: -10%;
  left: -5%;
  width: 400px;
  height: 400px;
  background: rgba(255, 255, 255, 0.02);
  border-radius: 50%;
}

.brand-content {
  position: relative;
  z-index: 1;
  text-align: center;
  color: #fff;
}

.brand-logo {
  width: 80px;
  height: 80px;
  margin: 0 auto 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 32px;
  font-weight: 700;
  color: #fff;
  background: rgba(255, 255, 255, 0.15);
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-radius: 16px;
}

.brand-title {
  font-size: 32px;
  font-weight: 700;
  margin: 0 0 12px;
  letter-spacing: 2px;
  color: #fff;
}

.brand-subtitle {
  font-size: 14px;
  color: rgba(255, 255, 255, 0.7);
  letter-spacing: 3px;
  margin-bottom: 48px;
}

.brand-features {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.feature-item {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  font-size: 16px;
  color: rgba(255, 255, 255, 0.9);
}

.feature-item .el-icon {
  font-size: 20px;
  color: rgba(255, 255, 255, 0.8);
}

.brand-footer {
  position: absolute;
  bottom: 40px;
  font-size: 12px;
  color: rgba(255, 255, 255, 0.5);
}

/* 右侧登录区 */
.admin-login__form-wrap {
  width: 480px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #fff;
  padding: 0 40px;
}

.login-form {
  width: 100%;
  max-width: 360px;
}

.form-header {
  margin-bottom: 40px;
}

.form-title {
  font-size: 28px;
  font-weight: 700;
  color: var(--yzh-color-text-primary, #1e293b);
  margin-bottom: 8px;
}

.form-subtitle {
  font-size: 14px;
  color: var(--yzh-color-text-secondary, #64748b);
}

.login-form__body {
  .el-form-item {
    margin-bottom: 24px;
  }

  .el-input {
    height: 48px;
  }
}

.login-form__btn {
  width: 100%;
  height: 48px;
  font-size: 16px;
  font-weight: 600;
  letter-spacing: 4px;
  background: var(--yzh-color-primary, #1e3a8a);
  border-color: var(--yzh-color-primary, #1e3a8a);
}

.login-form__btn:hover {
  background: var(--yzh-color-primary-light, #1e40af);
  border-color: var(--yzh-color-primary-light, #1e40af);
}

@media screen and (max-width: 768px) {
  .admin-login__brand {
    display: none;
  }
  .admin-login__form-wrap {
    width: 100%;
    padding: 40px 20px;
  }
}
</style>
