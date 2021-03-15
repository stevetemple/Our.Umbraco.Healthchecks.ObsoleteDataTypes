using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Our.Umbraco.HealthChecks.ObsoleteDataTypes.Conversions
{
	public class ConvertContentPickerAliasToContentPicker
	{
		private readonly IDataTypeService _dataTypeService;
		private readonly IContentTypeService _contentTypeService;
		private readonly IContentService _contentService;

		public ConvertContentPickerAliasToContentPicker(ServiceContext services)
		{
			_dataTypeService = services.DataTypeService;
			_contentTypeService = services.ContentTypeService;
			_contentService = services.ContentService;
		}

		private const string OldDataTypeAlias = "Umbraco.ContentPickerAlias";
		private const string NewDataTypeAlias = "Umbraco.ContentPicker";

		/// <summary>
		/// Convert ContentPickerAlias data types with the given name
		///
		/// Converts the content from ids to UDIs
		/// Swaps the data type alias itself over
		/// </summary>
		/// <param name="name"></param>
		public void Convert(string name)
		{
			var oldDataTypeDefinition = _dataTypeService.GetDataTypeDefinitionByName(name);
			oldDataTypeDefinition.PropertyEditorAlias = NewDataTypeAlias;
			_dataTypeService.Save(oldDataTypeDefinition);

			var allContentTypes = _contentTypeService.GetAllContentTypes();
			var contentTypesToConvert = allContentTypes
				.Where(c =>
					c.PropertyTypes.Any(a => a.DataTypeDefinitionId == oldDataTypeDefinition.Id)
					|| c.CompositionPropertyTypes.Any(a => a.DataTypeDefinitionId == oldDataTypeDefinition.Id))
				.ToArray();

			ConvertContent(contentTypesToConvert, oldDataTypeDefinition);
			ConvertDataType(contentTypesToConvert, oldDataTypeDefinition);
		}

		/// <summary>
		/// Convert content to equivalent
		/// </summary>
		/// <param name="contentTypesToConvert"></param>
		/// <param name="oldDataTypeDefinition"></param>
		private void ConvertContent(IEnumerable<IContentType> contentTypesToConvert, IDataTypeDefinition oldDataTypeDefinition)
		{
			foreach (var contentType in contentTypesToConvert)
			{
				//ConvertInsideNestedContents(archetypeContentType.Alias, Alias(archetypeDataType.Name + "nc"));
				ConvertOldValuesToNewerFormat(contentType.Id, oldDataTypeDefinition.Id);
			}
		}

		/// <summary>
		/// Convert the content to the new type
		/// </summary>
		/// <param name="contentTypesToConvert"></param>
		/// <param name="oldDataTypeDefinition"></param>
		private void ConvertDataType(IEnumerable<IContentType> contentTypesToConvert, IDataTypeDefinition oldDataTypeDefinition)
		{
			foreach (var contentTypeToConvert in contentTypesToConvert)
			{
				foreach (var composition in contentTypeToConvert.ContentTypeComposition)
				{
					if (composition.PropertyTypes.Any(IsOldDataTypeWithId(oldDataTypeDefinition.Id)))
					{
						var compositionContentType = _contentTypeService.GetContentType(composition.Id);

						var propertyTypes = compositionContentType.PropertyTypes.Where(IsOldDataTypeWithId(oldDataTypeDefinition.Id))
							.ToArray();
						foreach (var propType in propertyTypes)
						{
							propType.PropertyEditorAlias = NewDataTypeAlias;
						}

						//_contentTypeService.Save(compositionContentType);
					}
				}

				if (contentTypeToConvert.PropertyTypes.Any(IsOldDataTypeWithId(oldDataTypeDefinition.Id)))
				{
					var propertyTypes = contentTypeToConvert.PropertyTypes.Where(IsOldDataTypeWithId(oldDataTypeDefinition.Id)).ToArray();

					foreach (var propType in propertyTypes)
					{
						propType.PropertyEditorAlias = NewDataTypeAlias;
					}

					//_contentTypeService.Save(contentTypeToConvert);
				}
			}

			_dataTypeService.Delete(oldDataTypeDefinition);

			Func<PropertyType, bool> IsOldDataTypeWithId(int id)
			{
				return type => type.DataTypeDefinitionId == id && type.PropertyEditorAlias == OldDataTypeAlias;
			}
		}

		private void ConvertOldValuesToNewerFormat(int oldContentTypeId, int oldDataTypeId)
		{
			var allContent = _contentService.GetContentOfContentType(oldContentTypeId).ToList();

			foreach (var content in allContent)
			{
				var properties = content.Properties.Where(p => p.PropertyType.PropertyEditorAlias == OldDataTypeAlias && p.PropertyType.DataTypeDefinitionId == oldDataTypeId);
				foreach (var property in properties)
				{
					content.SetValue(property.Alias, ConvertValue(property.Value));
				}
				if (content.Published)
				{
					//_contentService.SaveAndPublishWithStatus(content);
				}
				else
				{
					//_contentService.Save(content);
				}
			}
		}

		private object ConvertValue(object value)
		{
			var content = _contentService.GetById(Int32.Parse(value.ToString()));
			return content.GetUdi().ToString();
		}
	}
}
