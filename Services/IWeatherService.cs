using Isdocovac.Models;

namespace Isdocovac.Services;

public interface IWeatherService
{
    Task<WeatherForecast[]> GetForecastAsync();
}
