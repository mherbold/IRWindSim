using System;

namespace IRWindSim
{
	internal class Program
	{
		public const string AppName = "IRWindSim";

		public static readonly string documentsFolder = Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ) + $"\\{AppName}\\";
	}
}
