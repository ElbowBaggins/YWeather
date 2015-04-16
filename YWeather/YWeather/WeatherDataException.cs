using System;

namespace Hemogoblin.YWeather {
    /// <summary>
    /// Basic exception thrown when making a WeatherData request goes wrong.
    /// </summary>
    public class WeatherDataException : Exception {
        public WeatherDataException() { }
        public WeatherDataException(string message) : base(message) { }
        public WeatherDataException(string message, Exception inner) : base(message, inner) { }
    }
}
