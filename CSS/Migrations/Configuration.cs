namespace ASTCUSA.Migrations
{
    using System.Data.Entity.Migrations;
    using ASTCUSA.Models;

    // Налаштування Code First Migrations для основної бази (ImportContext).
    // Схема змінюється міграціями з цієї папки, тож наявні дані не видаляються.
    public sealed class Configuration : DbMigrationsConfiguration<ImportContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
            // Ключ збігається з тим, під яким базу створив попередній ініціалізатор,
            // тому наявна база astcusa приймається як уже мігрована до InitialCreate
            ContextKey = "ASTCUSA.Models.ImportContext";
        }

        // Викликається після кожного застосування міграцій: тестові дані додаються лише в порожню базу
        protected override void Seed(ImportContext context)
        {
            ImportInitializer.SeedIfEmpty(context);
        }
    }
}
