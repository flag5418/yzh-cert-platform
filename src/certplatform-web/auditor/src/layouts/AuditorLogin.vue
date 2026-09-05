<template>
  <div class="auditor-login">
    <div class="auditor-login__box">
      <div class="auditor-login__header">
        <h1>映智汇认证审核管理系统</h1>
        <p>审核员端</p>
      </div>
      <el-form ref="formRef" :model="form" :rules="rules" class="auditor-login__form">
        <el-form-item prop="userName">
          <el-input v-model="form.userName" placeholder="用户名" prefix-icon="User" />
        </el-form-item>
        <el-form-item prop="password">
          <el-input v-model="form.password" type="password" placeholder="密码" prefix-icon="Lock" show-password @keyup.enter="handleLogin" />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" :loading="loading" class="auditor-login__btn" @click="handleLogin">登录</el-button>
        </el-form-item>
      </el-form>
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
    const mockToken = 'mock-auditor-token-' + Date.now()
    authStore.setToken(mockToken)
    authStore.userInfo = { userName: form.userName, roles: ['auditor'] }
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
.auditor-login { display: flex; align-items: center; justify-content: center; min-height: 100vh; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); }
.auditor-login__box { width: 400px; padding: 40px; background: #fff; border-radius: 8px; box-shadow: 0 4px 20px rgba(0, 0, 0, 0.15); }
.auditor-login__header { text-align: center; margin-bottom: 32px; }
.auditor-login__header h1 { font-size: 24px; color: #303133; margin-bottom: 8px; }
.auditor-login__header p { font-size: 14px; color: #909399; }
.auditor-login__form { width: 100%; }
.auditor-login__btn { width: 100%; }
</style>
