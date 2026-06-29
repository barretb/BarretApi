using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace BarretApi.Api.Features.SocialPost;

public sealed class AddTipOfDayEndpoint(
	TipOfDayService tipOfDayService,
	ILogger<AddTipOfDayEndpoint> logger)
	: Endpoint<AddTipOfDayRequest, AddTipOfDayResponse>
{
	public override void Configure()
	{
		Post("/api/social-posts/tips");

		Summary(s =>
		{
			s.Summary = "Add a tip of the day";
			s.Description = "Adds a new categorized tip record to Azure Table Storage.";
			s.ExampleRequest = new AddTipOfDayRequest
			{
				Category = "dotnet",
				Tip = "Use ConfigureAwait(false) in reusable library code when you do not need to resume on the captured context.",
				MoreInfoUrl = "https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/"
			};
			s.Responses[201] = "Tip was added.";
			s.Responses[400] = "Request validation failed.";
			s.Responses[401] = "Missing or invalid X-Api-Key.";
		});
	}

	public override async Task HandleAsync(AddTipOfDayRequest req, CancellationToken ct)
	{
		logger.LogInformation("Add tip of the day request received for category {Category}", req.Category);

		var record = await tipOfDayService.AddTipAsync(req.Category!, req.Tip!, req.MoreInfoUrl, ct);
		var response = new AddTipOfDayResponse
		{
			TipId = record.TipId,
			Category = record.Category,
			Tip = record.Tip,
			MoreInfoUrl = record.MoreInfoUrl,
			LastPostedDate = record.LastPostedDate,
			CreatedAtUtc = record.CreatedAtUtc
		};

		await Send.ResponseAsync(response, 201, ct);
	}
}
