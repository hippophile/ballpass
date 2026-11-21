using Microsoft.EntityFrameworkCore;

namespace Ballpass.Web.Data;

public class BallpassDbContext(DbContextOptions<BallpassDbContext> options) : DbContext(options);
