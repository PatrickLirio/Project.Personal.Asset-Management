

using Itam.AssetService.Application.Identity;
using Itam.AssetService.Application.Repositories;
using Itam.AssetService.Infrastructure.Data;
using Itam.AssetService.Infrastructure.Identity;
using Itam.AssetService.Infrastructure.Repositories;
using Itam.Common.Configuration.Options;
using MassTransit.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Itam.AssetService.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AssetDbContext>((provider, options)=>
            {
                var db = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
                options.UseSqlServer(db.BizDb, sql => sql.CommandTimeout(db.CommandTimeoutSeconds));
            });
            services.AddIdentityCore<AssetUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddRoles<AssetRole>()
            .AddEntityFrameworkStores<AssetDbContext>()
            .AddSignInManager();

            services.AddScoped<IAssetRepository, AssetRepository>();
            services.AddScoped<ICustodianRepository, CustodianRepository>();
            services.AddScoped<IUserAccountLookup, IdentityUserAccountLookup>();
            services.AddScoped<IAssetRequestRepository, AssetRequestRepository>();
            services.AddScoped<IIssueRepository, IssueRepository>();
            services.AddScoped<IAuthenticationService, IdentityAuthenticationService>();
            services.AddScoped<IUserAdminService, IdentityUserAdminService>();
            services.AddScoped<ITokenService, JwtTokenService>();
            services.AddScoped<IAssetEventPublisher, MassTransitAssetEventPublisher>();
            return services;

        }
    }
}