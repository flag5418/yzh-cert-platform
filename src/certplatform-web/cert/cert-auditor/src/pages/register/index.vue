<template>
  <div class="register-page">
    <div class="register-bg">
      <div class="register-bg__overlay"></div>
    </div>

    <div class="register-card">
      <!-- 左侧品牌区 -->
      <div class="register-card__brand">
        <div class="brand-inner">
          <div class="brand-logo">YZH</div>
          <h1 class="brand-title">映智汇认证专家平台</h1>
          <p class="brand-subtitle">CERTIFICATION EXPERT PLATFORM</p>
          <div class="brand-features">
            <div class="feature-item">
              <el-icon class="feature-icon"><OfficeBuilding /></el-icon>
              <span>选择已认证的体系认证机构</span>
            </div>
            <div class="feature-item">
              <el-icon class="feature-icon"><Check /></el-icon>
              <span>注册即开通独立工作区</span>
            </div>
            <div class="feature-item">
              <el-icon class="feature-icon"><Document /></el-icon>
              <span>一键 NC · 一键出报告</span>
            </div>
          </div>
        </div>
        <div class="brand-footer">© 2026 映智汇 (YZH) 版权所有</div>
      </div>

      <!-- 右侧注册表单 -->
      <div class="register-card__form">
        <div class="form-header">
          <h2 class="form-title">专家注册</h2>
          <p>一次注册开通一个认证机构的工作区</p>
        </div>

        <el-form ref="formRef" :model="form" :rules="rules" label-position="top" class="form-body" @submit.prevent="handleRegister">
          <el-form-item prop="CertBodyCode" label="体系认证机构">
            <el-select
              v-model="form.CertBodyCode"
              placeholder="请选择您所属的体系认证机构"
              :loading="orgLoading"
              filterable
              size="large"
              style="width: 100%"
            >
              <el-option
                v-for="opt in orgOptions"
                :key="opt.Code"
                :label="opt.Name"
                :value="opt.Code"
              >
                <span class="opt-name">{{ opt.Name }}</span>
                <span v-if="opt.CbCode" class="opt-code">{{ opt.CbCode }}</span>
              </el-option>
              <template #empty>
                <div class="org-empty">
                  <span v-if="orgLoading">加载中…</span>
                  <span v-else>暂无可注册的体系认证机构，请联系平台管理员</span>
                </div>
              </template>
            </el-select>
          </el-form-item>

          <div class="form-row">
            <el-form-item prop="UserName" label="登录名">
              <el-input v-model="form.UserName" placeholder="至少 3 位" :prefix-icon="User" size="large" />
            </el-form-item>
            <el-form-item prop="UserTrueName" label="姓名">
              <el-input v-model="form.UserTrueName" placeholder="真实姓名" :prefix-icon="Postcard" size="large" maxlength="20" />
            </el-form-item>
          </div>

          <div class="form-row">
            <el-form-item prop="Password" label="密码">
              <el-input v-model="form.Password" type="password" placeholder="至少 6 位" :prefix-icon="Lock" size="large" show-password />
            </el-form-item>
            <el-form-item prop="ConfirmPassword" label="确认密码">
              <el-input v-model="form.ConfirmPassword" type="password" placeholder="再次输入密码" :prefix-icon="Lock" size="large" show-password @keyup.enter="handleRegister" />
            </el-form-item>
          </div>

          <el-form-item prop="PhoneNo" label="手机号（选填）">
            <el-input v-model="form.PhoneNo" placeholder="用于后续找回密码" :prefix-icon="Iphone" size="large" maxlength="11" />
          </el-form-item>

          <el-form-item>
            <el-button type="primary" native-type="submit" size="large" :loading="loading" class="register-btn">注 册</el-button>
          </el-form-item>
        </el-form>

        <div class="form-footer">
          <span>已有专家账号？</span>
          <router-link to="/login" class="login-link">返回登录</router-link>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import { User, Lock, Postcard, Iphone, OfficeBuilding, Check, Document } from '@element-plus/icons-vue'
import { getCertBodyOptions, auditorRegister, type CertBodyOption } from '@/api/auditor-auth'

const router = useRouter()
const formRef = ref<FormInstance>()
const loading = ref(false)
const orgLoading = ref(false)
const orgOptions = ref<CertBodyOption[]>([])

const form = reactive({
  CertBodyCode: '',
  UserName: '',
  UserTrueName: '',
  Password: '',
  ConfirmPassword: '',
  PhoneNo: ''
})

const rules: FormRules = {
  CertBodyCode: [{ required: true, message: '请选择体系认证机构', trigger: 'change' }],
  UserName: [
    { required: true, message: '请输入登录名', trigger: 'blur' },
    { min: 3, message: '登录名至少 3 位', trigger: 'blur' }
  ],
  UserTrueName: [{ required: true, message: '请输入姓名', trigger: 'blur' }],
  Password: [
    { required: true, message: '请输入密码', trigger: 'blur' },
    { min: 6, message: '密码至少 6 位', trigger: 'blur' }
  ],
  ConfirmPassword: [
    { required: true, message: '请再次输入密码', trigger: 'blur' },
    {
      validator: (_rule, value: string, callback) => {
        if (value !== form.Password) callback(new Error('两次输入的密码不一致'))
        else callback()
      },
      trigger: 'blur'
    }
  ],
  PhoneNo: [
    {
      validator: (_rule, value: string, callback) => {
        if (!value) return callback()
        if (!/^1[3-9]\d{9}$/.test(value)) callback(new Error('手机号格式不正确'))
        else callback()
      },
      trigger: 'blur'
    }
  ]
}

/** 加载可注册的体系认证机构（数据源：后台已定义的机构） */
async function loadOrgOptions() {
  orgLoading.value = true
  try {
    const res = await getCertBodyOptions()
    // ApiResponse 信封：业务数据在 res.data（PascalCase 数组）
    orgOptions.value = (res?.data ?? []) as CertBodyOption[]
    if (orgOptions.value.length === 0) {
      ElMessage.warning('暂无可注册的体系认证机构')
    }
  } catch (e: any) {
    ElMessage.error(e?.message || '加载体系认证机构失败')
  } finally {
    orgLoading.value = false
  }
}

async function handleRegister() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  loading.value = true
  try {
    const res = await auditorRegister({
      CertBodyCode: form.CertBodyCode,
      UserName: form.UserName.trim(),
      UserTrueName: form.UserTrueName.trim(),
      Password: form.Password,
      PhoneNo: form.PhoneNo.trim() || undefined
    })

    ElMessage.success(res?.message || '注册成功，请使用该账号登录')
    // 注册成功 → 回登录页并预填登录名
    router.push({ path: '/login', query: { userName: form.UserName.trim() } })
  } catch (e: any) {
    ElMessage.error(e?.message || '注册失败')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadOrgOptions()
})
</script>

<style scoped>
.register-page {
  position: fixed;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: auto;
}

.register-bg {
  position: absolute;
  inset: 0;
  background: linear-gradient(135deg, #0b2b1f 0%, #0f5132 40%, #146c43 70%, #0b2b1f 100%);
  z-index: 0;
}

.register-bg__overlay {
  position: absolute;
  inset: 0;
  background-image:
    radial-gradient(circle at 20% 30%, rgba(25, 135, 84, 0.18) 0%, transparent 50%),
    radial-gradient(circle at 80% 70%, rgba(20, 108, 67, 0.12) 0%, transparent 50%);
}

.register-card {
  position: relative;
  z-index: 1;
  display: flex;
  width: 920px;
  margin: 24px 0;
  background: #fff;
  border-radius: 12px;
  box-shadow: 0 25px 60px rgba(0, 0, 0, 0.3), 0 0 0 1px rgba(255, 255, 255, 0.1);
  overflow: hidden;
}

/* 左侧品牌区 */
.register-card__brand {
  width: 340px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  padding: 40px;
  background: linear-gradient(135deg, #0f5132 0%, #146c43 50%, #0b2b1f 100%);
  position: relative;
  overflow: hidden;
}

.register-card__brand::before {
  content: '';
  position: absolute;
  top: -30%;
  right: -20%;
  width: 300px;
  height: 300px;
  background: rgba(255, 255, 255, 0.05);
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
  font-size: 20px;
  font-weight: 700;
  margin: 0 0 8px;
  letter-spacing: 1px;
  color: #fff;
}

.brand-subtitle {
  font-size: 10px;
  color: rgba(255, 255, 255, 0.6);
  letter-spacing: 2px;
  margin-bottom: 36px;
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
  font-size: 13px;
  color: rgba(255, 255, 255, 0.85);
}

.feature-icon {
  font-size: 15px;
  color: rgba(255, 255, 255, 0.7);
}

.brand-footer {
  position: absolute;
  bottom: 24px;
  font-size: 11px;
  color: rgba(255, 255, 255, 0.4);
}

/* 右侧注册表单 */
.register-card__form {
  flex: 1;
  min-width: 0;
  padding: 36px 40px;
  display: flex;
  flex-direction: column;
  justify-content: center;
}

.form-header {
  margin-bottom: 20px;
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

.form-body :deep(.el-form-item) {
  margin-bottom: 14px;
}

.form-body :deep(.el-form-item__label) {
  padding-bottom: 2px;
  font-size: 13px;
  color: #475569;
  line-height: 20px;
}

.form-row {
  display: flex;
  gap: 14px;
}

.form-row .el-form-item {
  flex: 1;
  min-width: 0;
}

/* 机构下拉项 */
.opt-name {
  float: left;
}

.opt-code {
  float: right;
  color: #94a3b8;
  font-size: 12px;
}

.org-empty {
  padding: 10px 0;
  text-align: center;
  color: #909399;
  font-size: 13px;
}

/* 注册按钮 */
.register-btn {
  width: 100%;
  height: 44px;
  font-size: 15px;
  font-weight: 600;
  letter-spacing: 4px;
  border-radius: 6px;
}

/* 底部登录入口 */
.form-footer {
  margin-top: 4px;
  text-align: center;
  font-size: 13px;
  color: #64748b;
}

.login-link {
  color: #146c43;
  font-weight: 600;
  text-decoration: none;
  margin-left: 4px;
}

.login-link:hover {
  text-decoration: underline;
}

/* 响应式 */
@media screen and (max-width: 960px) {
  .register-card {
    width: 100%;
    margin: 0;
    border-radius: 0;
    flex-direction: column;
  }

  .register-card__brand {
    width: 100%;
    padding: 24px 20px;
  }

  .brand-features,
  .brand-footer {
    display: none;
  }

  .register-card__form {
    padding: 24px 20px;
  }

  .form-row {
    flex-direction: column;
    gap: 0;
  }
}
</style>
