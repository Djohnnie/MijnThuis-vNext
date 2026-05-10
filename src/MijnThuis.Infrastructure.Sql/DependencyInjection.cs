using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MijnThuis.Infrastructure.Sql.Data;

namespace MijnThuis.Infrastructure.Sql;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureSql(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("mijnthuisdb")
            ?? throw new InvalidOperationException("Connection string 'mijnthuisdb' not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        return services;
    }
}
