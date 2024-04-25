using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RedditReports.Application.DTOs.Posts
{
    public class PostContainer : BaseContainer
    {
        [JsonProperty("data")]
        public PostData Data { get; set; }
    }
}
