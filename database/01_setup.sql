
CREATE TABLE dbo.Products (
        Id          INT             IDENTITY(1,1)             NOT NULL,
        Name        NVARCHAR(100)                             NOT NULL,
        Description NVARCHAR(500)                                 NULL,
        Price       DECIMAL(18,2)                             NOT NULL,
        CreatedDate DATETIMEOFFSET  DEFAULT SYSUTCDATETIME()   NOT NULL,
        IsActive    BIT             DEFAULT 1                 NOT NULL,
        CONSTRAINT PK_Products PRIMARY KEY (Id),
        CONSTRAINT CK_Products_Price CHECK (Price >= 0)
    );

    --Staging table for bulk insert
    CREATE TABLE dbo.Products_Staging (
        BatchId     UNIQUEIDENTIFIER NOT NULL,
        Name        NVARCHAR(100)    NOT NULL,
        Description NVARCHAR(500)        NULL,
        Price       DECIMAL(18,2)    NOT NULL
    );
    --Index for faster retrieval of staging data by BatchId
    CREATE INDEX IX_ProductsStaging_BatchId ON dbo.Products_Staging (BatchId);
    

    --Type for bulk insert
    CREATE TYPE dbo.ProductTableType AS TABLE (
        Name        NVARCHAR(100)    NOT NULL,
        Description NVARCHAR(500)        NULL,
        Price       DECIMAL(18,2)    NOT NULL
    );


--SPs

CREATE OR ALTER PROCEDURE dbo.sp_Product_Create
    @Name        NVARCHAR(100),
    @Description NVARCHAR(500) = NULL,
    @Price       DECIMAL(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        INSERT INTO dbo.Products (Name, Description, Price)
        VALUES (@Name, @Description, @Price);

        SELECT Id, Name, Description, Price, CreatedDate, IsActive
        FROM   dbo.Products
        WHERE  Id = SCOPE_IDENTITY();
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH
END;

CREATE OR ALTER PROCEDURE dbo.sp_Product_GetById
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Name, Description, Price, CreatedDate, IsActive
    FROM   dbo.Products
    WHERE  Id = @Id
      AND  IsActive = 1;
END;


CREATE OR ALTER PROCEDURE dbo.sp_Product_GetAll
    @PageNumber INT = 1,
    @PageSize   INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Name, Description, Price, CreatedDate, IsActive
    FROM   dbo.Products
    WHERE  IsActive = 1
    ORDER BY CreatedDate DESC, Id DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    SELECT COUNT(*) AS TotalCount
    FROM   dbo.Products
    WHERE  IsActive = 1;
END:

CREATE OR ALTER PROCEDURE dbo.sp_Product_Update
    @Id          INT,
    @Name        NVARCHAR(100),
    @Description NVARCHAR(500) = NULL,
    @Price       DECIMAL(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        UPDATE dbo.Products
        SET    Name        = @Name,
               Description = @Description,
               Price       = @Price
        WHERE  Id = @Id
          AND  IsActive = 1;

        SELECT Id, Name, Description, Price, CreatedDate, IsActive
        FROM   dbo.Products
        WHERE  Id = @Id
          AND  IsActive = 1;
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH
END;

CREATE OR ALTER PROCEDURE dbo.sp_Product_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        UPDATE dbo.Products
        SET    IsActive = 0
        WHERE  Id = @Id
          AND  IsActive = 1;

        SELECT @@ROWCOUNT AS RowsAffected;
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH
END;

CREATE OR ALTER PROCEDURE dbo.sp_Product_BulkCreate
    @Products dbo.ProductTableType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        INSERT INTO dbo.Products (Name, Description, Price)
        SELECT Name, Description, Price
        FROM   @Products;

        SELECT @@ROWCOUNT AS RowsAffected;
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH
END;

CREATE OR ALTER PROCEDURE dbo.sp_Product_BulkCreate_FromStaging
    @BatchId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        INSERT INTO dbo.Products (Name, Description, Price)
        SELECT Name, Description, Price
        FROM   dbo.Products_Staging
        WHERE  BatchId = @BatchId;

        DECLARE @RowsInserted INT = @@ROWCOUNT;

        DELETE FROM dbo.Products_Staging
        WHERE  BatchId = @BatchId;

        SELECT @RowsInserted AS RowsAffected;
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH
END;
