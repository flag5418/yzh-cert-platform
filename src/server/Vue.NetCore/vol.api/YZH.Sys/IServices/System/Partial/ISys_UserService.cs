using YZH.Core.BaseProvider;
using YZH.Core.Utilities;
using YZH.Entity.DomainModels;
using System.Threading.Tasks;

namespace YZH.Sys.IServices
{
    public partial interface ISys_UserService
    {

        Task<WebResponseContent> Login(LoginInfo loginInfo, bool verificationCode = true);
        Task<WebResponseContent> ReplaceToken();
        Task<WebResponseContent> ModifyPwd(string oldPwd, string newPwd);
        Task<WebResponseContent> GetCurrentUserInfo();
    }
}

