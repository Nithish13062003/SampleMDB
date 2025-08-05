using TariffSearch.Models;

namespace TariffSearch.Services
{
    public interface ISearchService
    {
        Task<List<DocumentDto>> SearchDocumentsAsync(string? filename = null, string? author = null, string? content = null, string? sortBy = "relevance");
        Task<List<DocumentDto>> SearchAllFieldsAsync(string keyword, string? sortBy = "relevance");
        Task<DocumentDto?> GetDocumentByIdAsync(string id);
    }
}
