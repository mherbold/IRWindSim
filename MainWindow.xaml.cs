
using HerboldRacing;
using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Serialization;

namespace IRWindSim
{
	public partial class MainWindow : Window
	{
		bool disableUpdates = false;

		int testBand = -1;

		UdpClient udpClient;
		SRSPacket srsPacket;
		int srsPacketSize;
		byte[] srsPacketBytes;
		IntPtr srsPacketPtr;

		Settings settings = new();

		DispatcherTimer dispatcherTimer = new();

		IRSDKSharper irsdk = new();

		bool isReplay = false;

		public MainWindow()
		{
			disableUpdates = true;

			InitializeComponent();

			udpClient = new UdpClient();

			srsPacket = new SRSPacket
			{
				apiMode = "lla".ToCharArray(),
				version = 101,
				leftFanPower = 0,
				rightFanPower = 0,
				rpm = 0,
				maxRpm = 8000,
				gear = 0
			};

			srsPacketSize = Marshal.SizeOf( srsPacket );

			srsPacketBytes = new byte[ srsPacketSize ];

			srsPacketPtr = Marshal.AllocHGlobal( srsPacketSize );

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

			disableUpdates = false;

			dispatcherTimer.Tick += OnTick;
			dispatcherTimer.Interval = new TimeSpan( 0, 0, 0, 0, 250 );

			dispatcherTimer.Start();

			irsdk.UpdateInterval = 15;

			irsdk.OnException += OnException;
			irsdk.OnStopped += OnStopped;
			irsdk.OnSessionInfo += OnSessionInfo;
			irsdk.OnTelemetryData += OnTelemetryData;

			irsdk.Start();
		}

		private void Update( object sender, RoutedEventArgs e )
		{
			if ( !disableUpdates )
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

		private void UpdateBand( Settings.Band band, TextBox speed, TextBox fanPower )
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
				var fanPower = settings.bands[ testBand ].fanPower;

				srsPacket.leftFanPower = fanPower;
				srsPacket.rightFanPower = fanPower;

				SendSRSPacket();
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

				srsPacket.leftFanPower = GetFanPower( (float) Math.Sqrt( lx * lx + z * z ) );
				srsPacket.rightFanPower = GetFanPower( (float) Math.Sqrt( rx * rx + z * z ) );

				SendSRSPacket();
			}
		}

		private float GetFanPower( float speed )
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

			return ( settings.bands[ band1 ].fanPower - settings.bands[ band0 ].fanPower ) * t + settings.bands[ band0 ].fanPower;
		}

		private void SendSRSPacket()
		{
			Marshal.StructureToPtr( srsPacket, srsPacketPtr, false );

			Marshal.Copy( srsPacketPtr, srsPacketBytes, 0, srsPacketSize );

			udpClient.Send( srsPacketBytes, srsPacketBytes.Length, "localhost", 33001 );
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
