using System;
using System.Net;
using System.Threading.Tasks;
using System.Xml;
// ReSharper disable ConvertIfStatementToSwitchStatement
// Seriously, the switch it wants for wind direction determination is so silly.

namespace Hemogoblin.YWeather {

    /// <summary>
    /// Represents a Yahoo! Weather API request.
    /// </summary>
    public class WeatherData {
        public string Location { get; private set; }
        public string Conditions { get; private set; }
        public string Temperature { get; private set; }
        public string WindSpeed { get; private set; }
        public string WindDirection { get; private set; }
        public string WindChill { get; private set; }
        public string Pressure { get; private set; }
        public string Humidity { get; private set; }
        public string Visibility { get; private set; }
        public string Sunrise { get; private set; }
        public string Sunset { get; private set; }
        public string TimeUpdated { get; private set; }
        public bool IsComplete { get; private set; }

        private readonly XmlDocument yahooWeatherXML;
        private readonly XmlNamespaceManager namespaceManager;

        /// <summary>
        /// Asynchronously queries Yahoo Weather's API for data relevant to the given location.
        /// </summary>
        /// <param name="location">Location to get weather data from</param>
        /// <returns>A WeatherData object that has all the relevant data.</returns>
        public static async Task<WeatherData> GetWeatherAsync(string location) {

            // Determine if the requested location is in the US.
            var isAmerica = await IsLocationUsAsync(location);
            // Prepare a request
            // If we're in the US, GetRequestURL will ask for Fahrenheit temps and imperial units, otherwise it will ask for metric.
            var request = WebRequest.CreateHttp(isAmerica ? GetRequestURL(location) : GetIntlRequestURL(location));
            // Throw an exception if the request is, for whatever reason, not prepared.
            if(null == request) {
                throw new WeatherDataException("It seems that I can't prepare a Yahoo! Weather request for the location you gave me.\r\n" +
                                                "It probably has a special character that HTTP doesn't like. Try again with a substitute.");
            }

            // Begin asynchronously getting a response
            var responseTask = request.GetResponseAsync();

            // Get an XML document ready
            var responseXML = new XmlDocument();

            // Wait for the ResponseStream to arrive
            var responseStream = (await responseTask).GetResponseStream();

            // Throw an exception if the stream is, for whatever reason, null
            if(null == responseStream) {
                throw new WeatherDataException("Yahoo! Weather is either not responding, or ignoring you because you've made too many requests recently.\r\n" +
                                                "In either case, it should work again in a little while.");
            }

            // Attempt to load the XML document with data from the stream
            // If this doesn't work, catch the exception and throw one of our own.
            try {
                responseXML.Load(responseStream);
            } catch(XmlException ex) {
                throw new WeatherDataException("Yahoo! Weather responded with unexpected data.", ex);
            }

            // We've got XML data, let WeatherData try to make sense of that.
            return new WeatherData(responseXML);
        }


        /// <summary>
        /// Returns a new WeatherData
        /// </summary>
        /// <param name="yahooWeatherXML">The XML response from Yahoo to build this object from</param>
        private WeatherData(XmlDocument yahooWeatherXML) {
            IsComplete = true;
            this.yahooWeatherXML = yahooWeatherXML;
            namespaceManager = new XmlNamespaceManager(yahooWeatherXML.NameTable);
            namespaceManager.AddNamespace("WeatherData", "http://xml.weather.yahoo.com/ns/rss/1.0");
            try {
                // Set location
                Location = GetPrettyLocationName();

                // Get current conditions
                Conditions = GetWeatherDataAttributeFromItem("condition", "text", "Unknown Conditions");
                // Get current temperature
                Temperature = GetWeatherDataAttributeFromItem("condition", "temp", "Unknown") + "° " +
                              GetWeatherDataAttributeFromChannel("units", "temperature", "Unknown Units");
                // Get current windspeed
                WindSpeed = GetWindSpeed();

                // Get current wind direction
                WindDirection = GetWindDirection();

                // Get current wind chill factor
                WindChill = GetWeatherDataAttributeFromChannel("wind", "chill", "Unknown") + "° " + GetWeatherDataAttributeFromChannel("units", "temperature", "Unknown Units");

                // Get current atmospheric pressure
                Pressure = GetWeatherDataAttributeFromChannel("atmosphere", "pressure", "Unknown Pressure") + " " +
                           GetWeatherDataAttributeFromChannel("units", "pressure", "Unknown Units") + ". and " +
                           GetPressureStability();
                // Get current humidity
                Humidity = GetWeatherDataAttributeFromChannel("atmosphere", "humidity", "?") + "%";
                // Get visibility
                Visibility = GetWeatherDataAttributeFromChannel("atmosphere", "visibility", "?") + " " +
                             GetWeatherDataAttributeFromChannel("units", "distance", "Unknown Units");

                // Get sunrise
                Sunrise = GetWeatherDataAttributeFromChannel("astronomy", "sunrise", "Unknown Time");

                // Get sunset
                Sunset = GetWeatherDataAttributeFromChannel("astronomy", "sunset", "Unknown Time");

                // Get time last updated
                TimeUpdated = GetWeatherDataAttributeFromItem("condition", "date", "Unknown Time");

            } catch(XmlException ex) {
                throw new WeatherDataException("Yahoo! Weather responded with unexpected or very incomplete data. " +
                                            "If you try again and it still doesn't work, double check the location you provided.", ex);
            }
        }

        /// <summary>
        /// Returns a "pretty" location name. The point of this is to avoid situations where one of the fields 
        /// is the empty string and you have a seemingly random comma hanging out in the output. Yuck!
        /// </summary>
        /// <returns>A string containing a "pretty" location name.</returns>
        private string GetPrettyLocationName() {
            // Get the values Yahoo! gave us for the name of the city, region, and location.
            var cityName = GetWeatherDataAttributeFromChannel("location", "city", "Unknown City");
            var regionName = GetWeatherDataAttributeFromChannel("location", "region", "Unknown Region");
            var countryName = GetWeatherDataAttributeFromChannel("location", "country", "Unknown Country");
            var locationName = cityName;

            // If there's anything to add to and there's actually a region name to add
            // then add a comma and the region name. Otherwise, just add the region name
            if(locationName.Length > 0 && regionName.Length > 0) {
                locationName += ", " + regionName;
            } else {
                locationName += regionName;
            }

            // If there's anything to add to and there's actually a country name to add
            // then add a comma and the country name. Otherwise, just set the country name.
            // Deja vu, man.
            if(locationName.Length > 0 && countryName.Length > 0) {
                locationName += ", " + countryName;
            } else {
                locationName += countryName;
            }

            return locationName;
        }

        /// <summary>
        /// Converts the number that Yahoo! Weather gives us into a string stating the current air pressure stability
        /// </summary>
        /// <returns>string stating the current air pressure stability</returns>
        private string GetPressureStability() {
            var pressureState = GetWeatherDataAttributeFromChannel("atmosphere", "rising", "Unknown Stability");

            if(pressureState.Equals("0")) {
                return "stable";
            }

            if(pressureState.Equals("1")) {
                return "rising";
            }

            if(pressureState.Equals("2")) {
                return "falling";
            }

            return pressureState;
        }

        /// <summary>
        /// Returns a string of the wind speed. Returns "Still" instead of 0 mph or 0 kph.
        /// </summary>
        /// <returns>a string representing the wind speed</returns>
        private string GetWindSpeed() {
            var speed = GetWeatherDataAttributeFromChannel("wind", "speed", "Unknown Speed");
            var units = GetWeatherDataAttributeFromChannel("units", "speed", "Unknown Units");
            if(speed.Equals("0")) {
                return "Still";
            }
            return speed + " " + units;
        }

        /// <summary>
        /// Determines the wind direction from the given measurement in°.
        /// </summary>
        /// <returns>A string describing the wind direction</returns>
        private string GetWindDirection() {
            // Try to get an int of the wind direction
            try {
                var windDirection = Int32.Parse(GetWeatherDataAttributeFromChannel("wind", "direction", "-999"));
                // Get the actual wind direction. Oh boy.
                if(-999 == windDirection) {
                    return "";
                } if(360 == windDirection) {
                    return "Due North";
                } if(0 < windDirection && 45 > windDirection) {
                    return 45 - windDirection + "° North of Northeast";
                } if(45 == windDirection) {
                    return "Due Northeast";
                } if(45 < windDirection && 90 > windDirection) {
                    return 90 - windDirection + "° East of Northeast";
                } if(90 == windDirection) {
                    return "Due East";
                } if(90 < windDirection && 135 > windDirection) {
                    return 135 - windDirection + "° East of Southeast";
                } if(135 == windDirection) {
                    return "Due Southeast";
                } if(135 < windDirection && 180 > windDirection) {
                    return 180 - windDirection + "° South of Southeast";
                } if(180 == windDirection) {
                    return "Due South";
                } if(180 < windDirection && 225 > windDirection) {
                    return 225 - windDirection + "° South of Southwest";
                } if(225 == windDirection) {
                    return "Due Southwest";
                } if(225 < windDirection && 270 > windDirection) {
                    return 270 - windDirection + "° West of Southwest";
                } if(270 == windDirection) {
                    return "Due West";
                } if(270 < windDirection && 315 > windDirection) {
                    return 315 - windDirection + "° West of Northwest";
                } if(315 == windDirection) {
                    return "Due Northwest";
                } if(315 < windDirection && 360 > windDirection) {
                    return 360 - windDirection + "° North of Northwest";
                }
                return "";
            } catch(OverflowException) {
                return "";
            } catch(FormatException) {
                return "";
            }
        }

        /// <summary>
        /// Returns the string representation of a partiuclar attribute of a yweather:tagName tag or the given fallback if it doesn't exist.
        /// </summary>
        /// <param name="tagName">Name of the tag to get an attribute from</param>
        /// <param name="attribute">Name of the attribute to get</param>
        /// <param name="fallback">Fallback string if no such attribute or tag exists.</param>
        /// <returns>The attribute requested or the fallback given</returns>
        private string GetWeatherDataAttributeFromChannel(string tagName, string attribute, string fallback) {
            var requestNode = yahooWeatherXML.SelectSingleNode("/query/results/channel/WeatherData:" + tagName + "/@" + attribute, namespaceManager);
            if(null != requestNode) {
                return requestNode.Value;
            }
            IsComplete = false;
            return fallback;
        }

        /// <summary>
        /// Returns the string representation of a partiuclar attribute of a yweahter:tagName tag or the given fallback if it doesn't exist.
        /// </summary>
        /// <param name="tagName">Name of the tag to get an attribute from</param>
        /// <param name="attribute">Name of the attribute to get</param>
        /// <param name="fallback">Fallback string if no such attribute or tag exists.</param>
        /// <returns>The attribute requested or the fallback given</returns>
        private string GetWeatherDataAttributeFromItem(string tagName, string attribute, string fallback) {
            var requestNode = yahooWeatherXML.SelectSingleNode("/query/results/channel/item/WeatherData:" + tagName + "/@" + attribute, namespaceManager);
            if(null != requestNode) {
                return requestNode.Value;
            }
            IsComplete = false;
            return fallback;
        }

        /// <summary>
        /// Returns a correctly formatted Yahoo weather API request URL
        /// </summary>
        /// <param name="location">location to get weather data for</param>
        /// <returns>Yahoo Weather API request URL</returns>
        private static string GetRequestURL(string location) {
            return "https://query.yahooapis.com/v1/public/yql?q=select * from weather.forecast where woeid in (select woeid from geo.places(1) where text=\"" + location + "\")";
        }

        /// <summary>
        /// Returns a correctly formatted International Yahoo weather API request URL
        /// </summary>
        /// <param name="location">location to get weather data for</param>
        /// <returns>Yahoo Weather API request URL</returns>
        private static string GetIntlRequestURL(string location) {
            return "https://query.yahooapis.com/v1/public/yql?q=select * from weather.forecast where woeid in (select woeid from geo.places(1) where text=\"" + location + "\") AND u=\"c\"";
        }


        /// <summary>
        /// Determines if the given location is in the US.
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        private static async Task<bool> IsLocationUsAsync(string location) {
            // Prepare a request
            var request = WebRequest.CreateHttp("https://query.yahooapis.com/v1/public/yql?q=select location from weather.forecast where woeid in (select woeid from geo.places(1) where text=\"" + location + "\")");
            // Throw an exception if the request is, for whatever reason, not prepared.
            if(null == request) {
                throw new WeatherDataException("It seems that I can't prepare a Yahoo! Weather request for the location you gave me.\r\n" +
                                                "It probably has a special character that HTTP doesn't like. Try again with a substitute.");
            }
            // Begin asynchronously getting a response
            var responseTask = request.GetResponseAsync();

            // Get an XML document ready
            var responseXML = new XmlDocument();

            // Wait for the ResponseStream to arrive
            var responseStream = (await responseTask).GetResponseStream();
            // Throw an exception if the stream is, for whatever reason, null
            if(null == responseStream) {
                throw new WeatherDataException("Yahoo! Weather is either not responding, or ignoring you because you've made too many requests recently.\r\n" +
                                                "In either case, it should work again in a little while.");
            }

            // Attempt to load the XML document with data from the stream
            // If this doesn't work, catch the exception and throw one of our own.
            try {
                responseXML.Load(responseStream);
                var namespaceManager = new XmlNamespaceManager(responseXML.NameTable);
                namespaceManager.AddNamespace("WeatherData", "http://xml.weather.yahoo.com/ns/rss/1.0");
                var requestNode = responseXML.SelectSingleNode("/query/results/channel/WeatherData:location/@country", namespaceManager);
                if(null != requestNode && requestNode.Value == "United States") {
                    return true;
                }
            } catch(XmlException ex) {
                throw new WeatherDataException("Yahoo! Weather responded with unexpected data.", ex);
            }
            return false;
        }
    }
}
