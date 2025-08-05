using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using TariffSearch.Models;
using TariffSearch.Services;

namespace TariffSearch.Controllers
{
    /// <summary>
    /// Controller responsible for document search operations and PDF generation
    /// Provides endpoints for searching documents with downloadable PDF links
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        #region Private Fields
        
        private readonly ISearchService _searchService;
        private readonly IPdfService _pdfService;
        
        #endregion

        #region Constructor
        
        /// <summary>
        /// Initializes a new instance of the DocumentsController
        /// </summary>
        /// <param name="searchService">Service for document search operations</param>
        /// <param name="pdfService">Service for PDF generation</param>
        public DocumentsController(ISearchService searchService, IPdfService pdfService)
        {
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
            _pdfService = pdfService ?? throw new ArgumentNullException(nameof(pdfService));
        }
        
        #endregion



        #region Public API Endpoints
        
        /// <summary>
        /// Searches documents by specific fields and returns results with download links
        /// </summary>
        [HttpGet("search-with-downloads")]
        public async Task<IActionResult> SearchWithDownloads(
            [FromQuery] string? filename = null,
            [FromQuery] string? author = null,
            [FromQuery] string? content = null,
            [FromQuery] string? sortBy = "relevance")
        {
            try
            {
                // Validate input parameters
                if (string.IsNullOrWhiteSpace(filename) && string.IsNullOrWhiteSpace(author) && string.IsNullOrWhiteSpace(content))
                    return BadRequest("At least one search parameter is required.");

                // Execute search operation
                var results = await _searchService.SearchDocumentsAsync(filename, author, content, sortBy);
                
                // Transform results with download URLs
                var searchResults = TransformToSearchResults(results);

                return Ok(searchResults);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Search error: {ex.Message}");
            }
        }

        /// <summary>
        /// Searches all document fields globally and returns results with download links
        /// </summary>
        [HttpGet("search-all-with-downloads")]
        public async Task<IActionResult> SearchAllWithDownloads(
            [FromQuery] string keyword,
            [FromQuery] string? sortBy = "relevance")
        {
            try
            {
                // Validate keyword parameter
                if (string.IsNullOrWhiteSpace(keyword))
                    return BadRequest("Keyword is required.");

                // Execute global search
                var results = await _searchService.SearchAllFieldsAsync(keyword, sortBy);
                
                // Transform results with download URLs
                var searchResults = TransformToSearchResults(results);

                return Ok(searchResults);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Search error: {ex.Message}");
            }
        }

        /// <summary>
        /// Downloads document as PDF by ID
        /// </summary>
        [HttpGet("download/{id}")]
        public async Task<IActionResult> DownloadDocument(string id)
        {
            try
            {
                // Retrieve document by ID
                var document = await _searchService.GetDocumentByIdAsync(id);
                
                if (document == null)
                    return NotFound("Document not found.");

                // Generate PDF content
                var pdfBytes = _pdfService.GenerateDocumentPdf(document);
                
                // Create download filename
                var fileName = GenerateDownloadFileName(document.FileName, id);

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Download error: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Private Helper Methods
        
        /// <summary>
        /// Transforms documents to search results with download URLs
        /// </summary>
        private List<SearchResultDto> TransformToSearchResults(List<DocumentDto> documents)
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            
            return documents.Select(doc => new SearchResultDto
            {
                Id = doc.Id,
                FileName = doc.FileName,
                Author = doc.Author,
                Title = doc.Title,
                PageCount = doc.PageCount,
                DownloadUrl = $"{baseUrl}/api/documents/download/{doc.Id}"
            }).ToList();
        }
        
        /// <summary>
        /// Generates appropriate filename for PDF download
        /// </summary>
        private static string GenerateDownloadFileName(string? originalFileName, string documentId)
        {
            if (!string.IsNullOrEmpty(originalFileName))
            {
                return originalFileName.Replace(".pdf", "", StringComparison.OrdinalIgnoreCase) + "-content.pdf";
            }
            
            return $"document-{documentId}.pdf";
        }
        
        #endregion

    }
}
