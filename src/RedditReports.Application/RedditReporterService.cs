using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;
using RedditReports.Application.Abstractions;
using RedditReports.Domain.Abstractions;
using RedditReports.Domain.Models;

namespace RedditReports.Application
{
	public class RedditReporterService : IRedditReporterService
	{
		private readonly IRedditApiClient _redditApiClient;
		private readonly ILogger<RedditReporterService> _logger;
		private readonly ConcurrentDictionary<string, Subreddit> _subreddits = new();

		public RedditReporterService(IRedditApiClient redditApiClient, ILogger<RedditReporterService> logger, IRedditReporterServiceSettings settings)
		{
			if (string.IsNullOrWhiteSpace(settings.ClientSecret))
			{
				var ex = new ApplicationException($"{nameof(settings.ClientSecret)} does not exist. Please obtain it and add it as an environment variable.");
				logger.LogCritical(ex, ex.Message);
				throw ex;
			}
			_redditApiClient = redditApiClient;
			_logger = logger;
		}

		public async Task StartAsync()
		{
			var authResult = await _redditApiClient.AuthenticateAsync();
			if (!authResult.Success)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"Error (authentication): {authResult.ErrorMessage}");
				Console.ForegroundColor = ConsoleColor.Gray;
				return;
			}

			var tasks = new ConcurrentBag<Task>();
			while (true)
			{
				Console.ForegroundColor = ConsoleColor.Blue;
				Console.WriteLine("Type the name of a subreddit to track, or just hit enter to exit:");
				Console.ForegroundColor = ConsoleColor.Gray;
				var subreddit = Console.ReadLine();
				if (string.IsNullOrWhiteSpace(subreddit))
				{
					break;
				}
				if (_subreddits.ContainsKey(subreddit))
				{
					Console.WriteLine($"Already searching \"{subreddit}\".  Please try again.");
					continue;
				}
				var task = Task.Run(GetPostsForSubredditAsync(subreddit));
				tasks.Add(task);
			}
			await Task.WhenAll(tasks);
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("Have a nice day!");
			Console.ForegroundColor = ConsoleColor.Gray;
		}

		private Func<Task?> GetPostsForSubredditAsync(string subreddit)
		{
			return async () =>
			{
				var after = string.Empty;
				while (true)
				{
					var result = await _redditApiClient.FetchPostsAsync(subreddit, after);
					if (!result.Success)
					{
						Console.Error.WriteLine(result.ErrorMessage);
						
						// Bail out completely if we've hit the rate limit
						if (result.ResponseStatusCode == (int)HttpStatusCode.TooManyRequests)
						{
							return;
						}
						break;
					}

					HandleNewPosts(result.Result!);

					if (result.After == null)
					{
						if (!_logger.IsEnabled(LogLevel.Debug))
						{
							Console.WriteLine(".");
						}
						DisplaySubredditResults(result.Result!);
						_subreddits.TryRemove(subreddit, out _);
						if (_subreddits.Count == 0)
						{
							Console.ForegroundColor = ConsoleColor.Green;
							Console.WriteLine("All requests completed.  Feed me more subreddits!");
							Console.ForegroundColor = ConsoleColor.Gray;
						}
						break;
					}
					after = result.After;

					// Calculate some kind of delay to help control the request rate
					var delay = TimeSpan.FromSeconds((float)result.ResetTimeInSeconds / result.RemainingRequests);
					if (_logger.IsEnabled(LogLevel.Debug))
					{
						_logger.LogDebug($"subreddit: {subreddit} | requests remaining: {result.RemainingRequests} | time to reset: {result.ResetTimeInSeconds} | delay: {delay}");
					}
					else
					{
						Console.Write(".");
					}
					await Task.Delay(delay);
				}
			};
		}

		private void HandleNewPosts(List<Post> posts)
		{
			if (posts == null || posts.Count == 0) { return; }
			var subredditName = posts.First().Subreddit;
			var subreddit = _subreddits.AddOrUpdate(subredditName, new Subreddit(subredditName, posts), (x, y) =>
			{
				y.Posts.AddRange(posts);
				return y;
			});
		}

		private void DisplaySubredditResults(List<Post> posts)
		{
			var subreddit = _subreddits[posts.First().Subreddit];
			var topUser = subreddit.GetUserWithMostPosts();
			var topPost = subreddit.GetMostUpvotedPost();

			Console.ForegroundColor = ConsoleColor.DarkYellow;
			Console.Write($"\nTotals for Subreddit \"");
			Console.ForegroundColor = ConsoleColor.Gray;
			Console.Write(subreddit.Name);
			Console.ForegroundColor = ConsoleColor.DarkYellow;
			Console.WriteLine($"\"");

			Console.ForegroundColor = ConsoleColor.DarkYellow;
			Console.Write("Top User: ");
			Console.ForegroundColor = ConsoleColor.Gray;
			Console.WriteLine($"{topUser.Item1}");
			Console.ForegroundColor = ConsoleColor.DarkYellow;
			Console.Write("Post Count: ");
			Console.ForegroundColor = ConsoleColor.Gray;
			Console.WriteLine($"{topUser.Item2}");
			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.WriteLine("-=-=-=-=-=-=");
			Console.ForegroundColor = ConsoleColor.DarkYellow;
			Console.Write($"Top Post: ");
			Console.ForegroundColor = ConsoleColor.Gray;
			Console.WriteLine($"{topPost.Item1}");
			Console.ForegroundColor = ConsoleColor.DarkYellow;
			Console.Write($"Upvote Count: ");
			Console.ForegroundColor = ConsoleColor.Gray;
			Console.WriteLine($"{topPost.Item2}");
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.Gray;
		}
	}
}
