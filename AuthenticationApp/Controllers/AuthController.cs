using AuthenticationApp.Repository.AuthRepository;
using AuthenticationApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;

        public AuthController(IUserService userService) => _userService = userService;

        [HttpPost("LoginUser")]
        [AllowAnonymous]
        public async Task<ActionResult> LoginUser([FromBody] LoginViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _userService.LoginUser(model, cancellationToken);
            return result.Code == "200" ? Ok(result) : BadRequest(result);
        }

        [HttpPost("RegisterUser")]
        [AllowAnonymous]
        //[Authorize]
        public async Task<ActionResult> RegisterUser([FromBody] RegisterViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _userService.RegisterUser(model, cancellationToken);
            return result.Status ? CreatedAtAction(nameof(RegisterUser), result) : BadRequest(result);
        }
    }
}
