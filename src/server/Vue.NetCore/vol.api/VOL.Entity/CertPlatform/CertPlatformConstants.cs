namespace VOL.Entity.CertPlatform
{
    /// <summary>
    /// 体系认证平台共享常量
    /// </summary>
    public static class CertPlatformConstants
    {
        /// <summary>
        /// YZH 标准企业编码（虚拟企业）：
        /// 标准目录文件的提取结果（B-08/B-09）挂在该企业名下，
        /// 工作流 get_field / get_table 按 enterprise_code='YZH-STD-ENT' 统一查询验证数据。
        /// 关联：docs/80-功能设计/提取结果落库-功能设计-V1.md
        /// </summary>
        public const string YZH_STANDARD_ENTERPRISE_CODE = "YZH-STD-ENT";
    }
}
