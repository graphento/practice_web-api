using System;
using System.Linq;
using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using WebApi.MinimalApi.Domain;
using WebApi.MinimalApi.Models;

namespace WebApi.MinimalApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly IUserRepository userRepository;
    private readonly IMapper mapper;
    private readonly LinkGenerator linkGenerator;

    public UsersController(
        IUserRepository userRepository,
        IMapper mapper,
        LinkGenerator linkGenerator)
    {
        this.userRepository = userRepository;
        this.mapper = mapper;
        this.linkGenerator = linkGenerator;
    }

    //  1
    [HttpGet("{userId}", Name = nameof(GetUserById))]
    [HttpHead("{userId}")]
    [Produces("application/json", "application/xml")]
    public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
    {
        if (userId == Guid.Empty)
            return NotFound();

        var userEntity = userRepository.FindById(userId);

        if (userEntity == null)
            return NotFound();

        var user = mapper.Map<UserDto>(userEntity);
        if (HttpMethods.IsHead(Request.Method))
            return new HeadObjectResult(user);

        return Ok(user);
    }

    //  2
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json", "application/xml")]
    public IActionResult CreateUser([FromBody] UserToCreateDto? user)
    {
        if (user == null)
            return BadRequest();

        if (!ModelState.IsValid)
            return UnprocessableEntity(ModelState);

        if (string.IsNullOrEmpty(user.Login) ||
            !user.Login.All(char.IsLetterOrDigit))
        {
            ModelState.AddModelError(
                "Login",
                "Login should contain only letters or digits");

            return UnprocessableEntity(ModelState);
        }

        var userEntity = mapper.Map<UserEntity>(user);
        var createdUser = userRepository.Insert(userEntity);

        return CreatedAtRoute(
            nameof(GetUserById),
            new { userId = createdUser.Id },
            createdUser.Id);
    }

    //  3
    [HttpPut("{userId}")]
    [Consumes("application/json")]
    [Produces("application/json", "application/xml")]
    public IActionResult UpdateUser(
        [FromRoute] Guid userId,
        [FromBody] UserToUpdateDto? user)
    {
        if (user == null || userId == Guid.Empty)
            return BadRequest();

        if (!ModelState.IsValid)
            return UnprocessableEntity(ModelState);

        var userEntity = userRepository.FindById(userId) ?? new UserEntity(userId);
        mapper.Map(user, userEntity);

        userRepository.UpdateOrInsert(userEntity, out var isInserted);

        if (isInserted)
        {
            return CreatedAtRoute(
                nameof(GetUserById),
                new { userId = userEntity.Id },
                userEntity.Id);
        }

        return NoContent();
    }

    //  4
    [HttpPatch("{userId}")]
    [Consumes("application/json-patch+json")]
    [Produces("application/json", "application/xml")]
    public IActionResult PartiallyUpdateUser(
        [FromRoute] Guid userId,
        [FromBody] JsonPatchDocument<UserToUpdateDto>? patchDoc)
    {
        if (patchDoc == null)
            return BadRequest();

        if (userId == Guid.Empty)
            return NotFound();

        var existingUser = userRepository.FindById(userId);

        if (existingUser == null)
            return NotFound();

        var updateDto = mapper.Map<UserToUpdateDto>(existingUser);

        patchDoc.ApplyTo(updateDto, ModelState);

        TryValidateModel(updateDto);

        if (!ModelState.IsValid)
            return UnprocessableEntity(ModelState);

        mapper.Map(updateDto, existingUser);
        userRepository.Update(existingUser);

        return NoContent();
    }

    // 5
    [HttpDelete("{userId}")]
    public IActionResult DeleteUser([FromRoute] Guid userId)
    {
        if (userRepository.FindById(userId) == null)
            return NotFound();

        userRepository.Delete(userId);
        return NoContent();
    }

    // 7
    [HttpGet(Name = nameof(GetUsers))]
    [Produces("application/json", "application/xml")]
    public IActionResult GetUsers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 20);
        var page = userRepository.GetPage(pageNumber, pageSize);
        var paginationHeader = new
        {
            previousPageLink = page.HasPrevious
                ? linkGenerator.GetUriByRouteValues(HttpContext, nameof(GetUsers), new { pageNumber = pageNumber - 1, pageSize })
                : null,
            nextPageLink = page.HasNext
                ? linkGenerator.GetUriByRouteValues(HttpContext, nameof(GetUsers), new { pageNumber = pageNumber + 1, pageSize })
                : null,
            totalCount = page.TotalCount,
            pageSize = page.PageSize,
            currentPage = page.CurrentPage,
            totalPages = page.TotalPages
        };
        Response.Headers["X-Pagination"] = JsonConvert.SerializeObject(paginationHeader);
        return Ok(mapper.Map<List<UserDto>>(page));
    }

    // 8
    [HttpOptions]
    public IActionResult GetUsersOptions()
    {
        Response.Headers.Allow = "POST, GET, OPTIONS";
        return Ok();
    }

    // Сохраняем выбор формата и заголовки GET, но не записываем тело HEAD.
    private sealed class HeadObjectResult : OkObjectResult
    {
        public HeadObjectResult(object value) : base(value)
        {
        }

        public override async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;
            var originalBody = response.Body;
            try
            {
                response.Body = Stream.Null;
                await base.ExecuteResultAsync(context);
            }
            finally
            {
                response.Body = originalBody;
            }
        }
    }
}
