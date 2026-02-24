using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenGate.Domain.Entities;

namespace OpenGate.Web.Controllers;

[Route("account")]
[AllowAnonymous]
public class AccountController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        [FromForm] string email,
        [FromForm] string password,
        [FromForm] string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return Redirect($"/login?error={Uri.EscapeDataString("Email and password are required.")}");
        }

        var result = await signInManager.PasswordSignInAsync(email, password, isPersistent: true, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            return LocalRedirect(returnUrl ?? "/");
        }

        return Redirect($"/login?error={Uri.EscapeDataString("Invalid email or password.")}");
    }

    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        [FromForm] string firstName,
        [FromForm] string lastName,
        [FromForm] string email,
        [FromForm] string password,
        [FromForm] string confirmPassword)
    {
        if (password != confirmPassword)
        {
            return Redirect($"/register?error={Uri.EscapeDataString("Passwords do not match.")}");
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

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return LocalRedirect("/login");
    }

    [HttpGet("logout")]
    public async Task<IActionResult> LogoutGet()
    {
        await signInManager.SignOutAsync();
        return LocalRedirect("/login");
    }
}
