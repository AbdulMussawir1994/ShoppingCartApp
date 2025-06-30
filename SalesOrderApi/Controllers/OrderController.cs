using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesOrderApi.Dtos;
using SalesOrderApi.Repository.OrderRepository;
using SalesOrderApi.ViewModels;

namespace SalesOrderApi.Controllers
{
    [ApiController]
    [Authorize]
    [ApiVersion("2.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrderController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpPost("ProductConfirm")]
        public async Task<ActionResult> ProductConfirm(ConfirmProductViewModel model)
        {
            var result = await _orderService.ProductConfirmAsync(model);
            return result.Status ? Ok(result) : BadRequest(result);
        }

        [HttpPost]
        [Route("OrderConfirmById")]
        public async Task<ActionResult> OrderConfirmById(ConfirmOrderViewModel model)
        {
            var result = await _orderService.ConfirmOrderByIdAsync(model);
            return result.Status ? Ok(result) : BadRequest(result);
        }

        //[HttpPost("ConfirmOrder")]
        //public async Task<ActionResult> ConfirmOrder(ConfirmOrderViewModel model)
        //{
        //    var result = await _orderService.ConfirmOrderByIdInQueueAsync(model);
        //    return Ok(result);
        //}

        [HttpPost("ConfirmOrderWithQueue/{queueName}")]
        public async Task<IActionResult> ConfirmOrder(string queueName)
        {
            var result = await _orderService.UpdateConfirmOrderDetails(queueName);
            return Ok(result);
        }

        [HttpGet("List")]
        public async Task<ActionResult> GetAll(CancellationToken ctx)
        {
            var response = await _orderService.GetAllAsync(ctx);
            return response.Status ? Ok(response) : NotFound(response);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult> GetById(string id, CancellationToken ctx)
        {
            if (id is null)
                return BadRequest("Order ID must be greater than 0.");

            var response = await _orderService.GetByIdAsync(id, ctx);
            return response.Status ? Ok(response) : NotFound(response);
        }

        [HttpPost("Create")]
        public async Task<ActionResult> Create([FromBody] CreateOrderDto model, CancellationToken ctx)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _orderService.CreateAsync(model, ctx);
            return response.Status ? CreatedAtAction(nameof(Create), response) : BadRequest(response);
        }

        [HttpPut("Update/{id:int}")]
        public async Task<ActionResult> Update(string id, [FromBody] CreateOrderDto model, CancellationToken ctx)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _orderService.UpdateAsync(id, model, ctx);
            return response.Status ? Ok(response) : BadRequest(response);
        }

        [HttpDelete("Delete/{id:int}")]
        public async Task<ActionResult> Delete(string id, CancellationToken ctx)
        {
            if (id is null)
                return BadRequest("Invalid Order ID.");

            var response = await _orderService.DeleteAsync(id, ctx);
            return response.Status ? Ok(response) : NotFound(response);
        }
    }
}
