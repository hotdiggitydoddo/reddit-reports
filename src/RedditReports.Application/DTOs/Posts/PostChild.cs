using Newtonsoft.Json;
using RedditReports.Domain.Models;

namespace RedditReports.Application.DTOs.Posts
{
	public class PostChild : BaseContainer
	{
		[JsonProperty("data")]
		public Post Data { get; set; }
	}
}
