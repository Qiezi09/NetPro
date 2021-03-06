﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Newtonsoft.Json;

namespace NetPro.Checker
{
	public static class CheckMiddlewareHandler
	{
		const string DEFAULT_CONTENT_TYPE = "application/json";

		/// <summary>
		/// inclued: EnvCheck ;InfoCheck
		/// </summary>
		/// <param name="app"></param>
		public static void UseCheck(this IApplicationBuilder app)
		{
			app.UseEnvCheck();
			app.UseInfoCheck();
		}

		public static void UseEnvCheck(this IApplicationBuilder app, string path = "/env")
		{
			app.Map(path, s =>
			{
				s.Run(async context =>
				{
					var env = AppEnvironment.GetAppEnvironment();
					context.Response.ContentType = DEFAULT_CONTENT_TYPE;
					await context.Response.WriteAsync(Serialize(env));
				});
			});
		}

		public static void UseInfoCheck(this IApplicationBuilder app, string path = "/info")
		{
			app.Map(path, s =>
			{
				s.Run(async context =>
				{
					var configuration = app.ApplicationServices.GetService(typeof(IConfiguration)) as IConfiguration;
					var info = AppInfo.GetAppInfo(configuration);
					info.RequestHeaders = context.Request.Headers.ToDictionary(kv => kv.Key, kv => kv.Value.First());
					//context.Response.Headers["Content-Type"] = "application/json";
					context.Response.ContentType = DEFAULT_CONTENT_TYPE;
					await context.Response.WriteAsync(Serialize(info));
				});
			});
		}

		[Obsolete("recommended to use IApplicationBuilder.UseCheck")]
		public static void UseHealthCheck(this IApplicationBuilder app, string path = "/health")
		{
			app.Map(path, s =>
			{
				s.Run(async context =>
				{
					HealthCheckRegistry.HealthStatus status = await Task.Run(() => HealthCheckRegistry.GetStatus());

					if (!status.IsHealthy)
					{
						// Return a service unavailable status code if any of the checks fail
						context.Response.StatusCode = 503;
					}
					context.Response.ContentType = DEFAULT_CONTENT_TYPE;
					await context.Response.WriteAsync(JsonConvert.SerializeObject(status));
				});
			});
		}

		public static async Task WriteHealthCheckUIResponse(HttpContext httpContext, HealthReport report)
		{
			httpContext.Response.ContentType = DEFAULT_CONTENT_TYPE;
			if (report != null)
			{
				await httpContext.Response.WriteAsync(CreateFrom(report));
			}
			else
			{
				await httpContext.Response.WriteAsync(Serialize(new { Status = HealthStatus.Degraded.ToString() }));
			}
		}

		private static string CreateFrom(HealthReport report)
		{
			if (report == null) return string.Empty;
			var result = new Dictionary<string, CustomerHealthReport>();
			foreach (var item in report.Entries)
			{
				var entry = new CustomerHealthReport
				{
					Data = item.Value.Data,
					Description = item.Value.Description,
					Duration = item.Value.Duration,
					Status = item.Value.Status.ToString()
				};

				if (item.Value.Exception != null)
				{
					var message = item.Value.Exception?
						.Message
						.ToString();

					entry.Exception = message;
					entry.Description = item.Value.Description ?? message;
				}
				result.Add(item.Key, entry);
			}
			return Serialize(new { Status = report.Status.ToString(), result = result });
		}
		private static string Serialize<T>(T obj)
		{
			return JsonConvert.SerializeObject(obj, new JsonSerializerSettings { Formatting = Formatting.Indented });
		}
	}

	public class CustomerHealthReport
	{
		public IReadOnlyDictionary<string, object> Data { get; set; }
		public string Description { get; set; }
		public TimeSpan Duration { get; set; }
		public string Exception { get; set; }
		public string Status { get; set; }
	}
}
