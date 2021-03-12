using Our.Umbraco.HealthChecks.ObsoleteDataTypes.Conversions;
using Umbraco.Core;

namespace Our.Umbraco.HealthChecks.ObsoleteDataTypes.KnownDataTypes
{
	public class ArchetypeObsoleteDataType : IObsoleteDataType
	{
		public string Alias => "Imulus.Archetype";

		public bool CanConvert => true;

		public void Convert(string name)
		{
			new ConvertArchetypeToNestedContent(ApplicationContext.Current.Services).Convert(name);
		}
	}
}
