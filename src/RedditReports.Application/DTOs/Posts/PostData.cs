using Newtonsoft.Json;

namespace RedditReports.Application.DTOs.Posts
{
	public class PostData : BaseData
	{
		[JsonProperty("children")]
		public List<PostChild> Children { get; set; }

		[JsonProperty("facets")]
		public object Facets { get; set; }
	}
}
