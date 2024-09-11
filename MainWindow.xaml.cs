
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Serialization;

using IRSDKSharper;

namespace IRWindSim
{
	public partial class MainWindow : Window
	{
		const float MPS_TO_MPH = 2.23694f;
		const float MPS_TO_KPH = 3.6f;

		static readonly byte[] handshake = { (byte) 'w', (byte) 'i', (byte) 'n', (byte) 'd' };

		bool disableWindowUpdates = false;
		bool arduinoIsConnected = false;

		int testBand = -1;
		int leftSpinState = 0;
		int rightSpinState = 0;

		Settings settings = new();

		readonly DispatcherTimer dispatcherTimer = new();
		readonly IRacingSdk irsdk = new();
		readonly ArduinoConnection arduinoConnection = new( handshake );

		bool isReplay = false;

		public MainWindow()
		{
			disableWindowUpdates = true;

			InitializeComponent();

			LoadSettings();

			imperial.IsChecked = settings.units == Settings.Units.Imperial;
			metric.IsChecked = settings.units == Settings.Units.Metric;

			speed_0.Text = settings.bands[ 0 ].speed.ToString();
			speed_1.Text = settings.bands[ 1 ].speed.ToString();
			speed_2.Text = settings.bands[ 2 ].speed.ToString();
			speed_3.Text = settings.bands[ 3 ].speed.ToString();
			speed_4.Text = settings.bands[ 4 ].speed.ToString();
			speed_5.Text = settings.bands[ 5 ].speed.ToString();
			speed_6.Text = settings.bands[ 6 ].speed.ToString();
			speed_7.Text = settings.bands[ 7 ].speed.ToString();

			fanPower_0.Text = settings.bands[ 0 ].fanPower.ToString();
			fanPower_1.Text = settings.bands[ 1 ].fanPower.ToString();
			fanPower_2.Text = settings.bands[ 2 ].fanPower.ToString();
			fanPower_3.Text = settings.bands[ 3 ].fanPower.ToString();
			fanPower_4.Text = settings.bands[ 4 ].fanPower.ToString();
			fanPower_5.Text = settings.bands[ 5 ].fanPower.ToString();
			fanPower_6.Text = settings.bands[ 6 ].fanPower.ToString();
			fanPower_7.Text = settings.bands[ 7 ].fanPower.ToString();

			curve.IsChecked = settings.curve;

			disableWindowUpdates = false;

			dispatcherTimer.Tick += OnTick;
			dispatcherTimer.Interval = new TimeSpan( 0, 0, 0, 0, 250 );

			dispatcherTimer.Start();

			irsdk.UpdateInterval = 15;

			irsdk.OnException += OnException;
			irsdk.OnStopped += OnStopped;
			irsdk.OnSessionInfo += OnSessionInfo;
			irsdk.OnTelemetryData += OnTelemetryData;

			irsdk.Start();

			arduinoConnection.ArduinoConnected += OnArduinoConnected;
			arduinoConnection.ArduinoDisconnected += OnArduinoDisconnected;

			arduinoConnection.Start();
		}

		private void Window_Closing( object? sender, CancelEventArgs e )
		{
			arduinoConnection.Stop();
			irsdk.Stop();
			dispatcherTimer.Stop();
		}

		private void Update( object sender, RoutedEventArgs e )
		{
			if ( !disableWindowUpdates )
			{
				settings.units = ( imperial.IsChecked ?? false ) ? Settings.Units.Imperial : Settings.Units.Metric;

				UpdateBand( settings.bands[ 0 ], speed_0, fanPower_0 );
				UpdateBand( settings.bands[ 1 ], speed_1, fanPower_1 );
				UpdateBand( settings.bands[ 2 ], speed_2, fanPower_2 );
				UpdateBand( settings.bands[ 3 ], speed_3, fanPower_3 );
				UpdateBand( settings.bands[ 4 ], speed_4, fanPower_4 );
				UpdateBand( settings.bands[ 5 ], speed_5, fanPower_5 );
				UpdateBand( settings.bands[ 6 ], speed_6, fanPower_6 );
				UpdateBand( settings.bands[ 7 ], speed_7, fanPower_7 );

				settings.curve = curve.IsChecked ?? true;

				SaveSettings();
			}
		}

		private static void UpdateBand( Settings.Band band, TextBox speed, TextBox fanPower )
		{
			var success = int.TryParse( speed.Text, out var value );

			if ( success )
			{
				band.speed = value;
			}

			success = int.TryParse( fanPower.Text, out value );

			if ( success )
			{
				band.fanPower = value;
			}
		}

		private void Test( object sender, RoutedEventArgs e )
		{
			var radioButton = (RadioButton) sender;

			var oldTestBand = testBand;

			testBand = radioButton.Name switch
			{
				"test_0" => 0,
				"test_1" => 1,
				"test_2" => 2,
				"test_3" => 3,
				"test_4" => 4,
				"test_5" => 5,
				"test_6" => 6,
				"test_7" => 7,
				_ => 0
			};

			if ( testBand == oldTestBand )
			{
				testBand = -1;

				radioButton.IsChecked = false;
			}
		}

		private void OnTick( object? sender, EventArgs e )
		{
			if ( testBand != -1 )
			{
				var fanPower = Math.Min( 320, Math.Max( 0, settings.bands[ testBand ].fanPower ) );

				UpdateFanPowers( fanPower, fanPower );
			}
		}

		private void OnException( Exception exception )
		{
			irsdk.Stop();
		}

		private void OnStopped()
		{
			irsdk.Start();
		}

		private void OnSessionInfo()
		{
			isReplay = ( irsdk.Data.SessionInfo.WeekendInfo.SimMode != "full" );
		}

		private void OnTelemetryData()
		{
			if ( !isReplay && ( testBand == -1 ) )
			{
				var velocityY = irsdk.Data.GetFloat( "VelocityY", 0 ); // positive = turning right, negative = turning left
				var velocityX = irsdk.Data.GetFloat( "VelocityX", 0 ); // positive = forward, negative = backwards

				var z = Math.Max( 0, velocityX );
				var lx = Math.Max( 0, -velocityY );
				var rx = Math.Max( 0, velocityY );

				if ( !settings.curve )
				{
					z += lx;
					z += rx;

					lx = 0;
					rx = 0;
				}

				if ( settings.units == Settings.Units.Imperial )
				{
					z *= MPS_TO_MPH;
					lx *= MPS_TO_MPH;
					rx *= MPS_TO_MPH;
				}
				else
				{
					z *= MPS_TO_KPH;
					lx *= MPS_TO_KPH;
					rx *= MPS_TO_KPH;
				}

				var leftFanPower = GetFanPower( (float) Math.Sqrt( Math.Max( 0, ( lx * lx ) - ( rx * rx ) + ( z * z ) ) ) );
				var rightFanPower = GetFanPower( (float) Math.Sqrt( Math.Max( 0, ( rx * rx ) - ( lx * lx ) + ( z * z ) ) ) );

				Debug.WriteLine( $"z={z}, lx={lx}, rx={rx}, lfp={leftFanPower}, rfp={rightFanPower}" );

				UpdateFanPowers( leftFanPower, rightFanPower );
			}
		}

		private void OnArduinoConnected( object connection, ArduinoConnection.ConnectionEventArgs connectionInformation )
		{
			arduinoIsConnected = true;

			Debug.WriteLine( $"Arduino connected on port {connectionInformation.ArduinoPort?.PortName}!" );

			Dispatcher.Invoke( () =>
			{
				status.Content = "Connected 😊";
			} );
		}

		private void OnArduinoDisconnected( object connection, ArduinoConnection.ConnectionEventArgs connectionInformation )
		{
			arduinoIsConnected = false;

			Debug.WriteLine( "Arduino disconnected!" );

			Dispatcher.Invoke( () =>
			{
				status.Content = "NOT CONNECTED 😭";
			} );
		}

		private int GetFanPower( float speed )
		{
			if ( speed <= 0 )
			{
				return 0;
			}

			int band0 = 0;
			int band1 = 0;

			for ( int i = 0; i < settings.bands.Length; i++ )
			{
				band1 = i;

				if ( speed < settings.bands[ i ].speed )
				{
					break;
				}

				band0 = band1;
			}

			var ds = settings.bands[ band1 ].speed - settings.bands[ band0 ].speed;

			if ( ds <= 0 )
			{
				return settings.bands[ band1 ].fanPower;
			}

			var t = ( speed - settings.bands[ band0 ].speed ) / ds;

			return (int) Math.Min( 320, Math.Max( 0, ( settings.bands[ band1 ].fanPower - settings.bands[ band0 ].fanPower ) * t + settings.bands[ band0 ].fanPower ) );
		}

		private void UpdateFanPowers( int leftFanPower, int rightFanPower )
		{
			if ( arduinoIsConnected )
			{
				var arduinoPort = arduinoConnection.ArduinoPort;

				if ( arduinoPort != null )
				{
					try
					{
						if ( leftFanPower < 10 )
						{
							leftSpinState = 0;
						}
						else if ( leftSpinState < 2 )
						{
							leftSpinState++;

							leftFanPower = 160;
						}

						arduinoPort.Write( $"L{leftFanPower:000}" );

						if ( rightFanPower < 10 )
						{
							rightSpinState = 0;
						}
						else if ( rightSpinState < 2 )
						{
							rightSpinState++;

							rightFanPower = 160;
						}

						arduinoPort.Write( $"R{rightFanPower:000}" );
					}
					catch
					{
					}
				}
			}
		}

		private void LoadSettings()
		{
			var filePath = Program.documentsFolder + "settings.xml";

			if ( File.Exists( filePath ) )
			{
				var xmlSerializer = new XmlSerializer( typeof( Settings ) );

				var fileStream = new FileStream( filePath, FileMode.Open );

				settings = (Settings) ( xmlSerializer.Deserialize( fileStream ) ?? throw new Exception() );

				fileStream.Close();
			}
		}

		private void SaveSettings()
		{
			if ( !Directory.Exists( Program.documentsFolder ) )
			{
				Directory.CreateDirectory( Program.documentsFolder );
			}

			var filePath = Program.documentsFolder + "settings.xml";

			var xmlSerializer = new XmlSerializer( typeof( Settings ) );

			var streamWriter = new StreamWriter( filePath );

			xmlSerializer.Serialize( streamWriter, settings );

			streamWriter.Close();
		}
	}
}
