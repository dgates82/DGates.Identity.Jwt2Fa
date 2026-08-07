using DGates.Identity.Jwt2Fa.Tests.Fixtures;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DGates.Identity.Jwt2Fa.Tests.Integration;

/// <summary>Minimal Identity store for integration tests — backs onto SQLite, not the consumer's real provider.</summary>
public class TestDbContext : IdentityDbContext<TestUser>
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }
}
