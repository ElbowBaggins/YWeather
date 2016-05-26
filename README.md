# YWeather
## Yahoo! Weather API class for C&#35;
### [DEPRECATED] due to Yahoo! closing their weather API. It still sort of works, sometimes. Use at your own risk.

The YWeather package includes the class "WeatherData". It is intended to, essentially, be a magical "black box" that novice developers can use to access Yahoo's weather API.

It exposes one public method: `public async Task<WeatherData> GetWeatherAsync(string location)`

It asynchonously queries Yahoo Weather for information on current conditions at the given location.

`string location` can be virtually anything. Yahoo's API is fairly robust and almost always returns *something* for whatever you type in. This class is designed to only throw exceptions on true failure conditions. That is to say, you'll only get exceptions if the API request somehow becomes malformed or Yahoo responds with broken XML. In any case, the only exception that should be emitted from this class is `WeatherDataException`. I've tried to give an at least somewhat helpful message for all exception-throwing cases.

Upon a successfully completed request, the following properties are populated and should be, largely, self explanatory.
A brief explanation accompanies this readme anyway, because I like you, probably.

```
public string Location              // "Location, Region, Country" || "Location, Country" || "Unknown Location"
public string Conditions            // A string describing current conditions, i.e. "Partly Cloudy"
public string Temperature           // The current temperature || "?"
public string WindSpeed             // The current wind speed || "Still" || "?"
public string WindDirection         // The current wind direction or the empty string.
public string WindChill             // The current temperature including wind chill.
public string Pressure              // "29.68 in and falling/rising/stable" || "Unknown Pressure"
public string Humidity              // "66%" || "?"
public string Visibility            // "5 mi" || "?"
public string Sunrise               // "7:08 am" || "Unknown Time"
public string Sunset                // "9:46 pm" || "Unknown Time"
public string TimeUpdated           // "Thu, 16 Apr 2015 3:52 pm CDT" || "Unknown Time"
public bool IsComplete              // Basically, if any of the above are "?" or "Unknown" anything, this is false.
```

Presently, the units used will be imperial for locations in the United States, and metric elsewhere. I plan to make this selectable in the future. I also intend to provide forecast access as well.
