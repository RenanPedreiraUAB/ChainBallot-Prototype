using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using VoterComponent.Web.Application;
using VoterComponent.Web.Contracts;
using VoterComponent.Web.Services;

namespace VoterComponent.Web.Pages;

public class LoginModel(IAuthProvider authProvider, IOptions<VoterOptions> options) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnElectionId { get; set; }

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Dashboard", new { electionId = ReturnElectionId ?? options.Value.DefaultElectionId });
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var response = await authProvider.AuthenticateAsync(new LoginRequest(Input.Username.Trim(), Input.Password), cancellationToken);
        if (response is null)
        {
            ErrorMessage = "Authentication failed. Check your username and password.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, response.Subject),
            new(ClaimTypes.Name, response.Username),
            new("unique_name", response.Username),
            new(SessionClaimNames.AccessToken, response.AccessToken),
            new(SessionClaimNames.AccessTokenExpiresUtc, response.ExpiresAtUtc.ToString("O"))
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = response.ExpiresAtUtc
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        var electionId = string.IsNullOrWhiteSpace(ReturnElectionId)
            ? options.Value.DefaultElectionId
            : ReturnElectionId.Trim();

        return RedirectToPage("/Dashboard", new { electionId });
    }

    public sealed class InputModel
    {
        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }
}