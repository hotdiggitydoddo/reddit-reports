using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RedditReports.Application.Abstractions;
using RedditReports.Application.DTOs;
using RedditReports.Application.DTOs.Posts;
using RedditReports.Application.Results;
using RedditReports.Domain.Models;
using RedditReports.Domain.Results;

namespace RedditReports.Infrastructure
{
	public class RedditApiClient : IRedditApiClient
	{
		private readonly ILogger<RedditApiClient> _logger;
		private readonly IRedditReporterServiceSettings _config;

		private int _remainingRequests;
		private int _rateLimitResetTime;
		private int _requestsUsed;

		private AuthModel? _authModel;
		private readonly SemaphoreSlim _concurrencySemaphore;

		private const string RateLimitHeaderName = "X-Ratelimit-Remaining";
		private const string RateLimitResetHeaderName = "X-Ratelimit-Reset";
		private const string RateLimitUsedHeaderName = "X-Ratelimit-Used";


		public RedditApiClient(ILogger<RedditApiClient> logger, IRedditReporterServiceSettings config)
		{
			_logger = logger;
			_config = config;
			_concurrencySemaphore = new SemaphoreSlim(_config.NumConcurrentConnections);
		}

		public async Task<RedditApiResponseResult<List<Post>>> FetchPostsAsync(string subredditName, string after = null)
		{
			if (_authModel == null || _authModel.IsExpired)
			{
				var result = await AuthenticateAsync();
				if (!result.Success)
				{
					var errorMsg = result.ErrorMessage ?? $"Authentication unsuccessful (subreddit: {subredditName})";
					_logger.LogError(errorMsg);
					return new RedditApiResponseResult<List<Post>>()
					{
						ResponseStatusCode = result.ResponseStatusCode,
						ErrorMessage = errorMsg
					};
				}
			}

			try
			{
				using (var httpClient = new HttpClient())
				{
					httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authModel.AccessToken);
					httpClient.DefaultRequestHeaders.Add("User-Agent", _config.UserAgent);

					await _concurrencySemaphore.WaitAsync();

					var url = $"{_config.RedditBaseUri}/r/{subredditName}/new?limit={_config.NumPostsPerRequest}&after={after}";
					var response = await httpClient.GetAsync(url);
					response.EnsureSuccessStatusCode(); // Check for successful response (200 OK)

					if (!response.Headers.Contains(RateLimitHeaderName))
					{
						return new RedditApiResponseResult<List<Post>>
						{
							ResponseStatusCode = (int)HttpStatusCode.NotFound,
							ErrorMessage = $"Couldn't find a subreddit named \"{subredditName}\""
						};
					}
					
					// Extract rate limit data from headers
					_remainingRequests = (int)float.Parse(response.Headers.GetValues(RateLimitHeaderName).FirstOrDefault());
					_rateLimitResetTime = Convert.ToInt32(response.Headers.GetValues(RateLimitResetHeaderName).FirstOrDefault());
					_requestsUsed = Convert.ToInt32(response.Headers.GetValues(RateLimitUsedHeaderName).FirstOrDefault());

					var responseJsonString = await response.Content.ReadAsStringAsync();
					var postData = JsonConvert.DeserializeObject<PostContainer>(responseJsonString); // Assuming a Post class exists

					var result = new RedditApiResponseResult<List<Post>>()
					{
						Result = postData!.Data.Children.Select(x => new Post
						{
							Id = x.Data.Id,
							Author = x.Data.Author,
							Subreddit = x.Data.Subreddit,
							Title = x.Data.Title,
							Ups = x.Data.Ups
						}).ToList(),
						RemainingRequests = _remainingRequests,
						ResetTimeInSeconds = _rateLimitResetTime,
						RequestsUsed = _requestsUsed,
						ResponseStatusCode = (int)response.StatusCode,
						After = postData?.Data.After
					};

					return result;
				}
			}
			catch (HttpRequestException ex)
			{
				_logger.LogError(ex, ex.Message, subredditName);
				return new RedditApiResponseResult<List<Post>>
				{
					ErrorMessage = ex.Message,
					ResponseStatusCode = (int)ex.StatusCode.GetValueOrDefault(),
				};
			}
			catch (Exception ex)
			{
				_logger.LogCritical(ex, ex.Message, subredditName);
				return new RedditApiResponseResult<List<Post>>
				{
					ErrorMessage = ex.Message,
					ResponseStatusCode = (int)HttpStatusCode.UnprocessableEntity,
				};
			}
			finally
			{
				_concurrencySemaphore.Release(); // Release semaphore slot
			}
		}

		public async Task<HttpResponseResult> AuthenticateAsync()
		{
			var sb = new StringBuilder(_config.AuthenticationUriTemplate);
			var replacements = new List<(string key, string value)>
			{
			  ("clientId", _config.ClientId),
			  ("redirectUri", Uri.EscapeDataString(_config.RedirectUri))
			};
			var authUrl = replacements.Aggregate(sb, (s, t) => s.Replace($"{{{t.key}}}", t.value)).ToString();
			
			Console.ForegroundColor = ConsoleColor.Gray;
			Console.WriteLine("Please visit the following URL to authorize the application:");
			Console.ForegroundColor = ConsoleColor.DarkYellow;
			Console.WriteLine(authUrl);
			Console.ForegroundColor = ConsoleColor.Gray;
			Console.WriteLine("After authorization, paste the redirect URL here:");

			// Wait for user to paste the redirect URL containing the authorization code
			var redirectUrl = Console.ReadLine();

			// Extract authorization code from redirect URL
			if (!Uri.TryCreate(redirectUrl, UriKind.Absolute, out var redirectUri))
			{
				return new HttpResponseResult
				{
					ErrorMessage = "Redirect URL is not properly formatted.",
					ResponseStatusCode = (int)HttpStatusCode.BadRequest
				};
			}
			var authorizationCode = System.Web.HttpUtility.ParseQueryString(redirectUri.Query).Get("code");

			// Exchange authorization code for access token
			using (var httpClient = new HttpClient())
			{
				var requestParams = new FormUrlEncodedContent(new[]
				{
					new KeyValuePair<string, string>("grant_type", "authorization_code"),
					new KeyValuePair<string, string>("code", authorizationCode),
					new KeyValuePair<string, string>("redirect_uri", _config.RedirectUri)
				});

				httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
					"Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_config.ClientId}:{_config.ClientSecret}")));

				httpClient.DefaultRequestHeaders.Add("User-Agent", _config.UserAgent);

				var tokenResponse = await httpClient.PostAsync($"{_config.RedditBaseUri}/api/v1/access_token", requestParams);
				if (!tokenResponse.IsSuccessStatusCode)
				{
					return new HttpResponseResult
					{
						ResponseStatusCode = (int)tokenResponse.StatusCode,
						ErrorMessage = tokenResponse.ReasonPhrase
					};
				}

				var responseContent = await tokenResponse.Content.ReadAsStringAsync();
				_authModel = JsonConvert.DeserializeObject<AuthModel>(responseContent);

				return new HttpResponseResult
				{
					ResponseStatusCode = (int)tokenResponse.StatusCode,
				};
			}
		}
	}
}
