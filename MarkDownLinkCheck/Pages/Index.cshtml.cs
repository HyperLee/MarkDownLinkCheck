using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarkDownLinkCheck.Pages;

/// <summary>
/// Index page model - serves as static entry point for the SPA-style link checker
/// </summary>
public class IndexModel : PageModel
{
    public void OnGet()
    {
        // Static page - all interaction happens via JavaScript and SSE endpoint
    }
}
