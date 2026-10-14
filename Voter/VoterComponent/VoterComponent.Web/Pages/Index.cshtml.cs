using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VoterComponent.Web.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        return User.Identity?.IsAuthenticated == true
            ? RedirectToPage("/Dashboard")
            : RedirectToPage("/Login");
    }
}
