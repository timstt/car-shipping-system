using System.Linq;
using ASTCUSA.Models;
using Xunit;

namespace ASTCUSA.Tests
{
    public class SmokeTests
    {
        [Fact]
        public void Context_connects_to_test_database()
        {
            using (var db = new ImportContext())
            {
                Assert.EndsWith("_tests", db.Database.Connection.Database);
                Assert.True(db.Cars.Count() >= 0);
            }
        }
    }
}
