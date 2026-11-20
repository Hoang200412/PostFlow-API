using System;

namespace PostFlow.Models.DTOs.Responses
{
    public sealed record TrendCollectionSummary(
        string Status,
        int VideosFound,
        int VideosNew,
        int SnapshotsWritten,
        int QuotaUnitsUsed,
        int QuotaUsedToday,
        string? Message);

    public sealed record TrendQuotaStatus(
        int UsedToday,
        int DailyBudget,
        bool ProviderQuotaExceeded,
        DateTime QuotaDayStartUtc);
}
