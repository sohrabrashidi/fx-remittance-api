using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Remittance.Api.Tests;

public class TransferFlowTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Quote_then_transfer_then_payout()
    {
        var quote = await CreateQuote(250m);

        var transfer = await PostTransfer(quote, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Created, transfer.StatusCode);

        var body = await transfer.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetString();
        Assert.Equal("Created", body.GetProperty("status").GetString());

        foreach (var status in new[] { "Funded", "SentToPartner", "Paid" })
        {
            var response = await ChangeStatus(id!, status);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var final = await _client.GetFromJsonAsync<JsonElement>($"/transfers/{id}");
        Assert.Equal("Paid", final.GetProperty("status").GetString());
        Assert.Equal(3, final.GetProperty("history").GetArrayLength());
    }

    [Fact]
    public async Task Retrying_with_the_same_key_returns_the_original_transfer()
    {
        var quote = await CreateQuote(100m);
        var key = Guid.NewGuid().ToString();

        var first = await PostTransfer(quote, key);
        var second = await PostTransfer(quote, key);

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.True(second.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Same_quote_cannot_fund_two_transfers()
    {
        var quote = await CreateQuote(100m);

        await PostTransfer(quote, Guid.NewGuid().ToString());
        var second = await PostTransfer(quote, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Transfer_without_idempotency_key_is_rejected()
    {
        var quote = await CreateQuote(100m);

        var response = await PostTransfer(quote, key: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unsupported_corridor_returns_problem_details()
    {
        var response = await _client.PostAsJsonAsync("/quotes", new { source = "KWD", target = "USD", amount = 100 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("corridor_not_supported", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Status_change_requires_api_key()
    {
        var response = await _client.PostAsJsonAsync(
            $"/transfers/{Guid.NewGuid()}/status",
            new { status = "Funded" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<string> CreateQuote(decimal amount)
    {
        var response = await _client.PostAsJsonAsync("/quotes", new { source = "KWD", target = "INR", amount });
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetString()!;
    }

    private Task<HttpResponseMessage> PostTransfer(string quoteId, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/transfers")
        {
            Content = JsonContent.Create(new
            {
                quoteId,
                sender = new { fullName = "Ahmed Ali", country = "KW" },
                recipient = new { fullName = "Priya Nair", country = "IN", accountNumber = "50100012345678" },
            }),
        };

        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> ChangeStatus(string transferId, string status)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/transfers/{transferId}/status")
        {
            Content = JsonContent.Create(new { status }),
        };
        request.Headers.Add("X-Api-Key", "change-me");
        return _client.SendAsync(request);
    }
}
