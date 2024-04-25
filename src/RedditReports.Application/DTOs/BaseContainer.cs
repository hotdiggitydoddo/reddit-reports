using Newtonsoft.Json;

namespace RedditReports.Application.DTOs
{
	public abstract class BaseContainer
	{
		[JsonProperty("kind")]
		public string Kind { get; set; }
	}
}
