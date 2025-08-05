using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using TariffSearch.Models;

namespace TariffSearch.Services
{
    public class PdfService : IPdfService
    {
        public byte[] GenerateDocumentPdf(DocumentDto document)
        {
            using var stream = new MemoryStream();
            using var writer = new PdfWriter(stream);
            using var pdf = new PdfDocument(writer);
            using var doc = new iText.Layout.Document(pdf);

            // Title
            if (!string.IsNullOrEmpty(document.FileName))
            {
                doc.Add(new Paragraph(document.FileName)
                    .SetFontSize(16)
                    .SetBold()
                    .SetMarginBottom(20));
            }

            // Full content
            if (!string.IsNullOrEmpty(document.Text))
            {
                doc.Add(new Paragraph(document.Text)
                    .SetFontSize(12)
                    .SetTextAlignment(TextAlignment.JUSTIFIED));
            }

            doc.Close();
            return stream.ToArray();
        }
    }
}