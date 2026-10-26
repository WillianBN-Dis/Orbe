using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Orbe.Server;
public sealed class DesignDb : IDesignTimeDbContextFactory<OrbeDb>
{
 public OrbeDb CreateDbContext(string[] args)=>new(new DbContextOptionsBuilder<OrbeDb>()
  .UseNpgsql("Host=127.0.0.1;Database=orbe_design;Username=orbe_design").Options);
}
