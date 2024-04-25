using Newtonsoft.Json;

namespace RedditReports.Application.DTOs.Posts
{
	public class PostContainer : BaseContainer
    {
        [JsonProperty("data")]
        public PostData Data { get; set; }
    }
}
