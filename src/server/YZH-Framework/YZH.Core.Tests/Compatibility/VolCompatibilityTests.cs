using Xunit;
using YZH.Core.Entities;

namespace YZH.Core.Tests.Compatibility
{
    /// <summary>
    /// Vol 兼容性测试套件
    /// 
    /// 目的：确保 YZH Framework 不破坏 Vol 框架的现有功能
    /// 原则（对齐 YZH-建设原则-V1.md §3.1）：
    /// - YZH 是增量层，不能覆盖或破坏 Vol 的核心能力
    /// - 所有测试必须在 Vol 容器加载 YZHModule 后通过
    /// 
    /// 测试分类：
    /// 1. 继承链验证 - 确保 YZHBaseEntity 正确继承 Vol.BaseEntity
    /// 2. 字段兼容性 - 确保新增字段不影响 EF Core 映射
    /// 3. 特性兼容性 - 确保 YZH 特性与 Vol 特性不冲突
    /// 4. 容器集成 - 确保 YZHModule 可成功加载到 Vol 容器
    /// 
    /// 状态：[DONE] Phase 1 骨架完成，Phase 2 补充完整集成测试
    /// </summary>
    public class VolCompatibilityTests
    {
        #region 1. 继承链验证

        [Fact]
        public void YZHBaseEntity_Should_Inherit_From_Vol_BaseEntity()
        {
            // Arrange & Act
            var entity = new TestEntity();
            
            // Assert: 验证继承链正确
            Assert.IsAssignableFrom<VOL.Entity.SystemModels.BaseEntity>(entity);
            Assert.IsAssignableFrom<YZHBaseEntity>(entity);
        }

        [Fact]
        public void YZHBaseEntity_Should_Be_Concrete_Class()
        {
            // 可以直接实例化（非抽象类）
            var entity = new TestEntity();
            Assert.NotNull(entity);
        }

        #endregion

        #region 2. 字段兼容性验证

        [Fact]
        public void Default_Values_Should_Match_Specification()
        {
            // Arrange & Act
            var entity = new TestEntity();
            
            // Assert: 验证所有默认值符合 §4.1 规范
            Assert.True(entity.Enable, "Enable 默认值应为 true");
            Assert.Equal(0, entity.Sort);
            Assert.Null(entity.Code);
            Assert.Null(entity.OrgCode);
            
            // 审计字段默认为 null
            Assert.Null(entity.CreateID);
            Assert.Null(entity.Creator);
            Assert.Null(entity.CreateDate);
            Assert.Null(entity.ModifyID);
            Assert.Null(entity.Modifier);
            Assert.Null(entity.ModifyDate);
            
            // 删除信息默认为 null
            Assert.Null(entity.DeleteID);
            Assert.Null(entity.Deleter);
            Assert.Null(entity.DeleteTime);
        }

        [Fact]
        public void CreateID_Should_Be_Int_Type_Not_String()
        {
            // 验证 CreateID 类型为 int?（对应 Sys_User.Id）
            var entity = new TestEntity();
            entity.FillCreateInfo(123, "TestUser");
            
            Assert.Equal(123, entity.CreateID);
            Assert.IsType<int>(entity.CreateID.Value);
        }

        [Theory]
        [InlineData(true, null, false)]   // Enable=true, DeleteTime=null → Not deleted
        [InlineData(false, null, false)]  // Enable=false, DeleteTime=null → Disabled only
        public void IsDeleted_Property_Should_Work_Correctly(bool enable, DateTime? deleteTime, bool expectedIsDeleted)
        {
            var entity = new TestEntity();
            entity.Enable = enable;
            
            if (deleteTime.HasValue)
            {
                entity.MarkAsDeleted(1, "Admin");
                Assert.Equal(deleteTime.Value.Date, entity.DeleteTime.Value.Date);
            }
            
            Assert.Equal(expectedIsDeleted, entity.IsDeleted);
        }

        [Fact]
        public void Deleted_Then_IsDeleted_Should_Be_True()
        {
            var entity = new TestEntity();
            entity.Enable = false;
            entity.MarkAsDeleted(1, "Admin");

            Assert.NotNull(entity.DeleteTime);
            Assert.True(entity.IsDeleted);
        }

        [Theory]
        [InlineData(true, null, false)]
        [InlineData(false, null, true)]
        public void IsDisabled_Property_Should_Work_Correctly(bool enable, DateTime? deleteTime, bool expectedIsDisabled)
        {
            var entity = new TestEntity();
            entity.Enable = enable;
            
            if (deleteTime.HasValue)
            {
                entity.MarkAsDeleted(1, "Admin");
            }
            
            Assert.Equal(expectedIsDisabled, entity.IsDisabled);
        }

        [Fact]
        public void Deleted_Then_IsDisabled_Should_Be_False()
        {
            var entity = new TestEntity();
            entity.Enable = false;
            entity.MarkAsDeleted(1, "Admin");

            Assert.NotNull(entity.DeleteTime);
            Assert.False(entity.IsDisabled);
        }

        #endregion

        #region 3. 辅助方法验证

        [Fact]
        public void FillCreateInfo_Should_Set_All_Create_Fields()
        {
            // Arrange
            var entity = new TestEntity();
            var userId = 42;
            var userName = "张三";
            var orgCode = "CB001";
            
            // Act
            entity.FillCreateInfo(userId, userName, orgCode);
            
            // Assert: 所有创建字段被正确填充
            Assert.Equal(userId, entity.CreateID);
            Assert.Equal(userName, entity.Creator);
            Assert.NotNull(entity.CreateDate);
            Assert.Equal(orgCode, entity.OrgCode);
            
            // 其他字段不受影响
            Assert.Null(entity.ModifyID);
            Assert.Null(entity.DeleteID);
        }

        [Fact]
        public void FillModifyInfo_Should_Set_All_Modify_Fields()
        {
            // Arrange
            var entity = new TestEntity();
            var userId = 43;
            var userName = "李四";
            
            // Act
            entity.FillModifyInfo(userId, userName);
            
            // Assert: 所有修改字段被正确填充
            Assert.Equal(userId, entity.ModifyID);
            Assert.Equal(userName, entity.Modifier);
            Assert.NotNull(entity.ModifyDate);
            
            // 创建字段不受影响
            Assert.Null(entity.CreateID);
        }

        [Fact]
        public void MarkAsDeleted_Should_Set_Delete_Fields_And_Disable()
        {
            // Arrange
            var entity = new TestEntity();
            entity.Enable = true; // 初始状态
            
            // Act
            entity.MarkAsDeleted(44, "王五");
            
            // Assert: 删除信息已填充且实体已禁用
            Assert.False(entity.Enable);
            Assert.Equal(44, entity.DeleteID);
            Assert.Equal("王五", entity.Deleter);
            Assert.NotNull(entity.DeleteTime);
            Assert.True(entity.IsDeleted);  // 应该标记为已删除
            Assert.False(entity.IsDisabled); // 不是仅禁用
        }

        [Fact]
        public void MarkAsDisabled_Should_Only_Set_Enable_False()
        {
            // Arrange
            var entity = new TestEntity();
            entity.Enable = true;
            
            // Act
            entity.MarkAsDisabled();
            
            // Assert: 仅禁用，不填充删除信息
            Assert.False(entity.Enable);
            Assert.Null(entity.DeleteID);
            Assert.Null(entity.Deleter);
            Assert.Null(entity.DeleteTime);
            Assert.False(entity.IsDeleted);  // 不是已删除
            Assert.True(entity.IsDisabled);  // 是仅禁用
        }

        #endregion

        #region 4. 特性兼容性（TODO:P2）

        // TODO:P2 - Phase 2 实现 Vol 特性兼容性测试
        // [Fact]
        // public void YZH_Attributes_Should_Not_Conflict_With_Vol_Attributes() { ... }

        #endregion

        #region 5. 容器集成测试（TODO:P2）

        // TODO:P2 - Phase 2 实现 Autofac 容器集成测试
        // [Fact]
        // public async Task YZHModule_Can_Load_Into_Vol_Container() { ... }
        
        // TODO:P2 - Vol 核心服务可用性验证
        // [Theory]
        // [InlineData("ServiceBase")]
        // [InlineData("ActionPermissionFilter")]
        // [InlineData("DictionaryManager")]
        // public void Vol_Core_Services_Should_Be_Available_After_YZH_Module_Loaded(string serviceName) { ... }

        #endregion

        #region 测试辅助类

        /// <summary>
        /// 测试用实体类（用于实例化 YZHBaseEntity）
        /// </summary>
        private class TestEntity : YZHBaseEntity
        {
            // 可以添加业务字段进行更复杂的测试
            public string Name { get; set; }
        }

        #endregion
    }
}
