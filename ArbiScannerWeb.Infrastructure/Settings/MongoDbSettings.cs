namespace ArbiScannerWeb.Infrastructure.Settings
{
    public class MongoDbSettings
    {
        public const string SectionName = "MongoDb";

        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = "SwapArbitrage";
        public string CurrentSpreadsCollection { get; set; } = "CurrentSpreads";
        public string SpreadsTickerCollection { get; set; } = "SpreadsTicker";
        public string SpreadStatsSnapshotsCollection { get; set; } = "SpreadStatsSnapshots";
        /// <summary>How many days ticker history is retained. Older documents are auto-deleted by MongoDB TTL index.</summary>
        public int TickerRetentionDays { get; set; } = 30;
    }
}
