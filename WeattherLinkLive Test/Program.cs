using log4net.Config;

using System;
using System.Threading.Tasks;

using WeatherLinkLive;

namespace WeatherLinkLive_Test
	{
	class Program
		{
		static async Task Main ()
			{
			_ = BasicConfigurator.Configure ();

			var wll = new WeatherLinkLiveAPI.WeatherLinkLive ("192.168.8.198", 10, 30, true);
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
			}
		}
	}
