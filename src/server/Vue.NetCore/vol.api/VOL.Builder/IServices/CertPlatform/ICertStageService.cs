using VOL.Core.BaseProvider;
using VOL.Entity.CertPlatform.Cert;

namespace VOL.Builder.IServices.CertPlatform
{
    public partial interface ICertStageService : IService<CertStage>
    {
        System.Threading.Tasks.Task<System.Collections.Generic.List<CertStage>> GetActiveStagesAsync();
        // TODO: GetOrgStageIdsAsync 依赖已删除的 CertOrgStage，待 cert_cb_stage 表重建后恢复
        // System.Threading.Tasks.Task<System.Collections.Generic.List<long>> GetOrgStageIdsAsync(string cbCode);
    }
}
