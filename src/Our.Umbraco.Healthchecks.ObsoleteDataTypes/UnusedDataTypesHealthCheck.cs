using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Services;
using Umbraco.Web.HealthCheck;

namespace Our.Umbraco.HealthChecks.ObsoleteDataTypes
{
	[HealthCheck("91709783-58E8-44D9-801F-E6223000CB33", "Unused data types", Description = "Check for unused data types. Warning: Does not check the data type is used by other non-standard data types such as Archetype", Group = "Data Integrity")]
	public class UnusedDataTypesHealthCheck : HealthCheck
	{
		private readonly IDataTypeService _dataTypeService;
		private readonly IContentTypeService _contentTypeService;

		
		public UnusedDataTypesHealthCheck(HealthCheckContext healthCheckContext) : base(healthCheckContext)
		{
			_dataTypeService = healthCheckContext.ApplicationContext.Services.DataTypeService;
			_contentTypeService = healthCheckContext.ApplicationContext.Services.ContentTypeService;
		}
		

		public override HealthCheckStatus ExecuteAction(HealthCheckAction action)
		{
			throw new ArgumentOutOfRangeException();
		}

		public override IEnumerable<HealthCheckStatus> GetStatus()
		{
			var foundDataTypes = new List<int>();
			var contentTypes = _contentTypeService.GetAllContentTypes();
			foreach (var contentType in contentTypes)
			{
				foundDataTypes.AddRange(contentType.PropertyTypes.Select(p => p.DataTypeDefinitionId));
				foundDataTypes.AddRange(contentType.NoGroupPropertyTypes.Select(p => p.DataTypeDefinitionId));
				foundDataTypes.AddRange(contentType.CompositionPropertyTypes.Select(p => p.DataTypeDefinitionId));
			}

			var mediaTypes = _contentTypeService.GetAllMediaTypes();
			foreach(var mediaType in mediaTypes)
			{
				foundDataTypes.AddRange(mediaType.PropertyTypes.Select(p => p.DataTypeDefinitionId));
				foundDataTypes.AddRange(mediaType.NoGroupPropertyTypes.Select(p => p.DataTypeDefinitionId));
				foundDataTypes.AddRange(mediaType.CompositionPropertyTypes.Select(p => p.DataTypeDefinitionId));
			}
			
			var unusedDataTypes = _dataTypeService.GetAllDataTypeDefinitions()
				.Where(d => !d.Name.StartsWith("List View -"))
				.Where(d => !foundDataTypes.Distinct().Contains(d.Id))
				.ToArray();

			if(!unusedDataTypes.Any())
			{
				yield return new HealthCheckStatus("No unused data types found")
				{
					ResultType = StatusResultType.Success
				};
			}

			foreach(var dataType in unusedDataTypes)
			{
				yield return new HealthCheckStatus($"{dataType.Name} does not appear to be used")
				{
					ResultType = StatusResultType.Warning
				};
			}
		}
	}
}
