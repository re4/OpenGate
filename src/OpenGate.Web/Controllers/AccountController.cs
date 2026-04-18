using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Entities;

namespace OpenGate.Web.Controllers;

/// <summary>
/// Handles user authentication: sign in, sign up, and sign out flows.
/// All state changing actions require an antiforgery token, the login and
/// register endpoints are rate limited per IP, and CAPTCHA validation is
/// enforced when configured.
/// </summary>
[Route("account")]
[AllowAnonymous]
public class AccountController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    ICaptchaService captchaService,
    ILogger<AccountController> logger) : Controller
{
    /// <summary>
    /// Authenticates a user with email and password. Failed attempts count
    /// against the configured Identity lockout policy and successful sign in
    /// only redirects to a local return URL.
    /// </summary>
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(
        [FromForm] string email,
        [FromForm] string password,
        [FromForm] string? returnUrl,
        [FromForm(Name = "captcha-token")] string? captchaToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return Redirect($"/login?error={Uri.EscapeDataString("Email and password are required.")}");
        }

        var captchaConfig = await captchaService.GetConfigAsync();
        if (captchaConfig is { IsConfigured: true, LoginEnabled: true })
        {
            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var captchaValid = await captchaService.VerifyAsync(captchaToken ?? "", remoteIp);
            if (!captchaValid)
            {
                return Redirect($"/login?error={Uri.EscapeDataString("CAPTCHA verification failed. Please try again.")}");
            }
        }

        var result = await signInManager.PasswordSignInAsync(email, password, isPersistent: true, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            logger.LogInformation("User {Email} signed in from {Ip}", email, HttpContext.Connection.RemoteIpAddress);
            return LocalRedirect(returnUrl ?? "/");
        }

        if (result.IsLockedOut)
        {
            logger.LogWarning("Sign-in lockout for {Email} from {Ip}", email, HttpContext.Connection.RemoteIpAddress);
            return Redirect($"/login?error={Uri.EscapeDataString("Too many failed attempts. Please try again later.")}");
        }

        if (result.IsNotAllowed)
        {
            return Redirect($"/login?error={Uri.EscapeDataString("Account is not allowed to sign in.")}");
        }

        return Redirect($"/login?error={Uri.EscapeDataString("Invalid email or password.")}");
    }

    /// <summary>
    /// Registers a new client account with the supplied details. The endpoint
    /// is rate limited and supports optional CAPTCHA validation.
    /// </summary>
    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("register")]
    public async Task<IActionResult> Register(
        [FromForm] string firstName,
        [FromForm] string lastName,
        [FromForm] string email,
        [FromForm] string password,
        [FromForm] string confirmPassword,
        [FromForm(Name = "captcha-token")] string? captchaToken)
    {
        if (password != confirmPassword)
        {
            return Redirect($"/register?error={Uri.EscapeDataString("Passwords do not match.")}");
        }

        var captchaConfig = await captchaService.GetConfigAsync();
        if (captchaConfig is { IsConfigured: true, RegisterEnabled: true })
        {
            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var captchaValid = await captchaService.VerifyAsync(captchaToken ?? "", remoteIp);
            if (!captchaValid)
            {
                return Redirect($"/register?error={Uri.EscapeDataString("CAPTCHA verification failed. Please try again.")}");
            }
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName ?? string.Empty,
            LastName = lastName ?? string.Empty
        };

        var result = await userManager.CreateAsync(user, password);

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, "Client");
            await signInManager.SignInAsync(user, isPersistent: true);
            return LocalRedirect("/");
        }

        var errors = string.Join(" ", result.Errors.Select(e => e.Description));
        return Redirect($"/register?error={Uri.EscapeDataString(errors)}");
    }

    /// <summary>
    /// Signs the current user out. POST is preferred, but a GET variant is
    /// provided so navigation links continue to work; both invalidate the
    /// authentication cookie.
    /// </summary>
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return LocalRedirect("/login");
    }

    /// <summary>
    /// GET logout that signs the user out before redirecting. Required for
    /// simple anchor based navigation links from the UI.
    /// </summary>
    [HttpGet("logout")]
    public async Task<IActionResult> LogoutGet()
    {
        await signInManager.SignOutAsync();
        return LocalRedirect("/login");
    }
}
