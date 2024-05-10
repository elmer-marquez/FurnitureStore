using API.Configurations;
using DATA;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SHARED.Common;
using SHARED.DTOs;
using SHARED.Models;
using SHARED.Models.Auth;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly JWTSettings _jwtConfig;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDBContext _context;
        private readonly TokenValidationParameters _tokenValidationParameters;

        public AuthenticationController(
            UserManager<IdentityUser> userManager, 
            IOptions<JWTSettings> jwtConfig,
            IEmailSender emailSender,
            ApplicationDBContext context,
            TokenValidationParameters tokenValidationParameters)
        {
            _userManager = userManager;
            _jwtConfig = jwtConfig.Value;
            _emailSender = emailSender;
            _context = context;
            _tokenValidationParameters = tokenValidationParameters;

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
                UserName = request.Email,
                EmailConfirmed = false
            };

            var isCreated = await _userManager.CreateAsync(user, request.Password);

            if (isCreated.Succeeded)
            {
                await SendVerificationEmail(user);

                //var token = GenerateToken(user);
                return Ok(new AuthResult
                {
                    Result = true
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

            if (!existingUser.EmailConfirmed)
                return BadRequest(new AuthResult
                {
                    Result = false,
                    Errors = new List<string> { "Email needs to be confirmed."}
                });

            var checkUserAndPass = await _userManager.CheckPasswordAsync(existingUser, request.Password);

            if (!checkUserAndPass) return BadRequest(new AuthResult
            {
                Result = false,
                Errors = new List<string> { "Invalid Credentials" }
            });

            var token = await GenerateTokenAsync(existingUser);

            return Ok(token);

        }

        [HttpPost("RefreshToken")]
        public async Task<IActionResult> RefreshToken([FromBody] TokenRequest request)
        {
            if(!ModelState.IsValid) return BadRequest(new AuthResult
            {
                Result= false,
                Errors = new List<string> { "Invalid Parameters"}
            });

            var results = VerifyAndGenerateTokenAsync(request);

            if(results == null)
            {
                return BadRequest(new AuthResult
                {
                    Result = false,
                    Errors = new List<string> { "Invalid Token" }
                });
            }

            return Ok(results);
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail([FromQuery] string userId, string code)
        {
            if(string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code)) 
                return BadRequest(new AuthResult
                {
                    Result= false,
                    Errors = new List<string> { "Invalid email confirmation"}
                });

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null) return NotFound($"Unable to load user with Id '{userId}'.");

            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));

            var result = await _userManager.ConfirmEmailAsync(user, code);

            var status = result.Succeeded ? "Thank you for confirming your email."
                                            : "There has been an error confirming email.";

            return Ok(status);
        }

        private async Task<AuthResult> GenerateTokenAsync(IdentityUser user)
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
                Expires = DateTime.UtcNow.Add(_jwtConfig.ExpiryTime),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
            };

            var token = jwtTokenHandler.CreateToken(tokenDescriptor);

            var jwtToken = jwtTokenHandler.WriteToken(token);
            var refreshToken = new RefreshToken
            {
                JwtId = token.Id,
                Token = RandomString.Generate(23),
                AddedDate = DateTime.UtcNow,
                ExpiryTime = DateTime.UtcNow.AddDays(30),
                IsRevoked = false,
                UserId = user.Id
            };

            await _context.RefreshTokens.AddAsync(refreshToken);
            await _context.SaveChangesAsync();

            return new AuthResult
            {
                Token = jwtToken,
                RefreshToken = refreshToken.Token,
                Result = true
            };
        }

        private async Task SendVerificationEmail(IdentityUser user)
        {
            var verificationCode = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            verificationCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(verificationCode));

            //example: https://localhost:8080/authentication/verifyemail/userId=asdkn&code=laksndf
            var callbackUrl = $@"{Request.Scheme}://{Request.Host}{Url.Action("ConfirmEmail", controller: "Authentication", new {userId = user.Id, code = verificationCode} )}";

            var emailbody = $"<h3>Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>Clicking here!</a></h3>";

            await _emailSender.SendEmailAsync(email: user.Email, subject: "Confirm your email", htmlMessage: emailbody );

        }

        private async Task<AuthResult> VerifyAndGenerateTokenAsync(TokenRequest request)
        {
            var jwtTokenHandler = new JwtSecurityTokenHandler();
            AuthResult authResult = null;

            try
            {
                _tokenValidationParameters.ValidateLifetime = false;

                var tokenBeingVerified = jwtTokenHandler.ValidateToken(request.Token, _tokenValidationParameters, out var validatedToken);
                if (validatedToken is JwtSecurityToken jwtSecurityToken)
                {
                    var result = jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase);

                    if(!result || tokenBeingVerified == null)
                    {
                        throw new Exception("Invalid Token");
                    }

                    var utcExpiryTime = long.Parse(tokenBeingVerified.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp).Value);

                    var expiryDate = DateTimeOffset.FromUnixTimeSeconds(utcExpiryTime).UtcDateTime;

                    if (expiryDate < DateTime.UtcNow) throw new Exception("Token Expired");

                    var storedToken = await _context.RefreshTokens.FirstOrDefaultAsync(t=>t.Token == request.RefreshToken);

                    if (storedToken == null) throw new Exception("Invalid Token");

                    if (storedToken.IsUsed || storedToken.IsRevoked) throw new Exception("Invalid Token");

                    var jti = tokenBeingVerified.Claims.FirstOrDefault(c=>c.Type == JwtRegisteredClaimNames.Jti).Value;

                    if (jti != storedToken.JwtId) throw new Exception("Invalid Token");

                    if(storedToken.ExpiryTime < DateTime.UtcNow) throw new Exception("Invalid Exception");

                    storedToken.IsUsed = true;

                    _context.RefreshTokens.Update(storedToken);
                    await _context.SaveChangesAsync();

                    var dbUser = await _userManager.FindByIdAsync(storedToken.UserId);

                    authResult = await GenerateTokenAsync(dbUser);
                }
            }
            catch (Exception e)
            {
                var message = e.Message == "Invalid Token" || e.Message == "Token Expired" ? e.Message : "Internal Server Error";
                authResult = new AuthResult { 
                    Result = false,
                    Errors = new List<string>() { message } 
                };
            }

            return authResult;
        }
    }
}
