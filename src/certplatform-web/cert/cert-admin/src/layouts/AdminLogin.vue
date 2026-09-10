<template>
  <div class="login-page">
    <!-- 全屏背景 -->
    <div class="login-bg">
      <div class="login-bg__overlay"></div>
    </div>

    <!-- 中央登录卡片 -->
    <div class="login-card">
      <!-- 左侧品牌区 -->
      <div class="login-card__brand">
        <div class="brand-inner">
          <div class="brand-logo">YZH</div>
          <h1 class="brand-title">映智汇认证管理平台</h1>
          <p class="brand-subtitle">CERTIFICATION MANAGEMENT SYSTEM</p>
          <div class="brand-features">
            <div class="feature-item">
              <el-icon class="feature-icon"><Check /></el-icon>
              <span>严谨 · 规范 · 专业</span>
            </div>
            <div class="feature-item">
              <el-icon class="feature-icon"><Lock /></el-icon>
              <span>ISO 体系认证全流程</span>
            </div>
            <div class="feature-item">
              <el-icon class="feature-icon"><Cpu /></el-icon>
              <span>AI 智能审核引擎</span>
            </div>
          </div>
        </div>
        <div class="brand-footer">© 2026 映智汇 (YZH) 版权所有</div>
      </div>

      <!-- 右侧登录表单 -->
      <div class="login-card__form">
        <div class="form-header">
          <h2 class="form-title">账号登录</h2>
          <p form-subtitle>请输入您的账号信息</p>
        </div>

        <el-form ref="formRef" :model="form" :rules="rules" class="form-body" @submit.prevent="handleLogin">
          <el-form-item prop="userName">
            <el-input v-model="form.userName" placeholder="请输入账号" :prefix-icon="User" size="large" />
          </el-form-item>
          <el-form-item prop="password">
            <el-input v-model="form.password" type="password" placeholder="请输入密码" :prefix-icon="Lock" size="large" show-password @keyup.enter="handleLogin" />
          </el-form-item>
          <el-form-item prop="captcha">
            <div class="verify-row">
              <el-input v-model="form.captcha" placeholder="请输入验证码" :prefix-icon="PictureRounded" size="large" maxlength="4" />
              <div class="verify-img" @click="refreshCaptcha" title="点击刷新验证码">
                <img v-if="captchaImg" :src="`data:image/png;base64,${captchaImg}`" alt="验证码" />
                <span v-else class="verify-placeholder">加载中...</span>
              </div>
            </div>
          </el-form-item>
          <el-form-item>
            <el-button type="primary" native-type="submit" size="large" :loading="loading" class="login-btn">登 录</el-button>
          </el-form-item>
        </el-form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import type { FormInstance } from 'element-plus'
import { User, Lock, PictureRounded, Check, Cpu } from '@element-plus/icons-vue'
import { useAuthStore } from '@/store/auth'
import { login, getCaptcha } from '@share/api/auth'

const router = useRouter()
const authStore = useAuthStore()
const formRef = ref<FormInstance>()
const loading = ref(false)
const captchaImg = ref('')
const captchaUuid = ref('')

const form = reactive({
  userName: '',
  password: '',
  captcha: ''
})

const rules = {
  userName: [{ required: true, message: '请输入账号', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }],
  captcha: [{ required: true, message: '请输入验证码', trigger: 'blur' }]
}

async function refreshCaptcha() {
  try {
    const res = await getCaptcha()
    captchaImg.value = res.data.img
    captchaUuid.value = res.data.uuid
  } catch (e: any) {
    ElMessage.error('验证码加载失败')
  }
}

async function handleLogin() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  loading.value = true
  try {
    const res = await login({
      userName: form.userName,
      password: form.password,
      captcha: form.captcha,
      uuid: captchaUuid.value
    })

    // 保存 token 和用户信息（后端返回 PascalCase 字段）
    const { Token, UserCode, UserName, UserTrueName, RoleId } = res.data
    authStore.setToken(Token)
    authStore.setUserInfo({ token: Token, userId: 0, userCode: UserCode, userName: UserName, userTrueName: UserTrueName ?? '', roleId: RoleId ?? 0 })

    ElMessage.success('登录成功')
    router.push('/')
  } catch (e: any) {
    ElMessage.error(e?.message || '登录失败')
    refreshCaptcha()
    form.captcha = ''
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  refreshCaptcha()
})
</script>

<style scoped>
/* 全屏登录页 */
.login-page {
  position: fixed;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
}

/* 全屏背景 - 渐变 + 纹理 */
.login-bg {
  position: absolute;
  inset: 0;
  background: linear-gradient(135deg, #0f172a 0%, #1e3a8a 40%, #1e40af 70%, #0f172a 100%);
  z-index: 0;
}

.login-bg__overlay {
  position: absolute;
  inset: 0;
  background-image:
    radial-gradient(circle at 20% 30%, rgba(59, 130, 246, 0.15) 0%, transparent 50%),
    radial-gradient(circle at 80% 70%, rgba(30, 64, 175, 0.1) 0%, transparent 50%);
}

/* 中央登录卡片 */
.login-card {
  position: relative;
  z-index: 1;
  display: flex;
  width: 860px;
  height: 480px;
  background: #fff;
  border-radius: 12px;
  box-shadow: 0 25px 60px rgba(0, 0, 0, 0.3), 0 0 0 1px rgba(255, 255, 255, 0.1);
  overflow: hidden;
}

/* 左侧品牌区 */
.login-card__brand {
  flex: 1;
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  padding: 40px;
  background: linear-gradient(135deg, #1e3a8a 0%, #1e40af 50%, #0f172a 100%);
  position: relative;
  overflow: hidden;
}

.login-card__brand::before {
  content: '';
  position: absolute;
  top: -30%;
  right: -20%;
  width: 300px;
  height: 300px;
  background: rgba(255, 255, 255, 0.05);
  border-radius: 50%;
}

.login-card__brand::after {
  content: '';
  position: absolute;
  bottom: -20%;
  left: -15%;
  width: 200px;
  height: 200px;
  background: rgba(255, 255, 255, 0.03);
  border-radius: 50%;
}

.brand-inner {
  position: relative;
  z-index: 1;
  text-align: center;
  color: #fff;
}

.brand-logo {
  width: 72px;
  height: 72px;
  margin: 0 auto 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 28px;
  font-weight: 700;
  color: #fff;
  background: rgba(255, 255, 255, 0.15);
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-radius: 14px;
}

.brand-title {
  font-size: 24px;
  font-weight: 700;
  margin: 0 0 8px;
  letter-spacing: 1px;
  color: #fff;
}

.brand-subtitle {
  font-size: 11px;
  color: rgba(255, 255, 255, 0.6);
  letter-spacing: 2px;
  margin-bottom: 40px;
}

.brand-features {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.feature-item {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  font-size: 14px;
  color: rgba(255, 255, 255, 0.85);
}

.feature-icon {
  font-size: 16px;
  color: rgba(255, 255, 255, 0.7);
}

.brand-footer {
  position: absolute;
  bottom: 24px;
  font-size: 11px;
  color: rgba(255, 255, 255, 0.4);
}

/* 右侧登录表单 */
.login-card__form {
  width: 340px;
  padding: 48px 40px;
  display: flex;
  flex-direction: column;
  justify-content: center;
}

.form-header {
  margin-bottom: 32px;
}

.form-title {
  font-size: 24px;
  font-weight: 700;
  color: #1e293b;
  margin: 0 0 6px;
}

.form-header p {
  font-size: 13px;
  color: #64748b;
  margin: 0;
}

.form-body {
  width: 100%;
}

.form-body .el-form-item {
  margin-bottom: 18px;
}

/* 验证码 */
.verify-row {
  display: flex;
  gap: 10px;
  width: 100%;
}

.verify-row .el-input {
  flex: 1;
}

.verify-img {
  width: 100px;
  height: 40px;
  border-radius: 6px;
  border: 1px solid #dcdfe6;
  cursor: pointer;
  background: #f5f7fa;
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
  flex-shrink: 0;
}

.verify-img img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.verify-placeholder {
  font-size: 12px;
  color: #909399;
}

/* 登录按钮 */
.login-btn {
  width: 100%;
  height: 44px;
  font-size: 15px;
  font-weight: 600;
  letter-spacing: 4px;
  border-radius: 6px;
}

/* 响应式 */
@media screen and (max-width: 768px) {
  .login-card {
    width: 100%;
    height: 100%;
    border-radius: 0;
    flex-direction: column;
  }

  .login-card__brand {
    flex: none;
    padding: 30px 20px;
  }

  .brand-features {
    display: none;
  }

  .brand-footer {
    display: none;
  }

  .login-card__form {
    width: 100%;
    flex: 1;
    padding: 30px 24px;
  }
}
</style>
