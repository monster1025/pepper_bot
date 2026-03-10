namespace PepperBot.Domain;

public class Deal
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal? CurrentPrice { get; set; }
    public decimal? RetailPrice { get; set; }
    public int? PercentOff { get; set; }
    public DateTime CreatedAt { get; set; }
    public long CreatedAtInMillis { get; set; }
    public string DealUrl { get; set; } = string.Empty;
    public string Permalink { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string StorePermalink { get; set; } = string.Empty;
}

