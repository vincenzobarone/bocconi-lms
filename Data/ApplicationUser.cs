using Microsoft.AspNetCore.Identity;

namespace BocconiLMS.Data;

public class ApplicationUser : IdentityUser<int>
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string FullName => $"{FirstName} {LastName}".Trim();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ShibbolethId { get; set; }
}

public class ApplicationRole : IdentityRole<int>
{
    public ApplicationRole() { }
    public ApplicationRole(string roleName) : base(roleName) { }
}
