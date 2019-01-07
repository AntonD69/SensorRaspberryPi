using System;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Unosquare.RaspberryIO;
using Unosquare.RaspberryIO.Gpio;

namespace sensorpi
{
	class Program
	{
		private static string _programName;
		private static string _logfileRelativePath;
		private static string _sqliteDatabaseNameAndLocation;
		private static bool _measureHigh;
		private static bool _measureLow;
		private static double _mmPerTip;
		private static string _notificationUrl;

		static void Main(string[] args)
		{
			_programName = AppDomain.CurrentDomain.FriendlyName;

			Console.WriteLine($"Starting {_programName}.\n");
			WriteToLogFile($"Starting {_programName}.");

			GetConfigurationSettings();

			CreateDatabaseAndTables();

			SetupPin00AsTippingListener();

			Console.WriteLine("\nPress any key to exit ...");
			Console.ReadKey();

			Console.WriteLine($"\nEnding {_programName}.");
			WriteToLogFile($"Ending {_programName}.");
		}

		private static void SetupPin00AsTippingListener()
		{
			var pin = Pi.Gpio.Pin00;

			pin.PinMode = GpioPinDriveMode.Input;

			pin.InputPullMode = GpioPinResistorPullMode.PullUp;

			pin.RegisterInterruptCallback(
				EdgeDetection.RisingAndFallingEdges,
				Pin00Callback);

			Console.WriteLine("\nListening to tipping events.");
		}

		private static void GetConfigurationSettings()
		{
			var builder = new ConfigurationBuilder()
				.SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "configs"))
				.AddJsonFile("appsettings_sensordaemon.json", optional: false, reloadOnChange: true);

			IConfigurationRoot configuration = builder.Build();

			var section = configuration.GetSection("AppSettings");
			Console.WriteLine($"Path : {section.Path}");

			Console.WriteLine($"  LogFileLocation : '{section["LogFileLocation"]}'");
			_logfileRelativePath = section["LogFileLocation"];

			Console.WriteLine($"  SqliteDatabaseLocationAndName : '{section["SqliteDatabaseLocationAndName"]}'");
			_sqliteDatabaseNameAndLocation = section["SqliteDatabaseLocationAndName"];

			Console.WriteLine($"  mmPerTip : '{section["MmPerTip"]}'");
			_mmPerTip = Double.Parse(section["MmPerTip"]);

			Console.WriteLine($"  MeasureHigh : '{section["MeasureHigh"]}'");
			_measureHigh = Boolean.Parse(section["MeasureHigh"]);

			Console.WriteLine($"  MeasureLow : '{section["MeasureLow"]}'");
			_measureLow = Boolean.Parse(section["MeasureLow"]);

			Console.WriteLine($"  NotficationUrl : '{section["NotficationUrl"]}'");
			_notificationUrl = section["NotficationUrl"];
		}

		private static void CreateDatabaseAndTables()
		{
			if (File.Exists(_sqliteDatabaseNameAndLocation))
			{
				WriteToLogFile("SQLite database exists.");
			}
			else
			{
				WriteToLogFile("SQLite database is being created.");

				using (var connection = new SqliteConnection($"Filename={_sqliteDatabaseNameAndLocation};"))
				{
					connection.Open();

					new SqliteCommand("CREATE TABLE RainMeter (" +
						"Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
						"Epoch TEXT NOT NULL, " +
						"ToState INTEGER NOT NULL CHECK(toState IN(0, 1)), " +
						"MmMeasure NUMERIC DECIMAL(4,2) NOT NULL " +
						"); ", connection).ExecuteNonQuery();
				}

				WriteToLogFile("SQLite database was created.");
			}
		}

		static void Pin00Callback()
		{
			var state = GetPin00State();
			var epoch = DateTime.Now;

			WriteToLogFile(epoch, state.ToString());
			WriteToDatabase(epoch, state);
			//SendNotification(url, epoch, state);

			Console.WriteLine($"Pin :  00, Epoch : {epoch}, state : {state.ToString()}");
		}

		private static void WriteToDatabase(DateTime epoch, GpioPinValue state)
		{
			using (var connection = new SqliteConnection($"Filename={_sqliteDatabaseNameAndLocation};"))
			{
				connection.Open();

				var mmMeasurement = state == GpioPinValue.Low && _measureLow
					? _mmPerTip
					: state == GpioPinValue.High && _measureHigh
						? _mmPerTip
						: 0;

				var insertCommand = connection.CreateCommand();

				insertCommand.CommandText = "INSERT INTO RainMeter (Epoch, ToState, MmMeasure) VALUES (@epoch, @state, @MmMeasure)";
				insertCommand.Parameters.AddWithValue("@epoch", epoch.ToString("o"));
				insertCommand.Parameters.AddWithValue("@state", state == GpioPinValue.Low ? 0 : 1);
				insertCommand.Parameters.AddWithValue("@MmMeasure", mmMeasurement);

				insertCommand.ExecuteNonQuery();

				connection.Close();
			}
		}

		private static void WriteToLogFile(string message)
		{
			DateTime thisMoment = DateTime.Now;

			WriteToLogFile(thisMoment, message);
		}

		private static void WriteToLogFile(DateTime epoch, string message)
		{
			var fileNameWithPath = GetFileNameWithPath(epoch);

			using (StreamWriter sw = File.AppendText(fileNameWithPath))
			{
				sw.WriteLine($"{epoch.ToString("o", CultureInfo.CreateSpecificCulture("en-za"))}\t{message}");
			}
		}

		private static string GetFileNameWithPath(DateTime epoch)
		{
			// Folder is ..\logs and we have a new file per day.
			// File is created and closed to ensure we just need to append.

			var fileName = $"{epoch.Year}-{epoch.Month.ToString().PadLeft(2, '0')}-{epoch.Day.ToString().PadLeft(2, '0')}_{_programName}.log";
			 
			var fileNameWithPath = $"{_logfileRelativePath}/{fileName}";

			if (!File.Exists(fileNameWithPath))
			{
				File.Create(fileNameWithPath).Close();
			}

			return fileNameWithPath;
		}

		private static GpioPinValue GetPin00State()
		{
			return Pi.Gpio.Pin00.ReadValue();
		}
	}
}