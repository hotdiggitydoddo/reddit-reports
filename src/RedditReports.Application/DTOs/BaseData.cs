using Newtonsoft.Json;

namespace RedditReports.Application.DTOs
{
	public abstract class BaseData
	{
		[JsonProperty("modhash")]
		public string Modhash { get; set; }

		[JsonProperty("dist")]
		public int? Dist { get; set; }

		[JsonProperty("after")]
		public string After { get; set; }

		[JsonProperty("before")]
		public string Before { get; set; }
	}
}
