using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using TariffSearch.Models;
using TariffSearch.Services;

namespace TariffSearch.Services
{
    /// <summary>
    /// Service responsible for document search operations using MongoDB Atlas Search
    /// Handles search across multiple collections with fuzzy matching and sorting capabilities
    /// </summary>
    public class SearchService : ISearchService
    {
        #region Private Fields and Constants
        
        private readonly IMongoCollection<Document> _documents;
        private readonly IMongoCollection<Document> _documents2;
        private readonly List<string> _searchableFields;
        
        // MongoDB Atlas Search index names
        private const string INDEX_01 = "index01";
        private const string INDEX_02 = "index02";
        private const string DOCUMENTS_2_COLLECTION = "Documents_2";
        
        #endregion

        #region Constructor
        
        /// <summary>
        /// Initializes a new instance of SearchService with MongoDB configuration
        /// </summary>
        /// <param name="settings">MongoDB configuration settings</param>
        public SearchService(IOptions<MongoDbSettings> settings)
        {
            var mongoSettings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            
            // Initialize MongoDB client and collections
            var client = new MongoClient(mongoSettings.ConnectionString);
            var database = client.GetDatabase(mongoSettings.DatabaseName);
            
            _documents = database.GetCollection<Document>(mongoSettings.CollectionName);
            _documents2 = database.GetCollection<Document>(DOCUMENTS_2_COLLECTION);
            _searchableFields = mongoSettings.SearchableFields ?? new List<string>();
        }
        
        #endregion

        #region Public Methods
        
        /// <summary>
        /// Searches documents using specific field criteria with fuzzy matching
        /// </summary>
        /// <param name="filename">Optional filename to search for</param>
        /// <param name="author">Optional author name to search for</param>
        /// <param name="content">Optional content text to search for</param>
        /// <param name="sortBy">Sort criteria: 'relevance', 'filename', or 'pagecount'</param>
        /// <returns>List of matching documents with metadata</returns>
        public async Task<List<DocumentDto>> SearchDocumentsAsync(string? filename = null, string? author = null, string? content = null, string? sortBy = "relevance")
        {
            // Build search clauses based on provided parameters
            var searchClauses = BuildSearchClauses(filename, author, content);
            
            // Return empty result if no search criteria provided
            if (searchClauses.Count == 0)
                return new List<DocumentDto>();

            // Execute the search pipeline across collections
            return await ExecuteSearchPipeline(searchClauses, sortBy);
        }

        /// <summary>
        /// Performs global search across all configured searchable fields
        /// </summary>
        /// <param name="keyword">Keyword to search across all document fields</param>
        /// <param name="sortBy">Sort criteria: 'relevance', 'filename', or 'pagecount'</param>
        /// <returns>List of matching documents with metadata</returns>
        public async Task<List<DocumentDto>> SearchAllFieldsAsync(string keyword, string? sortBy = "relevance")
        {
            // Validate keyword parameter
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<DocumentDto>();

            // Create global search clause for all searchable fields
            var globalSearchClause = new BsonDocument("text", new BsonDocument
            {
                { "query", keyword },
                { "path", new BsonArray(_searchableFields) },
                { "fuzzy", new BsonDocument { { "maxEdits", 2 } } }
            });

            // Execute search pipeline with global search clause
            return await ExecuteSearchPipeline(new BsonArray { globalSearchClause }, sortBy);
        }

        /// <summary>
        /// Retrieves a specific document by its unique identifier
        /// Searches across both document collections
        /// </summary>
        /// <param name="id">Unique document identifier (ObjectId string)</param>
        /// <returns>Document DTO if found, null otherwise</returns>
        public async Task<DocumentDto?> GetDocumentByIdAsync(string id)
        {
            try
            {
                // Parse string ID to ObjectId
                var objectId = ObjectId.Parse(id);
                var filter = Builders<Document>.Filter.Eq("_id", objectId);
                
                // Search in primary collection first
                var document = await _documents.Find(filter).FirstOrDefaultAsync();
                
                // If not found, search in secondary collection
                if (document == null)
                {
                    document = await _documents2.Find(filter).FirstOrDefaultAsync();
                }
                
                // Return null if document not found in either collection
                if (document == null) return null;
                
                // Map to DTO and return
                return MapDocumentToDto(document);
            }
            catch (FormatException)
            {
                // Invalid ObjectId format
                return null;
            }
        }
        
        #endregion
        
        #region Private Helper Methods

        /// <summary>
        /// Builds MongoDB Atlas Search clauses based on provided search parameters
        /// </summary>
        /// <param name="filename">Filename search term</param>
        /// <param name="author">Author search term</param>
        /// <param name="content">Content search term</param>
        /// <returns>Array of search clauses for MongoDB aggregation</returns>
        private static BsonArray BuildSearchClauses(string? filename, string? author, string? content)
        {
            var searchClauses = new BsonArray();

            // Add filename search clause if provided
            if (!string.IsNullOrWhiteSpace(filename))
            {
                searchClauses.Add(CreateTextSearchClause(filename, "FileName"));
            }

            // Add author search clause (searches both Author and Creator fields)
            if (!string.IsNullOrWhiteSpace(author))
            {
                searchClauses.Add(CreateTextSearchClause(author, new[] { "Author", "Creator" }));
            }

            // Add content search clause (searches Text, Title, and Subject fields)
            if (!string.IsNullOrWhiteSpace(content))
            {
                searchClauses.Add(CreateTextSearchClause(content, new[] { "Text", "Title", "Subject" }));
            }

            return searchClauses;
        }

        /// <summary>
        /// Creates a text search clause for a single field path with fuzzy matching
        /// </summary>
        /// <param name="query">Search query text</param>
        /// <param name="path">Field path to search in</param>
        /// <returns>MongoDB text search clause</returns>
        private static BsonDocument CreateTextSearchClause(string query, string path)
        {
            return new BsonDocument("text", new BsonDocument
            {
                { "query", query },
                { "path", path },
                { "fuzzy", new BsonDocument { { "maxEdits", 2 } } }
            });
        }

        /// <summary>
        /// Creates a text search clause for multiple field paths with fuzzy matching
        /// </summary>
        /// <param name="query">Search query text</param>
        /// <param name="paths">Array of field paths to search in</param>
        /// <returns>MongoDB text search clause</returns>
        private static BsonDocument CreateTextSearchClause(string query, string[] paths)
        {
            return new BsonDocument("text", new BsonDocument
            {
                { "query", query },
                { "path", new BsonArray(paths) },
                { "fuzzy", new BsonDocument { { "maxEdits", 2 } } }
            });
        }

        /// <summary>
        /// Executes the complete search pipeline across both document collections
        /// </summary>
        /// <param name="searchClauses">Array of search clauses to execute</param>
        /// <param name="sortBy">Sort criteria for results</param>
        /// <returns>List of matching documents</returns>
        private async Task<List<DocumentDto>> ExecuteSearchPipeline(BsonArray searchClauses, string? sortBy)
        {
            // Create search stages for both collections
            var searchStage1 = CreateSearchStage(INDEX_01, searchClauses);
            var searchStage2 = CreateSearchStage(INDEX_02, searchClauses);
            
            // Create union stage to combine results from both collections
            var unionStage = CreateUnionStage(searchStage2);
            
            // Create projection and sorting stages
            var projectStage = CreateProjectStage();
            var sortStage = CreateSortStage(sortBy);

            // Build and execute aggregation pipeline
            var pipeline = new[] { searchStage1, unionStage, projectStage, sortStage };
            var results = await _documents.Aggregate<BsonDocument>(pipeline).ToListAsync();

            // Map results to DTOs
            return MapToDocumentDto(results);
        }

        /// <summary>
        /// Creates a MongoDB Atlas Search stage for the aggregation pipeline
        /// </summary>
        /// <param name="indexName">Name of the search index to use</param>
        /// <param name="searchClauses">Search clauses to include</param>
        /// <returns>MongoDB search stage</returns>
        private static BsonDocument CreateSearchStage(string indexName, BsonArray searchClauses)
        {
            return new BsonDocument("$search", new BsonDocument
            {
                { "index", indexName },
                { "compound", new BsonDocument { { "should", searchClauses } } }
            });
        }

        /// <summary>
        /// Creates a union stage to combine results from the secondary collection
        /// </summary>
        /// <param name="searchStage2">Search stage for the secondary collection</param>
        /// <returns>MongoDB union stage</returns>
        private BsonDocument CreateUnionStage(BsonDocument searchStage2)
        {
            return new BsonDocument("$unionWith", new BsonDocument
            {
                { "coll", DOCUMENTS_2_COLLECTION },
                { "pipeline", new BsonArray { searchStage2 } }
            });
        }

        /// <summary>
        /// Creates a projection stage to select specific fields and include search score
        /// </summary>
        /// <returns>MongoDB projection stage</returns>
        private static BsonDocument CreateProjectStage()
        {
            return new BsonDocument("$project", new BsonDocument
            {
                { "_id", 1 },
                { "FileName", 1 },
                { "Text", 1 },
                { "Creator", 1 },
                { "Author", 1 },
                { "Title", 1 },
                { "Subject", 1 },
                { "Producer", 1 },
                { "PageCount", 1 },
                { "score", new BsonDocument { { "$meta", "searchScore" } } }
            });
        }

        /// <summary>
        /// Creates a sort stage based on the specified sort criteria
        /// </summary>
        /// <param name="sortBy">Sort criteria: 'pagecount', 'filename', or default 'relevance'</param>
        /// <returns>MongoDB sort stage</returns>
        private static BsonDocument CreateSortStage(string? sortBy)
        {
            return sortBy?.ToLower() switch
            {
                "pagecount" => new BsonDocument("$sort", new BsonDocument("PageCount", -1)),
                "filename" => new BsonDocument("$sort", new BsonDocument("FileName", 1)),
                _ => new BsonDocument("$sort", new BsonDocument("score", -1)) // Default: relevance
            };
        }

        /// <summary>
        /// Maps MongoDB BsonDocument results to DocumentDto objects
        /// </summary>
        /// <param name="results">List of BsonDocument results from MongoDB</param>
        /// <returns>List of DocumentDto objects</returns>
        private static List<DocumentDto> MapToDocumentDto(List<BsonDocument> results)
        {
            return results.Select(doc => new DocumentDto
            {
                Id = GetStringValue(doc, "_id"),
                FileName = GetStringValue(doc, "FileName"),
                Text = GetStringValue(doc, "Text"),
                Creator = GetStringValue(doc, "Creator"),
                Author = GetStringValue(doc, "Author"),
                Title = GetStringValue(doc, "Title"),
                Subject = GetStringValue(doc, "Subject"),
                Producer = GetStringValue(doc, "Producer"),
                PageCount = doc.GetValue("PageCount", 0).ToInt32()
            }).ToList();
        }

        /// <summary>
        /// Maps a Document entity to DocumentDto
        /// </summary>
        /// <param name="document">Document entity from MongoDB</param>
        /// <returns>DocumentDto object</returns>
        private static DocumentDto MapDocumentToDto(Document document)
        {
            return new DocumentDto
            {
                Id = document.Id,
                FileName = document.FileName,
                Text = document.Text,
                Creator = document.Creator,
                Author = document.Author,
                Title = document.Title,
                Subject = document.Subject,
                Producer = document.Producer,
                PageCount = document.PageCount
            };
        }

        /// <summary>
        /// Safely extracts string values from BsonDocument, handling different BSON types
        /// </summary>
        /// <param name="doc">BsonDocument to extract value from</param>
        /// <param name="fieldName">Name of the field to extract</param>
        /// <returns>String value or empty string if null/missing</returns>
        private static string GetStringValue(BsonDocument doc, string fieldName)
        {
            var value = doc.GetValue(fieldName, BsonNull.Value);
            
            // Handle null values
            if (value.IsBsonNull) return string.Empty;
            
            // Handle ObjectId values (like _id field)
            if (value.BsonType == BsonType.ObjectId) return value.AsObjectId.ToString();
            
            // Handle string values
            return value.AsString;
        }
        
        #endregion
    }
}