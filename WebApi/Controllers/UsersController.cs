using System;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using WebApi.Domain; 
using WebApi.Models; 

namespace WebApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly IUserRepository userRepository;
    private readonly IMapper mapper;
    
    public UsersController(IUserRepository userRepository, IMapper mapper)
    {
        this.userRepository = userRepository;
        this.mapper = mapper;
    }

    // Задание 1: GET /users/{userId}
    [HttpGet("{userId}", Name = nameof(GetUserById))]
    [Produces("application/json", "application/xml")]
    public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
    {

        var userEntity = userRepository.FindById(userId);


        if (userEntity == null)
            return NotFound();


        return Ok(mapper.Map<UserDto>(userEntity));
    }
}