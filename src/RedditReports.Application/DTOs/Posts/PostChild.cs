using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
