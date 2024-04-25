using RedditReports.Application.Abstractions;

namespace RedditReports.ConsoleApp
{
	internal class RedditReporterServiceSettings : IRedditReporterServiceSettings
	{
		public string ClientId { get; init; }
		public string ClientSecret => Environment.GetEnvironmentVariable("CLIENT_SECRET");
		public string AuthenticationUriTemplate { get; init; }
		public string RedditBaseUri { get; init; }
		public string RedirectUri { get; init; }
		public string UserAgent { get; init; }
		public int NumPostsPerRequest { get; init; }
		public int NumConcurrentConnections { get; init; }
	}
}
