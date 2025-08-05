using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TariffSearch.Models
{
    public class Document
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public string? Title { get; set; }

        public string? Author { get; set; }

        public string? Creator { get; set; }

        public string? Producer { get; set; }

        public string? Subject { get; set; }

        public string? CreationDate { get; set; }

        public string? ModifiedDate { get; set; }

        public int PageCount { get; set; }
    }
}
