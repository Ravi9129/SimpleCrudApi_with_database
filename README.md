-- Create database
CREATE DATABASE SimpleCrudDB;
GO

USE SimpleCrudDB;
GO

-- Create Products table
CREATE TABLE Products (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500),
    Price DECIMAL(18,2) NOT NULL,
    StockQuantity INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    IsActive BIT NOT NULL DEFAULT 1
);
GO

-- Create AuditLog table
CREATE TABLE AuditLog (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TableName NVARCHAR(100) NOT NULL,
    Action NVARCHAR(20) NOT NULL, -- INSERT, UPDATE, DELETE
    RecordId INT NOT NULL,
    OldValues NVARCHAR(MAX),
    NewValues NVARCHAR(MAX),
    ChangedBy NVARCHAR(100),
    ChangedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);
GO

-- Create function to check product existence
CREATE FUNCTION dbo.ProductExists(@ProductId INT)
RETURNS BIT
AS
BEGIN
    DECLARE @Exists BIT = 0;
    
    IF EXISTS (SELECT 1 FROM Products WHERE Id = @ProductId AND IsActive = 1)
        SET @Exists = 1;
        
    RETURN @Exists;
END;
GO

-- Create stored procedure for inserting products
CREATE PROCEDURE dbo.InsertProduct
    @Name NVARCHAR(100),
    @Description NVARCHAR(500),
    @Price DECIMAL(18,2),
    @StockQuantity INT,
    @CreatedBy NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        INSERT INTO Products (Name, Description, Price, StockQuantity)
        VALUES (@Name, @Description, @Price, @StockQuantity);
        
        DECLARE @ProductId INT = SCOPE_IDENTITY();
        
        -- Audit log
        INSERT INTO AuditLog (TableName, Action, RecordId, NewValues, ChangedBy)
        VALUES ('Products', 'INSERT', @ProductId, 
                JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY('{}', '$.Name', @Name), '$.Description', @Description), '$.Price', @Price), '$.StockQuantity', @StockQuantity),
                @CreatedBy);
        
        COMMIT TRANSACTION;
        
        SELECT @ProductId AS Id;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        THROW;
    END CATCH
END;
GO

-- Create stored procedure for updating products
CREATE PROCEDURE dbo.UpdateProduct
    @Id INT,
    @Name NVARCHAR(100),
    @Description NVARCHAR(500),
    @Price DECIMAL(18,2),
    @StockQuantity INT,
    @UpdatedBy NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Get old values for audit
        DECLARE @OldValues NVARCHAR(MAX) = (
            SELECT Name, Description, Price, StockQuantity
            FROM Products
            WHERE Id = @Id
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
        );
        
        UPDATE Products
        SET 
            Name = @Name,
            Description = @Description,
            Price = @Price,
            StockQuantity = @StockQuantity,
            UpdatedAt = GETDATE()
        WHERE Id = @Id;
        
        -- Audit log
        DECLARE @NewValues NVARCHAR(MAX) = JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY('{}', '$.Name', @Name), '$.Description', @Description), '$.Price', @Price), '$.StockQuantity', @StockQuantity);
        
        INSERT INTO AuditLog (TableName, Action, RecordId, OldValues, NewValues, ChangedBy)
        VALUES ('Products', 'UPDATE', @Id, @OldValues, @NewValues, @UpdatedBy);
        
        COMMIT TRANSACTION;
        
        SELECT @@ROWCOUNT AS RowsAffected;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        THROW;
    END CATCH
END;
GO

-- Create stored procedure for soft deleting products
CREATE PROCEDURE dbo.DeleteProduct
    @Id INT,
    @DeletedBy NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Get old values for audit
        DECLARE @OldValues NVARCHAR(MAX) = (
            SELECT Name, Description, Price, StockQuantity
            FROM Products
            WHERE Id = @Id
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
        );
        
        UPDATE Products
        SET 
            IsActive = 0,
            UpdatedAt = GETDATE()
        WHERE Id = @Id;
        
        -- Audit log
        INSERT INTO AuditLog (TableName, Action, RecordId, OldValues, ChangedBy)
        VALUES ('Products', 'DELETE', @Id, @OldValues, @DeletedBy);
        
        COMMIT TRANSACTION;
        
        SELECT @@ROWCOUNT AS RowsAffected;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        THROW;
    END CATCH
END;
GO

-- Create stored procedure for getting product by ID
CREATE PROCEDURE dbo.GetProductById
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT Id, Name, Description, Price, StockQuantity, CreatedAt, UpdatedAt
    FROM Products
    WHERE Id = @Id AND IsActive = 1;
END;
GO

-- Create stored procedure for getting all active products
CREATE PROCEDURE dbo.GetAllProducts
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT Id, Name, Description, Price, StockQuantity, CreatedAt, UpdatedAt
    FROM Products
    WHERE IsActive = 1
    ORDER BY Name;
END;
GO
