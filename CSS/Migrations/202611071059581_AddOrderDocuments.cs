namespace ASTCUSA.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    // Документи, які клієнт надає на запит менеджера (файли зберігаються в базі разом із заявкою),
    // і текст запиту менеджера. Лише додає таблицю й стовпець — наявні дані не змінюються.
    public partial class AddOrderDocuments : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.OrderDocuments",
                c => new
                    {
                        OrderDocumentId = c.Int(nullable: false, identity: true),
                        OrderId = c.Int(nullable: false),
                        FileName = c.String(nullable: false, maxLength: 255),
                        ContentType = c.String(nullable: false, maxLength: 100),
                        Size = c.Int(nullable: false),
                        Content = c.Binary(nullable: false),
                        UploadedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.OrderDocumentId)
                .ForeignKey("dbo.Orders", t => t.OrderId, cascadeDelete: true)
                .Index(t => t.OrderId);
            
            AddColumn("dbo.Orders", "DocumentsRequestNote", c => c.String(maxLength: 1000));
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.OrderDocuments", "OrderId", "dbo.Orders");
            DropIndex("dbo.OrderDocuments", new[] { "OrderId" });
            DropColumn("dbo.Orders", "DocumentsRequestNote");
            DropTable("dbo.OrderDocuments");
        }
    }
}
