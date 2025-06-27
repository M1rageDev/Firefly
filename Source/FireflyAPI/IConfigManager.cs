using UnityEngine;

namespace FireflyAPI
{
	public interface IConfigManager
	{
		BodyConfig DefaultConfig { get; set; }   
		
		bool TryGetBodyConfig(string bodyName, bool fallback, out BodyConfig cfg);
	}
}
