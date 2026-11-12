namespace ASTCUSA.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    // Ставка іншого учасника, яка перебила нашу. Лише додає стовпець, що допускає NULL, —
    // наявні ставки не змінюються.
    public partial class AddCompetitorBids : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.AuctionBids", "CompetitorAmount", c => c.Double());
        }
        
        public override void Down()
        {
            DropColumn("dbo.AuctionBids", "CompetitorAmount");
        }
    }
}
