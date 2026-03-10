using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PepperBot.Application.Interfaces;
using PepperBot.Domain;

namespace PepperBot.Infrastructure.Pepper;

public class PepperClient : IPepperClient
{
    private const string Url =
        "https://auth.pepper.ru/v4/home/new?per_page=10&page=1&type=new&fields=id%2Ctitle%2Ccurrent_price%2Cshipping_and_handling%2Cretail_price%2Cpercent_off%2Ctop_deal%2Cimage_medium%2Ccreated_at%2Ccreated_at_in_millis%2Ccomments_count%2Clife_time_hotness%2Cuser%7Bid%2Clogin%2Cimage_medium%2Ccurrent_title%2Cdisplay_title%2Cdisplay_title_icon%7D%2Cstore%7Bpermalink%2Cname%2Cimage%7D%2Cpermalink%2Cposts_count%2Cvote_value%2Cdeal_types%2Cview_count%2Cforum%7Bforum_type%2Cname%2Cpermalink%7D%2Ctag_color_code%2C+dark_theme_tag_color_code%2Cworkflow_state%2Cdisplay_hotness_icon%2Creferral_state%2Ccoupon_code%2Cdeal_url%2Ccomment_preview%2Cis_nsfw%2Cdescription";

    private const string ClientHeaderName = "x-desidime-client";
    private const string DefaultClientHeaderValue =
        "6c266d5dc070c811e61f611ddd970a3defb493a23c50ee01f32eb9b0cfe46cd8";

    private readonly HttpClient _httpClient;
    private readonly ILogger<PepperClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public PepperClient(ILogger<PepperClient> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://auth.pepper.ru")
        };
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        var clientHeader = Environment.GetEnvironmentVariable("PEPPER_CLIENT_HEADER");
        _httpClient.DefaultRequestHeaders.Add(ClientHeaderName,
            string.IsNullOrWhiteSpace(clientHeader) ? DefaultClientHeaderValue : clientHeader);

        _httpClient.DefaultRequestHeaders.Host = "auth.pepper.ru";
    }

    public async Task<IReadOnlyList<Deal>> GetLatestDealsAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Url);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var dto = await JsonSerializer.DeserializeAsync<PepperResponse>(stream, JsonOptions, cancellationToken);

        if (dto?.Deals is null)
        {
            _logger.LogWarning("Не удалось распарсить ответ Pepper");
            return Array.Empty<Deal>();
        }

        return dto.Deals.Select(MapToDeal).ToArray();
    }

    private static Deal MapToDeal(PepperDeal dto)
    {
        return new Deal
        {
            Id = dto.Id,
            Title = dto.Title ?? string.Empty,
            CurrentPrice = dto.CurrentPrice,
            RetailPrice = dto.RetailPrice,
            PercentOff = dto.PercentOff,
            CreatedAt = dto.CreatedAt ?? DateTimeOffset.FromUnixTimeMilliseconds(dto.CreatedAtInMillis).UtcDateTime,
            CreatedAtInMillis = dto.CreatedAtInMillis,
            DealUrl = dto.DealUrl ?? string.Empty,
            Permalink = dto.Permalink ?? string.Empty,
            StoreName = dto.Store?.Name ?? string.Empty,
            StorePermalink = dto.Store?.Permalink ?? string.Empty
        };
    }

    private sealed class PepperResponse
    {
        [JsonPropertyName("deals")]
        public List<PepperDeal>? Deals { get; set; }
    }

    private sealed class PepperDeal
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("current_price")]
        public decimal? CurrentPrice { get; set; }

        [JsonPropertyName("retail_price")]
        public decimal? RetailPrice { get; set; }

        [JsonPropertyName("percent_off")]
        public int? PercentOff { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("created_at_in_millis")]
        public long CreatedAtInMillis { get; set; }

        [JsonPropertyName("deal_url")]
        public string? DealUrl { get; set; }

        [JsonPropertyName("permalink")]
        public string? Permalink { get; set; }

        [JsonPropertyName("store")]
        public PepperStore? Store { get; set; }
    }

    private sealed class PepperStore
    {
        [JsonPropertyName("permalink")]
        public string? Permalink { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}

