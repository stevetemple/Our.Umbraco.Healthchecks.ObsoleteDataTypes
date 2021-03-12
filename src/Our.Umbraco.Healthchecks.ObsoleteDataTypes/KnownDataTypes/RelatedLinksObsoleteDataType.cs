using System;

namespace Our.Umbraco.HealthChecks.ObsoleteDataTypes.KnownDataTypes
{
	public class RelatedLinksObsoleteDataType : IObsoleteDataType
	{
		public string Alias => "Umbraco.RelatedLinks";

		public bool CanConvert => false;

		public void Convert(string name)
		{
			throw new NotImplementedException();
		}
	}
}
