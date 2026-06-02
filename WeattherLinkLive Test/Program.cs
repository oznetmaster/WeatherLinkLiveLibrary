using log4net.Config;

using System;
using System.IO;
using System.Threading.Tasks;

using WeatherLinkLive;

_ = BasicConfigurator.Configure ();

var weatherLinkLiveIp = Environment.GetEnvironmentVariable ("WEATHERLINK_LIVE_IP")
	?? ReadLocalWeatherLinkIp ();

var wll = new WeatherLinkLiveAPI.WeatherLinkLive (weatherLinkLiveIp, 10, 30, true);
await wll.InitializeAsync ();

do
	{
	await wll.RefreshAsync ();
	Console.WriteLine ($"Current Temperature = {wll.Temperature:F1}°C");
	Console.WriteLine ($"Current Humidity = {wll.Humidity:F1}%");
	Console.WriteLine ($"Current Wind Chill = {wll.WindChill:F1}°C");
	Console.WriteLine ($"Current Wind Speed = {wll.WindSpeedLast:F1} mph");
	Console.WriteLine ($"Current Wind Direction = {wll.WindDirectionLast:F2}°");
	Console.WriteLine ($"Current Rain Rate = {wll.RainRate:F1}\"/hr");
	Console.WriteLine ($"Rainfall last 24 hours = {wll.RainfallLast24Hours:F1}\"");
	Console.WriteLine ($"Barometer at sea level = {wll.BarometerAtSeaLevel:F2}\" Hg - {wll.BarometerTrend}");
	}
while (Console.ReadKey ().Key != ConsoleKey.Escape);

static string ReadLocalWeatherLinkIp ()
	{
	var currentDirectory = new DirectoryInfo (AppContext.BaseDirectory);
	while (currentDirectory != null)
		{
		var solutionLocalIpFile = Path.Combine (currentDirectory.FullName, ".local", "weatherlink-live-ip.txt");
		if (File.Exists (solutionLocalIpFile))
			{
			var ip = File.ReadAllText (solutionLocalIpFile).Trim ();
			if (!string.IsNullOrWhiteSpace (ip))
				{
				return ip;
				}
			}

		currentDirectory = currentDirectory.Parent;
		}

	throw new InvalidOperationException ("Set WEATHERLINK_LIVE_IP or create .local/weatherlink-live-ip.txt with the device IP address before running the test harness.");
	}

