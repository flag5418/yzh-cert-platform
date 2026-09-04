<template>
  <div class="login-container">
    <div v-if="$global.lang" class="app-lang">
      <lang color="#409eff"></lang>
    </div>
    <div class="login-form">
      <div class="form-user" @keypress="loginPress">
        <div class="login-text">
          <div>
            <div>映智汇认证管理平台</div>
            <div class="login-line"></div>
          </div>
          <div style="flex: 1"></div>
        </div>
        <div class="login-text-small">AUTHENTICATION MANAGEMENT SYSTEM</div>
        <div class="item">
          <div class="input-icon el-icon-user"></div>
          <input type="text" v-model="userInfo.userName" placeholder="请输入账号" />
        </div>
        <div class="item">
          <div class="input-icon el-icon-lock"></div>
          <input type="password" v-model="userInfo.password" placeholder="请输入密码" />
        </div>
        <div class="item">
          <div class="input-icon el-icon-mobile"></div>
          <input type="text" v-model="userInfo.verificationCode" placeholder="请输入验证码" />
          <div class="code" @click="getVierificationCode">
            <img v-show="codeImgSrc != ''" :src="codeImgSrc" />
          </div>
        </div>
      </div>
      <div class="loging-btn">
        <el-button size="large" :loading="loading" color="#1e3a8a" :dark="true" @click="login" long>
          <span v-if="!loading">{{ $ts('登录') }}</span>
          <span v-else>{{ $ts('正在登录') }}...</span>
        </el-button>
      </div>
      <div class="login-copyright">© 2026 映智汇 (YZH) 版权所有</div>
    </div>
  </div>
</template>

<script setup>
import http from '@/../src/api/http.js'
import lang from '@/components/lang/lang'
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
  background: #f8fafc;
  background-image: radial-gradient(#e2e8f0 1px, transparent 1px);
  background-size: 20px 20px;
  justify-content: center;
  align-items: center;
}

.login-form {
  align-items: center;
  width: 100%;
  max-width: 420px;
  display: flex;
  flex-direction: column;
  z-index: 999;
  background: #fff;
  padding: 40px 30px;
  box-shadow:
    0 10px 25px -5px rgba(0, 0, 0, 0.1),
    0 8px 10px -6px rgba(0, 0, 0, 0.1);
  border-radius: 12px;
  border: 1px solid #f1f5f9;

  .form-user {
    width: 100%;

    .item {
      border-radius: 8px;
      border: 1px solid #e2e8f0;
      display: flex;
      margin-bottom: 24px;
      background: #ffff;
      height: 48px;
      padding-left: 16px;
      transition: all 0.3s;

      &:focus-within {
        border-color: #1e3a8a;
        box-shadow: 0 0 0 2px rgba(30, 58, 138, 0.1);
      }

      .code {
        position: relative;
        cursor: pointer;
        width: 90px;
        padding: 4px 10px 0 0;
      }

      .input-icon {
        line-height: 48px;
        color: #94a3b8;
        padding-right: 12px;
        font-size: 18px;
      }
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
    color: #1e293b;
    line-height: inherit;
    text-align: left;
    border: 0;
    outline: none;
    font-size: 15px;
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
      box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1);
    }
  }
}

.login-text {
  font-weight: 700;
  font-size: 24px;
  color: #0f172a;
  margin-bottom: 8px;
  width: 100%;
  text-align: left;

  .login-line {
    height: 3px;
    width: 40px;
    background: #1e3a8a;
    margin-top: 4px;
    border-radius: 2px;
  }
}

.login-text-small {
  margin-bottom: 32px;
  font-size: 12px;
  color: #64748b;
  width: 100%;
  text-align: left;
  letter-spacing: 1px;
}

.login-copyright {
  margin-top: 40px;
  font-size: 12px;
  color: #94a3b8;
  text-align: center;
}

@media screen and (max-width: 700px) {
  .login-container {
    padding: 20px;
  }
  .login-form {
    padding: 30px 20px;
  }
}
</style>
