namespace MijnThuis.Api.Endpoints;

public static class EndpointExtensions
{
    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        app.MapGroup("/weatherforecast").MapWeatherForecastEndpoints();
        return app;
    }
}
