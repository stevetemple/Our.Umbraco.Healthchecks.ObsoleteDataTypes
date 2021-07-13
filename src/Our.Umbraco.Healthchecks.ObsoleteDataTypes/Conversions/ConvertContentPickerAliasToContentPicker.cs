using System;
using Umbraco.Core;
using Umbraco.Core.Services;

namespace Our.Umbraco.HealthChecks.ObsoleteDataTypes.Conversions
{
	public class ConvertContentPickerAliasToContentPicker
	{
		private const string OldDataTypeAlias = "Umbraco.ContentPickerAlias";
		private const string NewDataTypeAlias = "Umbraco.ContentPicker2";

		private readonly GenericConversion _conversion;
		private readonly IContentService _contentService;

		public ConvertContentPickerAliasToContentPicker(ServiceContext services)
		{
			_conversion = new GenericConversion(services, OldDataTypeAlias, NewDataTypeAlias, ConvertValue);
			_contentService = services.ContentService;
		}

		/// <summary>
		/// Convert ContentPickerAlias data types with the given name
		///
		/// Converts the content from ids to UDIs
		/// Swaps the data type alias itself over
		/// </summary>
		/// <param name="name"></param>
		public void Convert(string name)
		{
			_conversion.Convert(name);
		}

		private object ConvertValue(object value)
		{
			if (value == null)
			{
				return null;

			}

			var textId = System.Convert.ToString(value);
			if (!String.IsNullOrEmpty(textId))
			{
				var content = _contentService.GetById(Int32.Parse(value.ToString()));
				return content?.GetUdi().ToString();
			}

			return "";
		}
	}
}
