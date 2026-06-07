using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using MyOwnBank.Application.Abstractions;
using MyOwnBank.Application.Common;
using MyOwnBank.Infrastructure.Persistence;

namespace MyOwnBank.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IInviteCodeGenerator, InviteCodeGenerator>();
        services.AddDbContextFactory<MyOwnBankDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IBankRepository, SqliteBankRepository>();
        services.AddSingleton<IInvitationRepository, SqliteInvitationRepository>();
        services.AddSingleton<IUserNotificationRepository, SqliteUserNotificationRepository>();

        return services;
    }
}
