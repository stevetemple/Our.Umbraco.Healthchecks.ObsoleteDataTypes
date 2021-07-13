using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Our.Umbraco.HealthChecks.ObsoleteDataTypes.Conversions
{
	public class GenericConversion
	{
		private const string NestedContentAlias = "Umbraco.NestedContent";

		private readonly IDataTypeService _dataTypeService;
		private readonly IContentTypeService _contentTypeService;
		private readonly IContentService _contentService;

		private readonly string _oldDataTypeAlias;
		private readonly string _newDataTypeAlias;
		private readonly Func<object, object> _conversion;
		private readonly Func<IDictionary<string, PreValue>, IDictionary<string, PreValue>> _preValueConversion;

		private readonly JsonSerializerSettings _serializerSettings =
			new JsonSerializerSettings
			{
				ContractResolver = new DefaultContractResolver
				{
					NamingStrategy = new CamelCaseNamingStrategy()
				}
			};

		public GenericConversion(ServiceContext services, string oldDataTypeAlias, string newDataTypeAlias, Func<object, object> conversion, Func<IDictionary<string, PreValue>, IDictionary<string, PreValue>> preValueConversion = null)
		{
			_dataTypeService = services.DataTypeService;
			_contentTypeService = services.ContentTypeService;
			_contentService = services.ContentService;

			_oldDataTypeAlias = oldDataTypeAlias;
			_newDataTypeAlias = newDataTypeAlias;
			_conversion = conversion;
			_preValueConversion = preValueConversion;
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
			var oldDataTypeDefinition = _dataTypeService.GetDataTypeDefinitionByName(name);
			oldDataTypeDefinition.Name = oldDataTypeDefinition.Name + " (Obsolete)";
			_dataTypeService.Save(oldDataTypeDefinition);

			var oldPreValues = _dataTypeService.GetPreValuesCollectionByDataTypeId(oldDataTypeDefinition.Id);

			var newDataTypeDefinition = new DataTypeDefinition(_newDataTypeAlias);
			newDataTypeDefinition.Name = name;
			_dataTypeService.SaveDataTypeAndPreValues(newDataTypeDefinition, _preValueConversion == null ? new Dictionary<string, PreValue>() : _preValueConversion(oldPreValues.PreValuesAsDictionary));

			var allContentTypes = _contentTypeService.GetAllContentTypes();
			var contentTypesToConvert = allContentTypes
				.Where(c =>
					c.PropertyTypes.Any(a => a.DataTypeDefinitionId == oldDataTypeDefinition.Id)
					|| c.CompositionPropertyTypes.Any(a => a.DataTypeDefinitionId == oldDataTypeDefinition.Id))
				.ToArray();

			AddReplacementDataType(contentTypesToConvert, oldDataTypeDefinition, newDataTypeDefinition);
			ConvertContent(contentTypesToConvert, oldDataTypeDefinition);
			DeleteOldDataType(oldDataTypeDefinition);
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
				ConvertOldValuesToNewerFormat(contentType.Id, oldDataTypeDefinition.Id);
				ConvertOldValuesToNewerFormatInsideNestedContents(contentType, oldDataTypeDefinition.PropertyEditorAlias);
			}
		}

		/// <summary>
		/// Change the existing data type to be suffixed with Obsolete and add
		/// a new datatype with the old alias.
		/// </summary>
		/// <param name="contentTypesToConvert"></param>
		/// <param name="oldDataTypeDefinition"></param>
		/// <param name="newDataTypeDefinition"></param>
		private void AddReplacementDataType(IEnumerable<IContentType> contentTypesToConvert, IDataTypeDefinition oldDataTypeDefinition, IDataTypeDefinition newDataTypeDefinition)
		{
			var convertedCompositions = new HashSet<string>();
			foreach (var contentTypeToConvert in contentTypesToConvert)
			{
				var contentType = contentTypeToConvert;
				foreach (var composition in contentTypeToConvert.ContentTypeComposition)
				{
					if (convertedCompositions.Contains(composition.Alias))
					{
						continue;
					}

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

					convertedCompositions.Add(composition.Alias);
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
							.PropertyTypes.Add(new PropertyType(newDataTypeDefinition, alias) { Name = name, Description = propType.Description, SortOrder = propType.SortOrder, Mandatory = propType.Mandatory, ValidationRegExp = propType.ValidationRegExp });
						_contentTypeService.Save(contentType);
					}
				}
			}

			Func<PropertyType, bool> IsOldDataTypeWithId(int id)
			{
				return type => type.DataTypeDefinitionId == id && type.PropertyEditorAlias == _oldDataTypeAlias;
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
					content.SetValue(alias, _conversion(property.Value));
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

		private void ConvertOldValuesToNewerFormatInsideNestedContents(IContentType contentType, string propertyEditorAlias)
		{
			var dataTypeDefinitionIds = NestedContentIdsUsingContentType(contentType.Alias);
			foreach (var id in dataTypeDefinitionIds)
			{
				// Find all content types that have one of these nested contents in it
				var allContentTypes = _contentTypeService.GetAllContentTypes();
				var contentTypesContainingNestedContent = allContentTypes.Where(c => c.PropertyTypes.Any(a => a.DataTypeDefinitionId == id) || c.CompositionPropertyTypes.Any(a => a.DataTypeDefinitionId == id));
				foreach (var contentTypeContainingNestedContent in contentTypesContainingNestedContent)
				{
					// Then find each content using that content type
					var content = _contentService.GetContentOfContentType(contentTypeContainingNestedContent.Id);
					foreach (var c in content)
					{
						var changed = false;
						var affectedProperties = c.Properties.Where(p => p.PropertyType.DataTypeDefinitionId == id);
						foreach (var prop in affectedProperties)
						{
							if (prop.Value == null)
							{
								continue;
							}
							// Deserialize nested content values then convert any of the type we're looking for
							var nestedContent = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(System.Convert.ToString(prop.Value), _serializerSettings);
							foreach (var entry in nestedContent)
							{
								if (entry["ncContentTypeAlias"].Equals(contentType.Alias, StringComparison.InvariantCultureIgnoreCase))
								{
									var properties =
										contentType.CompositionPropertyTypes.Where(p => p.PropertyEditorAlias == propertyEditorAlias).ToList();
									properties.AddRange(contentType.PropertyTypes.Where(p => p.PropertyEditorAlias == propertyEditorAlias));

									foreach (var property in properties.Distinct())
									{
										var alias = property.Alias.TrimEnd("Obsolete");
										entry[alias] = _conversion(entry[alias]).ToString();
									}
								}
							}
							prop.Value = JsonConvert.SerializeObject(nestedContent, _serializerSettings);
							changed = true;

						}
						if (changed)
						{
							if (c.Published)
							{
								_contentService.SaveAndPublishWithStatus(c);
							}
							else
							{
								_contentService.Save(c);
							}
						}
					}

				}
			}
		}

		private IEnumerable<int> NestedContentIdsUsingContentType(string alias)
		{
			var nestedContents = _dataTypeService.GetDataTypeDefinitionByPropertyEditorAlias(NestedContentAlias);
			foreach (var dataType in nestedContents)
			{
				var preValues = _dataTypeService.GetPreValuesCollectionByDataTypeId(dataType.Id).FormatAsDictionary();
				var contentTypes = JsonConvert.DeserializeObject<NestedContentPreValue[]>(preValues["contentTypes"].Value, _serializerSettings);
				if (contentTypes.Any(n => n.NcAlias.Equals(alias, StringComparison.CurrentCultureIgnoreCase)))
				{
					yield return dataType.Id;
				}
			}
		}
	}
}
