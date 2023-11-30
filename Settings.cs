
using System;

namespace IRWindSim
{
	[Serializable]
	public class Settings
	{
		public Units units;
		public Band[] bands;
		public bool curve;

		public Settings()
		{
			units = Units.Imperial;
			bands = new Band[]
			{
				new Band { speed = 0, fanPower = 0 },
				new Band { speed = 25, fanPower = 0 },
				new Band { speed = 45, fanPower = 15 },
				new Band { speed = 90, fanPower = 20 },
				new Band { speed = 120, fanPower = 25 },
				new Band { speed = 150, fanPower = 30 },
				new Band { speed = 180, fanPower = 50 },
				new Band { speed = 210, fanPower = 100 }
			};
			curve = true;
		}

		[Serializable]
		public enum Units
		{
			Imperial = 0,
			Metric = 1
		}

		[Serializable]
		public class Band
		{
			public int speed;
			public int fanPower;
		}
	}
}
