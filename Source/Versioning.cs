using System.Linq;
using System.Reflection;

namespace Firefly
{
	public class Versioning
	{
		public static bool IsDev = false;

		public static int ConfigVersion = 5;

		public static string VersionAuthor(object caller)
		{
			AssemblyDescriptionAttribute attribute = caller.GetType().Assembly
				.GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false)
				.OfType<AssemblyDescriptionAttribute>()
				.FirstOrDefault();

			return attribute.Description;
		}
		public static string Version(object caller)
		{
			return caller.GetType().Assembly.GetName().Version.ToString();
		}
	}
}
