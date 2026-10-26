using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using MijnThuis.Integrations.Airconditioning;
using MijnThuis.Integrations.Airconditioning.Gree;
using MijnThuis.Integrations.Car;
using MijnThuis.Integrations.Charger;
using MijnThuis.Integrations.Common.Configuration;
using MijnThuis.Integrations.Common.Http;
using MijnThuis.Integrations.Common.Network;
using MijnThuis.Integrations.Common.Resilience;
using MijnThuis.Integrations.Forecast;
using MijnThuis.Integrations.Heating;
using MijnThuis.Integrations.Heating.Remeha;
using MijnThuis.Integrations.Lamps;
using MijnThuis.Integrations.Power;
using MijnThuis.Integrations.Samsung;
using MijnThuis.Integrations.Sauna;
using MijnThuis.Integrations.SmartLock;
using MijnThuis.Integrations.Solar;
using MijnThuis.Integrations.Solar.SolarEdge;
using Polly;

namespace MijnThuis.Integrations;

public static class DependencyInjection
{
    /// <summary>
    /// Registers every MijnThuis integration. Settings are read from the root configuration using the
    /// (environment variable style) keys declared on the option classes, and are validated on first use.
    /// </summary>
    public static IServiceCollection AddIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddIntegrationResiliencePipelines();
        services.TryAddSingleton<IWakeOnLanSender, WakeOnLanSender>();

        services
            .AddAirconditioning(configuration)
            .AddCar(configuration)
            .AddCharger(configuration)
            .AddForecast(configuration)
            .AddHeating(configuration)
            .AddLamps(configuration)
            .AddPower(configuration)
            .AddSamsung(configuration)
            .AddSauna(configuration)
            .AddSmartLock(configuration)
            .AddSolar(configuration);

        return services;
    }

    private static IServiceCollection AddAirconditioning(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIntegrationOptions<AirconditioningOptions>(configuration);
        services.TryAddSingleton<GreeClient>();
        services.TryAddSingleton<GreeDeviceRegistry>();
        services.AddTransient<IAirconditioningService, AirconditioningService>();

        return services;
    }

    private static IServiceCollection AddCar(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIntegrationOptions<CarOptions>(configuration);
        services.AddIntegrationHttpClient<ICarService, CarService>(
            (sp, client) =>
            {
                var options = Options<CarOptions>(sp);
                client.BaseAddress = options.BaseAddress.ToBaseAddress();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.AuthToken);
            },
            resilience =>
            {
                // Tessie commands are sent as GET requests, so they need to be excluded from retries explicitly
                // (e.g. to avoid honking twice or toggling a setting back).
                var shouldHandle = resilience.Retry.ShouldHandle;
                resilience.Retry.ShouldHandle = async args =>
                {
                    var request = args.Outcome.Result?.RequestMessage ?? args.Context.GetRequestMessage();
                    if (request?.RequestUri?.AbsolutePath.Contains("/command/", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return false;
                    }

                    return await shouldHandle(args);
                };
            });

        return services;
    }

    private static IServiceCollection AddCharger(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIntegrationOptions<ChargerOptions>(configuration);
        services.AddIntegrationHttpClient<IChargerService, ChargerService>(
            (sp, client) => client.BaseAddress = Options<ChargerOptions>(sp).BaseAddress.ToBaseAddress());

        return services;
    }

    private static IServiceCollection AddForecast(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIntegrationOptions<ForecastOptions>(configuration);
        services.AddIntegrationHttpClient<IForecastService, ForecastService>(
            (sp, client) => client.BaseAddress = Options<ForecastOptions>(sp).BaseAddress.ToBaseAddress());

        return services;
    }

    private static IServiceCollection AddHeating(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIntegrationOptions<HeatingOptions>(configuration);
        services.TryAddSingleton<RemehaTokenProvider>();
        services.TryAddTransient<RemehaAuthenticationHandler>();
        services
            .AddIntegrationHttpClient<IHeatingService, HeatingService>((sp, client) =>
            {
                var options = Options<HeatingOptions>(sp);
                client.BaseAddress = options.BaseAddress.ToBaseAddress();
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", options.SubscriptionKey);
            })
            .AddHttpMessageHandler<RemehaAuthenticationHandler>();

        return services;
    }

    private static IServiceCollection AddLamps(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIntegrationOptions<LampsOptions>(configuration);
        services.AddIntegrationHttpClient<ILampsService, LampsService>((sp, client) =>
        {
            var options = Options<LampsOptions>(sp);
            client.BaseAddress = options.BaseAddress.ToBaseAddress();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.AuthToken);
        });

        return services;
    }

    private static IServiceCollection AddPower(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIntegrationOptions<PowerOptions>(configuration);
        services.AddIntegrationOptions<EnergyPricesOptions>(configuration);
        services.AddIntegrationOptions<PowerSwitchOptions>(configuration);
        services.AddIntegrationOptions<WakeOnLanOptions>(configuration);

        services.AddIntegrationHttpClient<IPowerService, PowerService>(
            (sp, client) => client.BaseAddress = Options<PowerOptions>(sp).BaseAddress.ToBaseAddress());

        // The ENTSO-E request URI is built from the configured (absolute) base address.
        services.AddIntegrationHttpClient<IEnergyPricesService, EnergyPricesService>((_, _) => { });

        // Shelly plugs are addressed with absolute URIs; every plug gets its own pipeline.
        services.AddIntegrationHttpClient<IShellyService, ShellyService>((_, _) => { }, pipelinePerAuthority: true);

        services.AddTransient<IWakeOnLanService, WakeOnLanService>();

        return services;
    }

    private static IServiceCollection AddSamsung(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIntegrationOptions<SamsungOptions>(configuration);
        services.TryAddSingleton<ISamsungTokenStore, SamsungTokenStore>();

        // The HTTP client is only used to check whether the TV is on: a TV that is off never answers, so fail fast.
        services.AddIntegrationHttpClient<ISamsungService, SamsungService>(
            (_, _) => { },
            resilience =>
            {
                resilience.Retry.ShouldHandle = _ => ValueTask.FromResult(false);
                resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
                resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(3);
            });

        return services;
    }

    private static IServiceCollection AddSauna(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIntegrationOptions<SaunaOptions>(configuration);
        services.AddIntegrationHttpClient<ISaunaService, SaunaService>((sp, client) =>
        {
            var options = Options<SaunaOptions>(sp);
            client.BaseAddress = options.BaseAddress.ToBaseAddress();
            client.DefaultRequestHeaders.Add("ClientId", options.AuthToken);
        });

        return services;
    }

    private static IServiceCollection AddSmartLock(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIntegrationOptions<SmartLockOptions>(configuration);
        services.AddIntegrationHttpClient<ISmartLockService, SmartLockService>((sp, client) =>
        {
            var options = Options<SmartLockOptions>(sp);
            client.BaseAddress = options.BaseAddress.ToBaseAddress();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.AuthToken);
        });

        return services;
    }

    private static IServiceCollection AddSolar(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIntegrationOptions<SolarOptions>(configuration);
        services.AddIntegrationOptions<ModbusOptions>(configuration);

        services.TryAddSingleton<ISolarEdgeTokenStore, SolarEdgeTokenStore>();
        services.TryAddSingleton<SolarEdgeTokenProvider>();
        services.TryAddSingleton<ISolarAuthorizationService>(sp => sp.GetRequiredService<SolarEdgeTokenProvider>());
        services.TryAddTransient<SolarEdgeAuthenticationHandler>();

        services.AddIntegrationHttpClient(SolarEdgeTokenProvider.OAuthClientName,
            (sp, client) => client.BaseAddress = Options<SolarOptions>(sp).BaseAddress.ToBaseAddress());
        services
            .AddIntegrationHttpClient<ISolarService, SolarService>(
                (sp, client) => client.BaseAddress = Options<SolarOptions>(sp).BaseAddress.ToBaseAddress(),
                resilience =>
                {
                    var shouldHandle = resilience.Retry.ShouldHandle;
                    resilience.Retry.ShouldHandle = args =>
                    {
                        var response = args.Outcome.Result;
                        if (response?.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                            && (Header("x-ratelimit-remaining-month") == 0 || Header("x-ratelimit-remaining-minute") > 0))
                        {
                            // Monthly credits cannot be recovered by retrying; minute throttling still uses Retry-After.
                            return ValueTask.FromResult(false);
                        }

                        return shouldHandle(args);

                        int? Header(string name) => response is not null && response.Headers.TryGetValues(name, out var values)
                            && int.TryParse(values.FirstOrDefault(), out var remaining) ? remaining : null;
                    };
                })
            .AddHttpMessageHandler<SolarEdgeAuthenticationHandler>();

        services.AddIntegrationHttpClient<IModbusService, ModbusService>((sp, client) =>
        {
            var options = Options<ModbusOptions>(sp);
            client.BaseAddress = options.BaseAddress.ToBaseAddress();
            if (!string.IsNullOrEmpty(options.ClientSecret))
            {
                client.DefaultRequestHeaders.Add("X-Client-Secret", options.ClientSecret);
            }
        });

        return services;
    }

    private static T Options<T>(IServiceProvider serviceProvider) where T : class
        => serviceProvider.GetRequiredService<IOptions<T>>().Value;
}
