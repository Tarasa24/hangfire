using System.Web;
using Hangfire.Annotations;
using Hangfire.Dashboard;

namespace hangfire
{
    public class HangFireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            return true;
        }
    }
}