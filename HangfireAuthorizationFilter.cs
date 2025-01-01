using Hangfire.Dashboard;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // Geliştirme ortamında herkese izin ver
        return true;
        
        // Prodüksiyonda yetkilendirme eklenebilir
        // var httpContext = context.GetHttpContext();
        // return httpContext.User.Identity?.IsAuthenticated ?? false;
    }
} 