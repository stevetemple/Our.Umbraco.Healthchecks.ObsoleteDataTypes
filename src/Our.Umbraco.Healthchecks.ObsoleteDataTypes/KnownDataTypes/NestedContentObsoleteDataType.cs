using Umbraco.Core.Services;

namespace Our.Umbraco.HealthChecks.ObsoleteDataTypes.KnownDataTypes
{
	public class NestedContentObsoleteDataType : IObsoleteDataType
	{
		private readonly IDataTypeService _dataTypeService;
		
		
		public string Alias => "Our.Umbraco.NestedContent";

		public bool CanConvert => true;

		public NestedContentObsoleteDataType(ServiceContext services)
		{
			_dataTypeService = services.DataTypeService;
		}

		public void Convert(string name)
		{
			var nestedContentDataType = _dataTypeService.GetDataTypeDefinitionByName(name);
			var preValues = _dataTypeService.GetPreValuesCollectionByDataTypeId(nestedContentDataType.Id).FormatAsDictionary();

			nestedContentDataType.PropertyEditorAlias = "Umbraco.NestedContent";
			
			_dataTypeService.SaveDataTypeAndPreValues(nestedContentDataType, preValues);
			
		}
	}
}
