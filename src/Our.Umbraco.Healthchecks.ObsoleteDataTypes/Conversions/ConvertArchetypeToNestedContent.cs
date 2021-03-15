using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Our.Umbraco.HealthChecks.ObsoleteDataTypes.Conversions
{
	public class ConvertArchetypeToNestedContent
	{
		private readonly IDataTypeService _dataTypeService;
		private readonly IContentTypeService _contentTypeService;
		private readonly IContentService _contentService;

		private const string ArchetypeAlias = "Imulus.Archetype";
		private const string NestedContentAlias = "Umbraco.NestedContent";

		private readonly JsonSerializerSettings _serializerSettings =
			new JsonSerializerSettings {ContractResolver = new DefaultContractResolver
			{
				NamingStrategy = new CamelCaseNamingStrategy()
			}};

		public ConvertArchetypeToNestedContent(ServiceContext services)
		{
			_dataTypeService = services.DataTypeService;
			_contentTypeService = services.ContentTypeService;
			_contentService = services.ContentService;
		}

		/// <summary>
		/// Convert archetype data types with the given name
		///
		/// Creates new data types, including a content type to represent the Archetype
		/// Then converts the content from one json format to the other
		/// This searches inside other nested contents as well as just standard content
		/// Finally swaps the data type itself over
		/// </summary>
		/// <param name="name"></param>
		public void Convert(string name)
		{
			var archetypeDataType = _dataTypeService.GetDataTypeDefinitionByName(name);

			var nestedContentDataType = CreateNestedContentDataTypeBasedOnArchetype(archetypeDataType);
			
			var allContentTypes = _contentTypeService.GetAllContentTypes();
			var archetypeContentTypes = allContentTypes
				.Where(c =>
					c.PropertyTypes.Any(a => a.DataTypeDefinitionId == archetypeDataType.Id)
					|| c.CompositionPropertyTypes.Any(a => a.DataTypeDefinitionId == archetypeDataType.Id));

			ConvertContent(archetypeContentTypes, archetypeDataType);
			ConvertDataType(archetypeContentTypes, archetypeDataType, nestedContentDataType);
		}

		/// <summary>
		/// Convert Archetype content to equivalent in Nested Content
		/// </summary>
		/// <param name="archetypeContentTypes"></param>
		/// <param name="archetypeDataType"></param>
		private void ConvertContent(IEnumerable<IContentType> archetypeContentTypes, IDataTypeDefinition archetypeDataType)
		{
			foreach (var archetypeContentType in archetypeContentTypes)
			{
				ConvertInsideNestedContents(archetypeContentType.Alias, Alias(archetypeDataType.Name + "nc"));
				ConvertArchetypeValuesToNestedContent(archetypeContentType.Id, archetypeDataType.Id,
					Alias(archetypeDataType.Name + "nc"));
			}
		}

		/// <summary>
		/// Convert the Archetype data types to their Nested Content equivalent
		/// </summary>
		/// <param name="archetypeContentTypes"></param>
		/// <param name="archetypeDataType"></param>
		/// <param name="nestedContentDataType"></param>
		private void ConvertDataType(IEnumerable<IContentType> archetypeContentTypes, IDataTypeDefinition archetypeDataType,
			IDataTypeDefinition nestedContentDataType)
		{
			foreach (var archetypeContentType in archetypeContentTypes)
			{
				foreach (var composition in archetypeContentType.ContentTypeComposition)
				{
					if (composition.PropertyTypes.Any(IsArchetypeWithId(archetypeDataType.Id)))
					{
						var compositionContentType = _contentTypeService.GetContentType(composition.Id);

						var propertyTypes = compositionContentType.PropertyTypes.Where(IsArchetypeWithId(archetypeDataType.Id))
							.ToArray();
						foreach (var propType in propertyTypes)
						{
							propType.DataTypeDefinitionId = nestedContentDataType.Id;
							propType.PropertyEditorAlias = NestedContentAlias;
						}

						_contentTypeService.Save(compositionContentType);
					}
				}

				if (archetypeContentType.PropertyTypes.Any(IsArchetypeWithId(archetypeDataType.Id)))
				{
					var propertyTypes = archetypeContentType.PropertyTypes.Where(IsArchetypeWithId(archetypeDataType.Id)).ToArray();

					foreach (var propType in propertyTypes)
					{
						propType.DataTypeDefinitionId = nestedContentDataType.Id;
						propType.PropertyEditorAlias = NestedContentAlias;
					}

					_contentTypeService.Save(archetypeContentType);
				}
			}

			_dataTypeService.Delete(archetypeDataType);

			Func<PropertyType, bool> IsArchetypeWithId(int id)
			{
				return type => type.DataTypeDefinitionId == id && type.PropertyEditorAlias == ArchetypeAlias;
			}
		}

		


		private void ConvertArchetypeValuesToNestedContent(int oldContentTypeId, int archetypeDataTypeId, string newContentTypeAlias)
		{
			var allContent = _contentService.GetContentOfContentType(oldContentTypeId).ToList();

			foreach (var content in allContent)
			{
				var properties = content.Properties.Where(p => p.PropertyType.PropertyEditorAlias == ArchetypeAlias && p.PropertyType.DataTypeDefinitionId == archetypeDataTypeId);
				foreach (var property in properties)
				{
					content.SetValue(property.Alias, ConvertArchetypeJsonToNestedContentJson(newContentTypeAlias, content.GetValue<string>(property.Alias)));
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

		private string ConvertArchetypeJsonToNestedContentJson(string newContentTypeAlias, string value)
		{
			try
			{
				var archetype = JsonConvert.DeserializeObject<ArchetypeValue>(value);

				var vals = new List<Dictionary<string, string>>();
				foreach (var fieldset in archetype.Fieldsets)
				{
					var dictionary = new Dictionary<string, string>
					{
						{"key", Guid.NewGuid().ToString()},
						{"ncContentTypeAlias", newContentTypeAlias}
					};

					foreach (var prop in fieldset.Properties)
					{
						dictionary.Add(prop.Alias, prop.Value);
					}
					vals.Add(dictionary);
				}
				return JsonConvert.SerializeObject(vals, _serializerSettings);
			} catch(Exception)
			{
				return value;
			}
		}

		private IDataTypeDefinition CreateNestedContentDataTypeBasedOnArchetype(IDataTypeDefinition archetypeDataType)
		{

			var name = archetypeDataType.Name + " (NC)";
			var alias = Alias(archetypeDataType.Name + "nc"); // TODO Umbraco method of creating aliases
			var existingDataType = _dataTypeService.GetDataTypeDefinitionByName(name);
			if(existingDataType != null)
			{
				return existingDataType;
			}

			var preValue = _dataTypeService.GetPreValuesByDataTypeId(archetypeDataType.Id).FirstOrDefault();
			var archetypePreValue = JsonConvert.DeserializeObject<ArchetypePreValue>(preValue);

			var contentType = new ContentType(-1);
			contentType.Name = name;
			contentType.Alias = alias; 

			foreach (var fieldset in archetypePreValue.Fieldsets)
			{
				var propertyGroup = new PropertyGroup();
				propertyGroup.Name = "Content";

				foreach (var prop in fieldset.Properties)
				{
					var definition = _dataTypeService.GetDataTypeDefinitionById(prop.DataTypeGuid);
					propertyGroup.PropertyTypes.Add(new PropertyType(definition) { Name = prop.Label, Alias = prop.Alias, Mandatory = prop.Required, Description = prop.HelpText });
				}
				contentType.PropertyGroups.Add(propertyGroup);
			}
			_contentTypeService.Save(contentType);

			var dataType = new DataTypeDefinition(NestedContentAlias);
			dataType.Name = contentType.Name;

			var preValues = new Dictionary<string, PreValue>();
			preValues.Add("contentTypes", new PreValue(JsonConvert.SerializeObject(
				new NestedContentPreValue[]
				{
							new NestedContentPreValue
							{
								NcAlias = contentType.Alias,
								NcTabAlias = "Content",
								NameTemplate = archetypePreValue.Fieldsets.First().LabelTemplate
							}
				},
				_serializerSettings)
			));
			preValues.Add("minItems", new PreValue("0"));
			preValues.Add("maxItems", new PreValue("0"));
			preValues.Add("confirmDeletes", new PreValue("0"));
			preValues.Add("showIcons", new PreValue("0"));
			preValues.Add("hideLabel", new PreValue(archetypePreValue.HidePropertyLabel ? "1" : "0"));

			_dataTypeService.SaveDataTypeAndPreValues(dataType, preValues);
			return dataType;
		}

		private string Alias(string text)
		{
			return text.ToLower().Replace(" ", "");
		}

		private void ConvertInsideNestedContents(string alias, string ncAlias)
		{
			var nestedContentType = _contentTypeService.GetContentType(alias);
			var dataTypeDefinitionIds = NestedContentAliasWithContentType(alias);
			foreach (var id in dataTypeDefinitionIds)
			{
				// Find all content types that have one of these properties in it
				var allContentTypes = _contentTypeService.GetAllContentTypes();
				var contentTypesContainingDataType = allContentTypes.Where(c => c.PropertyTypes.Any(a => a.DataTypeDefinitionId == id) || c.CompositionPropertyTypes.Any(a => a.DataTypeDefinitionId == id));
				foreach (var contentType in contentTypesContainingDataType)
				{
					// Then find each content using that content type
					var content = _contentService.GetContentOfContentType(contentType.Id);
					foreach (var c in content)
					{
						var changed = false;
						var affectedProperties = c.Properties.Where(p => p.PropertyType.DataTypeDefinitionId == id);
						foreach(var prop in affectedProperties)
						{
							if(prop.Value == null)
							{
								continue;
							}
							// Deserialize nested content values then convert any of the type we're looking for
							var nestedContent = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(System.Convert.ToString(prop.Value), _serializerSettings);
							foreach(var entry in nestedContent)
							{
								if(entry["ncContentTypeAlias"] == alias)
								{
									
									foreach (var archetype in nestedContentType.CompositionPropertyTypes.Where(p => p.PropertyEditorAlias == ArchetypeAlias))
									{
										entry[archetype.Alias] = ConvertArchetypeJsonToNestedContentJson(ncAlias, entry[archetype.Alias]);
									}

								}
							}
							prop.Value = JsonConvert.SerializeObject(nestedContent, _serializerSettings);
							changed = true;
							
						}
						if(changed)
						{
							if(c.Published) 
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

		private IEnumerable<int> NestedContentAliasWithContentType(string alias)
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

	public class ArchetypeValue
	{
		public Guid Id { get; set; }
		public IEnumerable<ArchetypeFieldset> Fieldsets { get; set; }
	}

	public class NestedContentPreValue
	{
		public string NcAlias { get; set; }
		public string NcTabAlias { get; set; }
		public string NameTemplate { get; set; }
	}

	public class ArchetypePreValue
	{
		public bool ShowAdvancedOptions { get; set; }
		public bool StartWithButton { get; set; }
		public bool HideFieldsetToolbar { get; set; }
		public bool EnableMultipleFieldsets { get; set; }
		public bool HideFieldsetControls { get; set; }
		public bool HidePropertyLabel { get; set; }
		public int? MaxFieldsets { get; set; }
		public bool EnableCollapsing { get; set; }
		public bool EnableCloning { get; set; }
		public bool EnableDisabling { get; set; }
		public bool EnableDeepDatatypeRequests { get; set; }
		public bool EnablePublishing { get; set; }
		public bool EnableMemberGroups { get; set; }
		public bool EnableCrossDragging { get; set; }
		public IEnumerable<ArchetypeFieldset> Fieldsets { get; set; }
	}

	public class ArchetypeFieldset
	{
		public string Alias { get; set; }
		public bool Remove { get; set; }
		public bool Collapse { get; set; }
		public string LabelTemplate { get; set; }
		public string Icon { get; set; }
		public string Label { get; set; }
		public IEnumerable<ArchetypeProperty> Properties { get; set; }
	}

	public class ArchetypeProperty
	{
		public string Alias { get; set; }
		public bool Remove { get; set; }
		public bool Collapse { get; set; }
		public string Label { get; set; }
		public string HelpText { get; set; }
		public Guid DataTypeGuid { get; set; }
		public string Value { get; set; }
		public bool Required { get; set; }
	}
}
