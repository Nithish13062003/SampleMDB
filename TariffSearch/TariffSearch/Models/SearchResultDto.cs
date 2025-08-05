namespace TariffSearch.Models
{
    public class SearchResultDto
    {
        public string? Id { get; set; }
        public string? FileName { get; set; }
        public string? Author { get; set; }
        public string? Title { get; set; }
        public int? PageCount { get; set; }
        public string? DownloadUrl { get; set; }
    }
}