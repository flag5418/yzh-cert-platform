using Microsoft.Extensions.Logging;
using SqlSugar;
using CertPlatform.Auditor.Models;
using YZH.Core.Api.Models.Organization;
using YZH.Core.Api.Models.System;
using YZH.Core.Api.Models.Users;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Auditor.Services;

/// <summary>
/// 专家注册服务（一次注册 = 一个工作区）
///
/// <para><b>设计前提</b>（见《体系认证专家系统建设计划-V4》）：</para>
/// <list type="number">
///   <item>这是**专家个人**的付费服务平台；一次注册只能针对**一个**体系认证机构</item>
///   <item>注册人 = 虚拟体系机构管理员，注册成功即默认拥有该角色全部菜单与接口权限</item>
///   <item>权限**不在此处分配** —— 后台已把 <c>Sys_RoleMenu</c> + <c>sys_role_api</c> 预配到
///         <c>ROLE_AUDIT_CLIENT_ADMIN</c>，注册只负责把用户挂上该角色（写 <c>Sys_RoleUser</c>）</item>
/// </list>
///
/// <para><b>组织树形态</b>（集中定义在 <see cref="VirtualOrgRootCode"/> 与 <see cref="DefaultGroups"/>，
/// 调整形态只需改这一处）：</para>
/// <code>
/// ORG_ROOT_003 「虚拟体系机构」            VirtualOrg  L1   ← 幂等种子，只建一次
///   └── {机构简称} · {姓名}                VirtualOrg  L2   ← 工作区，一次注册一个
///         ├── 管理员                       Dept        L3   ← 注册人挂此节点
///         ├── 审核员                       Dept        L3
///         ├── 审核组长                     Dept        L3
///         └── 企业用户                     Dept        L3   ← 文件夹
/// </code>
///
/// <para><b>与历史实现的差异</b>（历史蓝本：<c>src/old/.../Controllers/Auditor/Partial/AuthController.cs</c>）：</para>
/// <list type="bullet">
///   <item>历史项目只写 <c>Sys_User.Role_Id = 200</c>；新架构权限链路走 <c>Sys_RoleUser</c> 关联表
///         （<c>AuthController.Login</c> → <c>RoleService.GetRoleCodeByUserCodeAsync</c>），
///         故**必须**写 <c>Sys_RoleUser</c>，否则登录后 <c>RoleCode</c> 为空、权限全无</item>
///   <item>历史项目 SQL 用 <c>enable = 1</c> 过滤机构；新库该列已 PascalCase 化为 <c>IsValid</c>
///         （<c>cert_certification_body</c> **不存在** <c>enable</c> 列），照抄历史 SQL 会直接报错</item>
///   <item>历史项目未建 <c>Sys_Organization</c> 节点；本实现补建工作区与分组节点，
///         使机构树、数据权限、人员归口三者可用</item>
/// </list>
/// </summary>
public class AuditorRegisterService
{
    private readonly IDbOrm _dbOrm;
    private readonly EntityService<Sys_Organization> _orgService;
    private readonly EntityService<Sys_User> _userService;
    private readonly EntityService<Sys_RoleUser> _roleUserService;
    private readonly PasswordHelper _password;
    private readonly ILogger<AuditorRegisterService> _logger;

    // ========================================================
    // 组织树形态常量（★ 唯一调整点）
    // ========================================================

    /// <summary>虚拟体系机构根节点 Code（VirtualOrg 根，幂等创建）</summary>
    public const string VirtualOrgRootCode = "ORG_ROOT_003";

    /// <summary>虚拟体系机构根节点名称</summary>
    public const string VirtualOrgRootName = "虚拟体系机构";

    /// <summary>注册人默认绑定角色 Code（体系认证客户端管理员，Sys_Role.Id = 200）</summary>
    public const string DefaultRoleCode = "ROLE_AUDIT_CLIENT_ADMIN";

    /// <summary>工作区在机构树中的层级（根为 1）</summary>
    private const int WorkspaceLevel = 2;

    /// <summary>角色分组在机构树中的层级</summary>
    private const int GroupLevel = 3;

    /// <summary>工作区下的默认角色分组（注册人挂「管理员」）</summary>
    private static readonly string[] DefaultGroups = { "管理员", "审核员", "审核组长", "企业用户" };

    /// <summary>注册人默认挂靠的分组名（必须是 <see cref="DefaultGroups"/> 的成员）</summary>
    private const string OwnerGroupName = "管理员";

    /// <summary>注册来源标识（写入 CreateBy；与历史实现 <c>auditor_reg</c> 一致）</summary>
    private const string RegisterOrigin = "auditor_reg";

    public AuditorRegisterService(
        IDbOrm dbOrm,
        EntityService<Sys_Organization> orgService,
        EntityService<Sys_User> userService,
        EntityService<Sys_RoleUser> roleUserService,
        PasswordHelper password,
        ILogger<AuditorRegisterService> logger)
    {
        _dbOrm = dbOrm;
        _orgService = orgService;
        _userService = userService;
        _roleUserService = roleUserService;
        _password = password;
        _logger = logger;
    }

    // ========================================================
    // 一、认证机构下拉（注册页数据源）
    // ========================================================

    /// <summary>
    /// 获取可用于注册的体系认证机构列表。
    ///
    /// <para>数据源 = <c>cert_certification_body</c> 中 <c>IsValid = 1</c> 且 <c>Status = 'active'</c>
    /// 且未软删除的记录 —— 即「后台系统已经定义好的体系认证机构」。</para>
    /// </summary>
    public async Task<List<CertBodyOptionDto>> GetCertBodyOptionsAsync()
    {
        return await _dbOrm.Client.Queryable<CertificationBody>()
            .Where(x => x.IsValid == 1 && x.Status == "active" && !x.IsDeleted)
            .OrderBy(x => x.Sort)
            .OrderBy(x => x.Name)
            .Select(x => new CertBodyOptionDto
            {
                Code = x.Code,
                Name = x.Name,
                ShortName = x.ShortName,
                CbCode = x.CbCode
            })
            .ToListAsync();
    }

    // ========================================================
    // 二、注册主流程
    // ========================================================

    /// <summary>
    /// 专家注册：事务内建「工作区机构节点 + 默认分组 + 用户 + 角色关联」。
    /// </summary>
    public async Task<Result<AuditorRegisterResultDto>> RegisterAsync(
        AuditorRegisterRequest req, string? clientIp)
    {
        // ---------- 1. 入参校验 ----------
        var userName = (req.UserName ?? string.Empty).Trim();
        var trueName = (req.UserTrueName ?? string.Empty).Trim();
        var password = (req.Password ?? string.Empty).Trim();

        if (userName.Length < 3)
            return Result<AuditorRegisterResultDto>.Fail("登录名不能少于 3 个字符");
        if (password.Length < 6)
            return Result<AuditorRegisterResultDto>.Fail("密码不能少于 6 位");
        if (string.IsNullOrEmpty(trueName))
            trueName = userName;
        if (trueName.Length > 20)
            return Result<AuditorRegisterResultDto>.Fail("姓名不能超过 20 个字符");
        if (string.IsNullOrWhiteSpace(req.CertBodyCode))
            return Result<AuditorRegisterResultDto>.Fail("请选择所属体系认证机构");

        // ---------- 2. 所选机构必须存在且可用 ----------
        var certBody = await _dbOrm.Client.Queryable<CertificationBody>()
            .Where(x => x.Code == req.CertBodyCode && !x.IsDeleted)
            .FirstAsync();

        if (certBody == null)
            return Result<AuditorRegisterResultDto>.Fail("所选体系认证机构不存在");
        if (certBody.IsValid != 1 || certBody.Status != "active")
            return Result<AuditorRegisterResultDto>.Fail($"体系认证机构【{certBody.Name}】已停用，暂不可注册");

        // ---------- 3. 登录名唯一性（含软删除记录，避免复用已删账号造成歧义） ----------
        var nameTaken = await _dbOrm.Client.Queryable<Sys_User>()
            .Where(x => x.UserName == userName && !x.IsDeleted)
            .AnyAsync();
        if (nameTaken)
            return Result<AuditorRegisterResultDto>.Fail($"登录名【{userName}】已被占用，请更换");

        // ---------- 4. 确保虚拟体系机构根节点存在（幂等） ----------
        var rootResult = await EnsureVirtualOrgRootAsync(clientIp);
        if (!rootResult.Success)
            return Result<AuditorRegisterResultDto>.Fail(rootResult.Error ?? "初始化虚拟体系机构根节点失败");
        var rootCode = rootResult.Data!;

        // ---------- 5. 事务写入 ----------
        using var tx = _dbOrm.BeginTransaction();
        try
        {
            // 5.1 工作区节点
            var workspaceCode = Guid.NewGuid().ToString("N");
            var workspaceName = BuildWorkspaceName(certBody, trueName);

            var workspace = new Sys_Organization
            {
                Code = workspaceCode,
                OrgName = workspaceName,
                OrgCode = certBody.CbCode,
                ParentCode = rootCode,
                OrgType = "VirtualOrg",
                OrgLevel = WorkspaceLevel,
                OrgPath = $"/{rootCode}/{workspaceCode}",
                LeaderName = trueName,
                LeaderPhone = req.PhoneNo,
                Sort = 0,
                Enable = 1,
                IsValid = 1,
                Remark = $"专家工作区｜所属认证机构：{certBody.Name}（{certBody.Code}）"
            };

            var wsResult = await _orgService.Insert(workspace, clientIp);
            if (!wsResult.Success)
            {
                tx.Rollback();
                return Result<AuditorRegisterResultDto>.Fail($"创建工作区机构节点失败：{wsResult.Error}");
            }

            // 5.2 默认角色分组（注册人挂「管理员」分组）
            string? ownerGroupCode = null;
            for (var i = 0; i < DefaultGroups.Length; i++)
            {
                var groupName = DefaultGroups[i];
                var groupCode = Guid.NewGuid().ToString("N");

                var group = new Sys_Organization
                {
                    Code = groupCode,
                    OrgName = groupName,
                    ParentCode = workspaceCode,
                    OrgType = "Dept",
                    OrgLevel = GroupLevel,
                    OrgPath = $"/{rootCode}/{workspaceCode}/{groupCode}",
                    Sort = (i + 1) * 10,
                    Enable = 1,
                    IsValid = 1,
                    Remark = $"{groupName}分组（专家工作区默认）"
                };

                var groupResult = await _orgService.Insert(group, clientIp);
                if (!groupResult.Success)
                {
                    tx.Rollback();
                    return Result<AuditorRegisterResultDto>.Fail($"创建【{groupName}】分组失败：{groupResult.Error}");
                }

                if (groupName == OwnerGroupName)
                    ownerGroupCode = groupCode;
            }

            if (string.IsNullOrEmpty(ownerGroupCode))
            {
                tx.Rollback();
                return Result<AuditorRegisterResultDto>.Fail($"未找到注册人默认分组【{OwnerGroupName}】，请检查 DefaultGroups 配置");
            }

            // 5.3 用户（密码 AES 加密；OrgCode 指向「管理员」分组，满足「人员挂叶子机构」约束）
            var userCode = Guid.NewGuid().ToString("N");
            var user = new Sys_User
            {
                Code = userCode,
                UserName = userName,
                UserTrueName = trueName,
                UserPwd = _password.AesEncrypt(password),
                OrgCode = ownerGroupCode,
                PhoneNo = string.IsNullOrWhiteSpace(req.PhoneNo) ? null : req.PhoneNo!.Trim(),
                Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email!.Trim(),
                Enable = 1,
                IsValid = 1,
                IsDeleted = false,
                Remark = $"专家注册｜认证机构：{certBody.Name}（{certBody.Code}）"
            };

            var userResult = await _userService.Insert(user, clientIp);
            if (!userResult.Success)
            {
                tx.Rollback();
                return Result<AuditorRegisterResultDto>.Fail($"创建用户失败：{userResult.Error}");
            }

            // 5.4 角色关联（新架构权限链路的唯一入口）
            //     注：Sys_RoleUser.Id 是 varchar(64) 非自增，必须手工赋 Guid；
            //         表上有唯一约束 uk_role_user(RoleCode, UserCode)，重复绑定会报错。
            var roleUser = new Sys_RoleUser
            {
                Id = Guid.NewGuid().ToString("N"),
                RoleCode = DefaultRoleCode,
                UserCode = userCode
            };

            var ruResult = await _roleUserService.Insert(roleUser, clientIp);
            if (!ruResult.Success)
            {
                tx.Rollback();
                return Result<AuditorRegisterResultDto>.Fail($"绑定默认角色失败：{ruResult.Error}");
            }

            // 5.5 回写匿名注册场景下缺失的字段（三处，均为框架层既有行为导致）
            //   (a) Sys_User.RoleId —— 实体 Sys_User 未声明该列（框架层遗留），
            //       但视图 v_sys_user 用 `u.RoleId = r.Id` JOIN Sys_Role 取 RoleName；
            //       不写会让后台「用户管理」页的角色列空白（登录与权限不受影响，那条链路走 Sys_RoleUser）。
            //   (b) CreateBy —— EntityService.Insert 的 FillCreateAudit 取 _userContext.UserCode，
            //       匿名请求下为空串，回写来源标识以便审计追溯。
            //
            //   ⚠️ 坑：SqlScalarAsync<T> 的内部实现是 Convert.ChangeType(result, typeof(T))，
            //      而 Convert.ChangeType **不支持 Nullable<T>** —— 传 int? 会抛 InvalidCastException，
            //      被 catch 后静默返回 Fail（Success=false），表现为「查到了却拿到 0」。
            //      故此处泛型实参必须是 int，且必须检查 Success。
            var roleIdResult = await _dbOrm.SqlScalarAsync<int>(
                "SELECT Id FROM Sys_Role WHERE Code = @code LIMIT 1",
                new { code = DefaultRoleCode });
            if (!roleIdResult.Success)
            {
                tx.Rollback();
                return Result<AuditorRegisterResultDto>.Fail($"查询默认角色失败：{roleIdResult.Error}");
            }

            var stampUser = await _dbOrm.SqlExecuteAsync(
                "UPDATE Sys_User SET RoleId = @roleId, CreateBy = @createBy WHERE Code = @code",
                new { roleId = roleIdResult.Data, createBy = RegisterOrigin, code = userCode });
            if (!stampUser.Success)
            {
                tx.Rollback();
                return Result<AuditorRegisterResultDto>.Fail($"回写用户角色字段失败：{stampUser.Error}");
            }

            var stampOrg = await _dbOrm.SqlExecuteAsync(
                "UPDATE Sys_Organization SET CreateBy = @createBy WHERE OrgPath LIKE @prefix",
                new { createBy = RegisterOrigin, prefix = $"/{rootCode}/{workspaceCode}%" });
            if (!stampOrg.Success)
            {
                tx.Rollback();
                return Result<AuditorRegisterResultDto>.Fail($"回写机构审计字段失败：{stampOrg.Error}");
            }

            var stampRoleUser = await _dbOrm.SqlExecuteAsync(
                "UPDATE Sys_RoleUser SET CreateBy = @createBy WHERE Id = @id",
                new { createBy = RegisterOrigin, id = roleUser.Id });
            if (!stampRoleUser.Success)
            {
                tx.Rollback();
                return Result<AuditorRegisterResultDto>.Fail($"回写角色关联审计字段失败：{stampRoleUser.Error}");
            }

            tx.Commit();

            _logger.LogInformation(
                "专家注册成功：UserName={UserName}, UserCode={UserCode}, Workspace={WorkspaceCode}, CertBody={CertBody}",
                userName, userCode, workspaceCode, certBody.Code);

            return Result<AuditorRegisterResultDto>.Ok(new AuditorRegisterResultDto
            {
                UserCode = userCode,
                UserName = userName,
                OrgCode = workspaceCode,
                OrgName = workspaceName,
                RoleCode = DefaultRoleCode
            });
        }
        catch (Exception ex)
        {
            tx.Rollback();
            _logger.LogError(ex, "专家注册失败：UserName={UserName}", userName);
            return Result<AuditorRegisterResultDto>.Fail($"注册失败：{ex.Message}");
        }
    }

    // ========================================================
    // 三、辅助
    // ========================================================

    /// <summary>
    /// 确保虚拟体系机构根节点存在；不存在则创建。返回根节点 Code。
    /// <para>幂等：已存在直接返回，不修改既有记录。</para>
    /// </summary>
    private async Task<Result<string>> EnsureVirtualOrgRootAsync(string? clientIp)
    {
        var existing = await _orgService.GetByCodeAny(VirtualOrgRootCode);
        if (existing.Success && existing.Data != null)
            return Result<string>.Ok(existing.Data.Code);

        var root = new Sys_Organization
        {
            Code = VirtualOrgRootCode,
            OrgName = VirtualOrgRootName,
            OrgType = "VirtualOrg",
            OrgLevel = 1,
            OrgPath = $"/{VirtualOrgRootCode}",
            ParentCode = null,
            Sort = 0,
            Enable = 1,
            IsValid = 1,
            Remark = "专家注册工作区根节点（由 AuditorRegisterService 幂等创建）"
        };

        var insert = await _orgService.Insert(root, clientIp);
        if (!insert.Success)
            return Result<string>.Fail(insert.Error ?? "创建虚拟体系机构根节点失败");

        return Result<string>.Ok(VirtualOrgRootCode);
    }

    /// <summary>工作区节点名：{机构简称/全称} · {姓名}</summary>
    private static string BuildWorkspaceName(CertificationBody certBody, string trueName)
    {
        var orgLabel = string.IsNullOrWhiteSpace(certBody.ShortName) ? certBody.Name : certBody.ShortName!;
        var name = $"{orgLabel} · {trueName}";
        // OrgName 列长 200，留足余量
        return name.Length > 150 ? name[..150] : name;
    }
}
