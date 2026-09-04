#!/bin/bash
echo "=== 后端 ==="
curl -s http://127.0.0.1:9992/api/User/getVierificationCode > /dev/null && echo "后端: ✓"

echo ""
echo "=== 前端 ==="
curl -s -o /dev/null -w "index.html: %{http_code}" http://127.0.0.1:9990/ && echo ""
curl -s -o /dev/null -w "auditor-login: %{http_code}" http://127.0.0.1:9990/#/auditor-login && echo ""

echo ""
echo "=== 路由验证 ==="
echo "审核员登录: http://127.0.0.1:9990/#/auditor-login"
echo "审核员工作台: http://127.0.0.1:9990/#/auditor/workspace"
echo "管理员登录: http://127.0.0.1:9990/#/admin-login"
echo "管理员主页: http://127.0.0.1:9990/#/home"
