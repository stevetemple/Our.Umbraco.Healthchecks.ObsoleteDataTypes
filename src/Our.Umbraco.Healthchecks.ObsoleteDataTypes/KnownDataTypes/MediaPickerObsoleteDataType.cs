using System;

namespace Our.Umbraco.HealthChecks.ObsoleteDataTypes.KnownDataTypes
{
	public class MediaPickerObsoleteDataType : IObsoleteDataType
	{
		public string Alias => "Umbraco.MediaPicker";
		public bool CanConvert => false;

		public void Convert(string name) => throw new NotImplementedException();

	}
}
