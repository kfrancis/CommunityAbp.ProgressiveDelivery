using CommunityAbp.ProgressiveDelivery.Sample.Web.Auth;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace CommunityAbp.ProgressiveDelivery.Sample.Web.Pages;

public class IndexModel : AbpPageModel
{
    public SamplePersona Persona { get; private set; } = SamplePersonas.OpsPersona;

    public void OnGet()
    {
        Persona = SamplePersonas.Resolve(Request.Cookies[DevAuthenticationHandler.RoleCookie]);
    }

    /// <summary>Switches the sample persona (ops / support) via cookie.</summary>
    public IActionResult OnPostSwitch(string role, string? returnUrl = null)
    {
        Response.Cookies.Append(DevAuthenticationHandler.RoleCookie, SamplePersonas.Resolve(role).Role, new CookieOptions { HttpOnly = true, IsEssential = true });
        return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
    }
}
