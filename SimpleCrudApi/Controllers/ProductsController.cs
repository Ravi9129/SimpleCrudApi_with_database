using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SimpleCrudApi.Models;

namespace SimpleCrudApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public ProductsController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection")!;
        }

        // GET: api/products
        [HttpGet]
        public async Task<IActionResult> GetAllProducts()
        {
            using var connection = new SqlConnection(_connectionString);
            var products = await connection.QueryAsync<Product>("dbo.GetAllProducts", commandType: System.Data.CommandType.StoredProcedure);
            return Ok(products);
        }

        // GET: api/products/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProduct(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            var product = await connection.QueryFirstOrDefaultAsync<Product>(
                "dbo.GetProductById",
                new { Id = id },
                commandType: System.Data.CommandType.StoredProcedure);

            if (product == null)
            {
                return NotFound();
            }

            return Ok(product);
        }

        // POST: api/products
        [HttpPost]
        public async Task<IActionResult> CreateProduct([FromBody] Product product, [FromQuery] string? changedBy = null)
        {
            using var connection = new SqlConnection(_connectionString);
            var productId = await connection.ExecuteScalarAsync<int>(
                "dbo.InsertProduct",
                new
                {
                    product.Name,
                    product.Description,
                    product.Price,
                    product.StockQuantity,
                    CreatedBy = changedBy
                },
                commandType: System.Data.CommandType.StoredProcedure);

            return CreatedAtAction(nameof(GetProduct), new { id = productId }, product);
        }

        // PUT: api/products/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] Product product, [FromQuery] string? changedBy = null)
        {
            using var connection = new SqlConnection(_connectionString);

            // Check if product exists using the function
            var exists = await connection.ExecuteScalarAsync<bool>(
                "SELECT dbo.ProductExists(@ProductId)",
                new { ProductId = id });

            if (!exists)
            {
                return NotFound();
            }

            var rowsAffected = await connection.ExecuteAsync(
                "dbo.UpdateProduct",
                new
                {
                    Id = id,
                    product.Name,
                    product.Description,
                    product.Price,
                    product.StockQuantity,
                    UpdatedBy = changedBy
                },
                commandType: System.Data.CommandType.StoredProcedure);

            return NoContent();
        }

        // DELETE: api/products/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(int id, [FromQuery] string? changedBy = null)
        {
            using var connection = new SqlConnection(_connectionString);

            // Check if product exists using the function
            var exists = await connection.ExecuteScalarAsync<bool>(
                "SELECT dbo.ProductExists(@ProductId)",
                new { ProductId = id });

            if (!exists)
            {
                return NotFound();
            }

            var rowsAffected = await connection.ExecuteAsync(
                "dbo.DeleteProduct",
                new { Id = id, DeletedBy = changedBy },
                commandType: System.Data.CommandType.StoredProcedure);

            return NoContent();
        }

        // GET: api/products/audit
        [HttpGet("audit")]
        public async Task<IActionResult> GetAuditLogs()
        {
            using var connection = new SqlConnection(_connectionString);
            var logs = await connection.QueryAsync<AuditLog>(
                "SELECT * FROM AuditLog ORDER BY ChangedAt DESC",
                commandType: System.Data.CommandType.Text);

            return Ok(logs);
        }
    }
}