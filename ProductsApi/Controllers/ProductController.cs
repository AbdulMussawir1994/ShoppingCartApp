using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductsApi.Dtos;
using ProductsApi.Repository.ProductRepository;

namespace ProductsApi.Controllers
{
    [ApiController]
    [Authorize]
    [ApiVersion("2.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpGet]
        [Route("GetProductsList")]
        public async Task<ActionResult> GetProducts(CancellationToken ctx)
        {
            var response = await _productService.GetProductsAsync(ctx);

            return response.Status ? Ok(response) : BadRequest(response);
        }

        [HttpGet("List")]
        public async Task<ActionResult> GetAll(CancellationToken ctx)
        {
            var response = await _productService.GetAllAsync(ctx);
            return response.Status ? Ok(response) : NotFound(response);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult> GetById(string id, CancellationToken ctx)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Product ID is required.");

            var response = await _productService.GetByIdAsync(id, ctx);
            return response.Status ? Ok(response) : NotFound(response);
        }

        [HttpPost]
        [Route("SelectProduct")]
        public async Task<ActionResult> SelectProduct([FromBody] SelectProductDto model, CancellationToken ctx)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _productService.SelectProductAsync(model, ctx);
            return response.Status ? CreatedAtAction(nameof(SelectProduct), response) : BadRequest(response);
        }

        [HttpPost("Create")]
        public async Task<ActionResult> Create([FromForm] CreateProductDto model, CancellationToken ctx)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _productService.CreateAsync(model, ctx);
            return response.Status ? CreatedAtAction(nameof(Create), response) : BadRequest(response);
        }

        [HttpPut("Update/{id}")]
        public async Task<ActionResult> Update(string id, [FromBody] CreateProductDto model, CancellationToken ctx)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _productService.UpdateAsync(id, model, ctx);
            return response.Status ? Ok(response) : BadRequest(response);
        }

        [HttpDelete("Delete/{id}")]
        public async Task<ActionResult> Delete(string id, CancellationToken ctx)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Product ID is required.");

            var response = await _productService.DeleteAsync(id, ctx);
            return response.Status ? Ok(response) : NotFound(response);
        }

        [HttpGet("GetProductName/{Id}")]
        public async Task<ActionResult> GetProductName(string Id, CancellationToken ctx)
        {
            if (string.IsNullOrWhiteSpace(Id))
                return BadRequest("Product ID is required.");

            var response = await _productService.GetProductNameAsync(Id, ctx);
            return response.Status ? Ok(response) : NotFound(response);
        }
    }
}
