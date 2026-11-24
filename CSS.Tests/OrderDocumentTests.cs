using System;
using System.Text;
using ASTCUSA.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ASTCUSA.Tests
{
    // Перевірка завантажених документів (OrderDocument.FromUpload) і формат телефону (PhoneNumbers)
    [TestClass]
    public class OrderDocumentTests
    {
        [TestMethod]
        public void FromUpload_RealPdf_IsAccepted()
        {
            var doc = OrderDocument.FromUpload("passport.pdf", Encoding.ASCII.GetBytes("%PDF-1.7 test"));

            Assert.AreEqual("application/pdf", doc.ContentType);
            Assert.AreEqual("passport.pdf", doc.FileName);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void FromUpload_HtmlRenamedToPdf_IsRejected()
        {
            OrderDocument.FromUpload("passport.pdf", Encoding.ASCII.GetBytes("<html><script>alert(1)</script></html>"));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void FromUpload_TooLargeFile_IsRejected()
        {
            var content = new byte[OrderDocument.MaxSize + 1];
            Encoding.ASCII.GetBytes("%PDF").CopyTo(content, 0);
            OrderDocument.FromUpload("big.pdf", content);
        }

        [TestMethod]
        public void PhoneNumbers_Normalize_ToInternationalFormat()
        {
            Assert.AreEqual("+380501112233", PhoneNumbers.Normalize("(050) 111-22-33"));
            Assert.AreEqual("+380501112233", PhoneNumbers.Normalize("+38 050 111 22 33"));
            Assert.IsNull(PhoneNumbers.Normalize("  "));
        }
    }
}
