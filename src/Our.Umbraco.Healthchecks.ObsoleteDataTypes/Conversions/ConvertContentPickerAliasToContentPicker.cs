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
		private const string NewDataTypeAlias = "Umbraco.ContentPicker2";

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
			oldDataTypeDefinition.Name = oldDataTypeDefinition.Name + " (Obsolete)";
			_dataTypeService.Save(oldDataTypeDefinition);
			
			var newDataTypeDefinition = new DataTypeDefinition(NewDataTypeAlias);
			newDataTypeDefinition.Name = name;
			_dataTypeService.SaveDataTypeAndPreValues(newDataTypeDefinition, new Dictionary<string, PreValue>());
			
			var allContentTypes = _contentTypeService.GetAllContentTypes();
			var contentTypesToConvert = allContentTypes
				.Where(c =>
					c.PropertyTypes.Any(a => a.DataTypeDefinitionId == oldDataTypeDefinition.Id)
					|| c.CompositionPropertyTypes.Any(a => a.DataTypeDefinitionId == oldDataTypeDefinition.Id))
				.ToArray();

			AddReplacementDataType(contentTypesToConvert, oldDataTypeDefinition, newDataTypeDefinition);
			ConvertContent(contentTypesToConvert, oldDataTypeDefinition, newDataTypeDefinition);
			DeleteOldDataType(oldDataTypeDefinition);
		}

		/// <summary>
		/// Convert content to equivalent
		/// </summary>
		/// <param name="contentTypesToConvert"></param>
		/// <param name="oldDataTypeDefinition"></param>
		private void ConvertContent(IEnumerable<IContentType> contentTypesToConvert, IDataTypeDefinition oldDataTypeDefinition, IDataTypeDefinition newDataTypeDefinition)
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
		private void AddReplacementDataType(IEnumerable<IContentType> contentTypesToConvert, IDataTypeDefinition oldDataTypeDefinition, IDataTypeDefinition newDataTypeDefinition)
		{
			foreach (var contentTypeToConvert in contentTypesToConvert)
			{
				var contentType = contentTypeToConvert;
				foreach (var composition in contentTypeToConvert.ContentTypeComposition)
				{
					if (composition.PropertyTypes.Any(IsOldDataTypeWithId(oldDataTypeDefinition.Id)))
					{
						var compositionContentType = _contentTypeService.GetContentType(composition.Id);

						var propertyTypes = compositionContentType.PropertyTypes.Where(IsOldDataTypeWithId(oldDataTypeDefinition.Id))
							.ToArray();

						foreach (var propType in propertyTypes)
						{
							var alias = propType.Alias;
							var name = propType.Name;

							var pt = compositionContentType.PropertyTypes.First(p => p.Alias == alias);

							pt.Name = propType.Name + " (Obsolete)";
							pt.Alias = propType.Alias + "Obsolete";
							_contentTypeService.Save(compositionContentType);
							
							compositionContentType = _contentTypeService.GetContentType(composition.Id);
							compositionContentType.PropertyGroups.First(g => g.PropertyTypes.Contains(propType))
								.PropertyTypes.Add(new PropertyType(newDataTypeDefinition, alias) { Name = name, Description = propType.Description, SortOrder = propType.SortOrder, Mandatory = propType.Mandatory, ValidationRegExp = propType.ValidationRegExp });
							_contentTypeService.Save(compositionContentType);
						}
					}
				}

				if (contentType.PropertyTypes.Any(IsOldDataTypeWithId(oldDataTypeDefinition.Id)))
				{
					var propertyTypes = contentType.PropertyTypes.Where(IsOldDataTypeWithId(oldDataTypeDefinition.Id))
						.ToArray();

					foreach (var propType in propertyTypes)
					{
						var alias = propType.Alias;
						var name = propType.Name;

						var pt = contentType.PropertyTypes.First(p => p.Alias == alias);

						pt.Name = propType.Name + " (Obsolete)";
						pt.Alias = propType.Alias + "Obsolete";
						_contentTypeService.Save(contentType);

						contentType = _contentTypeService.GetContentType(contentTypeToConvert.Id);

						contentType.PropertyGroups.First(g => g.PropertyTypes.Contains(propType))
							.PropertyTypes.Add(new PropertyType(newDataTypeDefinition, alias) { Name =  name, Description = propType.Description, SortOrder =  propType.SortOrder, Mandatory = propType.Mandatory, ValidationRegExp = propType.ValidationRegExp });
						_contentTypeService.Save(contentType);
					}
				}
			}
			
			Func<PropertyType, bool> IsOldDataTypeWithId(int id)
			{
				return type => type.DataTypeDefinitionId == id && type.PropertyEditorAlias == OldDataTypeAlias;
			}
		}

		/// <summary>
		/// Delete the old data type
		/// </summary>
		/// <param name="oldDataTypeDefinition"></param>
		private void DeleteOldDataType(IDataTypeDefinition oldDataTypeDefinition)
		{
			_dataTypeService.Delete(oldDataTypeDefinition);
		}

		/// <summary>
		/// Convert the values
		/// </summary>
		/// <param name="oldContentTypeId"></param>
		/// <param name="oldDataTypeId"></param>
		private void ConvertOldValuesToNewerFormat(int oldContentTypeId, int oldDataTypeId)
		{
			var allContent = _contentService.GetContentOfContentType(oldContentTypeId).ToList();

			foreach (var content in allContent)
			{
				var properties = content.Properties.Where(p => p.PropertyType.DataTypeDefinitionId == oldDataTypeId);
				foreach (var property in properties)
				{
					var alias = property.Alias.TrimEnd("Obsolete");
					content.SetValue(alias, ConvertValue(property.Value));
				}
				if (content.Published)
				{
					_contentService.SaveAndPublishWithStatus(content);
				}
				else
				{
					_contentService.Save(content);
				}
			}
		}

		private object ConvertValue(object value)
		{
			if (value == null) return null;
			var content = _contentService.GetById(Int32.Parse(value.ToString()));
			return content.GetUdi().ToString();
		}
	}
}
