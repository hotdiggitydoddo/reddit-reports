using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditReports.Application;
using RedditReports.Application.Abstractions;
using RedditReports.Domain.Abstractions;
using RedditReports.Domain.Models;
using RedditReports.Infrastructure;

namespace RedditReports.ConsoleApp
{
	internal class Program
	{
		static async Task Main(string[] args)
		{
			var builder = new ConfigurationBuilder();
			BuildConfig(builder);

			var host = Host.CreateDefaultBuilder()
				.ConfigureServices((context, services) =>
				{
					services.AddTransient<IRedditReporterService, RedditReporterService>();
					services.AddTransient<IRedditApiClient, RedditApiClient>();
				})
				.Build();
			
			var svc = ActivatorUtilities.CreateInstance<RedditReporterService>(host.Services);
			await svc.GoAsync();

			//var a = new RedditApiClient();

			//string subreddit = "subreddit";
			//string after = null;
			//List<RedditPost> allPosts = new List<RedditPost>();
			//string latestPostId = null;

			////while (true) // Loop continuously for near real-time updates
			//{
			//	//// Fetch a smaller chunk of new posts
			//	var task1 = a.FetchAsync("music");
			//	var task2 = a.FetchAsync("funny");
			//	var task3 = a.FetchAsync("jokes");
			//	var task4 = a.FetchAsync("worldnews");
			//	var task5 = a.FetchAsync("patientgamers");

			//	await Task.WhenAll(task1, task2, task3, task4, task5);
			//	// Wait for the task to complete
			//	var postsChunk = new List<RedditPost>();// await task1;

			//	// Process retrieved posts
			//	foreach (var post in postsChunk)
			//	{
			//		if (allPosts.Count == 0 || post.Id != latestPostId)
			//		{
			//			allPosts.Add(post);
			//		}
			//		latestPostId = post.Id; // Update latestPostId for next iteration
			//	}

			//	// Maintain top 1000 posts (optional)
			//	if (allPosts.Count > 1000)
			//	{
			//		allPosts.RemoveRange(1000, allPosts.Count - 1000);
			//	}

			//	// Update logic based on your requirements (e.g., sorting)
			//	// ...

			//	// Update 'after' for next iteration
			//	after = postsChunk.LastOrDefault()?.After;

			//	// Introduce a delay to avoid overwhelming Reddit's API
			//	int delay = 1;
			//	//await Task.Delay(delay * 1000); // Replace 'delay' with appropriate value
			//}
		}

		private static void BuildConfig(IConfigurationBuilder builder)
		{
			builder.SetBasePath(Directory.GetCurrentDirectory())
				.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
				.AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("HOSTING_ENVIRONMENT") ?? "Production"}.json")
				.AddEnvironmentVariables();
		}
	}
}
