using System;
using System.Collections.Generic;
using System.Linq;
using Our.Umbraco.HealthChecks.ObsoleteDataTypes.KnownDataTypes;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Web.HealthCheck;

namespace Our.Umbraco.HealthChecks.ObsoleteDataTypes
{
	[HealthCheck("94F8D1D7-6E20-4A43-BFDE-239F72114C71", "Obsolete data types", Description = "Check for obsolete data types in use", Group = "Data Integrity")]
	public class ObsoleteDataTypesHealthCheck : HealthCheck
	{
		private readonly IDataTypeService _dataTypeService;
		private readonly IEnumerable<IObsoleteDataType> _obsoleteDataTypes;

		public ObsoleteDataTypesHealthCheck(HealthCheckContext healthCheckContext) : base(healthCheckContext)
		{
			_obsoleteDataTypes = new IObsoleteDataType[]
			{
				new ArchetypeObsoleteDataType(),
				new NestedContentObsoleteDataType(healthCheckContext.ApplicationContext.Services),
				new MultiNodeTreePickerObsoleteDataType(),
				new RelatedLinksObsoleteDataType(),
				new MemberPickerObsoleteDataType()
			};
			_dataTypeService = healthCheckContext.ApplicationContext.Services.DataTypeService;
		}


		public override HealthCheckStatus ExecuteAction(HealthCheckAction action)
		{
			foreach(var dataType in _obsoleteDataTypes)
			{
				if(action.Alias == dataType.Alias)
				{
					dataType.Convert(action.ActionParameters["name"].ToString());
					return new HealthCheckStatus("Updated");
				}
			}
			throw new ArgumentOutOfRangeException();
		}

		public override IEnumerable<HealthCheckStatus> GetStatus()
		{
			var obsoleteFound = false;
			var dataTypes = _dataTypeService.GetAllDataTypeDefinitions();
			foreach (var dataType in dataTypes)
			{
				if (_obsoleteDataTypes.Select(d => d.Alias).Contains(dataType.PropertyEditorAlias))
				{
					var obsolete = _obsoleteDataTypes.First(d => d.Alias == dataType.PropertyEditorAlias);
					obsoleteFound = true;
					yield return new HealthCheckStatus($"{dataType.Name} is using obsolete data type {dataType.PropertyEditorAlias}")
					{
						ResultType = StatusResultType.Warning,
						Actions = Actions(dataType, obsolete)
					};
				}
			}
			if (!obsoleteFound)
			{
				yield return new HealthCheckStatus("No obsolete data types found")
				{
					ResultType = StatusResultType.Success
				};
			}
		}

		private HealthCheckAction[] Actions(IDataTypeDefinition dataType, IObsoleteDataType obsolete)
		{
			if(obsolete.CanConvert){
				return new[]
				{
					new HealthCheckAction(dataType.PropertyEditorAlias, Guid.Parse("94F8D1D7-6E20-4A43-BFDE-239F72114C71"))
					{
						Name = "Fix", ActionParameters = new Dictionary<string, object>
						{
							["name"] = dataType.Name
						}
					}
				};
			}
			return new HealthCheckAction[0];
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

