using Our.Umbraco.HealthChecks.ObsoleteDataTypes.Conversions;
using Umbraco.Core;

namespace Our.Umbraco.HealthChecks.ObsoleteDataTypes.KnownDataTypes
{
	public class ContentPickerAliasObsoleteDataType : IObsoleteDataType
	{
		public string Alias => "Umbraco.ContentPickerAlias";
		public bool CanConvert => true;
		public void Convert(string name)
		{
			new ConvertContentPickerAliasToContentPicker(ApplicationContext.Current.Services).Convert(name);
		}
	}
}
