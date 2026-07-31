using Microsoft.AspNetCore.Mvc;
using ProductsCRUD_API.Dtos;
using ProductsCRUD_API.Repositories;

namespace ProductsCRUD_API.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(ProductRepository repository, IConfiguration configuration) : ControllerBase
{
    private const int MaxPageSize = 100;
    private readonly int _maxBatchSize = configuration.GetValue<int>("BulkInsert:MaxBatchSize");
    private readonly int _sqlBulkCopyThreshold = configuration.GetValue<int>("BulkInsert:SqlBulkCopyThreshold");

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var result = await repository.GetAllAsync(page, pageSize);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var product = await repository.GetByIdAsync(id);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProductCreateDto dto)
    {
        var product = await repository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ProductUpdateDto dto)
    {
        var product = await repository.UpdateAsync(id, dto);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await repository.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("bulk")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> BulkCreate([FromBody] List<ProductCreateDto> products)
    {
        if (products.Count == 0)
        {
            return BadRequest("The batch cannot be empty.");
        }

        if (products.Count > _maxBatchSize)
        {
            return BadRequest($"The batch exceeds the maximum of {_maxBatchSize} items.");
        }

        if (products.Count > _sqlBulkCopyThreshold)
        {
            var insertedViaBulkCopy = await repository.BulkCreateViaStagingAsync(products);
            return Created(string.Empty, new { inserted = insertedViaBulkCopy, strategy = "BulkCopy" });
        }

        var inserted = await repository.BulkCreateAsync(products);
        return Created(string.Empty, new { inserted, strategy = "TVP" });
    }
}
