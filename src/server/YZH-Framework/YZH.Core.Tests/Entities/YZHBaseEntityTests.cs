using Xunit;
using YZH.Core.Entities;

namespace YZH.Core.Tests.Entities
{
    /// <summary>
    /// YZHBaseEntity 单元测试
    /// 
    /// 覆盖范围：
    /// - 默认值验证
    /// - 辅助方法验证
    /// - 状态属性（IsDeleted/IsDisabled）验证
    /// 
    /// 状态：[DONE] Phase 1 基础测试完成
    /// </summary>
    public class YZHBaseEntityTests
    {
        #region 默认值测试

        [Fact]
        public void Default_Enable_ShouldBe_True()
        {
            var entity = new TestEntity();
            Assert.True(entity.Enable);
        }

        [Fact]
        public void Default_Sort_ShouldBe_Zero()
        {
            var entity = new TestEntity();
            Assert.Equal(0, entity.Sort);
        }

        [Fact]
        public void Default_Code_ShouldBe_Null()
        {
            var entity = new TestEntity();
            Assert.Null(entity.Code);
        }

        [Fact]
        public void Default_OrgCode_ShouldBe_Null()
        {
            var entity = new TestEntity();
            Assert.Null(entity.OrgCode);
        }

        [Fact]
        public void Default_Audit_Fields_ShouldBe_Null()
        {
            var entity = new TestEntity();
            
            // 创建信息
            Assert.Null(entity.CreateID);
            Assert.Null(entity.Creator);
            Assert.Null(entity.CreateDate);
            
            // 修改信息
            Assert.Null(entity.ModifyID);
            Assert.Null(entity.Modifier);
            Assert.Null(entity.ModifyDate);
            
            // 删除信息
            Assert.Null(entity.DeleteID);
            Assert.Null(entity.Deleter);
            Assert.Null(entity.DeleteTime);
        }

        #endregion

        #region FillCreateInfo 测试

        [Fact]
        public void FillCreateInfo_Should_Set_Create_Fields()
        {
            // Arrange
            var entity = new TestEntity();
            
            // Act
            entity.FillCreateInfo(1, "Admin", "CB001");
            
            // Assert
            Assert.Equal(1, entity.CreateID);
            Assert.Equal("Admin", entity.Creator);
            Assert.NotNull(entity.CreateDate);
            Assert.Equal("CB001", entity.OrgCode);
        }

        [Fact]
        public void FillCreateInfo_Without_OrgCode_Should_Not_Set_OrgCode()
        {
            // Arrange
            var entity = new TestEntity();
            
            // Act
            entity.FillCreateInfo(1, "Admin");
            
            // Assert
            Assert.Equal(1, entity.CreateID);
            Assert.Null(entity.OrgCode); // 不应该设置
        }

        [Fact]
        public void FillCreateInfo_Called_Twice_Should_Overwrite()
        {
            // Arrange
            var entity = new TestEntity();
            entity.FillCreateInfo(1, "Admin1");
            
            // Act
            entity.FillCreateInfo(2, "Admin2");
            
            // Assert: 应该覆盖
            Assert.Equal(2, entity.CreateID);
            Assert.Equal("Admin2", entity.Creator);
        }

        #endregion

        #region FillModifyInfo 测试

        [Fact]
        public void FillModifyInfo_Should_Set_Modify_Fields()
        {
            // Arrange
            var entity = new TestEntity();
            
            // Act
            entity.FillModifyInfo(10, "Editor");
            
            // Assert
            Assert.Equal(10, entity.ModifyID);
            Assert.Equal("Editor", entity.Modifier);
            Assert.NotNull(entity.ModifyDate);
        }

        #endregion

        #region MarkAsDeleted 测试

        [Fact]
        public void MarkAsDeleted_Should_Set_Enable_False_And_Delete_Info()
        {
            // Arrange
            var entity = new TestEntity { Enable = true };
            
            // Act
            entity.MarkAsDeleted(99, "Deleter");
            
            // Assert
            Assert.False(entity.Enable);
            Assert.Equal(99, entity.DeleteID);
            Assert.Equal("Deleter", entity.Deleter);
            Assert.NotNull(entity.DeleteTime);
        }

        [Fact]
        public void MarkAsDeleted_Should_Make_IsDeleted_True()
        {
            // Arrange
            var entity = new TestEntity();
            
            // Act
            entity.MarkAsDeleted(1, "Admin");
            
            // Assert
            Assert.True(entity.IsDeleted);
            Assert.False(entity.IsDisabled);
        }

        #endregion

        #region MarkAsDisabled 测试

        [Fact]
        public void MarkAsDisabled_Should_Only_Set_Enable_False()
        {
            // Arrange
            var entity = new TestEntity { Enable = true };
            
            // Act
            entity.MarkAsDisabled();
            
            // Assert
            Assert.False(entity.Enable);
            Assert.Null(entity.DeleteID);
            Assert.Null(entity.Deleter);
            Assert.Null(entity.DeleteTime);
        }

        [Fact]
        public void MarkAsDisabled_Should_Make_IsDisabled_True()
        {
            // Arrange
            var entity = new TestEntity();
            
            // Act
            entity.MarkAsDisabled();
            
            // Assert
            Assert.False(entity.IsDeleted);
            Assert.True(entity.IsDisabled);
        }

        #endregion

        #region IsDeleted / IsDisabled 边界测试

        [Theory]
        [InlineData(true, null, false, false)]   // 正常状态
        [InlineData(false, null, false, true)]   // 仅禁用
        public void State_Properties_Should_Work_Correctly(
            bool enable, 
            DateTime? deleteTime, 
            bool expectedIsDeleted, 
            bool expectedIsDisabled)
        {
            // Arrange
            var entity = new TestEntity();
            entity.Enable = enable;
            
            if (deleteTime.HasValue)
            {
                entity.MarkAsDeleted(1, "Test");
            }
            else if (!enable)
            {
                entity.MarkAsDisabled();
            }
            
            // Assert
            Assert.Equal(expectedIsDeleted, entity.IsDeleted);
            Assert.Equal(expectedIsDisabled, entity.IsDisabled);
        }

        [Fact]
        public void State_Properties_Should_Reflect_Deleted()
        {
            // Arrange: Enable=false + MarkAsDeleted（DeleteTime 有值）
            var entity = new TestEntity();
            entity.Enable = false;
            entity.MarkAsDeleted(1, "Test");

            // Assert: 已删除 → IsDeleted=true, IsDisabled=false
            Assert.Equal(true, entity.IsDeleted);
            Assert.Equal(false, entity.IsDisabled);
        }

        #endregion

        #region 测试辅助类

        private class TestEntity : YZHBaseEntity
        {
            public string Name { get; set; }
        }

        #endregion
    }
}
