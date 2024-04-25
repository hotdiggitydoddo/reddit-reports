namespace RedditReports.Application.Abstractions
{
	public interface IRedditReporterServiceSettings
	{
		string ClientId { get; init; }
		string ClientSecret { get; init; }
		string AuthenticationUriTemplate { get; init; }
		string RedditBaseUri { get; init; }
		string RedirectUri { get; init; }
		string UserAgent { get; init; }
		int NumPostsPerRequest { get; init; }
		int NumConcurrentConnections { get; init; }
	}
}
