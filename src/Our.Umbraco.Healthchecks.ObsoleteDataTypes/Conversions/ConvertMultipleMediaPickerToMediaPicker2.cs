using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Our.Umbraco.HealthChecks.ObsoleteDataTypes.Conversions
{
	public class ConvertMultipleMediaPickerToMediaPicker2
	{
		private const string OldDataTypeAlias = "Umbraco.MultipleMediaPicker";
		private const string NewDataTypeAlias = "Umbraco.MediaPicker2";

		private readonly GenericConversion _conversion;
		private readonly IMediaService _mediaService;

		private bool _isMuliple;

		public ConvertMultipleMediaPickerToMediaPicker2(ServiceContext services)
		{
			_conversion = new GenericConversion(services, OldDataTypeAlias, NewDataTypeAlias, ConvertValue, ConvertPreValues);
			_mediaService = services.MediaService;
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

			if (_isMuliple)
			{
				var medias = value.ToString().Split(',')
					.Select(id => _mediaService.GetById(Int32.Parse(id.Trim())))
					.Where(m => m != null)
					.Select(m => m.GetUdi().ToString());

				return JsonConvert.SerializeObject(medias);
			}
			else
			{
				var textId = System.Convert.ToString(value).Trim();
				if (!String.IsNullOrEmpty(textId))
				{
					var media = _mediaService.GetById(Int32.Parse(textId));
					return media == null ? "" : media.GetUdi().ToString();
				}
			}

			return "";
		}

		private IDictionary<string, PreValue> ConvertPreValues(IDictionary<string, PreValue> oldValues)
		{
			_isMuliple = oldValues.ContainsKey("multiPicker") && oldValues["multiPicker"].Value == "1";
			oldValues.Add("ignoreUserStartNodes", new PreValue("0"));
			return oldValues;
		}
	}
}
