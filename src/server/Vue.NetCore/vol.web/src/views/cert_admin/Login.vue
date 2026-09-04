<template>
  <div class="auditor-login-container">
    <div class="auditor-login-form">
      <div class="form-header">
        <div class="logo-text">审核员平台</div>
        <div class="logo-subtitle">AUDITOR PORTAL</div>
        <div class="login-line"></div>
      </div>

      <!-- Tab 切换 -->
      <div class="tab-container">
        <div
          :class="['tab-item', { active: activeTab === 'login' }]"
          @click="activeTab = 'login'"
        >
          登录
        </div>
        <div
          :class="['tab-item', { active: activeTab === 'register' }]"
          @click="activeTab = 'register'"
        >
          注册
        </div>
      </div>

      <!-- 登录表单 -->
      <div v-show="activeTab === 'login'" class="form-content">
        <div class="form-user">
          <div class="item">
            <div class="input-icon el-icon-user"></div>
            <input type="text" v-model="loginForm.userName" placeholder="请输入账号" />
          </div>
          <div class="item">
            <div class="input-icon el-icon-lock"></div>
            <input type="password" v-model="loginForm.password" placeholder="请输入密码" @keyup.enter="handleLogin" />
          </div>
          <div class="item">
            <div class="input-icon el-icon-mobile"></div>
            <input type="text" v-model="loginForm.verificationCode" placeholder="请输入验证码" @keyup.enter="handleLogin" />
            <div class="code" @click="getVerificationCode">
              <img v-show="codeImgSrc != ''" :src="codeImgSrc" />
            </div>
          </div>
        </div>
        <div class="loging-btn">
          <el-button size="large" :loading="loading" color="#1e3a8a" :dark="true" @click="handleLogin" long>
            <span v-if="!loading">登录</span>
            <span v-else>正在登录...</span>
          </el-button>
        </div>
      </div>

      <!-- 注册表单 -->
      <div v-show="activeTab === 'register'" class="form-content register-content">
        <div class="form-user">
          <div class="item">
            <div class="input-icon el-icon-user"></div>
            <input type="text" v-model="registerForm.userName" placeholder="请输入账号" />
          </div>
          <div class="item">
            <div class="input-icon el-icon-lock"></div>
            <input type="password" v-model="registerForm.userPwd" placeholder="请输入密码" />
          </div>
          <div class="item">
            <div class="input-icon el-icon-lock"></div>
            <input type="password" v-model="registerForm.confirmPwd" placeholder="请确认密码" />
          </div>
          <!-- 机构选择 -->
          <div class="item org-select-item">
            <div class="input-icon el-icon-office-building"></div>
            <el-select
              v-model="registerForm.orgId"
              placeholder="请选择所属认证机构"
              class="org-select"
              filterable
            >
              <el-option
                v-for="org in orgList"
                :key="org.id"
                :value="org.id"
                :label="org.orgName"
              />
            </el-select>
          </div>
        </div>
        <div class="loging-btn">
          <el-button size="large" :loading="loading" color="#1e3a8a" :dark="true" @click="handleRegister" long>
            <span v-if="!loading">注册</span>
            <span v-else>正在注册...</span>
          </el-button>
        </div>
      </div>

      <div class="login-copyright">© 2026 映智汇 (YZH) 版权所有</div>
    </div>
  </div>
</template>

<script setup>
import http from '../../api/http.js'
import { getCurrentInstance, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import store from '../../store/index'
import { isAdminRole, isAuditorRole } from '@/router/index'

const router = useRouter()
const { proxy } = getCurrentInstance()
const $message = proxy.$message

const loading = ref(false)
const activeTab = ref('login')
const codeImgSrc = ref('')

const loginForm = reactive({
  userName: '',
  password: '',
  verificationCode: '',
  UUID: undefined
})

// 极简注册：只保留账号、密码、确认密码、机构
const registerForm = reactive({
  userName: '',
  userPwd: '',
  confirmPwd: '',
  orgId: undefined
})

// 认证机构列表
const orgList = ref([])

// 获取验证码
const getVerificationCode = () => {
  http.get('/api/User/getVierificationCode').then((x) => {
    codeImgSrc.value = 'data:image/png;base64,' + x.img
    loginForm.UUID = x.uuid
  })
}
getVerificationCode()

// 获取认证机构列表
const loadOrgList = () => {
  http.get('/api/AuditorAuth/GetOrgList', null, false).then((res) => {
    if (res.status && res.data) {
      orgList.value = (Array.isArray(res.data) ? res.data : []).filter(item => item.id)
    }
  }).catch(err => {
    console.error('获取机构列表失败:', err)
  })
}
onMounted(() => {
  loadOrgList()
})

// 登录处理
const handleLogin = () => {
  if (!loginForm.userName) return $message.error('请输入账号')
  if (!loginForm.password) return $message.error('请输入密码')
  if (!loginForm.verificationCode) return $message.error('请输入验证码')

  loading.value = true
  http.post('/api/user/login', loginForm, '正在登录....').then((result) => {
    if (!result.status) {
      loading.value = false
      getVerificationCode()
      return $message.error(result.message)
    }
    store.commit('setUserInfo', result.data)
    http.get('/api/AuditorAuth/GetCurrentUser', null, false).then((userInfoRes) => {
      if (userInfoRes.status && userInfoRes.data) {
        // 保存完整用户信息（包含 roleName）
        store.commit('setUserInfo', {
          ...result.data,
          roleId: userInfoRes.data.roleId,
          roleName: userInfoRes.data.roleName,
          userTrueName: userInfoRes.data.userTrueName
        })
        // 根据角色跳转
        const roleId = userInfoRes.data.roleId
        const roleName = userInfoRes.data.roleName
        if (isAuditorRole(roleId) || isAuditorRole(roleName)) {
          router.push('/cert_admin/workspace')
        } else if (isAdminRole(roleId) || isAdminRole(roleName)) {
          router.push('/home')
        } else {
          router.push('/cert_admin/workspace')
        }
      } else {
        router.push('/cert_admin/workspace')
      }
    }).catch(() => {
      router.push('/cert_admin/workspace')
    })
  }).catch(() => {
    loading.value = false
  })
}

// 极简注册处理：账号 + 密码 + 确认密码 + 机构
const handleRegister = () => {
  if (!registerForm.userName) return $message.error('请输入账号')
  if (!registerForm.userPwd) return $message.error('请输入密码')
  if (registerForm.userPwd !== registerForm.confirmPwd) return $message.error('两次密码不一致')
  if (!registerForm.orgId) return $message.error('请选择所属认证机构')

  loading.value = true
  http.post('/api/AuditorAuth/Register', registerForm, '正在注册....').then((result) => {
    loading.value = false
    if (!result.status) {
      return $message.error(result.message)
    }
    $message.success(result.message)
    activeTab.value = 'login'
    loginForm.userName = registerForm.userName
  }).catch(() => {
    loading.value = false
  })
}
</script>

<style lang="less" scoped>
// 使用项目全局 CSS 变量
.auditor-login-container {
  display: flex;
  width: 100%;
  height: 100%;
  background: var(--yzh-color-bg-page, #f0f2f5);
  background-image:
    radial-gradient(circle at 20% 30%, rgba(30, 58, 138, 0.06) 0%, transparent 50%),
    radial-gradient(circle at 80% 70%, rgba(30, 58, 138, 0.04) 0%, transparent 50%);
  justify-content: center;
  align-items: center;
}

.auditor-login-form {
  align-items: center;
  width: 100%;
  max-width: 440px;
  display: flex;
  flex-direction: column;
  z-index: 999;
  background: var(--yzh-color-bg-card, #fff);
  padding: 40px 30px;
  box-shadow: var(--yzh-shadow-lg, 0 12px 32px -8px rgba(0, 0, 0, 0.12));
  border-radius: 8px;
  border: 1px solid var(--yzh-color-border, #e2e8f0);
}

.form-header {
  width: 100%;
  margin-bottom: 20px;
}

.logo-text {
  font-weight: 700;
  font-size: 26px;
  color: #1e3a8a;
  text-align: center;
}

.logo-subtitle {
  margin-top: 4px;
  font-size: 11px;
  color: var(--yzh-color-text-secondary, #8c8c8c);
  text-align: center;
  letter-spacing: 2px;
}

.login-line {
  height: 3px;
  width: 40px;
  background: #1e3a8a;
  margin: 12px auto 0;
  border-radius: 2px;
}

.tab-container {
  display: flex;
  width: 100%;
  margin-bottom: 24px;
  border-bottom: 1px solid var(--yzh-color-border, #e5e7eb);
}

.tab-item {
  flex: 1;
  padding: 12px 0;
  text-align: center;
  font-size: 15px;
  font-weight: 500;
  color: var(--yzh-color-text-secondary, #6b7280);
  cursor: pointer;
  border-bottom: 2px solid transparent;
  transition: all 0.3s;

  &.active {
    color: #1e3a8a;
    border-bottom-color: #1e3a8a;
  }

  &:hover {
    color: #1e3a8a;
  }
}

.form-content {
  width: 100%;

  &.register-content {
    max-height: 60vh;
    overflow-y: auto;
    padding-right: 4px;

    &::-webkit-scrollbar {
      width: 4px;
    }
    &::-webkit-scrollbar-thumb {
      background: var(--yzh-color-border, #d9d9d9);
      border-radius: 2px;
    }
  }
}

.form-user {
  width: 100%;

  .item {
    border-radius: 8px;
    border: 1px solid var(--yzh-color-border, #e2e8f0);
    display: flex;
    margin-bottom: 16px;
    background: var(--yzh-color-bg-card, #fff);
    height: 48px;
    padding-left: 16px;
    transition: all 0.3s;
    align-items: center;

    &:focus-within {
      border-color: #1e3a8a;
      box-shadow: 0 0 0 2px rgba(30, 58, 138, 0.1);
    }

    .input-icon {
      line-height: 48px;
      color: rgba(0, 0, 0, 0.45);
      padding-right: 12px;
      font-size: 16px;
      flex-shrink: 0;
    }
  }
}

// 机构选择器样式
.org-select-item {
  padding-left: 0 !important;

  .input-icon {
    padding-left: 16px;
  }

  .org-select {
    flex: 1;
    height: 46px;

    :deep(.el-select__wrapper) {
      box-shadow: none !important;
      background: transparent;
      padding-left: 8px;
      font-size: 14px;

      &:hover {
        box-shadow: none !important;
      }
    }

    :deep(.el-select__wrapper.is-focus) {
      box-shadow: none !important;
    }

    :deep(.el-input__inner) {
      border: none;
      outline: none;
      background: transparent;
    }
  }
}

.org-option {
  display: flex;
  align-items: center;
  gap: 8px;

  .org-name {
    font-weight: 500;
    color: var(--yzh-color-text-primary, #1f1f1f);
    font-size: 14px;
  }

  .org-short {
    color: var(--yzh-color-text-secondary, #8c8c8c);
    font-size: 12px;
  }
}

input:-webkit-autofill {
  box-shadow: 0 0 0px 1000px white inset;
  -webkit-box-shadow: 0 0 0px 1000px white inset !important;
}

input {
  background: white;
  display: block;
  box-sizing: border-box;
  width: 100%;
  min-width: 0;
  margin: 0;
  padding: 0;
  color: var(--yzh-color-text-primary, #1f1f1f);
  line-height: inherit;
  text-align: left;
  border: 0;
  outline: none;
  font-size: 14px;
}

.code {
  cursor: pointer;
  width: 90px;
  padding: 4px 10px 0 0;

  img {
    height: 38px;
    width: 100%;
  }
}

.loging-btn {
  width: 100%;
  margin-top: 8px;

  button {
    height: 48px;
    border-radius: 8px;
    font-size: 16px !important;
    font-weight: 500;
    background-color: #1e3a8a !important;
    border: none;

    &:hover {
      background-color: #1e40af !important;
      transform: translateY(-1px);
      box-shadow: 0 4px 6px -1px rgba(30, 58, 138, 0.3);
    }
  }
}

.login-copyright {
  margin-top: 24px;
  font-size: 12px;
  color: var(--yzh-color-text-disabled, #94a3b8);
  text-align: center;
}

@media screen and (max-width: 700px) {
  .auditor-login-container {
    padding: 20px;
  }
  .auditor-login-form {
    padding: 30px 20px;
  }
}
</style>
