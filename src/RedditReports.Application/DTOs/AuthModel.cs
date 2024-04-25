using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace RedditReports.Application.DTOs
{
	public class AuthModel
	{
		private DateTime _expiration;

		[JsonProperty("access_token")]
		public string AccessToken { get; set; }

		[JsonProperty("expires_in")]
		public int ExpiresIn { get; set; }

		public bool IsExpired => DateTime.UtcNow >= _expiration;

		[OnDeserialized]
		internal void OnDeserializedMethod(StreamingContext context)
		{
			_expiration = DateTime.UtcNow.AddSeconds(ExpiresIn);
		}
	}
}
