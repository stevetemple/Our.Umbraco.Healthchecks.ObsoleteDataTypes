namespace Our.Umbraco.HealthChecks.ObsoleteDataTypes.KnownDataTypes
{
	public interface IObsoleteDataType
	{
		string Alias {get;}

		bool CanConvert {get;}
		void Convert(string name);
	}
}
