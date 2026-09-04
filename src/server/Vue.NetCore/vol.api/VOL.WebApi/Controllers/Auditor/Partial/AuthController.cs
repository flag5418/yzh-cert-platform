/*
 *审核员端认证接口编写处...
 *如果接口需要做Action的权限验证，请在Action上使用属性
 *如: [ApiActionPermission("AuditorAuth", Enums.ActionPermissionOptions.Add)]
 *
 * 注册接口：审核员自助注册，默认分配"体系认证客户端管理员"角色（Role_Id = 200），绑定认证机构
 * 登录接口：复用 Vol 原生 /api/User/login，前端根据 Role_Id 判断跳转
 *
 * 前端调用方式：
 *   proxy.http.post('/api/AuditorAuth/Register', form)
 *   proxy.http.get('/api/AuditorAuth/GetCurrentUser')
 *   proxy.http.get('/api/AuditorAuth/GetOrgList')
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using System;
using System.Collections.Generic;
using VOL.Core.Configuration;
using VOL.Core.DBManager;
using VOL.Core.Enums;
using VOL.Core.Extensions;
using VOL.Core.ManageUser;
using VOL.Core.Services;
using VOL.Core.Utilities;
using VOL.Entity.DomainModels;
using VOL.Sys.IRepositories;
using VOL.Sys.IServices;

namespace VOL.WebApi.Controllers.Auditor
{
    public partial class AuthController
    {
        private readonly ISys_UserService _userService;
        private readonly ISys_UserRepository _userRepository;
        private readonly ISys_RoleRepository _roleRepository;

        [ActivatorUtilitiesConstructor]
        public AuthController(
            ISys_UserService userService,
            ISys_UserRepository userRepository,
            ISys_RoleRepository roleRepository
        )
        : base(userService)
        {
            _userService = userService;
            _userRepository = userRepository;
            _roleRepository = roleRepository;
        }

        /// <summary>
        /// 审核员注册（AllowAnonymous）
        /// 极简注册：账号 + 密码 + 选择机构
        /// 校验账号唯一性 → 校验机构存在 → 创建 Sys_User → Role_Id = AUDITOR_CLIENT_ADMIN_ROLE_ID
        /// 真实姓名/手机号/邮箱 为可选字段，用户后续可在个人中心补充
        /// </summary>
        [HttpPost, Route("Register"), AllowAnonymous]
        public IActionResult Register([FromBody] AuditorRegisterRequest request)
        {
            var webResponse = new WebResponseContent();

            // 1. 极简参数校验（仅校验必填项）
            if (request == null)
                return Json(webResponse.Error("请求参数不能为空"));

            if (string.IsNullOrWhiteSpace(request.UserName) || request.UserName.Trim().Length < 3)
                return Json(webResponse.Error("账号不能为空且至少3位"));

            if (string.IsNullOrWhiteSpace(request.UserPwd) || request.UserPwd.Trim().Length < 6)
                return Json(webResponse.Error("密码不能为空且至少6位"));

            if (request.OrgId == null || request.OrgId <= 0)
                return Json(webResponse.Error("请选择所属认证机构"));

            try
            {
                // 2. 查询机构信息
                var connStr = DBServerProvider.GetConnectionString();
                using var conn = new MySqlConnection(connStr);
                conn.Open();

                long orgId = 0;
                string orgCode = "";
                string orgName = "";

                using (var cmd = new MySqlCommand(
                    "SELECT id, code, name FROM cert_certification_body WHERE status = 'active' AND enable = 1 AND id = @Id LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", request.OrgId.Value);
                    using var reader = cmd.ExecuteReader();
                    if (!reader.Read())
                        return Json(webResponse.Error("所选认证机构不存在或已停用"));

                    int idxId = reader.GetOrdinal("id");
                    int idxCode = reader.GetOrdinal("code");
                    int idxName = reader.GetOrdinal("name");
                    orgId = reader.GetInt64(idxId);
                    orgCode = reader.IsDBNull(idxCode) ? "" : reader.GetString(idxCode);
                    orgName = reader.IsDBNull(idxName) ? "" : reader.GetString(idxName);
                }

                // 3. 校验账号唯一性
                var existUser = _userRepository.FindFirst(x => x.UserName == request.UserName.Trim());
                if (existUser != null)
                    return Json(webResponse.Error($"账号「{request.UserName}」已存在"));

                // 4. 构建用户实体（手机号/邮箱/真实姓名为可选，后续可补充）
                var user = new Sys_User
                {
                    UserName = request.UserName.Trim(),
                    UserPwd = request.UserPwd.Trim().EncryptDES(AppSetting.Secret.User),
                    UserTrueName = request.UserTrueName ?? request.UserName.Trim(),
                    PhoneNo = request.PhoneNo?.Trim() ?? "",
                    Email = request.Email?.Trim() ?? "",
                    Role_Id = AUDITOR_CLIENT_ADMIN_ROLE_ID,
                    OrgId = orgId,
                    OrgCode = orgCode,
                    Enable = 1,
                    CreateID = 0,
                    Creator = "auditor_reg",
                    CreateDate = DateTime.Now
                };

                // 5. 保存到数据库
                _userRepository.Add(user, true);

                return Json(webResponse.OK("注册成功，请使用账号密码登录", new
                {
                    userId = user.User_Id,
                    userName = user.UserName,
                    orgId = user.OrgId,
                    orgName = orgName
                }));
            }
            catch (Exception ex)
            {
                Logger.Error(LoggerType.Login, ex.Message, null, ex.StackTrace);
                return Json(webResponse.Error($"注册失败: {ex.Message}"));
            }
        }

        /// <summary>
        /// 获取当前登录用户信息
        /// </summary>
        [HttpGet, Route("GetCurrentUser")]
        public IActionResult GetCurrentUser()
        {
            var webResponse = new WebResponseContent();
            var userInfo = UserContext.Current;

            if (userInfo == null)
                return Json(webResponse.Error("未登录或登录已过期"));

            var user = _userRepository.FindFirst(x => x.User_Id == userInfo.UserId);
            if (user == null)
                return Json(webResponse.Error("用户不存在"));

            // 查询角色名称
            var role = _roleRepository.FindFirst(x => x.Role_Id == user.Role_Id);
            var roleName = role?.RoleName ?? "";

            return Json(webResponse.OK("获取成功", new
            {
                userId = user.User_Id,
                userName = user.UserName,
                userTrueName = user.UserTrueName,
                roleId = user.Role_Id,
                roleName = roleName,
                orgId = user.OrgId,
                orgCode = user.OrgCode,
                isAuditor = user.Role_Id == AUDITOR_ROLE_ID || user.Role_Id == AUDITOR_CLIENT_ADMIN_ROLE_ID
            }));
        }

        /// <summary>
        /// 获取认证机构列表（供注册时下拉选择）
        /// </summary>
        [HttpGet, Route("GetOrgList"), AllowAnonymous]
        public IActionResult GetOrgList()
        {
            var webResponse = new WebResponseContent();

            try
            {
                var connStr = DBServerProvider.GetConnectionString();
                using var conn = new MySqlConnection(connStr);
                conn.Open();

                var list = new List<object>();

                using (var cmd = new MySqlCommand(
                    "SELECT id, code, name FROM cert_certification_body WHERE status = 'active' AND enable = 1 ORDER BY name ASC", conn))
                {
                    using var reader = cmd.ExecuteReader();
                    int idxId = reader.GetOrdinal("id");
                    int idxCode = reader.GetOrdinal("code");
                    int idxName = reader.GetOrdinal("name");
                    while (reader.Read())
                    {
                        list.Add(new
                        {
                            id = reader.GetValue(idxId).ToString(),
                            orgCode = reader.IsDBNull(idxCode) ? "" : reader.GetString(idxCode),
                            orgName = reader.IsDBNull(idxName) ? "" : reader.GetString(idxName)
                        });
                    }
                }

                return Json(webResponse.OK("获取成功", list));
            }
            catch (Exception ex)
            {
                Logger.Error(LoggerType.ApiException, ex.Message, null, ex.StackTrace);
                return Json(webResponse.Error($"获取机构列表失败: {ex.Message}"));
            }
        }

        // 审核员角色固定 ID
        private const int AUDITOR_ROLE_ID = 20;
        private const int AUDITOR_CLIENT_ADMIN_ROLE_ID = 200;
    }

    /// <summary>
    /// 审核员注册请求 DTO
    /// </summary>
    public class AuditorRegisterRequest
    {
        public string UserName { get; set; }
        public string UserPwd { get; set; }
        public string UserTrueName { get; set; }
        public string PhoneNo { get; set; }
        public string Email { get; set; }
        public long? OrgId { get; set; }
    }
}
