/**
 * 审核员端路由配置
 * 
 * 角色权限：审核员（Role_Id = 100，与数据库 Sys_Role 一致）
 * 入口路径：/auditor-login → /auditor → /auditor/workspace
 */

let viewgird = [
  // 审核员工作台
  {
    path: '/auditor/workspace',
    name: 'AuditorWorkspace',
    component: () => import('@/views/cert_admin/Workspace.vue'),
    meta: { title: '工作台', icon: 'odometer' }
  },
  // 我的任务列表（待开发）
  // {
  //   path: '/auditor/tasks',
  //   name: 'AuditorTasks',
  //   component: () => import('@/views/cert_admin/Tasks.vue'),
  //   meta: { title: '我的任务', icon: 'list' }
  // },
  // 消息中心（待开发）
  // {
  //   path: '/auditor/messages',
  //   name: 'AuditorMessages',
  //   component: () => import('@/views/cert_admin/Message/Index.vue'),
  //   meta: { title: '消息中心', icon: 'bell' }
  // },
  // 个人中心（待开发）
  // {
  //   path: '/auditor/profile',
  //   name: 'AuditorProfile',
  //   component: () => import('@/views/cert_admin/Profile/Index.vue'),
  //   meta: { title: '个人中心', icon: 'user' }
  // }
]

export default viewgird
