using TariffSearch.Models;

namespace TariffSearch.Services
{
    public interface IPdfService
    {
        byte[] GenerateDocumentPdf(DocumentDto document);
    }
}