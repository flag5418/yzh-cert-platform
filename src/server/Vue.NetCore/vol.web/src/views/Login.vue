<template>
  <div class="login-container">
    <!-- 左侧品牌区 -->
    <div class="login-brand">
      <div class="brand-content">
        <div class="brand-logo">
          <!-- 使用文字 Logo，避免依赖图片 -->
          <div class="logo-text">YZH</div>
        </div>
        <h1 class="brand-title">映智汇认证管理平台</h1>
        <p class="brand-subtitle">AUTHENTICATION MANAGEMENT SYSTEM</p>
        <div class="brand-features">
          <div class="feature-item">
            <i class="bi bi-check-circle-fill icon-lg"></i>
            <span>严谨 · 规范 · 专业</span>
          </div>
          <div class="feature-item">
            <i class="bi bi-shield-check icon-lg"></i>
            <span>ISO 体系认证全流程</span>
          </div>
          <div class="feature-item">
            <i class="bi bi-cpu icon-lg"></i>
            <span>AI 智能审核引擎</span>
          </div>
        </div>
      </div>
      <div class="brand-footer">© 2026 映智汇 (YZH) 版权所有</div>
    </div>

    <!-- 右侧登录区 -->
    <div class="login-form-wrap">
      <div class="login-form">
        <div class="form-header">
          <div class="form-title">账号登录</div>
          <div class="form-subtitle">请输入您的账号信息</div>
        </div>
        
        <div class="form-user" @keypress="loginPress">
          <div class="input-wrapper">
            <i class="bi bi-person input-icon"></i>
            <input 
              type="text" 
              v-model="userInfo.userName" 
              placeholder="请输入账号"
              class="yzh-input"
            />
          </div>
          <div class="input-wrapper">
            <i class="bi bi-lock input-icon"></i>
            <input 
              type="password" 
              v-model="userInfo.password" 
              placeholder="请输入密码"
              class="yzh-input"
            />
          </div>
          <div class="input-wrapper">
            <i class="bi bi-shield-lock input-icon"></i>
            <input 
              type="text" 
              v-model="userInfo.verificationCode" 
              placeholder="请输入验证码"
              class="yzh-input yzh-input--code"
            />
            <div class="code" @click="getVierificationCode">
              <img v-show="codeImgSrc != ''" :src="codeImgSrc" />
            </div>
          </div>
        </div>
        
        <div class="loging-btn">
          <button 
            type="button" 
            class="yzh-btn yzh-btn--primary"
            :disabled="loading"
            @click="login"
          >
            <span v-if="!loading">登 录</span>
            <span v-else>正在登录...</span>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import http from '@/../src/api/http.js'
import { getCurrentInstance, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import store from '../store/index'
import { isAdminRole, isAuditorRole } from '@/router/index'

const loading = ref(false)
const codeImgSrc = ref('')
const userInfo = reactive({
  userName: '',
  password: '',
  verificationCode: '',
  UUID: undefined
})

const getVierificationCode = () => {
  http.get('/api/User/getVierificationCode').then((x) => {
    codeImgSrc.value = 'data:image/png;base64,' + x.img
    userInfo.UUID = x.uuid
  })
}
getVierificationCode()

const { proxy } = getCurrentInstance()
let $message = proxy.$message
let router = useRouter()
let $ts = proxy.$ts

const login = () => {
  if (!userInfo.userName) return $message.error($ts(['请输入', '账号']))
  if (!userInfo.password) return $message.error($ts(['请输入', '密码']))
  if (!userInfo.verificationCode) {
    return $message.error($ts(['请输入', '验证码']))
  }
  loading.value = true
  http.post('/api/user/login', userInfo, $ts('正在登录') + '....').then((result) => {
    if (!result.status) {
      loading.value = false
      getVierificationCode()
      return $message.error(result.message)
    }
    // 登录成功，先保存基础信息
    store.commit('setUserInfo', result.data)
    
    // 获取用户详细信息（包含 roleName）
    http.get('/api/AuditorAuth/GetCurrentUser', null, false).then((userInfoRes) => {
      if (userInfoRes.status && userInfoRes.data) {
        // 合并用户信息，保存 roleName
        store.commit('setUserInfo', {
          ...result.data,
          roleId: userInfoRes.data.roleId,
          roleName: userInfoRes.data.roleName,
          userTrueName: userInfoRes.data.userTrueName
        })
        // 根据角色跳转到对应首页
        const roleId = userInfoRes.data.roleId
        const roleName = userInfoRes.data.roleName
        if (isAdminRole(roleId) || isAdminRole(roleName)) {
          router.push('/home')
        } else if (isAuditorRole(roleId) || isAuditorRole(roleName)) {
          router.push('/cert/auditor/workspace')
        } else {
          router.push('/home')
        }
      } else {
        router.push('/home')
      }
    }).catch(() => {
      router.push('/home')
    })
  })
}

const loginPress = (e) => {
  if (e.keyCode == 13) {
    login()
  }
}
</script>

<style lang="less" scoped>
.login-container {
  display: flex;
  width: 100%;
  height: 100%;
  background: var(--yzh-color-bg-page, #f5f7fa);
}

/* 左侧品牌区 */
.login-brand {
  flex: 1;
  background: linear-gradient(135deg, #1e3a8a 0%, #1e40af 50%, #0f172a 100%);
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  padding: 60px 40px;
  position: relative;
  overflow: hidden;

  /* 装饰性几何图形 */
  &::before {
    content: '';
    position: absolute;
    top: -20%;
    right: -10%;
    width: 600px;
    height: 600px;
    background: rgba(255, 255, 255, 0.03);
    border-radius: 50%;
  }

  &::after {
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

  .logo-text {
    width: 80px;
    height: 80px;
    margin-bottom: 32px;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 32px;
    font-weight: 700;
    color: #fff;
    background: rgba(255, 255, 255, 0.15);
    border: 2px solid rgba(255, 255, 255, 0.3);
  }

  .brand-title {
    font-size: 32px;
    font-weight: 700;
    margin: 0 0 12px 0;
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

    .feature-item {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 12px;
      font-size: 16px;
      color: rgba(255, 255, 255, 0.9);

      .bi {
        font-size: 20px;
        color: rgba(255, 255, 255, 0.8);
      }
    }
  }

  .brand-footer {
    position: absolute;
    bottom: 40px;
    font-size: 12px;
    color: rgba(255, 255, 255, 0.5);
  }
}

/* 右侧登录区 */
.login-form-wrap {
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

  .form-header {
    margin-bottom: 40px;

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
  }

  .form-user {
    .input-wrapper {
      position: relative;
      margin-bottom: 20px;

      .input-icon {
        position: absolute;
        left: 16px;
        top: 50%;
        transform: translateY(-50%);
        font-size: 18px;
        color: var(--yzh-color-text-secondary, #94a3b8);
        line-height: 1;
      }

      .yzh-input {
        width: 100%;
        height: 48px;
        padding: 0 16px 0 48px;
        font-size: 15px;
        color: var(--yzh-color-text-primary, #1e293b);
        background: #fff;
        border: 1px solid var(--yzh-color-border, #e2e8f0);
        border-radius: var(--yzh-radius-none, 0);
        outline: none;
        transition: all 0.2s;

        &::placeholder {
          color: var(--yzh-color-text-placeholder, #cbd5e1);
        }

        &:focus {
          border-color: var(--yzh-color-primary, #1e3a8a);
          box-shadow: none;
        }

        &.yzh-input--code {
          padding-right: 110px;
        }
      }

      .code {
        position: absolute;
        right: 8px;
        top: 50%;
        transform: translateY(-50%);
        cursor: pointer;
        height: 32px;

        img {
          height: 100%;
          width: auto;
          border-radius: var(--yzh-radius-none, 0);
        }
      }
    }
  }

  .loging-btn {
    width: 100%;
    margin-top: 8px;

    .yzh-btn {
      width: 100%;
      height: 48px;
      font-size: 16px;
      font-weight: 600;
      letter-spacing: 4px;
      border: none;
      border-radius: var(--yzh-radius-none, 0);
      cursor: pointer;
      transition: all 0.2s;

      &--primary {
        background: var(--yzh-color-primary, #1e3a8a);
        color: #fff;

        &:hover {
          background: var(--yzh-color-primary-light-3, #1e40af);
        }

        &:disabled {
          background: var(--yzh-color-text-disabled, #94a3b8);
          cursor: not-allowed;
        }
      }
    }
  }
}

@media screen and (max-width: 700px) {
  .login-brand {
    display: none;
  }
  
  .login-form-wrap {
    width: 100%;
    padding: 20px;
  }
}
</style>
