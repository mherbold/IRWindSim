using System.Runtime.InteropServices;

namespace IRWindSim
{
	[StructLayout( LayoutKind.Sequential )]
	internal struct SRSPacket
	{
		[MarshalAs( UnmanagedType.ByValArray, SizeConst = 3 )]
		public char[] apiMode;
		public uint version;
		public float leftFanPower;
		public float rightFanPower;
		public float rpm;
		public float maxRpm;
		public int gear;
	}
}
