using API.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SHARED.DTOs;
using SHARED.Models.Auth;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly JWTConfig _jwtConfig;

        public AuthenticationController(
            UserManager<IdentityUser> userManager, 
            IOptions<JWTConfig> jwtConfig)
        {
            _userManager = userManager;
            _jwtConfig = jwtConfig.Value;
        }

        [HttpPost("Register")]
        public async Task<IActionResult> Register([FromBody] UserRegistrationRequestDto request)
        {
            if(!ModelState.IsValid) return BadRequest();

            //verify if email exists
            var emailExists = await _userManager.FindByEmailAsync(request.Email);

            if (emailExists != null) return BadRequest(new AuthResult()
            {
                Result = false,
                Errors = new System.Collections.Generic.List<string>()
                {
                    "Email already exists"
                }
            });

            var user = new IdentityUser
            {
                Email = request.Email,
                UserName = request.Email
            };

            var isCreated = await _userManager.CreateAsync(user, request.Password);

            if (isCreated.Succeeded)
            {
                var token = GenerateToken(user);
                return Ok(new AuthResult
                {
                    Result = true,
                    Token = token
                });
            }
            else
            {
                var errors = new List<string>();
                foreach(var error in isCreated.Errors)
                {
                    errors.Add(error.Description);
                }

                return BadRequest(new AuthResult
                {
                    Result= false,
                    Errors = errors
                });
            }

            return BadRequest(new AuthResult
            {
                Result = false,
                Errors = new List<string> { "User couldn`t be created"}
            });
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] UserLoginRequestDto request)
        {
            if (request == null) return BadRequest();

            //Check if user exists
            var existingUser = await _userManager.FindByEmailAsync(request.Email);

            if (existingUser == null) return BadRequest(new AuthResult
            {
                Result = false,
                Errors = new List<string> { "Invalid Paylod" }
            });

            var checkUserAndPass = await _userManager.CheckPasswordAsync(existingUser, request.Password);

            if (!checkUserAndPass) return BadRequest(new AuthResult
            {
                Result = false,
                Errors = new List<string> { "Invalid Credentials" }
            });

            var token = GenerateToken(existingUser);

            return Ok(new AuthResult
            {
                Result = true,
                Token = token,
            });
        }

        private string GenerateToken(IdentityUser user)
        {
            var jwtTokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtConfig.Secret);

            var tokenDescriptor = new SecurityTokenDescriptor()
            {
                Subject = new System.Security.Claims.ClaimsIdentity(new ClaimsIdentity(new[]
                {
                    new Claim("Id", user.Id),
                    new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                    new Claim(JwtRegisteredClaimNames.Email, user.Email),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim(JwtRegisteredClaimNames.Iat, DateTime.Now.ToUniversalTime().ToString())
                })),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
            };

            var token = jwtTokenHandler.CreateToken(tokenDescriptor);

            return jwtTokenHandler.WriteToken(token);
        }
    }
}
