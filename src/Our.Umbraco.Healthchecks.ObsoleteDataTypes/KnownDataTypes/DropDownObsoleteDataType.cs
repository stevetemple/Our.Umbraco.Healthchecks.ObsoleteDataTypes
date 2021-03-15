using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Our.Umbraco.HealthChecks.ObsoleteDataTypes.KnownDataTypes
{
	public class DropDownObsoleteDataType : IObsoleteDataType
	{
		public string Alias => "Umbraco.DropDown";
		public bool CanConvert => false;
		public void Convert(string name) => throw new NotImplementedException();
	}
}
