using FCG.Users.Api.Extensions;
using FCG.Users.Application.DTOs.Users;
using FCG.Users.Application.Services.Interfaces;
using FCG.Users.Domain.Commons;
using FCG.Users.Domain.Commons.Result;
using FCG.Users.Domain.Enums;
using FCG.Users.Domain.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace FCG.Users.Api.Controllers;

[Route("api/v{version:apiVersion}/users")]
public class UsersController : BaseController
{
    private readonly IUserService _userService;
    private readonly IUserRoleService _userRoleService;

    public UsersController(IUserService userService, IUserRoleService userRoleService)
    {
        _userService = userService;
        _userRoleService = userRoleService;
    }

    [AllowAnonymous]
    [HttpGet("roles")]
    [SwaggerOperation(Summary = "List user roles")]
    [ProducesResponseType(typeof(SuccessResult<IReadOnlyList<ReadUserRoleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRolesAsync(CancellationToken cancellationToken)
    {
        return (await _userRoleService.GetAllAsync(cancellationToken)).ToActionResult();
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [SwaggerOperation(Summary = "Register a new user (publishes UserCreatedEvent)")]
    [ProducesResponseType(typeof(SuccessResult<ReadUserDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterUserDto dto, CancellationToken cancellationToken)
    {
        return (await _userService.RegisterAsync(dto, cancellationToken)).ToActionResult(StatusCodes.Status201Created);
    }

    [AllowAnonymous]
    [HttpPost("logins")]
    [SwaggerOperation(Summary = "Authenticate user")]
    [ProducesResponseType(typeof(SuccessResult<TokenDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> LoginAsync([FromBody] LoginDto dto, CancellationToken cancellationToken)
    {
        return (await _userService.LoginAsync(dto, cancellationToken)).ToActionResult();
    }

    [HttpGet]
    [Authorize(Roles = nameof(UserRoles.Administrator))]
    [SwaggerOperation(Summary = "List users")]
    [ProducesResponseType(typeof(SuccessResult<Pagination<ReadUserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPagedAsync([FromQuery] UserFilter filter, CancellationToken cancellationToken)
    {
        return (await _userService.GetPagedAsync(filter, cancellationToken)).ToActionResult();
    }

    [HttpGet("{id:long}")]
    [SwaggerOperation(Summary = "Get user by id (administrator or the user himself)")]
    [ProducesResponseType(typeof(SuccessResult<ReadUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        return (await _userService.GetByIdAsync(id, cancellationToken)).ToActionResult();
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = nameof(UserRoles.Administrator))]
    [SwaggerOperation(Summary = "Update user")]
    [ProducesResponseType(typeof(SuccessResult<ReadUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync([FromRoute] long id, [FromBody] UpdateUserDto dto, CancellationToken cancellationToken)
    {
        return (await _userService.UpdateAsync(id, dto, cancellationToken)).ToActionResult();
    }

    [HttpDelete("{id:long}")]
    [Authorize(Roles = nameof(UserRoles.Administrator))]
    [SwaggerOperation(Summary = "Delete user (soft delete)")]
    [ProducesResponseType(typeof(SuccessResult<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        return (await _userService.DeleteAsync(id, cancellationToken)).ToActionResult();
    }
}
