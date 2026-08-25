<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { NInput, NButton, NCard, NSpace, NText, NAlert, useMessage } from 'naive-ui'
import { useUserStore } from '@/stores/user'

const router = useRouter()
const route = useRoute()
const userStore = useUserStore()
const message = useMessage()

const loading = ref(false)
const form = reactive({
  account: '',
  password: ''
})

function handleLogin(): void {
  if (!form.account || !form.password) {
    message.warning('请输入账号和密码')
    return
  }
  loading.value = true
  // TODO: 对接后端登录接口（POST /api/Login），示例先写入演示 token
  setTimeout(() => {
    userStore.setToken('demo-token')
    userStore.setUserInfo({ userName: form.account, realName: form.account })
    message.success('登录成功')
    const redirect = (route.query.redirect as string) || '/workspace'
    router.replace(redirect)
    loading.value = false
  }, 300)
}
</script>

<template>
  <div class="login-page">
    <NCard style="width: 380px" title="审核员登录">
      <NSpace vertical size="large">
        <NAlert type="info" :show-icon="true">
          当前为基础框架占位页，登录逻辑需对接后端接口
        </NAlert>
        <NSpace vertical>
          <NInput
            v-model:value="form.account"
            placeholder="账号"
            size="large"
            @keyup.enter="handleLogin"
          />
          <NInput
            v-model:value="form.password"
            type="password"
            show-password-on="click"
            placeholder="密码"
            size="large"
            @keyup.enter="handleLogin"
          />
        </NSpace>
        <NButton type="primary" block size="large" :loading="loading" @click="handleLogin">
          登 录
        </NButton>
        <NText depth="3" style="font-size: 12px">
          Naive UI 基础框架 · 端口 9991 · API 代理 /api → 9992
        </NText>
      </NSpace>
    </NCard>
  </div>
</template>

<style scoped>
.login-page {
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--yzh-bg);
}
</style>
