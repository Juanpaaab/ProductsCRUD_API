using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using ProductsCRUD_API.Dtos;
using ProductsCRUD_API.Models;

namespace ProductsCRUD_API.Repositories;

public class ProductRepository(Db db)
{
    public Task<Product> CreateAsync(ProductCreateDto dto) =>
        db.QuerySingleAsync<Product>("dbo.sp_Product_Create", dto);

    public Task<Product?> GetByIdAsync(int id) =>
        db.QuerySingleOrDefaultAsync<Product>("dbo.sp_Product_GetById", new { Id = id });

    public async Task<PagedResult<Product>> GetAllAsync(int page, int pageSize)
    {
        using var reader = await db.QueryMultipleAsync("dbo.sp_Product_GetAll", new { PageNumber = page, PageSize = pageSize });
        var items = await reader.ReadAsync<Product>();
        var totalCount = await reader.ReadSingleAsync<int>();

        return new PagedResult<Product>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public Task<Product?> UpdateAsync(int id, ProductUpdateDto dto) =>
        db.QuerySingleOrDefaultAsync<Product>("dbo.sp_Product_Update", new { Id = id, dto.Name, dto.Description, dto.Price });

    public async Task<bool> DeleteAsync(int id)
    {
        var rowsAffected = await db.QuerySingleAsync<int>("dbo.sp_Product_Delete", new { Id = id });
        return rowsAffected > 0;
    }

    public Task<int> BulkCreateAsync(IEnumerable<ProductCreateDto> products)
    {
        var table = new DataTable();
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Description", typeof(string));
        table.Columns.Add("Price", typeof(decimal));

        foreach (var product in products)
        {
            table.Rows.Add(product.Name, (object?)product.Description ?? DBNull.Value, product.Price);
        }

        var parameters = new { Products = table.AsTableValuedParameter("dbo.ProductTableType") };
        return db.QuerySingleAsync<int>("dbo.sp_Product_BulkCreate", parameters);
    }

    public async Task<int> BulkCreateViaStagingAsync(IEnumerable<ProductCreateDto> products)
    {
        var batchId = Guid.NewGuid();

        var table = new DataTable();
        table.Columns.Add("BatchId", typeof(Guid));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Description", typeof(string));
        table.Columns.Add("Price", typeof(decimal));

        foreach (var product in products)
        {
            table.Rows.Add(batchId, product.Name, (object?)product.Description ?? DBNull.Value, product.Price);
        }

        using var connection = db.CreateConnection();
        await connection.OpenAsync();
        using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

        try
        {
            using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
            {
                bulkCopy.DestinationTableName = "dbo.Products_Staging";
                bulkCopy.BatchSize = 5000;
                bulkCopy.BulkCopyTimeout = 300;
                bulkCopy.ColumnMappings.Add("BatchId", "BatchId");
                bulkCopy.ColumnMappings.Add("Name", "Name");
                bulkCopy.ColumnMappings.Add("Description", "Description");
                bulkCopy.ColumnMappings.Add("Price", "Price");

                await bulkCopy.WriteToServerAsync(table);
            }

            var inserted = await connection.QuerySingleAsync<int>(
                "dbo.sp_Product_BulkCreate_FromStaging",
                new { BatchId = batchId },
                transaction: transaction,
                commandType: CommandType.StoredProcedure);

            await transaction.CommitAsync();
            return inserted;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
