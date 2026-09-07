using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using YZH.Core.Controllers.Basic;
using YZH.Core.Enums;
using YZH.Core.Filters;
using YZH.Core.Services;
using YZH.Entity.DomainModels;

namespace YZH.Core.Controllers
{
    /// <summary>
    /// 左树右表控制器基类
    /// 适用于组织机构、标准目录等场景
    /// </summary>
    /// <typeparam name="TService">服务接口</typeparam>
    /// <typeparam name="TEntity">实体类型（需实现 ITreeTableEntity）</typeparam>
    public abstract class TreeTableControllerBase<TService, TEntity> : ApiBaseController<TService>
        where TEntity : class, ITreeTableEntity, new()
    {
        protected TreeTableControllerBase(TService service) : base(service)
        {
        }

        /// <summary>
        /// 加载树根节点数据
        /// 子类必须实现，根据业务设置根节点过滤条件
        /// </summary>
        [HttpPost, Route("getTreeTableRootData")]
        [ApiActionPermission(ActionPermissionOptions.Search)]
        public abstract Task<ActionResult> GetTreeTableRootData(PageDataOptions options);

        /// <summary>
        /// 懒加载树子节点数据
        /// 子类必须实现，根据 parentId 返回子节点列表
        /// </summary>
        [HttpPost, Route("getTreeTableChildrenData")]
        [ApiActionPermission(ActionPermissionOptions.Search)]
        public abstract Task<ActionResult> GetTreeTableChildrenData(Guid departmentId);

        /// <summary>
        /// 获取树节点 DTO（供子类使用）
        /// </summary>
        protected object CreateTreeNodeDto(
            Guid id,
            string name,
            string code,
            Guid? parentId,
            int? enable,
            string remark,
            bool hasChildren,
            DateTime? createDate = null,
            string creator = null,
            string modifier = null,
            DateTime? modifyDate = null)
        {
            return new
            {
                departmentId = id,
                departmentName = name,
                departmentCode = code,
                parentId,
                enable,
                remark,
                hasChildren,
                createDate,
                creator,
                modifier,
                modifyDate
            };
        }
    }
}
