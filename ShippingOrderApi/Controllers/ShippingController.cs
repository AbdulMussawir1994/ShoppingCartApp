using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShippingOrderApi.Dtos;
using ShippingOrderApi.Repository.SupplierRepository;
using ShippingOrderApi.ViewModel;

namespace ShippingOrderApi.Controllers
{
    [ApiController]
    [Authorize]
    [ApiVersion("2.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class ShippingController : ControllerBase
    {
        private readonly ISupplierService _supplierService;

        public ShippingController(ISupplierService supplierService)
        {
            _supplierService = supplierService;
        }

        [HttpPost("Create")]
        public async Task<ActionResult> CreateSupplier([FromBody] SupplierCreateDto model, CancellationToken ctx)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _supplierService.CreateSupplierAsync(model, ctx);
            return response.Status ? CreatedAtAction(nameof(CreateSupplier), response) : BadRequest(response);
        }

        [HttpPost]
        [Route("ConfirmDelivery")]
        public async Task<ActionResult> ConfirmDelivery([FromBody] DispatchViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _supplierService.ConfirmDeliveryAsync(model);
            return response.Status ? Ok(response) : BadRequest(response);
        }
    }
}
