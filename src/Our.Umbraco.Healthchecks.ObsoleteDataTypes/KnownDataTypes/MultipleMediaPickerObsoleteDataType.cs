using Our.Umbraco.HealthChecks.ObsoleteDataTypes.Conversions;
using Umbraco.Core;

namespace Our.Umbraco.HealthChecks.ObsoleteDataTypes.KnownDataTypes
{
	public class MultipleMediaPickerObsoleteDataType : IObsoleteDataType
	{
		public string Alias => "Umbraco.MultipleMediaPicker";
		public bool CanConvert => true;

		public void Convert(string name)
		{
			new ConvertMultipleMediaPickerToMediaPicker2(ApplicationContext.Current.Services).Convert(name);
		}
	}
}
