namespace TariffSearch.Models
{
    public class SearchFieldSettings
    {
        public List<string> TextFields { get; set; } = new();
        public List<string> TitleFields { get; set; } = new();
        public List<string> AuthorFields { get; set; } = new();
        public List<string> FileNameFields { get; set; } = new();
    }
}
